using System.Diagnostics;

namespace GlazeShell.Windows.Applications;

/// <summary>
/// Возвращает удобочитаемое имя приложения по идентификатору процесса: описание из
/// версии исполняемого файла («Google Chrome»), а при его отсутствии — имя процесса.
/// </summary>
public static class ApplicationNameResolver
{
    public static string? GetFriendlyName(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            var description = process.MainModule?.FileVersionInfo?.FileDescription;

            if (!string.IsNullOrWhiteSpace(description))
            {
                return description.Trim();
            }

            return process.ProcessName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }
}
