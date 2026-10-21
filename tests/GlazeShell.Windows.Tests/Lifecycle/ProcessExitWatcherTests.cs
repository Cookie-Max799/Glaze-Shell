using System.Diagnostics;
using GlazeShell.Windows.Lifecycle;

namespace GlazeShell.Windows.Tests.Lifecycle;

[TestClass]
public sealed class ProcessExitWatcherTests
{
    [TestMethod]
    public void FinishedProcessIsDetected()
    {
        using var process = StartProcess("/c exit");
        process.WaitForExit(10_000);

        Assert.IsTrue(
            ProcessExitWatcher.WaitForExit(process.Id, TimeSpan.FromSeconds(10)),
            "A process that has already finished must be reported as finished.");
    }

    [TestMethod]
    public void RunningProcessIsNotReportedAsFinished()
    {
        using var process = StartProcess("/c ping -n 30 127.0.0.1 > nul");

        try
        {
            Assert.IsFalse(
                ProcessExitWatcher.WaitForExit(process.Id, TimeSpan.FromMilliseconds(300)),
                "A living process must not be reported as finished.");
        }
        finally
        {
            TryKill(process);
        }
    }

    [TestMethod]
    public void UnknownProcessIsReportedAsFinished()
    {
        Assert.IsTrue(
            ProcessExitWatcher.WaitForExit(int.MaxValue - 1, TimeSpan.FromSeconds(5)),
            "A process that does not exist cannot be alive.");
    }

    private static Process StartProcess(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        return Process.Start(startInfo) ?? throw new InvalidOperationException("The test process could not be started.");
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
