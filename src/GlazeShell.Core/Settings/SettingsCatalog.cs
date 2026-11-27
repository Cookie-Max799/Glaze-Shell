using GlazeShell.Core.Models;
using GlazeShell.Core.Recovery;

namespace GlazeShell.Core.Settings;

/// <summary>
/// Декларативный каталог настроек Glaze (I1): страницы и их настройки, а также поиск по ним.
/// Каждая функция регистрирует свою страницу здесь, а окно настроек рисует её по описанию —
/// поэтому добавление настройки не требует правок интерфейса. Каталог не знает про WinUI и
/// покрыт тестами.
/// </summary>
public static class SettingsCatalog
{
    /// <summary>Идентификатор страницы «Оформление».</summary>
    public const string AppearancePageId = "appearance";

    /// <summary>Идентификатор страницы «Прозрачность».</summary>
    public const string TransparencyPageId = "transparency";

    /// <summary>Идентификатор страницы «Dock».</summary>
    public const string DockPageId = "dock";

    /// <summary>Идентификатор страницы «Верхняя панель».</summary>
    public const string TopBarPageId = "topbar";

    /// <summary>Идентификатор страницы «Переключение приложений».</summary>
    public const string SwitcherPageId = "switcher";

    /// <summary>Идентификатор страницы «Проводник».</summary>
    public const string ExplorerPageId = "explorer";

    /// <summary>Идентификатор страницы «Доступность».</summary>
    public const string AccessibilityPageId = "accessibility";

    /// <summary>Идентификатор страницы «Восстановление».</summary>
    public const string RecoveryPageId = "recovery";

    /// <summary>Идентификатор страницы «Оформление Windows».</summary>
    public const string WindowsPageId = "windows";

    /// <summary>Идентификатор страницы «О программе».</summary>
    public const string AboutPageId = "about";
    /// <summary>Все страницы настроек в порядке сайдбара.</summary>
    public static IReadOnlyList<SettingsPage> Pages { get; } =
    [
        new(AppearancePageId, "Оформление", "\uE790", "Основные"),
        new(TransparencyPageId, "Прозрачность", "\uE8A7", "Основные"),
        new(DockPageId, "Dock", "\uE8FC", "Интерфейс"),
        new(TopBarPageId, "Верхняя панель", "\uE7C4", "Интерфейс"),
        new(SwitcherPageId, "Переключение", "\uE8AB", "Интерфейс"),
        new(ExplorerPageId, "Проводник", "\uE8B7", "Интерфейс"),
        new(WindowsPageId, "Оформление Windows", "\uE771", "Система"),
        new(AccessibilityPageId, "Доступность", "\uE776", "Система"),
        new(RecoveryPageId, "Восстановление", "\uE90F", "Система"),
        new(AboutPageId, "О программе", "\uE946", "Система"),
    ];

    /// <summary>Настройки, сгруппированные по странице, в порядке отображения.</summary>
    public static IReadOnlyList<SettingDefinition> All { get; } = BuildDefinitions();

    /// <summary>
    /// Настройки страницы в порядке отображения. Неизвестная страница даёт пустой список.
    /// </summary>
    public static IReadOnlyList<SettingDefinition> ForPage(string pageId) =>
        All.Where(definition => string.Equals(definition.PageId, pageId, StringComparison.Ordinal)).ToArray();

    /// <summary>
    /// Настройки, совпадающие с запросом. Пустой запрос возвращает весь каталог, чтобы поиск
    /// без текста показывал полный список, а не пустоту.
    /// </summary>
    public static IReadOnlyList<SettingDefinition> Search(string? query) =>
        All.Where(definition => definition.Matches(query)).ToArray();

    /// <summary>
    /// Копия настроек, где каждая настройка страницы сброшена к значению из
    /// <see cref="UserSettings.CreateDefault"/>. Используется кнопкой «Сбросить страницу».
    /// </summary>
    public static UserSettings Reset(UserSettings current, string pageId)
    {
        ArgumentNullException.ThrowIfNull(current);

        var defaults = UserSettings.CreateDefault();
        var result = current;

        foreach (var definition in ForPage(pageId))
        {
            result = definition.Write(result, definition.Read(defaults)!);
        }

        return result;
    }

