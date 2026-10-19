using System.IO;
using GlazeShell.Core.Glance;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Источник кандидатов для глобального поиска Glance (B1): приложения, открытые окна,
/// страницы системных настроек и команды Glaze Shell.
/// </summary>
/// <remarks>
/// <para>
/// Приложения сканируются один раз и кэшируются (сканирование меню «Пуск» и упакованных
/// приложений не бесплатно), окна берутся свежим снимком на каждый поиск — они меняются
/// постоянно, а снимок дешёвый. Страницы настроек и команды заданы статически: это
/// фиксированные цели, а не обнаруженные данные.
/// </para>
/// <para>
/// Поиск по файлам (индекс Windows Search) в этом слайсе не реализован: он требует
/// OLE DB-провайдера и отдельной обработки ошибок, поэтому отмечен как следующий шаг B1.
/// </para>
/// </remarks>
public sealed class GlanceSearchCatalog
{
    private const int MaxWindowCount = 60;

    private readonly IApplicationManager _applications;
    private readonly IWindowManager _windows;
    private readonly int _ownProcessId = Environment.ProcessId;

    private Task? _loadTask;
    private IReadOnlyList<GlanceResult>? _applicationResults;

    public GlanceSearchCatalog(IApplicationManager applications, IWindowManager windows)
    {
        _applications = applications ?? throw new ArgumentNullException(nameof(applications));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
    }

    /// <summary>Список приложений уже просканирован и его можно показывать.</summary>
    public bool IsApplicationListLoaded => _applicationResults is not null;

    /// <summary>Сканирует приложения один раз (повторные вызовы ждут тот же проход).</summary>
    public Task EnsureApplicationsAsync()
    {
        _loadTask ??= LoadApplicationsAsync();
        return _loadTask;
    }

    /// <summary>Сбрасывает кэш приложений и сканирует заново.</summary>
    public Task RefreshApplicationsAsync()
    {
        _applicationResults = null;
        _loadTask = LoadApplicationsAsync();
        return _loadTask;
    }

    /// <summary>Собирает полный список кандидатов для ранжирования.</summary>
    public IReadOnlyList<GlanceResult> BuildCandidates()
    {
        var candidates = new List<GlanceResult>();

        if (_applicationResults is not null)
        {
            candidates.AddRange(_applicationResults);
        }

        candidates.AddRange(BuildWindowResults());
        candidates.AddRange(Settings);
        candidates.AddRange(Commands);

        return candidates;
    }

    private async Task LoadApplicationsAsync()
    {
        var discovered = await _applications.DiscoverAsync().ConfigureAwait(true);

        var results = new List<GlanceResult>(discovered.Count);

        foreach (var application in discovered)
        {
            results.Add(new GlanceResult(
                GlanceResultKind.Application,
                application.Id,
                application.Name,
                BuildApplicationSubtitle(application),
                BuildApplicationKeywords(application)));
        }

        _applicationResults = results;
    }

    private List<GlanceResult> BuildWindowResults()
    {
        IReadOnlyList<WindowInfo> windows;

        try
        {
            windows = _windows.GetWindows();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return [];
        }

        var results = new List<GlanceResult>();

        foreach (var window in windows)
        {
            if (results.Count >= MaxWindowCount)
            {
                break;
            }

            // Собственные окна Glaze Shell не должны искать сами себя: трекер может
            // прислать панели, если пропуск собственного процесса выключен.
            if (window.ProcessId == _ownProcessId || string.IsNullOrWhiteSpace(window.Title))
            {
                continue;
            }

            var subtitle = window.ProcessName is { Length: > 0 } process &&
                !string.Equals(process, window.Title, StringComparison.OrdinalIgnoreCase)
                    ? process
                    : "Открытое окно";

            results.Add(new GlanceResult(GlanceResultKind.Window, window.Id, window.Title.Trim(), subtitle));
        }

        return results;
    }

    private static string BuildApplicationSubtitle(Application application) =>
        application.Publisher ?? application.Description ?? string.Empty;

