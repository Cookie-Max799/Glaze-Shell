using GlazeShell.Windows.Shell;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Tests.Shell;

[TestClass]
public sealed class WindowsUiRestorerTests
{
    [TestMethod]
    public void WindowBehindTheMonitorEdgeIsOutside()
    {
        var monitor = new Rect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        var window = new Rect { Left = 0, Top = 1100, Right = 800, Bottom = 1200 };

        Assert.IsTrue(WindowsUiRestorer.IsOutsideMonitor(window, monitor));
    }

    [TestMethod]
    public void WindowOnTheMonitorIsNotOutside()
    {
        var monitor = new Rect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        var window = new Rect { Left = 100, Top = 100, Right = 500, Bottom = 400 };

        Assert.IsFalse(WindowsUiRestorer.IsOutsideMonitor(window, monitor));
    }

    [TestMethod]
    public void WindowInsideAnotherMonitorIsNotOutside()
    {
        // Монитор определяется как ближайший к окну, поэтому окно на втором мониторе
        // остаётся на месте.
        var monitor = new Rect { Left = 1920, Top = 0, Right = 3840, Bottom = 1080 };
        var window = new Rect { Left = 2000, Top = 100, Right = 3000, Bottom = 400 };

        Assert.IsFalse(WindowsUiRestorer.IsOutsideMonitor(window, monitor));
    }

    [TestMethod]
    public void RestoreDoesNotThrowOnTheDesktop()
    {
        // Восстановление должно быть безопасным на живом рабочем столе: если панель задач
        // и значки на месте, ничего меняться не должно.
        var result = WindowsUiRestorer.Restore();

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Notes);
    }
}
