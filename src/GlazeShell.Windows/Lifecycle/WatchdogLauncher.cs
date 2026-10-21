using System.Diagnostics;
using System.Globalization;
using GlazeShell.Core.Recovery;

namespace GlazeShell.Windows.Lifecycle;

/// <summary>
/// Запуск сторожа восстановления (функция I2).
/// </summary>
/// <remarks>
/// <para>
/// Сторож работает отдельным процессом: если главный процесс убить, восстановление
/// интерфейса всё равно выполнится. Запускается тот же исполняемый файл с ключом
/// <c>--watchdog</c>, поэтому отдельная сборка не нужна, а поведение сторожа полностью
/// совпадает с поведением основного приложения при восстановлении.
/// </para>
/// <para>
/// Если запустить сторож не удалось, оболочка продолжает работать как обычно: отсутствие
/// сторожа не должно мешать пользователю.
/// </para>
/// </remarks>
public static class WatchdogLauncher
{
    /// <summary>
    /// Запускает сторож за указанным процессом.
    /// </summary>
    /// <param name="processId">Идентификатор процесса, за которым нужно наблюдать.</param>
    /// <returns>Запущенный процесс сторожа либо <see langword="null"/>, если запустить его не удалось.</returns>
    public static Process? TryLaunch(int processId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processId, 1);

        var executable = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(executable))
        {
            return null;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };

            startInfo.ArgumentList.Add(RecoveryCommandLine.WatchdogArgument);
            startInfo.ArgumentList.Add(processId.ToString(CultureInfo.InvariantCulture));

            return Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or System.IO.IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
