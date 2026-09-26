namespace GlazeShell.Core.Models;

public sealed record Application
{
    public Application(
        string id,
        string name,
        ApplicationType type = ApplicationType.None,
        string? executablePath = null,
        string? packageFamilyName = null,
        string? applicationUserModelId = null,
        string? iconPath = null,
        string? description = null,
        string? publisher = null,
        string? version = null,
        bool isSystemApplication = false)
    {
        Id = ModelValidation.Required(id, nameof(id));
        Name = ModelValidation.Required(name, nameof(name));
        Type = type;
        ExecutablePath = ModelValidation.Optional(executablePath, nameof(executablePath));
        PackageFamilyName = ModelValidation.Optional(packageFamilyName, nameof(packageFamilyName));
        ApplicationUserModelId = ModelValidation.Optional(applicationUserModelId, nameof(applicationUserModelId));
        IconPath = ModelValidation.Optional(iconPath, nameof(iconPath));
        Description = ModelValidation.Optional(description, nameof(description));
        Publisher = ModelValidation.Optional(publisher, nameof(publisher));
        Version = ModelValidation.Optional(version, nameof(version));
        IsSystemApplication = isSystemApplication;

        if (!IsLaunchable)
        {
            throw new ArgumentException("An application launch identity is required.", nameof(id));
        }
    }

    public string Id { get; }

    public string Name { get; }

    public ApplicationType Type { get; }

    public string? ExecutablePath { get; }

    public string? PackageFamilyName { get; }

    public string? ApplicationUserModelId { get; }

    public string? IconPath { get; }

    public string? Description { get; }

    public string? Publisher { get; }

    public string? Version { get; }

    public bool IsSystemApplication { get; }

    public bool IsLaunchable =>
        !string.IsNullOrWhiteSpace(ExecutablePath) ||
        !string.IsNullOrWhiteSpace(PackageFamilyName) ||
        !string.IsNullOrWhiteSpace(ApplicationUserModelId);
}
