using GlazeShell.Core.Models;

namespace GlazeShell.Core.Discovery;

public sealed record ApplicationCandidate
{
    public ApplicationCandidate(
        string name,
        string source,
        ApplicationType type = ApplicationType.None,
        string? executablePath = null,
        string? packageFamilyName = null,
        string? applicationUserModelId = null,
        string? iconPath = null,
        string? description = null,
        string? publisher = null,
        string? version = null,
        bool isSystemApplication = false,
        string? arguments = null,
        string? workingDirectory = null,
        int? iconIndex = null)
    {
        Name = ModelValidation.Required(name, nameof(name));
        Source = ModelValidation.Required(source, nameof(source));
        Type = type;
        ExecutablePath = ModelValidation.Optional(executablePath, nameof(executablePath));
        PackageFamilyName = ModelValidation.Optional(packageFamilyName, nameof(packageFamilyName));
        ApplicationUserModelId = ModelValidation.Optional(applicationUserModelId, nameof(applicationUserModelId));
        IconPath = ModelValidation.Optional(iconPath, nameof(iconPath));
        Description = ModelValidation.Optional(description, nameof(description));
        Publisher = ModelValidation.Optional(publisher, nameof(publisher));
        Version = ModelValidation.Optional(version, nameof(version));
        IsSystemApplication = isSystemApplication;
        Arguments = ModelValidation.Optional(arguments, nameof(arguments));
        WorkingDirectory = ModelValidation.Optional(workingDirectory, nameof(workingDirectory));
        IconIndex = ModelValidation.NonNegative(iconIndex, nameof(iconIndex));
    }

    public string Name { get; }

    public string Source { get; }

    public ApplicationType Type { get; }

    public string? ExecutablePath { get; }

    public string? PackageFamilyName { get; }

    public string? ApplicationUserModelId { get; }

    public string? IconPath { get; }

    public string? Description { get; }

    public string? Publisher { get; }

    public string? Version { get; }

    public bool IsSystemApplication { get; }

    public string? Arguments { get; }

    public string? WorkingDirectory { get; }

    public int? IconIndex { get; }

    public bool HasLaunchIdentity =>
        !string.IsNullOrWhiteSpace(ExecutablePath) ||
        !string.IsNullOrWhiteSpace(PackageFamilyName) ||
        !string.IsNullOrWhiteSpace(ApplicationUserModelId);

    public bool IsShell32Executable =>
        string.Equals(Path.GetFileName(ExecutablePath), "explorer.exe", StringComparison.OrdinalIgnoreCase);
}
