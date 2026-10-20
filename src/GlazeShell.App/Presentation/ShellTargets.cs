using System.Diagnostics;

namespace GlazeShell.App.Presentation;

/// <summary>
/// Открывает системную цель оболочки (<c>ms-settings:</c>, <c>shell:</c>, http) через
/// ShellExecute. Общий помощник верхней панели, Glance и popover «Центр управления».
/// </summary>
internal static class ShellTargets
{
    internal static void Open(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            Trace.WriteLine($"GlazeShell.ShellTargets: opening '{target}' failed. {exception}");
        }
    }
}
