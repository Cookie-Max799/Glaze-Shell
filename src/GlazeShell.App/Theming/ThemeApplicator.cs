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
    private const string AccentFillKey = "AccentFillColorDefaultBrush";
    private const string TextOnAccentPrimaryKey = "TextOnAccentFillColorPrimaryBrush";
    private const string TextOnAccentSecondaryKey = "TextOnAccentFillColorSecondaryBrush";
    private const string HighlightAccentKey = "SystemControlHighlightAccentBrush";
    private const string BackgroundAccentKey = "SystemControlBackgroundAccentBrush";
    private const string SliderAccentKey = "SliderTrackValueFill";
    private const string ToggleAccentKey = "ToggleSwitchFillOn";
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
    /// Идентификатор основного цвета или <c>null</c>, если поверхности берут цвет темы.
    /// Основной цвет тонирует фон, панели и стекло всех окон, акцент — выделение,
    /// фокус-ринг и брендовый градиент.
    /// </summary>
    public string? BaseColorId { get; private set; }

    /// <summary>
    /// Прозрачность стекла в процентах, действующая сейчас (0–90).
    /// </summary>
    public int GlassTransparency { get; private set; } = UserSettings.DefaultGlassTransparency;

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

        Apply(root, settings.ThemeMode, settings.AccentId, settings.BaseColorId, settings.GlassTransparency, accessibility, system.Palette);
    }

    /// <summary>
    /// Красит системный заголовок окна в цвета действующей темы. Заголовок рисует DWM,
    /// а не XAML, поэтому <see cref="FrameworkElement.RequestedTheme"/> на него не влияет:
    /// без этого вызова тёмное окно оставалось со светлой полосой сверху.
    /// </summary>
    public void ApplyTitleBar(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var light = ThemeModeResolver.Resolve(Mode, SystemUsesLightTheme()) == ThemeMode.Light;
        var theme = GlazePalette.Create(light, BaseColorId, AccentId, GlassTransparency);
        var titleBar = window.AppWindow.TitleBar;

        titleBar.BackgroundColor = theme.Background;
        titleBar.ForegroundColor = theme.TextPrimary;
        titleBar.InactiveBackgroundColor = theme.Background;
        titleBar.InactiveForegroundColor = theme.TextSecondary;
        titleBar.ButtonBackgroundColor = theme.Background;
        titleBar.ButtonForegroundColor = theme.TextPrimary;
        titleBar.ButtonInactiveBackgroundColor = theme.Background;
        titleBar.ButtonInactiveForegroundColor = theme.TextSecondary;
        titleBar.ButtonHoverBackgroundColor = theme.Elevated;
        titleBar.ButtonHoverForegroundColor = theme.TextPrimary;
        titleBar.ButtonPressedBackgroundColor = theme.DefaultAccent;
        titleBar.ButtonPressedForegroundColor = GlazePalette.OnAccent(theme.DefaultAccent);
    }

    public void Apply(FrameworkElement root, ThemeMode mode, string? accentId)
    {
        ArgumentNullException.ThrowIfNull(root);

        var settings = _settings.GetSettings();
        var system = SystemAccessibilityProvider.GetInfo();
        var accessibility = AccessibilityResolver.Resolve(
            settings,
            system.HighContrast,
            system.AnimationsEnabled);

        Apply(root, mode, accentId, settings.BaseColorId, settings.GlassTransparency, accessibility, system.Palette);
    }

    /// <summary>
    /// Красит токены по теме и по правилам доступности. В контрастном режиме системная палитра
    /// пользователя заменяет приглушённые оттенки, а фон стекла становится непрозрачным.
    /// </summary>
    private void Apply(
        FrameworkElement root,
        ThemeMode mode,
        string? accentId,
        string? baseColorId,
        int glassTransparency,
        AccessibilityState accessibility,
        SystemAccessibilityPalette palette)
    {
        var light = ThemeModeResolver.Resolve(mode, SystemUsesLightTheme()) == ThemeMode.Light;

        // Основной цвет перекрашивает поверхности, поэтому палитра собирается до акцента:
        // акцент должен читаться на тонированном фоне.
        var theme = GlazePalette.Create(light, baseColorId, accentId, glassTransparency);
        var accent = theme.DefaultAccent;

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
            ApplyBrushes(theme);
        }

        ApplyFocusVisual(accessibility.HighContrast ? ToColor(palette.Highlight) : theme.AccentForeground);

        // Брендовый градиент — акцентный: он единственный элемент, где акцент виден
        // сплошной заливкой, поэтому смена акцента заметна и на плитках приложений.
        var brand = accessibility.HighContrast ? ToColor(palette.Highlight) : accent;
        ApplyBrandGradient(brand, accessibility.HighContrast ? brand : GlazePalette.Lighten(accent, 0.4));

        Mode = mode;
        AccentId = accentId;
        BaseColorId = baseColorId;
        GlassTransparency = glassTransparency;
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

    private static void ApplyBrushes(GlazeTheme theme)
    {
        var accent = theme.DefaultAccent;
        var accentForeground = theme.AccentForeground;
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
        SetBrush(AccentKey, accentForeground);
        SetBrush(SelectionKey, selection);

        // Стандартные элементы (ползунок, переключатель, флажок, подсветка списков)
        // берут цвет из системных акцентных кистей: без них акцент менял слишком мало.
        // Заливки остаются насыщенными, а текст на них выбирается чёрным или белым.
        SetBrush(AccentFillKey, accent);
        SetBrush(HighlightAccentKey, accentForeground);
        SetBrush(BackgroundAccentKey, accent);
        SetBrush(SliderAccentKey, accent);
        SetBrush(ToggleAccentKey, accent);
        SetBrush(TextOnAccentPrimaryKey, GlazePalette.OnAccent(accent));
        SetBrush(TextOnAccentSecondaryKey, GlazePalette.WithAlpha(GlazePalette.OnAccent(accent), 0xE6));

        SetBrush(ListViewItemPointerOverKey, GlazePalette.WithAlpha(accent, 0x1F));
        SetBrush(ListViewItemPressedKey, GlazePalette.WithAlpha(accent, 0x33));
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
        SetBrush(TextOnAccentPrimaryKey, ToColor(palette.HighlightText));
        SetBrush(TextOnAccentSecondaryKey, ToColor(palette.HighlightText));

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
    /// Брендовый градиент собирается из акцента: первый стоп — сам акцент, второй — его
    /// осветлённый оттенок. В контрастном режиме оба стопа равны системному выделению:
    /// плавные переходы между оттенками читаются плохо.
    /// </summary>
    private static void ApplyBrandGradient(Color start, Color end)
    {
        if (Microsoft.UI.Xaml.Application.Current is not { } app
            || !app.Resources.TryGetValue(BrandGradientKey, out var resource)
            || resource is not LinearGradientBrush gradient)
        {
            return;
        }

        var index = 0;

        foreach (var stop in gradient.GradientStops)
        {
            stop.Color = index++ == 0 ? start : end;
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
