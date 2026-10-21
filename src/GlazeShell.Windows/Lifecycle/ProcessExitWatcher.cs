using System.ComponentModel;
using System.Diagnostics;

namespace GlazeShell.Windows.Lifecycle;

/// <summary>
/// Наблюдение за завершением процесса (функция I2).
/// </summary>
/// <remarks>
/// <para>
/// Сторож должен заметить падение оболочки быстро: критерий функции I2 — рабочий стол
/// возвращается в течение трёх секунд после убийства главного процесса. Поэтому процесс
/// опрашивается часто, а ожидание построено на опросе, а не на блокирующем ожидании
/// дескриптора: так сторож работает и с процессом, который ему не принадлежит.
/// </para>
/// <para>
/// Идентификатор процесса может быть переиспользован системой, поэтому запоминается момент
/// запуска процесса: если он изменился, исходный процесс завершён, а под тем же
/// идентификатором работает уже другой процесс.
/// </para>
/// </remarks>
public static class ProcessExitWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Ждёт завершения процесса без ограничения по времени.
    /// </summary>
    /// <param name="processId">Идентификатор наблюдаемого процесса.</param>
    /// <param name="cancellationToken">Отмена ожидания.</param>
    /// <returns><see langword="true"/>, если процесс завершился.</returns>
    public static bool WaitForExit(int processId, CancellationToken cancellationToken = default) =>
        WaitForExit(processId, timeout: null, cancellationToken);

    /// <summary>
    /// Ждёт завершения процесса не дольше указанного времени.
    /// </summary>
    /// <param name="processId">Идентификатор наблюдаемого процесса.</param>
    /// <param name="timeout">Предельное ожидание; <see langword="null"/> — без ограничения.</param>
    /// <param name="cancellationToken">Отмена ожидания.</param>
    /// <returns><see langword="true"/>, если процесс завершился; <see langword="false"/>, если
    /// время вышло, а процесс ещё жив.</returns>
    public static bool WaitForExit(int processId, TimeSpan? timeout, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processId, 1);

        DateTime? startTime = null;
        DateTime? deadline = timeout is null ? null : DateTime.UtcNow + timeout.Value;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsSameProcess(processId, ref startTime))
            {
                return true;
            }

            if (deadline is not null && DateTime.UtcNow >= deadline.Value)
            {
                return false;
            }

            cancellationToken.WaitHandle.WaitOne(PollInterval);
        }
    }

    /// <summary>
    /// Проверяет, что под идентификатором всё ещё работает тот же процесс.
    /// </summary>
    private static bool IsSameProcess(int processId, ref DateTime? startTime)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            if (process.HasExited)
            {
                return false;
            }

            DateTime current;

            try
            {
                current = process.StartTime;
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                // Момент запуска недоступен: идентификатор занят, процесс считается живым.
                return true;
            }

            startTime ??= current;
            return startTime == current;
        }
        catch (ArgumentException)
        {
            // Процесса с таким идентификатором больше нет.
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