    private static IReadOnlyList<SettingDefinition> BuildDefinitions() =>
    [
        // Оформление.
        new(
            "themeMode",
            AppearancePageId,
            "Тема оформления",
            "Светлая, тёмная или системная тема Glaze Shell.",
            ["тема", "theme", "dark", "light", "тёмная", "светлая"],
            SettingKind.Choice,
            settings => settings.ThemeMode.ToString(),
            (settings, value) => UserSettingsEditor.With(settings, themeMode: ParseThemeMode(value)),
            [
                new(ThemeMode.System.ToString(), "Системная"),
                new(ThemeMode.Light.ToString(), "Светлая"),
                new(ThemeMode.Dark.ToString(), "Тёмная"),
            ]),

        new(
            "accentId",
            AppearancePageId,
            "Акцентный цвет",
            "Тон выделения, ссылок и выбранных элементов.",
            ["акцент", "accent", "цвет", "color", "тон"],
            SettingKind.Choice,
            settings => settings.AccentId ?? string.Empty,
            (settings, value) =>
            {
                var accent = value as string;
                return string.IsNullOrWhiteSpace(accent)
                    ? UserSettingsEditor.With(settings, clearAccent: true)
                    : UserSettingsEditor.With(settings, accentId: accent);
            },
            BuildAccentChoices()),

        new(
            "baseColorId",
            AppearancePageId,
            "Основной цвет",
            "Цвет фона, панелей и стекла всех окон Glaze Shell.",
            ["основной", "base", "фон", "поверхность", "поверхности", "цвет"],
            SettingKind.Choice,
            settings => settings.BaseColorId ?? string.Empty,
            (settings, value) =>
            {
                var baseColor = value as string;
                return string.IsNullOrWhiteSpace(baseColor)
                    ? UserSettingsEditor.With(settings, clearBaseColor: true)
                    : UserSettingsEditor.With(settings, baseColorId: baseColor);
            },
            BuildBaseColorChoices()),

        // Прозрачность панелей: отдельная страница, потому что это независимые ползунки
        // для верхней панели и dock, а не один общий параметр стекла.
        new(
            "transparencyEnabled",
            TransparencyPageId,
            "Включить прозрачность",
            "Верхняя панель, dock и меню «Пуск» пропускают рабочий стол. Когда выключено, панели остаются непрозрачными.",
            ["прозрачность", "transparency", "прозрачный", "стекло", "glass", "панели", "эффект"],
            SettingKind.Toggle,
            settings => settings.TransparencyEnabled,
            (settings, value) => UserSettingsEditor.With(settings, transparencyEnabled: AsBool(value)),
            []),

        new(
            "topBarTransparency",
            TransparencyPageId,
            "Прозрачность верхней панели",
            "0 % — плотная заливка, 90 % — панель почти растворяется в рабочем столе.",
            ["прозрачность", "верхняя панель", "top bar", "transparency", "стекло", "фон"],
            SettingKind.Range,
            settings => (double)settings.TopBarTransparency,
            (settings, value) => UserSettingsEditor.With(
                settings,
                topBarTransparency: (int)Math.Round(AsDouble(value))),
            [],
            Maximum: UserSettings.MaxTransparency,
            Unit: "%",
            IsEnabledBy: "transparencyEnabled"),

        new(
            "dockTransparency",
            TransparencyPageId,
            "Прозрачность нижней панели",
            "0 % — плотная капсула, 90 % — dock почти сливается с рабочим столом.",
            ["прозрачность", "нижняя панель", "док", "dock", "transparency", "стекло", "фон"],
            SettingKind.Range,
            settings => (double)settings.DockTransparency,
            (settings, value) => UserSettingsEditor.With(
                settings,
                dockTransparency: (int)Math.Round(AsDouble(value))),
            [],
            Maximum: UserSettings.MaxTransparency,
            Unit: "%",
            IsEnabledBy: "transparencyEnabled"),

        new(
            "startMenuTransparency",
            TransparencyPageId,
            "Прозрачность меню «Пуск»",
            "0 % — плотное меню, 90 % — панель почти растворяется в рабочем столе.",
            ["прозрачность", "меню", "пуск", "start", "start menu", "transparency", "стекло", "фон"],
            SettingKind.Range,
            settings => (double)settings.StartMenuTransparency,
            (settings, value) => UserSettingsEditor.With(
                settings,
                startMenuTransparency: (int)Math.Round(AsDouble(value))),
            [],
            Maximum: UserSettings.MaxTransparency,
            Unit: "%",
            IsEnabledBy: "transparencyEnabled"),

        // Dock.
        new(
            "dockEnabled",
            DockPageId,
            "Показывать dock",
            "Плавающая панель приложений у нижнего края экрана.",
            ["док", "dock", "панель", "taskbar"],
            SettingKind.Toggle,
            settings => settings.DockEnabled,
            (settings, value) => UserSettingsEditor.With(settings, dockEnabled: AsBool(value)),
            []),

        new(
            "dockReplacesTaskbar",
            DockPageId,
            "Заменять панель задач",
            "Системная панель задач скрывается, пока работает dock.",
            ["панель задач", "taskbar", "замена", "скрыть"],
            SettingKind.Toggle,
            settings => settings.DockReplacesTaskbar,
            (settings, value) => UserSettingsEditor.With(settings, dockReplacesTaskbar: AsBool(value)),
            []),

        // Верхняя панель.
        new(
            "topBarEnabled",
            TopBarPageId,
            "Показывать верхнюю панель",
            "Плавающая панель сверху: активное приложение, поиск, статус и часы.",
            ["верхняя панель", "top bar", "менюбар", "menubar"],
            SettingKind.Toggle,
            settings => settings.TopBarEnabled,
            (settings, value) => UserSettingsEditor.With(settings, topBarEnabled: AsBool(value)),
            []),

        new(
            "hidePanelsInGlazeShell",
            TopBarPageId,
            "Скрывать панели внутри Glaze Shell",
            "Верхняя и нижняя панели убираются в окнах Glaze Shell. На рабочем столе панели возвращаются; в приложениях их можно вызвать у кромки экрана.",
            ["скрывать", "автоскрытие", "панели", "верхняя панель", "dock", "свайп", "представление задач"],
            SettingKind.Toggle,
            settings => settings.HidePanelsInGlazeShell,
            (settings, value) => UserSettingsEditor.With(settings, hidePanelsInGlazeShell: AsBool(value)),
            []),

        new(
            "topBarHidesWithQuickSettings",
            TopBarPageId,
            "Закрывать верхнюю панель при открытом центре управления",
            "Верхняя панель убирается по обычным правилам, даже когда открыт «Центр управления»: popover следует за ней и уезжает вместе с полосой. Когда параметр выключен, открытый центр управления удерживает панель на экране.",
            ["центр управления", "quick settings", "панель", "автоскрытие", "popover"],
            SettingKind.Toggle,
            settings => settings.TopBarHidesWithQuickSettings,
            (settings, value) => UserSettingsEditor.With(settings, topBarHidesWithQuickSettings: AsBool(value)),
            []),

        new(
            "topBarVolumePercent",
            TopBarPageId,
            "Цифры громкости в верхней панели",
            "Показывать рядом со значком громкости маленькое число текущего уровня (0–100).",
            ["громкость", "звук", "цифры", "проценты", "верхняя панель", "volume"],
            SettingKind.Toggle,
            settings => settings.TopBarVolumePercent,
            (settings, value) => UserSettingsEditor.With(settings, topBarVolumePercent: AsBool(value)),
            []),

        new(
            "topBarDeviceRowLimit",
            TopBarPageId,
            "Значков устройств в строке",
            "Сколько подключённых устройств с зарядом показывать значками в верхней панели. Когда устройств больше, строка сворачивается в один значок со списком.",
            ["устройства", "заряд", "батарея", "наушники", "bluetooth", "devices", "battery", "верхняя панель"],
            SettingKind.Range,
            settings => (double)settings.TopBarDeviceRowLimit,
            (settings, value) => UserSettingsEditor.With(
                settings,
                topBarDeviceRowLimit: (int)Math.Round(AsDouble(value))),
            [],
            Minimum: UserSettings.MinTopBarDeviceRowLimit,
            Maximum: UserSettings.MaxTopBarDeviceRowLimit,
            Unit: "шт."),

        new(
            "glanceAppIcons",
            TopBarPageId,
            "Настоящие значки приложений в поиске",
            "Результаты поиска показывают значки приложений и открытых окон, как в dock. Когда выключено, строки показывают единые значки категории.",
            ["поиск", "glance", "значки", "иконки", "приложения", "icons", "search"],
            SettingKind.Toggle,
            settings => settings.GlanceAppIcons,
            (settings, value) => UserSettingsEditor.With(settings, glanceAppIcons: AsBool(value)),
            []),

        // Переключение приложений (Alt+Tab в стиле macOS).
        new(
            "altTabSwitcherEnabled",
            SwitcherPageId,
            "Обзор окон Alt+Tab",
            "Показывать миниатюры окон и полосу рабочих столов. Выберите окно клавишей Tab или стрелками и отпустите Alt.",
            ["alt", "tab", "переключатель", "switcher", "приложения", "apps", "macos", "окна"],
            SettingKind.Toggle,
            settings => settings.AltTabSwitcherEnabled,
            (settings, value) => UserSettingsEditor.With(settings, altTabSwitcherEnabled: AsBool(value)),
            []),

        new(
            "altTabPanelDelayMs",
            SwitcherPageId,
            "Задержка появления панели",
            "Пауза перед показом панели переключателя. Быстрое нажатие и отпускание Alt+Tab переключает окно без показа обзора.",
            ["задержка", "delay", "панель", "мигание", "alt", "tab", "switcher"],
            SettingKind.Range,
            settings => (double)settings.AltTabPanelDelayMs,
            (settings, value) => UserSettingsEditor.With(
                settings,
                altTabPanelDelayMs: (int)Math.Round(AsDouble(value))),
            [],
            Minimum: UserSettings.MinAltTabPanelDelayMs,
            Maximum: UserSettings.MaxAltTabPanelDelayMs,
            Step: 10,
            Unit: "мс",
            IsEnabledBy: "altTabSwitcherEnabled"),

        new(
            "altTabRestoreMinimized",
            SwitcherPageId,
            "Разворачивать свёрнутые окна",
            "Разворачивать выбранное свёрнутое окно при переключении. Shift при отпускании Alt разворачивает окно и при выключенной настройке.",
            ["свёрнутые", "развернуть", "restore", "minimized", "окна", "alt", "tab"],
            SettingKind.Toggle,
            settings => settings.AltTabRestoreMinimized,
            (settings, value) => UserSettingsEditor.With(settings, altTabRestoreMinimized: AsBool(value)),
            [],
            IsEnabledBy: "altTabSwitcherEnabled"),

        // Проводник.
        new(
            "explorerViewMode",
            ExplorerPageId,
            "Вид по умолчанию",
            "Как показывать содержимое папок: иконками или списком.",
            ["проводник", "explorer", "вид", "иконки", "список"],
            SettingKind.Choice,
            settings => settings.ExplorerViewMode.ToString(),
            (settings, value) => UserSettingsEditor.With(settings, explorerViewMode: ParseViewMode(value)),
            [
                new(ExplorerViewMode.Icons.ToString(), "Иконки"),
                new(ExplorerViewMode.List.ToString(), "Список"),
            ]),

        // Оформление Windows: перенос темы Glaze на систему. Записи идут в реестр
        // пользователя и не требуют прав администратора.
        new(
            "systemAppearanceEnabled",
            WindowsPageId,
            "Оформление Windows",
            "Красить акцент, светлый или тёмный режим приложений и прозрачность Windows так же, как Glaze Shell. Системные приложения и рамки окон подхватывают цвета оболочки.",
            ["windows", "система", "акцент", "accent", "тёмная тема", "светлая тема", "рамки", "проводник", "параметры"],
            SettingKind.Toggle,
            settings => settings.SystemAppearanceEnabled,
            (settings, value) => UserSettingsEditor.With(settings, systemAppearanceEnabled: AsBool(value)),
            []),

        new(
            "glazeWallpaperEnabled",
            WindowsPageId,
            "Обои рабочего стола Glaze",
            "Заменять обои градиентом из основного цвета и акцента Glaze. Исходные обои возвращаются при выключении.",
            ["обои", "wallpaper", "рабочий стол", "desktop", "фон", "градиент"],
            SettingKind.Toggle,
            settings => settings.GlazeWallpaperEnabled,
            (settings, value) => UserSettingsEditor.With(settings, glazeWallpaperEnabled: AsBool(value)),
            []),

        new(
            "glazeDesktopLayerEnabled",
            WindowsPageId,
            "Слой рабочего стола Glaze",
            "Показывать значки приложений Glaze на рабочем столе отдельным слоем. Explorer не заменяется, обычные окна остаются поверх.",
            ["рабочий стол", "desktop", "значки", "иконки", "слой", "layer"],
            SettingKind.Toggle,
            settings => settings.GlazeDesktopLayerEnabled,
            (settings, value) => UserSettingsEditor.With(settings, glazeDesktopLayerEnabled: AsBool(value)),
            []),

        // Доступность (I5).
        new(
            "reduceMotion",
            AccessibilityPageId,
            "Уменьшить движение",
            "Сокращать анимации: магнификация dock и переходы становятся мгновенными.",
            ["анимации", "движение", "motion", "accessibility", "доступность", "меньше движения"],
            SettingKind.Toggle,
            settings => settings.ReduceMotion,
            (settings, value) => UserSettingsEditor.With(settings, reduceMotion: AsBool(value)),
            []),

        new(
            "highContrast",
            AccessibilityPageId,
            "Высокая контрастность",
            "Отключить стекло и приглушённые цвета, оставить контрастные. Тема Windows действует всегда.",
            ["контраст", "contrast", "accessibility", "доступность", "читаемость"],
            SettingKind.Toggle,
            settings => settings.HighContrast,
            (settings, value) => UserSettingsEditor.With(settings, highContrast: AsBool(value)),
            []),

        // Восстановление (I2).
        new(
            "recoveryHotkeyEnabled",
            RecoveryPageId,
            "Клавиша восстановления",
            "Редкое сочетание клавиш возвращает панель задач и значки рабочего стола, если оболочка сломалась.",
            ["восстановление", "recovery", "горячие клавиши", "hotkey", "панель задач", "рабочий стол"],
            SettingKind.Toggle,
            settings => settings.RecoveryHotkeyEnabled,
            (settings, value) => UserSettingsEditor.With(settings, recoveryHotkeyEnabled: AsBool(value)),
            []),

        new(
            "recoveryHotkeyId",
            RecoveryPageId,
            "Сочетание клавиш",
            "Сочетание используется только для восстановления, поэтому состоит из трёх модификаторов и функциональной клавиши.",
            ["сочетание", "комбинация", "клавиши", "hotkey", "восстановление"],
            SettingKind.Choice,
            settings => settings.RecoveryHotkeyId,
            (settings, value) => UserSettingsEditor.With(settings, recoveryHotkeyId: value as string),
            BuildRecoveryHotkeyChoices()),

        new(
            "safeModeEnabled",
            RecoveryPageId,
            "Безопасный режим",
            "Запускать оболочку без хуков и оверлеев: она ничего не меняет в системе.",
            ["безопасный режим", "safe mode", "восстановление", "авария", "краш"],
            SettingKind.Toggle,
            settings => settings.SafeModeEnabled,
            (settings, value) => UserSettingsEditor.With(settings, safeModeEnabled: AsBool(value)),
            []),
    ];

