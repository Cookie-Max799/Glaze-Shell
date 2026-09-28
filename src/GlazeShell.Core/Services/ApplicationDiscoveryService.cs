using GlazeShell.Core.Discovery;
using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Services;

public sealed class ApplicationDiscoveryService : IApplicationManager, IDisposable
{
    private readonly IApplicationDiscoverySource[] _sources;
    private readonly IEventManager _eventManager;
    private readonly ApplicationDiscoveryOptions _options;
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private readonly object _stateSync = new();

    private IReadOnlyList<Application> _applications = Array.Empty<Application>();
    private DateTimeOffset _snapshotStamp;
    private bool _hasSnapshot;
    private bool _disposed;

    public ApplicationDiscoveryService(
        IEnumerable<IApplicationDiscoverySource> sources,
        IEventManager eventManager,
        ApplicationDiscoveryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _sources = sources.Where(static source => source is not null).ToArray();
        _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
        _options = (options ?? ApplicationDiscoveryOptions.Default).Validate();
    }

    public event EventHandler<DiscoveryReport>? DiscoveryCompleted;

    public async Task<IReadOnlyList<Application>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (TryGetFreshSnapshot(out var snapshot))
        {
            return snapshot;
        }

        return await ScanAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Application>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ScanAsync(cancellationToken);
    }

    public Task<Application?> GetAsync(string applicationId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ModelValidation.Required(applicationId, nameof(applicationId));

        IReadOnlyList<Application> snapshot;
        lock (_stateSync)
        {
            snapshot = _applications;
        }

        foreach (var discovered in snapshot)
        {
            if (string.Equals(discovered.Id, applicationId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<Application?>(discovered);
            }
        }

        return Task.FromResult<Application?>(null);
    }

    public async Task<IReadOnlyList<Application>> ScanAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var candidates = new List<ApplicationCandidate>();
            var warnings = new List<string>();

            foreach (var source in _sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CollectSource(source, candidates, warnings, cancellationToken);
            }

            var applications = BuildSnapshot(candidates, warnings, out var skipped);

            if (skipped > 0)
            {
                warnings.Add(string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"Skipped {skipped} candidate(s) without a launch identity or with a disallowed target."));
            }

            var changed = StoreSnapshot(applications);

            if (warnings.Count > 0)
            {
                DiscoveryCompleted?.Invoke(this, new DiscoveryReport(applications.Count, skipped, warnings));
            }

            if (changed)
            {
                _eventManager.Publish(new ApplicationChanged(applications));
            }

            return applications;
        }
        finally
        {
            _scanLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scanLock.Dispose();
    }

    private void CollectSource(
        IApplicationDiscoverySource source,
        List<ApplicationCandidate> candidates,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = source.Discover(cancellationToken);
            candidates.AddRange(result.Candidates);
            AppendWarnings(warnings, source.Name, result.Warnings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            AppendWarnings(warnings, source.Name, [$"{exception.GetType().Name}: {exception.Message}"]);
        }
    }

    private void AppendWarnings(List<string> warnings, string sourceName, IEnumerable<string> sourceWarnings)
    {
        var limit = _options.MaxWarningsPerSource * Math.Max(1, _sources.Length);

        foreach (var warning in sourceWarnings)
        {
            if (warnings.Count >= limit)
            {
                return;
            }

            warnings.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"[{sourceName}] {warning}"));
        }
    }

    private List<Application> BuildSnapshot(
        List<ApplicationCandidate> candidates,
        List<string> warnings,
        out int skippedCount)
    {
        var byKey = new Dictionary<string, ApplicationCandidate>(StringComparer.Ordinal);
        var order = new List<string>();
        skippedCount = 0;

        foreach (var candidate in candidates)
        {
            if (!candidate.HasLaunchIdentity)
            {
                skippedCount++;
                continue;
            }

            if (!_options.IncludeSystemApplications && candidate.IsSystemApplication)
            {
                skippedCount++;
                continue;
            }

            var key = ApplicationIdentity.GetLaunchKey(candidate);
            if (key is null)
            {
                skippedCount++;
                continue;
            }

            if (!byKey.TryGetValue(key, out var existing))
            {
                byKey.Add(key, candidate);
                order.Add(key);
                continue;
            }

            byKey[key] = Merge(existing, candidate);
        }

        var applications = new List<Application>(order.Count);

        foreach (var key in order)
        {
            var resolved = ApplicationIdentity.ToApplication(byKey[key]);
            if (resolved is null)
            {
                skippedCount++;
                warnings.Add(string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"Candidate '{byKey[key].Name}' could not be converted into an application."));
                continue;
            }

            applications.Add(resolved);
        }

        applications.Sort(static (left, right) =>
        {
            var byName = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : string.CompareOrdinal(left.Id, right.Id);
        });

        return applications;
    }

    private static ApplicationCandidate Merge(ApplicationCandidate primary, ApplicationCandidate secondary) =>
        new(
            primary.Name,
            primary.Source,
            primary.Type,
            primary.ExecutablePath,
            primary.PackageFamilyName ?? secondary.PackageFamilyName,
            primary.ApplicationUserModelId,
            primary.IconPath ?? secondary.IconPath,
            primary.Description ?? secondary.Description,
            primary.Publisher ?? secondary.Publisher,
            primary.Version ?? secondary.Version,
            primary.IsSystemApplication && secondary.IsSystemApplication,
            primary.Arguments ?? secondary.Arguments,
            primary.WorkingDirectory ?? secondary.WorkingDirectory,
            primary.IconIndex ?? secondary.IconIndex);

    private bool StoreSnapshot(List<Application> applications)
    {
        lock (_stateSync)
        {
            var changed = !_hasSnapshot || !_applications.SequenceEqual(applications);
            _applications = applications;
            _snapshotStamp = DateTimeOffset.UtcNow;
            _hasSnapshot = true;
            return changed;
        }
    }

    private bool TryGetFreshSnapshot(out IReadOnlyList<Application> snapshot)
    {
        lock (_stateSync)
        {
            if (_hasSnapshot && DateTimeOffset.UtcNow - _snapshotStamp < _options.CacheDuration)
            {
                snapshot = _applications;
                return true;
            }
        }

        snapshot = Array.Empty<Application>();
        return false;
    }
}

public sealed record DiscoveryReport(int ApplicationCount, int SkippedCount, IReadOnlyList<string> Warnings);
