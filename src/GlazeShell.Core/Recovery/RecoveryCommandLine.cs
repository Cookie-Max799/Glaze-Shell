namespace GlazeShell.Core.Recovery;

/// <summary>
/// Разобранные аргументы командной строки, относящиеся к восстановлению (функция I2).
/// </summary>
public sealed record RecoveryCommandLineOptions
{
    /// <summary>Запрошен безопасный режим: <c>--safe-mode</c>.</summary>
    public bool SafeMode { get; init; }

    /// <summary>Запрошено восстановление интерфейса Windows: <c>--restore-ui</c>.</summary>
    public bool RestoreUserInterface { get; init; }

    /// <summary>Запрошена справка по ключам: <c>--help</c>.</summary>
    public bool Help { get; init; }

    /// <summary>
    /// Идентификатор процесса, за которым наблюдает сторож: <c>--watchdog &lt;pid&gt;</c>.
    /// </summary>
    public int? WatchdogProcessId { get; init; }

    /// <summary>Аргументы, которые разобрать не удалось: они не мешают запуску.</summary>
    public IReadOnlyList<string> UnknownArguments { get; init; } = [];
}

/// <summary>
/// Разбор командной строки для восстановления (I2).
/// </summary>
/// <remarks>
/// Ключи разбираются в ядре без WinUI, поэтому их поведение проверяется обычными тестами,
/// а приложение только исполняет результат.
/// </remarks>
public static class RecoveryCommandLine
{
    /// <summary>Ключ безопасного режима.</summary>
    public const string SafeModeArgument = "--safe-mode";

    /// <summary>Ключ восстановления интерфейса Windows.</summary>
    public const string RestoreArgument = "--restore-ui";

    /// <summary>Ключ справки.</summary>
    public const string HelpArgument = "--help";

    /// <summary>Ключ режима сторожа.</summary>
    public const string WatchdogArgument = "--watchdog";

    /// <summary>
    /// Разбирает аргументы командной строки. Первый элемент (путь к исполняемому файлу)
    /// не разбирается: вызывающий код передаёт только пользовательские аргументы.
    /// </summary>
    public static RecoveryCommandLineOptions Parse(IReadOnlyList<string>? arguments)
    {
        var safeMode = false;
        var restore = false;
        var help = false;
        int? watchdogProcessId = null;
        var unknown = new List<string>();

        if (arguments is null)
        {
            return new RecoveryCommandLineOptions();
        }

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];

            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            if (Matches(argument, SafeModeArgument))
            {
                safeMode = true;
                continue;
            }

            if (Matches(argument, RestoreArgument))
            {
                restore = true;
                continue;
            }

            if (Matches(argument, HelpArgument))
            {
                help = true;
                continue;
            }

            if (TryReadWatchdogArgument(argument, arguments, ref index, out var processId))
            {
                watchdogProcessId = processId;
                continue;
            }

            unknown.Add(argument);
        }

        return new RecoveryCommandLineOptions
        {
            SafeMode = safeMode,
            RestoreUserInterface = restore,
            Help = help,
            WatchdogProcessId = watchdogProcessId,
            UnknownArguments = unknown,
        };
    }

    /// <summary>
    /// Текст справки по ключам восстановления.
    /// </summary>
    public static string HelpText =>
        $"{SafeModeArgument} — запустить оболочку в безопасном режиме без хуков и оверлеев. " +
        $"{RestoreArgument} — вернуть системную панель задач и иконки рабочего стола и выйти. " +
        $"{WatchdogArgument} <pid> — работать сторожем за указанным процессом.";

    private static bool TryReadWatchdogArgument(
        string argument,
        IReadOnlyList<string> arguments,
        ref int index,
        out int? processId)
    {
        processId = null;

        if (Matches(argument, WatchdogArgument))
        {
            if (index + 1 < arguments.Count && int.TryParse(arguments[index + 1], out var value) && value > 0)
            {
                index++;
                processId = value;
                return true;
            }

            return true;
        }

        const string prefix = WatchdogArgument + "=";

        if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(argument[prefix.Length..], out var inline) &&
            inline > 0)
        {
            processId = inline;
            return true;
        }

        return false;
    }

    private static bool Matches(string argument, string name) =>
        string.Equals(argument, name, StringComparison.OrdinalIgnoreCase);
}
