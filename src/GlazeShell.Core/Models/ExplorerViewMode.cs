namespace GlazeShell.Core.Models;

/// <summary>Вид списка Explorer (E1), сохраняемый в настройках.</summary>
public enum ExplorerViewMode
{
    Icons = 0,
    List = 1
}

/// <summary>
/// Строковые идентификаторы вида Explorer для пользовательских данных.
/// </summary>
public static class ExplorerViewModeExtensions
{
    public const string IconsId = "icons";

    public const string ListId = "list";

    public static string ToId(this ExplorerViewMode mode) =>
        mode == ExplorerViewMode.List ? ListId : IconsId;

    public static bool TryParse(string? value, out ExplorerViewMode mode)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case IconsId:
                mode = ExplorerViewMode.Icons;
                return true;
            case ListId:
                mode = ExplorerViewMode.List;
                return true;
            default:
                mode = ExplorerViewMode.Icons;
                return false;
        }
    }
}
