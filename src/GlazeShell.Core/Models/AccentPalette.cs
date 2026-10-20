namespace GlazeShell.Core.Models;

/// <summary>
/// Акцентный цвет, доступный пользователю в настройках оформления.
/// </summary>
/// <param name="Id">Идентификатор для пользовательских данных (например, <c>cobalt</c>).</param>
/// <param name="Name">Отображаемое имя.</param>
/// <param name="Hex">Цвет в формате <c>#RRGGBB</c>.</param>
public sealed record AccentColor(string Id, string Name, string Hex);

/// <summary>
/// Набор акцентных цветов Glaze Shell. Значения соответствуют дизайн-системе
/// (<c>docs/design/tokens/tokens.json</c> → <c>color.accent-options</c>).
/// </summary>
public static class AccentPalette
{
    /// <summary>
    /// Акцент по умолчанию для светлой темы, когда пользователь не выбрал свой.
    /// </summary>
    public const string DefaultLightHex = "#6A5BEA";

    /// <summary>
    /// Акцент по умолчанию для тёмной темы, когда пользователь не выбрал свой.
    /// </summary>
    public const string DefaultDarkHex = "#A99BFF";

    public static IReadOnlyList<AccentColor> All { get; } = new AccentColor[]
    {
        new("cobalt", "Cobalt", "#5E7BFF"),
        new("orchid", "Orchid", "#BF5AF2"),
        new("rose", "Rose", "#FF6F9F"),
        new("peach", "Peach", "#FF9A76"),
        new("mint", "Mint", "#3FCF9B"),
        new("slate", "Slate", "#8A88B5")
    };

    public static bool IsKnown(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var normalized = id.Trim();

        foreach (var accent in All)
        {
            if (string.Equals(accent.Id, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static AccentColor? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var normalized = id.Trim();

        foreach (var accent in All)
        {
            if (string.Equals(accent.Id, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return accent;
            }
        }

        return null;
    }
}
