namespace GlazeShell.Core.Recovery;

/// <summary>
/// Счётчик аварий оболочки (функция I2): считает неудачные запуски и решает, нужен ли
/// безопасный режим.
/// </summary>
/// <remarks>
/// <para>
/// Авария считается отдельным запуском, а не отдельным сбоем внутри работающего приложения:
/// признак падения хранится в состоянии сессии, поэтому правило остаётся простым и
/// проверяемым без доступа к системе.
/// </para>
/// <para>
/// Правило живёт в ядре без WinUI и без файлов: хранилище отдаёт моменты падений, а решение
/// о безопасном режиме принимается здесь.
/// </para>
/// </remarks>
public sealed class CrashTracker
{
    /// <summary>Число падений подряд, после которого включается безопасный режим.</summary>
    public const int CrashesForSafeMode = 3;

    /// <summary>Окно, за которым считаются падения.</summary>
    public static readonly TimeSpan CrashWindow = TimeSpan.FromMinutes(5);

    private readonly List<DateTimeOffset> _crashes;

    /// <param name="crashes">Моменты прошлых падений; могут быть пустыми.</param>
    public CrashTracker(IEnumerable<DateTimeOffset>? crashes = null)
    {
        _crashes = crashes is null
            ? []
            : [.. crashes.OrderBy(crash => crash)];
    }

    /// <summary>Все известные моменты падений, от раннего к позднему.</summary>
    public IReadOnlyList<DateTimeOffset> Crashes => _crashes;

    /// <summary>
    /// Запоминает ещё одно падение.
    /// </summary>
    public void RecordCrash(DateTimeOffset moment) => _crashes.Add(moment);

    /// <summary>
    /// Число падений внутри окна <see cref="CrashWindow"/> относительно момента <paramref name="now"/>.
    /// </summary>
    public int CountRecent(DateTimeOffset now)
    {
        var count = 0;

        foreach (var crash in _crashes)
        {
            if (now - crash <= CrashWindow)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Нужно ли включать безопасный режим: падений за окно не меньше
    /// <see cref="CrashesForSafeMode"/>.
    /// </summary>
    public bool ShouldEnterSafeMode(DateTimeOffset now) => CountRecent(now) >= CrashesForSafeMode;

    /// <summary>
    /// Убирает падения, вышедшие за окно, и возвращает оставшиеся. Старые падения не должны
    /// влиять на решение спустя долгое время.
    /// </summary>
    public IReadOnlyList<DateTimeOffset> Prune(DateTimeOffset now)
    {
        _crashes.RemoveAll(crash => now - crash > CrashWindow);
        return _crashes;
    }
}
