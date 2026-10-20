namespace GlazeShell.Core.Files;

/// <summary>Системная папка, всегда доступная в сайдбаре Explorer (E2).</summary>
public enum SystemFolderKind
{
    Home = 0,
    Desktop = 1,
    Documents = 2,
    Downloads = 3,
    Pictures = 4,
    Music = 5,
    Videos = 6
}

/// <summary>
/// Готовая к показу системная папка сайдбара (E2): вид, имя и путь. UI получает готовое имя
/// и подбирает значок по виду, поэтому о <see cref="Environment.SpecialFolder"/> знает только
/// Windows-слой.
/// </summary>
public sealed record SystemFolderLocation(SystemFolderKind Kind, string Name, string Path);
