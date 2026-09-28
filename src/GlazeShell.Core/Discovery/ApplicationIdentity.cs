using System.Security.Cryptography;
using System.Text;
using GlazeShell.Core.Models;

namespace GlazeShell.Core.Discovery;

public static class ApplicationIdentity
{
    public const string MsixIdPrefix = "msix:";
    public const string ExecutableIdPrefix = "app:";
    public const string PackageIdPrefix = "pkg:";

    public static string? GetLaunchKey(ApplicationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!string.IsNullOrWhiteSpace(candidate.ApplicationUserModelId))
        {
            return "aumid:" + candidate.ApplicationUserModelId.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(candidate.ExecutablePath))
        {
            return "path:" + NormalizePath(candidate.ExecutablePath);
        }

        if (!string.IsNullOrWhiteSpace(candidate.PackageFamilyName))
        {
            return "pfn:" + candidate.PackageFamilyName.Trim().ToLowerInvariant();
        }

        return null;
    }

    public static bool TryCreateId(ApplicationCandidate candidate, out string id)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!string.IsNullOrWhiteSpace(candidate.ApplicationUserModelId))
        {
            id = MsixIdPrefix + candidate.ApplicationUserModelId.Trim().ToLowerInvariant();
            return true;
        }

        if (!string.IsNullOrWhiteSpace(candidate.ExecutablePath))
        {
            id = ExecutableIdPrefix + CreatePathHash(candidate.ExecutablePath);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(candidate.PackageFamilyName))
        {
            id = PackageIdPrefix + candidate.PackageFamilyName.Trim().ToLowerInvariant();
            return true;
        }

        id = string.Empty;
        return false;
    }

    public static string CreateId(ApplicationCandidate candidate)
    {
        if (!TryCreateId(candidate, out var id))
        {
            throw new ArgumentException("The candidate has no launch identity.", nameof(candidate));
        }

        return id;
    }

    public static Application? ToApplication(ApplicationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!TryCreateId(candidate, out var id))
        {
            return null;
        }

        try
        {
            return new Application(
                id,
                candidate.Name,
                candidate.Type,
                candidate.ExecutablePath,
                candidate.PackageFamilyName,
                candidate.ApplicationUserModelId,
                candidate.IconPath,
                candidate.Description,
                candidate.Publisher,
                candidate.Version,
                candidate.IsSystemApplication,
                candidate.Arguments,
                candidate.WorkingDirectory,
                candidate.IconIndex,
                candidate.Source);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return path.Trim().ToLowerInvariant();
    }

    private static string CreatePathHash(string path)
    {
        var normalized = NormalizePath(path);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}
