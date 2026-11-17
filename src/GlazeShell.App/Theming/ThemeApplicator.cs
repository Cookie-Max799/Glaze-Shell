using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Core.Services;
using GlazeShell.Windows.SystemAccessibility;
using GlazeShell.Windows.SystemStatus;
using CorePalette = GlazeShell.Core.Theming.GlazePalette;
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
    private const string TopBarGlassKey = "GlazeTopBarGlassBrush";
    private const string DockGlassKey = "GlazeDockGlassBrush";
    private const string StartMenuGlassKey = "GlazeStartMenuGlassBrush";
    private const string PillKey = "GlazeTopBarPillBrush";
    private const string BlackKey = "GlazeBlackBrush";
    private const string WhiteKey = "GlazeWhiteBrush";
    private const string TextPrimaryKey = "GlazeTextPrimaryBrush";
    private const string TextSecondaryKey = "GlazeTextSecondaryBrush";
    private const string TextMutedKey = "GlazeTextMutedBrush";
    private const string AccentKey = "GlazeAccentBrush";
    private const string GlassTextPrimaryKey = "GlazeGlassTextPrimaryBrush";
    private const string GlassTextSecondaryKey = "GlazeGlassTextSecondaryBrush";
    private const string GlassTextMutedKey = "GlazeGlassTextMutedBrush";
    private const string GlassTextAccentKey = "GlazeGlassTextAccentBrush";
    private const string PanelTextPrimaryKey = "GlazePanelTextPrimaryBrush";
    private const string PanelTextSecondaryKey = "GlazePanelTextSecondaryBrush";
    private const string PanelTextMutedKey = "GlazePanelTextMutedBrush";
    private const string PanelTextAccentKey = "GlazePanelTextAccentBrush";
    private const string SelectionKey = "GlazeSelectionBrush";
    private const string HoverKey = "GlazeHoverBrush";
    private const string PressedKey = "GlazePressedBrush";
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
    /// Прозрачность подсветки под курсором и нажатия. Полупрозрачная примесь акцента
    /// читается и на тёмной поверхности, и на светлой, и на стекле панелей.
    /// </summary>
    private const byte HoverAlpha = 0x24;
    private const byte PressedAlpha = 0x40;

    /// <summary>
    /// Высота перехода от плотности полосы к плотности всплывающей панели (DIP). Верхний
    /// край панели примыкает к полосе, поэтому первые пиксели совпадают с ней по цвету и
    /// дальше плавно уплотняются: граница перестаёт читаться как отдельный прямоугольник.
    /// </summary>
    private const double OverlayFadeHeight = 48;

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
    /// Включена ли настраиваемая прозрачность панелей.
    /// </summary>
    public bool TransparencyEnabled { get; private set; } = true;

    /// <summary>
    /// Прозрачность верхней панели в процентах (0–90).
    /// </summary>
    public int TopBarTransparency { get; private set; } = UserSettings.DefaultTopBarTransparency;

    /// <summary>
    /// Прозрачность dock в процентах (0–90).
    /// </summary>
    public int DockTransparency { get; private set; } = UserSettings.DefaultDockTransparency;

    /// <summary>
    /// Прозрачность меню «Пуск» в процентах (0–90).
    /// </summary>
    public int StartMenuTransparency { get; private set; } = UserSettings.DefaultStartMenuTransparency;

    /// <summary>
    /// Действует ли режим высокой контрастности: стекло отключено, цвета системные.
    /// </summary>
    public bool HighContrast { get; private set; }

    /// <summary>
    /// Требуется ли сократить или отключить анимации (доступность, I5).
    /// </summary>
    public bool ReduceMotion { get; private set; }

    public void ApplyFromSettings(FrameworkElement root) => ApplyFromSettings(root, panel: null);

    /// <summary>
    /// Применяет тему к окну и отдельно к раскрытой панели, если она есть.
    /// </summary>
    /// <param name="root">Корень визуального дерева окна.</param>
    /// <param name="panel">
    /// Элемент раскрытой панели («Центр управления», календарь, результаты поиска) или
    /// <see langword="null"/>, если панель закрыта. По его области читается фон под панелью:
    /// он бывает противоположен фону под полосой, и подписи панели выбираются отдельно.
    /// </param>
    public void ApplyFromSettings(FrameworkElement root, FrameworkElement? panel, PanelSurface surface = PanelSurface.TopBar)
    {
        var settings = _settings.GetSettings();
        var system = SystemAccessibilityProvider.GetInfo();
        var accessibility = AccessibilityResolver.Resolve(
            settings,
            system.HighContrast,
            system.AnimationsEnabled);

        Apply(root, panel, settings.ThemeMode, settings.AccentId, settings.BaseColorId, settings.TransparencyEnabled, settings.TopBarTransparency, settings.DockTransparency, settings.StartMenuTransparency, accessibility, system.Palette, surface);
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

        // Заголовок обычного окна не участвует в прозрачности панелей: он всегда плотный,
        // иначе под ним просвечивал бы рабочий стол и текст кнопок терял читаемость.
        var theme = GlazePalette.Create(light, BaseColorId, AccentId, 0);
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

        Apply(root, panel: null, mode, accentId, settings.BaseColorId, settings.TransparencyEnabled, settings.TopBarTransparency, settings.DockTransparency, settings.StartMenuTransparency, accessibility, system.Palette);
    }

    /// <summary>
    /// Красит токены по теме и по правилам доступности. В контрастном режиме системная палитра
    /// пользователя заменяет приглушённые оттенки, а фон стекла становится непрозрачным.
    /// </summary>
    private void Apply(
        FrameworkElement root,
        FrameworkElement? panel,
        ThemeMode mode,
        string? accentId,
        string? baseColorId,
        bool transparencyEnabled,
        int topBarTransparency,
        int dockTransparency,
        int startMenuTransparency,
        AccessibilityState accessibility,
        SystemAccessibilityPalette palette,
        PanelSurface surface = PanelSurface.TopBar)
    {
        var light = ThemeModeResolver.Resolve(mode, SystemUsesLightTheme()) == ThemeMode.Light;

        // Основной цвет перекрашивает поверхности, поэтому палитра собирается до акцента:
        // акцент должен читаться на тонированном фоне.
        // При сборке темы используем TopBarTransparency для универсальных токенов.
        var theme = GlazePalette.Create(light, baseColorId, accentId, topBarTransparency);
        var accent = theme.DefaultAccent;

        // В контрастном режиме решение о светлой или тёмной теме принимает система:
        // собственная тема высокой контрастности Windows уже содержит свои цвета.
        root.RequestedTheme = accessibility.HighContrast
            ? ElementTheme.Default
            : (light ? ElementTheme.Light : ElementTheme.Dark);

        if (accessibility.HighContrast)
        {
            ApplyHighContrast(palette);
            ApplyLocalGlassText(root, theme, false, ToColor(palette.Foreground));
            RememberLocalText(root, theme, false, ToColor(palette.Foreground));
        }
        else
        {
            // Без backdrop полупрозрачная кисть показывала бы чёрное окно: если прозрачность
            // выключена или система не умеет Acrylic, панели остаются плотными.
            var backdropAvailable = transparencyEnabled && PanelBackdrop.IsSupported();
            var topBarAlpha = backdropAvailable
                ? GlazePalette.GlassAlphaFor(topBarTransparency)
                : (byte)0xFF;
            var dockAlpha = backdropAvailable
                ? GlazePalette.GlassAlphaFor(dockTransparency)
                : (byte)0xFF;
            var startMenuAlpha = backdropAvailable
                ? GlazePalette.GlassAlphaFor(startMenuTransparency)
                : (byte)0xFF;

            // Фон читается по области той поверхности, для которой собираются подписи:
            // под полосой и под раскрытым popover он разный, и общая схема на окно
            // делала часть подписей нечитаемой.
            var surfaceLuminance = ReadSurfaceLuminance(root, SurfaceProbe.TopBar);
            var surfaceTransparency = surface switch
            {
                PanelSurface.Dock => dockTransparency,
                PanelSurface.StartMenu => startMenuTransparency,
                _ => topBarTransparency
            };

            var glassText = ResolveGlassTextTheme(
                light,
                baseColorId,
                accentId,
                surfaceTransparency,
                backdropAvailable,
                surfaceLuminance);

            var panelGlassText = ResolveGlassTextTheme(
                light,
                baseColorId,
                accentId,
                surfaceTransparency,
                backdropAvailable,
                ReadSurfaceLuminance(panel ?? root, SurfaceProbe.Panel));

            ApplyBrushes(theme, glassText, topBarTransparency, topBarAlpha, dockAlpha, startMenuAlpha);
            ApplyPanelGlassText(panelGlassText);

            // Локальные кисти вешаются на элементы каждого окна: общие токены лежат
            // в Application.Resources, а они одни на процесс. Любое другое окно
            // перекрашивает их под свой фон, и без собственных кистей подписи полосы
            // зависели бы от того, какое окно применило тему последним.
            //
            // Когда корень окна сам является панелью (Wi-Fi, Bluetooth, dock, плашки),
            // подписи лежат на поверхности панели: им нужна схема панели, а не полосы.
            var surfaceText = surface == PanelSurface.TopBar && panel != root ? theme : panel == root ? panelGlassText : glassText;
            ApplyLocalGlassText(root, surfaceText, panel == root);
            RememberLocalText(root, surfaceText, panel == root, null);
            if (panel is not null && panel != root)
            {
                ApplyLocalGlassText(panel, panelGlassText, true);
                RememberLocalText(panel, panelGlassText, true, null);
            }
        }

        ApplyFocusVisual(accessibility.HighContrast ? ToColor(palette.Highlight) : theme.AccentForeground);

        // Брендовый градиент — акцентный: он единственный элемент, где акцент виден
        // сплошной заливкой, поэтому смена акцента заметна и на плитках приложений.
        var brand = accessibility.HighContrast ? ToColor(palette.Highlight) : accent;
        ApplyBrandGradient(brand, accessibility.HighContrast ? brand : GlazePalette.Lighten(accent, 0.4));

        Mode = mode;
        AccentId = accentId;
        BaseColorId = baseColorId;
        TransparencyEnabled = transparencyEnabled;
        TopBarTransparency = topBarTransparency;
        DockTransparency = dockTransparency;
        StartMenuTransparency = startMenuTransparency;
        HighContrast = accessibility.HighContrast;
        ReduceMotion = accessibility.ReduceMotion;
    }

    /// <summary>
    /// Создаёт Acrylic-backdrop для панели под текущую тему и её ползунок прозрачности.
    /// Возвращает <c>null</c>, когда прозрачность выключена, включён режим высокой
    /// контрастности или система не поддерживает Acrylic: тогда панель остаётся плотной.
    /// </summary>
    /// <param name="surface">Панель: верхняя, dock или меню «Пуск».</param>
    public SystemBackdrop? CreatePanelBackdrop(PanelSurface surface)
    {
        if (HighContrast || !TransparencyEnabled || !PanelBackdrop.IsSupported())
        {
            return null;
        }

        var light = ThemeModeResolver.Resolve(Mode, SystemUsesLightTheme()) == ThemeMode.Light;
        var transparency = Math.Clamp(
            surface switch
            {
                PanelSurface.Dock => DockTransparency,
                PanelSurface.StartMenu => StartMenuTransparency,
                _ => TopBarTransparency,
            },
            0,
            UserSettings.MaxTransparency);

        // Плотность тонирования обратна ползунку: 0 % — плотная панель, 90 % — почти
        // только размытие. Цвет берём непрозрачным: прозрачность задаёт контроллер,
        // а не альфа-канал краски.
        var theme = GlazePalette.Create(light, BaseColorId, AccentId, 0);
        var tint = theme.Glass;
        var opacity = 1 - (transparency / 100.0);

        return new PanelBackdrop(Color.FromArgb(0xFF, tint.R, tint.G, tint.B), opacity);
    }

    /// <summary>
    /// Собирает тему подписей для стеклянных поверхностей. Панель действительно прозрачная,
    /// поэтому её светлота зависит от того, что оказалось под ней: над панелью задач или
    /// светлым окном подписи должны быть тёмными, над тёмным окном — светлыми. Смена цвета
    /// текста, а не плотности стекла, сохраняет прозрачность на всём диапазоне ползунка.
    /// </summary>
    /// <param name="light">Действующая тема приложения.</param>
    /// <param name="baseColorId">Основной цвет темы.</param>
    /// <param name="accentId">Акцент темы.</param>
    /// <param name="transparency">Прозрачность поверхности: она задаёт долю фона в смеси.</param>
    /// <param name="backdropAvailable">Есть ли Acrylic: без него панель остаётся плотной.</param>
    /// <param name="backgroundLuminance">
    /// Яркость фона под поверхностью (0–1). <see langword="null"/> — фон неизвестен, и тогда
    /// схема остаётся темой: она предсказуема и уже проверена на контраст.
    /// </param>
    private static GlazeTheme ResolveGlassTextTheme(
        bool light,
        string? baseColorId,
        string? accentId,
        int transparency,
        bool backdropAvailable,
        double? backgroundLuminance)
    {
        // Без backdrop стеклянные окна рисуются плотной заливкой темы, поэтому подписи
        // остаются обычными.
        if (!backdropAvailable)
        {
            return GlazePalette.Create(light, baseColorId, accentId, 0);
        }

        if (backgroundLuminance is not { } luminance)
        {
            return GlazePalette.Create(light, baseColorId, accentId, 0);
        }

        var core = CorePalette.Create(light, baseColorId, accentId, 0);
        var darkOnLight = GlazePalette.GlassIsLight(core, transparency, luminance);

        return darkOnLight == light
            ? GlazePalette.FromCore(core)
            : GlazePalette.FromCore(CorePalette.Create(!light, baseColorId, accentId, 0));
    }

    /// <summary>
    /// Яркость фона непосредственно под поверхностью окна. Прозрачная панель показывает
    /// не рабочий стол вообще, а конкретный участок экрана: под полосой обычно окно
    /// приложения или панель задач, под popover — часть того же окна. Подписи должны
    /// контрастировать именно с ним.
    /// </summary>
    /// <param name="root">Корень визуального дерева окна с прозрачной поверхностью.</param>
    /// <param name="surface">Какую поверхность окна измерять.</param>
    private static double? ReadSurfaceLuminance(FrameworkElement root, SurfaceProbe surface)
    {
        try
        {
            if (root.ActualWidth < 1 || root.ActualHeight < 1)
            {
                return null;
            }

            var scale = root.XamlRoot?.RasterizationScale ?? 1;

            if (scale <= 0)
            {
                scale = 1;
            }

            // Область отсчёта берётся из указанного элемента, а не из всего окна: у окна
            // верхней панели высота включает раскрытый popover, и средняя яркость по нему
            // относилась бы сразу к двум разным фонам.
            var area = surface == SurfaceProbe.TopBar
                ? new global::Windows.Foundation.Rect(0, 0, root.ActualWidth, BarProbeHeight)
                : new global::Windows.Foundation.Rect(0, 0, root.ActualWidth, root.ActualHeight);

            var bounds = root.TransformToVisual(null).TransformBounds(area);

            var position = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(root.XamlRoot!.ContentIslandEnvironment.AppWindowId).Position;
            var x = position.X + (int)Math.Round(bounds.X * scale);
            var y = position.Y + (int)Math.Round(bounds.Y * scale);
            var width = (int)Math.Round(bounds.Width * scale);
            var height = (int)Math.Round(bounds.Height * scale);

            return ScreenLuminance.Read(x, y, width, height);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // Окно могло закрыться между вызовами: тогда фон неизвестен, и схема подписей
            // остаётся темой.
            return null;
        }
    }

    /// <summary>
    /// Высота полосы верхней панели (DIP): по ней читается фон под ней. Совпадает с
    /// высотой панели в разметке окна.
    /// </summary>
    private const double BarProbeHeight = 30;

    /// <summary>Какая поверхность окна измеряется для выбора схемы подписей.</summary>
    private enum SurfaceProbe
    {
        /// <summary>Полоса верхней панели.</summary>
        TopBar,

        /// <summary>Раскрытые панели: результаты поиска, «Центр управления», календарь.</summary>
        Panel,
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

    private static void ApplyBrushes(GlazeTheme theme, GlazeTheme glassText, int topBarTransparency, byte topBarAlpha, byte dockAlpha, byte startMenuAlpha)
    {
        var accent = theme.DefaultAccent;
        var accentForeground = theme.AccentForeground;
        var selection = GlazePalette.WithAlpha(accent, SelectionAlpha);

        // Popover, результаты Glance и стеклянные карточки следуют за ползунком, но не
        // становятся прозрачнее предела: на пёстром рабочем столе их содержимое должно
        // оставаться читаемым. Верхний стоп градиента совпадает с плотностью полосы,
        // поэтому панель переливается в неё без резкой границы.
        var overlayAlpha = Math.Max(topBarAlpha, GlazePalette.OverlayAlphaFor(topBarTransparency));

        // Плашки панели следуют её прозрачности: на плотной полосе это заметная подложка,
        // на предельно прозрачной — лёгкая примесь, которая не отделяет pill от полосы.
        var pillAlpha = GlazePalette.SurfaceAlphaFor(topBarTransparency);

        SetBrush(BackgroundKey, theme.Background);
        SetBrush(SurfaceKey, theme.Surface);
        SetBrush(ElevatedKey, theme.Elevated);
        SetBrush(BorderKey, theme.Border);
        SetOverlayGlass(
            GlazePalette.WithAlpha(theme.Glass, topBarAlpha),
            GlazePalette.WithAlpha(theme.Glass, overlayAlpha));
        SetBrush(TopBarGlassKey, GlazePalette.WithAlpha(theme.Glass, topBarAlpha));
        SetBrush(DockGlassKey, GlazePalette.WithAlpha(theme.Glass, dockAlpha));
        SetBrush(StartMenuGlassKey, GlazePalette.WithAlpha(theme.Glass, startMenuAlpha));
        SetBrush(PillKey, GlazePalette.WithAlpha(theme.Elevated, Math.Max(pillAlpha, (byte)0xE6)));
        SetBrush(BlackKey, Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
        SetBrush(WhiteKey, Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        SetBrush(TextPrimaryKey, theme.TextPrimary);
        SetBrush(TextSecondaryKey, theme.TextSecondary);
        SetBrush(TextMutedKey, theme.TextMuted);
        SetBrush(AccentKey, accentForeground);

        // Подписи на стекле выбираются по светлоте рабочего стола: панель прозрачная,
        // и на светлых обоях тёмный текст читается лучше светлого.
        SetBrush(GlassTextPrimaryKey, glassText.TextPrimary);
        SetBrush(GlassTextSecondaryKey, glassText.TextSecondary);
        SetBrush(GlassTextMutedKey, glassText.TextMuted);
        SetBrush(GlassTextAccentKey, glassText.AccentForeground);

        SetBrush(SelectionKey, selection);
        SetBrush(HoverKey, GlazePalette.WithAlpha(accent, HoverAlpha));
        SetBrush(PressedKey, GlazePalette.WithAlpha(accent, PressedAlpha));

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
    /// Подписи раскрытых панелей. Схема может отличаться от полосы: popover занимает
    /// другую часть экрана, и фон под ним бывает противоположным. Отдельные токены нужны,
    /// чтобы поправить читаемость панели, не трогая полосу и остальные окна.
    /// </summary>
    private static void ApplyPanelGlassText(GlazeTheme glassText)
    {
        SetBrush(PanelTextPrimaryKey, glassText.TextPrimary);
        SetBrush(PanelTextSecondaryKey, glassText.TextSecondary);
        SetBrush(PanelTextMutedKey, glassText.TextMuted);
        SetBrush(PanelTextAccentKey, glassText.AccentForeground);
    }

    private sealed record LocalGlassText(SolidColorBrush Brush, int Role);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DependencyObject, LocalGlassText> LocalGlassBrushes = new();

    private sealed record LocalTextState(GlazeTheme Theme, bool IncludeStandardText, Color? OverrideColor);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, LocalTextState> LocalTextStates = new();

    /// <summary>
    /// Перекрашивает элементы окна в схему подписей, вычисленную при последнем
    /// <see cref="Apply"/>. Нужно спискам с шаблонами: их контейнеры создаются во время
    /// раскладки, уже после применения темы, и берут общие кисти. К этому моменту общие
    /// кисти могло перекрасить соседнее окно под свой фон, поэтому список получал цвет
    /// чужой поверхности. Повторный проход возвращает подписям схему своего окна.
    /// </summary>
    public static void RefreshLocalText(FrameworkElement root)
    {
        if (LocalTextStates.TryGetValue(root, out var state))
        {
            ApplyLocalGlassText(root, state.Theme, state.IncludeStandardText, state.OverrideColor);
        }
    }

    private static void RememberLocalText(
        FrameworkElement root,
        GlazeTheme theme,
        bool includeStandardText,
        Color? overrideColor) =>
        LocalTextStates.AddOrUpdate(root, new LocalTextState(theme, includeStandardText, overrideColor));

    private static void ApplyLocalGlassText(DependencyObject element, GlazeTheme theme, bool includeStandardText, Color? overrideColor = null)
    {
        var foreground = element switch
        {
            Microsoft.UI.Xaml.Controls.Control control => control.Foreground,
            Microsoft.UI.Xaml.Controls.TextBlock text => text.Foreground,
            Microsoft.UI.Xaml.Controls.IconElement icon => icon.Foreground,
            // Лампочка громкости и другие значки-контуры рисуются Path: у фигур цвет
            // задаёт обводка, и без неё они оставались бы цветом чужого окна.
            Microsoft.UI.Xaml.Shapes.Shape shape => shape.Stroke,
            _ => null
        };
        var resources = Microsoft.UI.Xaml.Application.Current.Resources;
        var keys = includeStandardText
            ? new[] { GlassTextPrimaryKey, GlassTextSecondaryKey, GlassTextMutedKey, GlassTextAccentKey, PanelTextPrimaryKey, PanelTextSecondaryKey, PanelTextMutedKey, PanelTextAccentKey, TextPrimaryKey, TextSecondaryKey, TextMutedKey, AccentKey }
            : new[] { GlassTextPrimaryKey, GlassTextSecondaryKey, GlassTextMutedKey, GlassTextAccentKey, PanelTextPrimaryKey, PanelTextSecondaryKey, PanelTextMutedKey, PanelTextAccentKey };
        var index = Array.FindIndex(keys, key => ReferenceEquals(foreground, resources[key]));
        if (index >= 0 || LocalGlassBrushes.TryGetValue(element, out _))
        {
            var local = LocalGlassBrushes.GetValue(element, _ => new LocalGlassText(new SolidColorBrush(), index % 4));
            var brush = local.Brush;
            brush.Color = overrideColor ?? (local.Role switch
            {
                1 => theme.TextSecondary,
                2 => theme.TextMuted,
                3 => theme.AccentForeground,
                _ => theme.TextPrimary
            });
            switch (element)
            {
                case Microsoft.UI.Xaml.Controls.Control control: control.Foreground = brush; break;
                case Microsoft.UI.Xaml.Controls.TextBlock text: text.Foreground = brush; break;
                case Microsoft.UI.Xaml.Controls.IconElement icon: icon.Foreground = brush; break;
                case Microsoft.UI.Xaml.Shapes.Shape shape: shape.Stroke = brush; break;
            }
        }
        if (element is Microsoft.UI.Xaml.Controls.TextBox textBox)
        {
            var primary = new SolidColorBrush(overrideColor ?? theme.TextPrimary);
            var secondary = new SolidColorBrush(overrideColor ?? theme.TextSecondary);
            textBox.Foreground = primary;
            textBox.PlaceholderForeground = secondary;
            foreach (var key in new[] { "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused" })
            {
                textBox.Resources[key] = primary;
            }
            foreach (var key in new[] { "TextControlPlaceholderForeground", "TextControlPlaceholderForegroundPointerOver", "TextControlPlaceholderForegroundFocused" })
            {
                textBox.Resources[key] = secondary;
            }
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            ApplyLocalGlassText(VisualTreeHelper.GetChild(element, i), theme, includeStandardText, overrideColor);
        }
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
        SetOverlayGlass(background, background);
        SetBrush(TopBarGlassKey, background);
        SetBrush(DockGlassKey, background);
        SetBrush(StartMenuGlassKey, background);
        SetBrush(PillKey, surface);
        SetBrush(BlackKey, Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
        SetBrush(WhiteKey, Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        SetBrush(TextPrimaryKey, foreground);
        SetBrush(TextSecondaryKey, surfaceText);
        SetBrush(TextMutedKey, muted);
        SetBrush(AccentKey, highlight);

        // В высокой контрастности стекло непрозрачное, поэтому подписи на нём совпадают
        // с системными цветами обычных поверхностей.
        SetBrush(GlassTextPrimaryKey, foreground);
        SetBrush(GlassTextSecondaryKey, surfaceText);
        SetBrush(GlassTextMutedKey, muted);
        SetBrush(GlassTextAccentKey, highlight);
        SetBrush(PanelTextPrimaryKey, foreground);
        SetBrush(PanelTextSecondaryKey, surfaceText);
        SetBrush(PanelTextMutedKey, muted);
        SetBrush(PanelTextAccentKey, highlight);
        SetBrush(SelectionKey, selection);
        SetBrush(HoverKey, selection);
        SetBrush(PressedKey, GlazePalette.WithAlpha(highlight, ContrastSelectionAlpha));
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

    /// <summary>
    /// Перекрашивает оба стопа градиента всплывающих панелей: верхний совпадает с полосой,
    /// нижний задаёт плотность самой панели. Фиксированная высота перехода делает стык
    /// мягким при любой разнице прозрачностей.
    /// </summary>
    private static void SetOverlayGlass(Color top, Color bottom)
    {
        if (Microsoft.UI.Xaml.Application.Current is not { } app
            || !app.Resources.TryGetValue(GlassKey, out var resource)
            || resource is not LinearGradientBrush brush
            || brush.GradientStops.Count < 2)
        {
            return;
        }

        brush.StartPoint = new global::Windows.Foundation.Point(0.5, 0);
        brush.EndPoint = new global::Windows.Foundation.Point(0.5, OverlayFadeHeight);
        brush.GradientStops[0].Color = top;
        brush.GradientStops[1].Color = bottom;
    }
}
