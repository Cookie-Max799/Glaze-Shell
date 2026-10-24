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
    /// <summary>Идентификатор страницы «Общие».</summary>
    public const string GeneralPageId = "general";

    /// <summary>Идентификатор страницы «Оформление».</summary>
    public const string AppearancePageId = "appearance";

    /// <summary>Идентификатор страницы «Dock».</summary>
    public const string DockPageId = "dock";

    /// <summary>Идентификатор страницы «Верхняя панель».</summary>
    public const string TopBarPageId = "topbar";

    /// <summary>Идентификатор страницы «Glance».</summary>
    public const string GlancePageId = "glance";

    /// <summary>Идентификатор страницы «Проводник».</summary>
    public const string ExplorerPageId = "explorer";

    /// <summary>Идентификатор страницы «Доступность».</summary>
    public const string AccessibilityPageId = "accessibility";

    /// <summary>Идентификатор страницы «Восстановление».</summary>
    public const string RecoveryPageId = "recovery";

    /// <summary>Идентификатор страницы «О программе».</summary>
    public const string AboutPageId = "about";

    /// <summary>Все страницы настроек в порядке сайдбара.</summary>
    public static IReadOnlyList<SettingsPage> Pages { get; } =
    [
        new(GeneralPageId, "Общие", "\uE713", "Основные"),
        new(AppearancePageId, "Оформление", "\uE790", "Основные"),
        new(DockPageId, "Dock", "\uE8FC", "Интерфейс"),
        new(TopBarPageId, "Верхняя панель", "\uE7C4", "Интерфейс"),
        new(GlancePageId, "Glance", "\uE721", "Интерфейс"),
        new(ExplorerPageId, "Проводник", "\uE8B7", "Интерфейс"),
        new(AccessibilityPageId, "Доступность", "\uE776", "Система"),
        new(RecoveryPageId, "Восстановление", "\uE90F", "Система"),
        new("windows", "Windows", "\uE782", "Система"),
        new("notifications", "Уведомления", "\uEA8F", "Система"),
        new("shortcuts", "Горячие клавиши", "\uE765", "Система"),
        new("privacy", "Приватность", "\uE72E", "Система"),
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
        // Основные.
        new(
            "startWithWindows",
            GeneralPageId,
            "Запускать при входе в систему",
            "Glaze Shell стартует вместе с Windows и сразу показывает панели.",
            ["автозапуск", "startup", "login", "автостарт"],
            SettingKind.Toggle,
            settings => settings.StartWithWindows,
            (settings, value) => UserSettingsEditor.With(settings, startWithWindows: AsBool(value)),
            []),

        new(
            "minimizeToTray",
            GeneralPageId,
            "Сворачивать в трей",
            "Кнопка сворачивания убирает окно, а не закрывает приложение.",
            ["трей", "tray", "свернуть", "minimize"],
            SettingKind.Toggle,
            settings => settings.MinimizeToTray,
            (settings, value) => UserSettingsEditor.With(settings, minimizeToTray: AsBool(value)),
            []),

        new(
            "restoreLastLayout",
            GeneralPageId,
            "Восстанавливать раскладку",
            "При запуске возвращать расположение панелей и рабочих столов.",
            ["раскладка", "layout", "восстановление"],
            SettingKind.Toggle,
            settings => settings.RestoreLastLayout,
            (settings, value) => UserSettingsEditor.With(settings, restoreLastLayout: AsBool(value)),
            []),

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
            "Базовая цветовая схема оформления.",
            ["основной", "base", "цвет", "scheme", "палитра"],
            SettingKind.Choice,
            settings => settings.BaseColorId ?? string.Empty,
            (settings, value) =>
            {
                var baseColor = value as string;
                return string.IsNullOrWhiteSpace(baseColor)
                    ? UserSettingsEditor.With(settings, clearBaseColor: true)
                    : UserSettingsEditor.With(settings, baseColorId: baseColor);
            },
            BuildAccentChoices()),

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
            choices.Add(new SettingChoice(accent.Id, accent.Name));
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

    private static ThemeMode ParseThemeMode(object value) =>
        Enum.TryParse<ThemeMode>(value as string, ignoreCase: true, out var mode) ? mode : ThemeMode.System;

    private static ExplorerViewMode ParseViewMode(object value) =>
        Enum.TryParse<ExplorerViewMode>(value as string, ignoreCase: true, out var mode) ? mode : ExplorerViewMode.Icons;
}
