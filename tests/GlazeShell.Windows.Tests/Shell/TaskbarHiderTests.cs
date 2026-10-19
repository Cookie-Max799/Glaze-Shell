using GlazeShell.Windows.Shell;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Tests.Shell;

[TestClass]
public sealed class TaskbarHiderTests
{
    [TestMethod]
    public void TaskbarBelowMonitorIsRecognizedAsHidden()
    {
        var monitor = new Rect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        var taskbar = new Rect { Left = 0, Top = 1096, Right = 1920, Bottom = 1136 };

        Assert.IsTrue(TaskbarHider.IsBelowMonitor(taskbar, monitor));
    }

    [TestMethod]
    public void TaskbarAtMonitorEdgeIsNotHidden()
    {
        var monitor = new Rect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        var taskbar = new Rect { Left = 0, Top = 1040, Right = 1920, Bottom = 1080 };

        Assert.IsFalse(TaskbarHider.IsBelowMonitor(taskbar, monitor));
    }

    [TestMethod]
    public void TaskbarOnSecondMonitorIsNotHidden()
    {
        var monitor = new Rect { Left = 0, Top = 1080, Right = 1920, Bottom = 2160 };
        var taskbar = new Rect { Left = 0, Top = 2120, Right = 1920, Bottom = 2160 };

        Assert.IsFalse(TaskbarHider.IsBelowMonitor(taskbar, monitor));
    }
}
