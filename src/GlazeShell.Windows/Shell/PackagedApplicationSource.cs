using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GlazeShell.Core.Discovery;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;

namespace GlazeShell.Windows.Shell;

/// <summary>
/// Источник упакованных приложений (MSIX и Desktop Bridge): перечисляет пакеты текущего
/// пользователя через <see cref="PackageManager"/> и превращает записи списка приложений
/// в кандидатов с настоящим AUMID. Оболочный <c>shell:AppsFolder</c> не используется:
/// его элементы не отдают <c>PKEY_AppUserModel_ID</c>, а для части пакетов приходят с
/// именем семейства без идентификатора приложения, поэтому запуск по ним невозможен.
/// </summary>
public sealed class PackagedApplicationSource : IApplicationDiscoverySource
{
    private const int MinimumBuildWithAppListEntries = 19041;
    private const int MaximumParallelism = 8;

    private static readonly TimeSpan EnumerationTimeout = TimeSpan.FromSeconds(30);

    public string Name => "packages";

    public ApplicationDiscoveryResult Discover(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, MinimumBuildWithAppListEntries))
        {
            return new ApplicationDiscoveryResult(
                Array.Empty<ApplicationCandidate>(),
                ["Listing packaged applications requires Windows 10 version 2004 or later."]);
        }

        List<PackagedApplication> packaged;

        try
        {
            // Ожидание блокирующее намеренно: контракт источника синхронный, а вызывающая
            // сторона переносит весь сбор кандидатов на пул потоков.
            packaged = ReadPackagesAsync(cancellationToken).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            return new ApplicationDiscoveryResult(
                Array.Empty<ApplicationCandidate>(),
                [string.Create(CultureInfo.InvariantCulture, $"Package enumeration failed: {exception.GetType().Name}: {exception.Message}")]);
        }

        var candidates = new List<ApplicationCandidate>(packaged.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;

        foreach (var application in packaged)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!seen.Add(application.ApplicationUserModelId))
            {
                skipped++;
                continue;
            }

            try
            {
                candidates.Add(new ApplicationCandidate(
                    application.DisplayName,
                    Name,
                    ApplicationType.Msix,
                    packageFamilyName: application.PackageFamilyName,
                    applicationUserModelId: application.ApplicationUserModelId,
                    description: application.Description,
                    publisher: application.Publisher,
                    version: application.Version));
            }
            catch (ArgumentException)
            {
                skipped++;
            }
        }

        return new ApplicationDiscoveryResult(candidates, null, skipped);
    }

    /// <summary>
    /// Читает записи списка приложений всех основных пакетов пользователя. Пакеты
    /// обрабатываются параллельно: у каждого вызова своя стоимость загрузки манифеста,
    /// и последовательный обход на машине со ста пакетами занимает секунды.
    /// </summary>
    [SupportedOSPlatform("windows10.0.19041.0")]
    private static async Task<List<PackagedApplication>> ReadPackagesAsync(CancellationToken cancellationToken)
    {
        var manager = new PackageManager();
        var packages = manager
            .FindPackagesForUserWithPackageTypes(string.Empty, PackageTypes.Main)
            .Where(static package => !package.IsFramework && !package.IsResourcePackage)
            .ToList();

        var batches = new List<PackagedApplication>[packages.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, packages.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(MaximumParallelism, Environment.ProcessorCount),
                CancellationToken = cancellationToken,
            },
            async (index, token) =>
            {
                batches[index] = await ReadEntriesAsync(packages[index], token).ConfigureAwait(false);
            }).WaitAsync(EnumerationTimeout, cancellationToken).ConfigureAwait(false);

        var result = new List<PackagedApplication>();

        foreach (var batch in batches)
        {
            result.AddRange(batch);
        }

        return result;
    }

    private static async Task<List<PackagedApplication>> ReadEntriesAsync(
        Package package,
        CancellationToken cancellationToken)
    {
        var applications = new List<PackagedApplication>();

        IReadOnlyList<AppListEntry> entries;

        try
        {
            entries = await package.GetAppListEntriesAsync().AsTask(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            // Повреждённый пакет не должен скрывать приложения остальных.
            return applications;
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var applicationUserModelId = entry.AppUserModelId;

                if (string.IsNullOrWhiteSpace(applicationUserModelId) ||
                    !applicationUserModelId.Contains('!', StringComparison.Ordinal))
                {
                    continue;
                }

                var displayName = ReadDisplayName(entry, package);

                if (displayName is null)
                {
                    continue;
                }

                applications.Add(new PackagedApplication(
                    displayName,
                    applicationUserModelId.Trim(),
                    package.Id.FamilyName,
                    ReadOptional(() => entry.DisplayInfo.Description),
                    ReadOptional(() => package.PublisherDisplayName),
                    ReadVersion(package)));
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                // Метаданные одного элемента могут быть недоступны: остальные остаются.
            }
        }

        return applications;
    }

    private static string? ReadDisplayName(AppListEntry entry, Package package)
    {
        var name = ReadOptional(() => entry.DisplayInfo.DisplayName) ??
            ReadOptional(() => package.DisplayName);

        return name ?? package.Id.FamilyName;
    }

    private static string? ReadVersion(Package package) =>
        ReadOptional(() =>
        {
            var version = package.Id.Version;
            return string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}");
        });

    private static string? ReadOptional(Func<string?> read)
    {
        try
        {
            var value = read();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            return null;
        }
    }

    private static bool IsExpectedFailure(Exception exception) =>
        exception is COMException
            or UnauthorizedAccessException
            or InvalidOperationException
            or NotSupportedException
            or IOException
            or ArgumentException
            or TimeoutException;

    private sealed record PackagedApplication(
        string DisplayName,
        string ApplicationUserModelId,
        string PackageFamilyName,
        string? Description,
        string? Publisher,
        string? Version);
}
