namespace GlazeShell.Core.Models;

/// <summary>
/// Акцентный цвет, доступный пользователю в настройках оформления.
/// </summary>
/// <param name="Id">Идентификатор для пользовательских данных (например, <c>cobalt</c>).</param>
/// <param name="Name">Отображаемое имя.</param>
/// <param name="Hex">Цвет в формате <c>#RRGGBB</c>.</param>
public sealed record AccentColor(string Id, string Name, string Hex);

/// <summary>
/// Набор цветов Glaze Shell. Один и тот же набор используется и для акцента, и для
/// основного цвета поверхностей: пользователь получает одинаково широкий выбор в обоих
/// списках, а сохранённые идентификаторы остаются совместимыми.
/// </summary>
public static class AccentPalette
{
    /// <summary>
    /// Акцент по умолчанию для светлой темы, когда пользователь не выбрал свой.
    /// </summary>
    public const string DefaultLightHex = "#6C4DFF";

    /// <summary>
    /// Акцент по умолчанию для тёмной темы, когда пользователь не выбрал свой.
    /// </summary>
    public const string DefaultDarkHex = "#B4A4FF";

    public static IReadOnlyList<AccentColor> All { get; } = new AccentColor[]
    {
        new("cobalt", "Cobalt", "#4C6FFF"),
        new("indigo", "Indigo", "#5B5BF5"),
        new("violet", "Violet", "#8B5CF6"),
        new("orchid", "Orchid", "#C24BFF"),
        new("magenta", "Magenta", "#F03BE8"),
        new("rose", "Rose", "#FF4D8D"),
        new("crimson", "Crimson", "#F43F5E"),
        new("coral", "Coral", "#FF6B5B"),
        new("peach", "Peach", "#FF8A5C"),
        new("amber", "Amber", "#FFA31A"),
        new("butter", "Butter", "#FFC61A"),
        new("lime", "Lime", "#8BDC2E"),
        new("emerald", "Emerald", "#12C26B"),
        new("mint", "Mint", "#16D99A"),
        new("teal", "Teal", "#00C9A7"),
        new("cyan", "Cyan", "#00C8E0"),
        new("sky", "Sky", "#1FA8FF"),
        new("azure", "Azure", "#2F7BFF"),
        new("slate", "Slate", "#6F86C9"),
        new("graphite", "Graphite", "#64748B")
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