    private static List<SettingChoice> BuildAccentChoices()
    {
        var choices = new List<SettingChoice> { new(string.Empty, "По умолчанию") };

        foreach (var accent in AccentPalette.All)
        {
            choices.Add(new SettingChoice(accent.Id, accent.Name, accent.Hex));
        }

        return choices;
    }

    /// <summary>
    /// Варианты основного цвета: тот же набор оттенков, что и у акцента, но он красит
    /// поверхности (фон, панели, стекло), а акцент — выделение, фокус и градиент.
    /// </summary>
    private static List<SettingChoice> BuildBaseColorChoices()
    {
        var choices = new List<SettingChoice> { new(string.Empty, "Тема по умолчанию") };

        foreach (var accent in AccentPalette.All)
        {
            choices.Add(new SettingChoice(accent.Id, accent.Name, accent.Hex));
        }

        return choices;
    }

    /// <summary>
    /// Варианты сочетания клавиш восстановления: только редкие комбинации из трёх
    /// модификаторов и функциональной клавиши.
    /// </summary>
    private static List<SettingChoice> BuildRecoveryHotkeyChoices()
    {
        var choices = new List<SettingChoice>();

        foreach (var hotkey in RecoveryHotkey.Presets)
        {
            choices.Add(new SettingChoice(hotkey.Id, hotkey.Id));
        }

        return choices;
    }

    private static bool AsBool(object value) => value is true;

    private static double AsDouble(object value) => value switch
    {
        double number => number,
        int number => number,
        float number => number,
        decimal number => (double)number,
        _ => 0
    };

    private static ThemeMode ParseThemeMode(object value) =>
        Enum.TryParse<ThemeMode>(value as string, ignoreCase: true, out var mode) ? mode : ThemeMode.System;

    private static ExplorerViewMode ParseViewMode(object value) =>
        Enum.TryParse<ExplorerViewMode>(value as string, ignoreCase: true, out var mode) ? mode : ExplorerViewMode.Icons;
}
