using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;
using GlazeShell.Windows.SystemAccessibility;
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
/// <remarks>
/// Здесь же живёт поддержка доступности (функция I5): в режиме высокой контрастности
/// приглушённые цвета и стекло заменяются системными, а фокус-ринг становится контрастным.
/// </remarks>
public sealed class ThemeApplicator
{
    private const string BackgroundKey = "GlazeBackgroundBrush";
    private const string SurfaceKey = "GlazeSurfaceBrush";
    private const string ElevatedKey = "GlazeElevatedBrush";
    private const string BorderKey = "GlazeBorderBrush";
    private const string GlassKey = "GlazeGlassBrush";
    private const string BlackKey = "GlazeBlackBrush";
    private const string WhiteKey = "GlazeWhiteBrush";
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
    private const string FocusPrimaryKey = "FocusVisualPrimaryBrush";
    private const string FocusSecondaryKey = "FocusVisualSecondaryBrush";
    private const string BrandGradientKey = "GlazeBrandGradientBrush";

    private const byte SelectionAlpha = 0x33;

    /// <summary>
    /// Прозрачность выделения в контрастном режиме. Выделение остаётся заметным, но текст
    /// поверх него читается: непрозрачный системный <c>Highlight</c> с обычным текстом
    /// нечитаем на светлых схемах.
    /// </summary>
    private const byte ContrastSelectionAlpha = 0x59;

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

    /// <summary>
    /// Действует ли режим высокой контрастности: стекло отключено, цвета системные.
    /// </summary>
    public bool HighContrast { get; private set; }

    /// <summary>
    /// Требуется ли сократить или отключить анимации (доступность, I5).
    /// </summary>
    public bool ReduceMotion { get; private set; }

    public void ApplyFromSettings(FrameworkElement root)
    {
        var settings = _settings.GetSettings();
        var system = SystemAccessibilityProvider.GetInfo();
        var accessibility = AccessibilityResolver.Resolve(
            settings,
            system.HighContrast,
            system.AnimationsEnabled);

        Apply(root, settings.ThemeMode, settings.AccentId, accessibility, system.Palette);
    }

    public void Apply(FrameworkElement root, ThemeMode mode, string? accentId)
    {
        ArgumentNullException.ThrowIfNull(root);

        var system = SystemAccessibilityProvider.GetInfo();
        var accessibility = AccessibilityResolver.Resolve(
            _settings.GetSettings(),
            system.HighContrast,
            system.AnimationsEnabled);

        Apply(root, mode, accentId, accessibility, system.Palette);
    }

    /// <summary>
    /// Красит токены по теме и по правилам доступности. В контрастном режиме системная палитра
    /// пользователя заменяет приглушённые оттенки, а фон стекла становится непрозрачным.
    /// </summary>
    private void Apply(
        FrameworkElement root,
        ThemeMode mode,
        string? accentId,
        AccessibilityState accessibility,
        SystemAccessibilityPalette palette)
    {
        var light = ThemeModeResolver.Resolve(mode, SystemUsesLightTheme()) == ThemeMode.Light;
        var theme = light ? GlazePalette.Light : GlazePalette.Dark;
        var accent = ResolveAccent(accentId, theme);

        // В контрастном режиме решение о светлой или тёмной теме принимает система:
        // собственная тема высокой контрастности Windows уже содержит свои цвета.
        root.RequestedTheme = accessibility.HighContrast
            ? ElementTheme.Default
            : (light ? ElementTheme.Light : ElementTheme.Dark);

        if (accessibility.HighContrast)
        {
            ApplyHighContrast(palette);
        }
        else
        {
            ApplyBrushes(theme, accent);
        }

        ApplyFocusVisual(accessibility.HighContrast ? ToColor(palette.Highlight) : accent);
        ApplyBrandGradient(accessibility.HighContrast ? ToColor(palette.Highlight) : null);

        Mode = mode;
        AccentId = accentId;
        HighContrast = accessibility.HighContrast;
        ReduceMotion = accessibility.ReduceMotion;
    }

    /// <summary>
    /// Читает системную настройку «светлая/тёмная тема приложений». Если значение
    /// отсутствует или недоступно, считается тема светлая (как в Windows по умолчанию).
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
        SetBrush(BlackKey, Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
        SetBrush(WhiteKey, Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
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

    /// <summary>
    /// Перекрашивает токены в системные цвета темы высокой контрастности. Стекло становится
    /// непрозрачным фоном, приглушённый текст — системным «серым», а акцент — системным
    /// выделением.
    /// </summary>
    private static void ApplyHighContrast(SystemAccessibilityPalette palette)
    {
        var background = ToColor(palette.Background);
        var foreground = ToColor(palette.Foreground);
        var highlight = ToColor(palette.Highlight);
        var surface = ToColor(palette.Surface);
        var surfaceText = ToColor(palette.SurfaceText);
        var muted = ToColor(palette.Muted);
        var selection = GlazePalette.WithAlpha(highlight, ContrastSelectionAlpha);

        SetBrush(BackgroundKey, background);
        SetBrush(SurfaceKey, surface);
        SetBrush(ElevatedKey, surface);
        SetBrush(BorderKey, foreground);
        SetBrush(GlassKey, background);
        SetBrush(BlackKey, Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
        SetBrush(WhiteKey, Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        SetBrush(TextPrimaryKey, foreground);
        SetBrush(TextSecondaryKey, surfaceText);
        SetBrush(TextMutedKey, muted);
        SetBrush(AccentKey, highlight);
        SetBrush(SelectionKey, selection);

        SetBrush(ListViewItemPointerOverKey, surface);
        SetBrush(ListViewItemPressedKey, surface);
        SetBrush(ListViewItemSelectedKey, selection);
        SetBrush(ListViewItemSelectedPointerOverKey, selection);
        SetBrush(ListViewItemSelectedPressedKey, selection);
    }

    /// <summary>
    /// Настраивает встроенный фокус-ринг WinUI: прозрачная заливка и акцентная обводка.
    /// Это и есть требуемый DESIGN §7 ринг — 2 px акцентом с отступом.
    /// </summary>
    private static void ApplyFocusVisual(Color accent)
    {
        SetBrush(FocusPrimaryKey, Color.FromArgb(0, 0, 0, 0));
        SetBrush(FocusSecondaryKey, accent);
    }

    /// <summary>
    /// В контрастном режиме брендовый градиент заменяется сплошным системным выделением:
    /// плавные переходы между оттенками читаются плохо.
    /// </summary>
    private static void ApplyBrandGradient(Color? solid)
    {
        if (solid is not { } color)
        {
            return;
        }

        if (Microsoft.UI.Xaml.Application.Current is { } app
            && app.Resources.TryGetValue(BrandGradientKey, out var resource)
            && resource is LinearGradientBrush gradient)
        {
            foreach (var stop in gradient.GradientStops)
            {
                stop.Color = color;
            }
        }
    }

    private static Color ToColor(SystemColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

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