    /// <summary>
    /// Ключевые слова приложения: имя исполняемого файла без расширения помогает найти
    /// программу по её exe-имени, даже если пункт меню «Пуск» назван иначе.
    /// </summary>
    private static string BuildApplicationKeywords(Application application)
    {
        var path = application.ExecutablePath;

        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFileNameWithoutExtension(path);
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Страницы системных настроек: открываются по схеме <c>ms-settings:</c>. Русское
    /// название показывается, английское добавлено в ключевые слова для второго языка.
    /// </summary>
    private static readonly GlanceResult[] Settings =
    [
        new(GlanceResultKind.Setting, "ms-settings:display", "Дисплей", "Система", "display экран монитор"),
        new(GlanceResultKind.Setting, "ms-settings:sound", "Звук", "Система", "sound audio громкость динамики"),
        new(GlanceResultKind.Setting, "ms-settings:notifications", "Уведомления", "Система", "notifications уведомления"),
        new(GlanceResultKind.Setting, "ms-settings:powersleep", "Питание и спящий режим", "Система", "power sleep питание сон"),
        new(GlanceResultKind.Setting, "ms-settings:battery", "Батарея", "Система", "battery батарея заряд"),
        new(GlanceResultKind.Setting, "ms-settings:network", "Сеть и Интернет", "Сеть", "network wi-fi сеть интернет"),
        new(GlanceResultKind.Setting, "ms-settings:bluetooth", "Bluetooth и устройства", "Сеть", "bluetooth устройства"),
        new(GlanceResultKind.Setting, "ms-settings:appsfeatures", "Приложения", "Система", "apps приложения установка"),
        new(GlanceResultKind.Setting, "ms-settings:personalization", "Персонализация", "Оформление", "personalization оформление"),
        new(GlanceResultKind.Setting, "ms-settings:colors", "Цвета", "Оформление", "colors цвета тема"),
        new(GlanceResultKind.Setting, "ms-settings:background", "Фон", "Оформление", "background обои фон"),
        new(GlanceResultKind.Setting, "ms-settings:lockscreen", "Экран блокировки", "Оформление", "lockscreen блокировка"),
        new(GlanceResultKind.Setting, "ms-settings:taskbar", "Панель задач", "Оформление", "taskbar панель задач"),
        new(GlanceResultKind.Setting, "ms-settings:windowsupdate", "Центр обновления Windows", "Система", "windows update обновление"),
        new(GlanceResultKind.Setting, "ms-settings:datetime", "Дата и время", "Система", "date time дата время"),
        new(GlanceResultKind.Setting, "ms-settings:regionlanguage", "Язык и регион", "Система", "language region язык регион"),
        new(GlanceResultKind.Setting, "ms-settings:privacy", "Конфиденциальность", "Система", "privacy конфиденциальность"),
        new(GlanceResultKind.Setting, "ms-settings:easeofaccess", "Специальные возможности", "Система", "accessibility специальные возможности"),
        new(GlanceResultKind.Setting, "ms-settings:mouse", "Мышь", "Устройства", "mouse мышь"),
        new(GlanceResultKind.Setting, "ms-settings:keyboard", "Клавиатура", "Устройства", "keyboard клавиатура"),
        new(GlanceResultKind.Setting, "ms-settings:storagesense", "Память", "Система", "storage память диск"),
        new(GlanceResultKind.Setting, "ms-settings:multitasking", "Многозадачность", "Система", "multitasking многозадачность"),
        new(GlanceResultKind.Setting, "ms-settings:gaming", "Игры", "Система", "gaming игры"),
    ];

    /// <summary>
    /// Команды Glaze Shell: безопасные действия, не требующие подтверждения. Идентификатор
    /// команды разбирает <see cref="TopBarViewModel"/>.
    /// </summary>
    private static readonly GlanceResult[] Commands =
    [
        new(GlanceResultKind.Command, "open-shell", "Открыть Glaze Shell", "Главное окно", "glaze shell окно главное"),
        new(GlanceResultKind.Command, "refresh-apps", "Обновить список приложений", "Поиск", "refresh обновить сканирование"),
        new(GlanceResultKind.Command, "open-downloads", "Открыть папку «Загрузки»", "Проводник", "downloads загрузки"),
        new(GlanceResultKind.Command, "open-recycle-bin", "Открыть корзину", "Проводник", "recycle bin корзина мусор"),
    ];
}
