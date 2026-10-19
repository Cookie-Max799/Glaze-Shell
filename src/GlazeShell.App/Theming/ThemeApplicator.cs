using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GlazeShell.App.Theming;

/// <summary>
/// Применяет тему оформления Glaze Shell к визуальному дереву: переключает
/// <see cref="FrameworkElement.RequestedTheme"/> (светлая/тёмная) и обновляет цвета
/// токенов-кистей в ресурсах приложения. Собственные кисти меняются на месте, поэтому
/// уже открытые элементы перекрашиваются без перезагрузки XAML.
/// </summary>
public sealed class ThemeApplicator
{
    private const string BackgroundKey = "GlazeBackgroundBrush";
    private const string SurfaceKey = "GlazeSurfaceBrush";
    private const string ElevatedKey = "GlazeElevatedBrush";
    private const string BorderKey = "GlazeBorderBrush";
    private const string GlassKey = "GlazeGlassBrush";
    private const string GlassBorderKey = "GlazeGlassBorderBrush";
    private const string TextPrimaryKey = "GlazeTextPrimaryBrush";
    private const string TextSecondaryKey = "GlazeTextSecondaryBrush";
    private const string TextMutedKey = "GlazeTextMutedBrush";
    private const string AccentKey = "GlazeAccentBrush";
    private const string SelectionKey = "GlazeSelectionBrush";
    private const string ListViewItemPointerOverKey = "ListViewItemBackgroundPointerOver";
    private const string ListViewItemPressedKey = "ListViewItemBackgroundPressed";
    private const string ListViewItemSelectedKey = "ListViewItemBackgroundSelected";
    private const string ListViewItemSelectedPointerOverKey = "ListViewItemBackgroundSelectedPointerOver";
    private const string ListViewItemSelectedPressedKey = "ListViewItemBackgroundSelectedPressed";

    private const byte SelectionAlpha = 0x33;

    private readonly ISettingsManager _settings;

    public ThemeApplicator(ISettingsManager settings) =>
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>
    /// Режим, выбранный пользователем (не разрешённый во светлую/тёмную).
    /// </summary>
    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    /// <summary>
    /// Идентификатор акцента или <c>null</c>, если используется акцент темы по умолчанию.
    /// </summary>
    public string? AccentId { get; private set; }

    public void ApplyFromSettings(FrameworkElement root)
    {
        var settings = _settings.GetSettings();
        Apply(root, settings.ThemeMode, settings.AccentId);
    }

    public void Apply(FrameworkElement root, ThemeMode mode, string? accentId)
    {
        ArgumentNullException.ThrowIfNull(root);

        var light = ThemeModeResolver.Resolve(mode, SystemUsesLightTheme()) == ThemeMode.Light;
        var theme = light ? GlazePalette.Light : GlazePalette.Dark;
        var accent = ResolveAccent(accentId, theme);

        root.RequestedTheme = light ? ElementTheme.Light : ElementTheme.Dark;
        ApplyBrushes(theme, accent);

        Mode = mode;
        AccentId = accentId;
    }

    /// <summary>
    /// Читает системную настройку «светлая/тёмная тема приложений». Если значение
    /// отсутствует или недоступно, считает тему светлой (как в Windows по умолчанию).
    /// </summary>
    public static bool SystemUsesLightTheme()
    {
        try
        {
            var value = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);

            return value is int intValue && intValue != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static Color ResolveAccent(string? accentId, GlazeTheme theme)
    {
        var accent = AccentPalette.Find(accentId);
        return accent is null ? theme.DefaultAccent : GlazePalette.ParseHex(accent.Hex, theme.DefaultAccent);
    }

    private static void ApplyBrushes(GlazeTheme theme, Color accent)
    {
        var selection = GlazePalette.WithAlpha(accent, SelectionAlpha);

        SetBrush(BackgroundKey, theme.Background);
        SetBrush(SurfaceKey, theme.Surface);
        SetBrush(ElevatedKey, theme.Elevated);
        SetBrush(BorderKey, theme.Border);
        SetBrush(GlassKey, theme.Glass);
        SetBrush(GlassBorderKey, theme.GlassBorder);
        SetBrush(TextPrimaryKey, theme.TextPrimary);
        SetBrush(TextSecondaryKey, theme.TextSecondary);
        SetBrush(TextMutedKey, theme.TextMuted);
        SetBrush(AccentKey, accent);
        SetBrush(SelectionKey, selection);

        SetBrush(ListViewItemPointerOverKey, theme.Elevated);
        SetBrush(ListViewItemPressedKey, theme.Elevated);
        SetBrush(ListViewItemSelectedKey, selection);
        SetBrush(ListViewItemSelectedPointerOverKey, selection);
        SetBrush(ListViewItemSelectedPressedKey, selection);
    }

    private static void SetBrush(string key, Color color)
    {
        if (Microsoft.UI.Xaml.Application.Current is { } app
            && app.Resources.TryGetValue(key, out var resource)
            && resource is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }
}
