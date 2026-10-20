using GlazeShell.App.Theming;
using GlazeShell.Core.Models;
using Microsoft.UI.Xaml.Media;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Вариант режима оформления для переключателя в интерфейсе.
/// </summary>
/// <param name="Mode">Режим.</param>
/// <param name="Label">Отображаемое имя.</param>
public sealed record ThemeModeOption(ThemeMode Mode, string Label);

/// <summary>
/// Вариант акцентного цвета для переключателя в интерфейсе.
/// </summary>
/// <param name="Id">Идентификатор акцента или <c>null</c> для акцента по умолчанию.</param>
/// <param name="Label">Отображаемое имя.</param>
/// <param name="Preview">Кисть для образца цвета.</param>
public sealed record AccentOption(string? Id, string Label, SolidColorBrush Preview);

/// <summary>
/// Уведомление о смене пользователем темы или акцента оформления.
/// </summary>
public sealed class ThemeSelectionChangedEventArgs : EventArgs
{
    public ThemeSelectionChangedEventArgs(ThemeMode mode, string? accentId)
    {
        Mode = mode;
        AccentId = accentId;
    }

    public ThemeMode Mode { get; }

    public string? AccentId { get; }
}

/// <summary>
/// Сборка списков режимов и акцентов для переключателя оформления.
/// </summary>
internal static class ThemeOptions
{
    public static IReadOnlyList<ThemeModeOption> CreateThemeModes() =>
    [
        new(ThemeMode.System, "Системная"),
        new(ThemeMode.Light, "Светлая"),
        new(ThemeMode.Dark, "Тёмная")
    ];

    public static IReadOnlyList<AccentOption> CreateAccents()
    {
        var options = new List<AccentOption>
        {
            new(null, "По умолчанию", CreatePreview(AccentPalette.DefaultLightHex))
        };

        foreach (var accent in AccentPalette.All)
        {
            options.Add(new AccentOption(accent.Id, accent.Name, CreatePreview(accent.Hex)));
        }

        return options;
    }

    private static SolidColorBrush CreatePreview(string hex)
    {
        var color = GlazePalette.ParseHex(hex, GlazePalette.Light.DefaultAccent);
        return new SolidColorBrush(color);
    }
}
