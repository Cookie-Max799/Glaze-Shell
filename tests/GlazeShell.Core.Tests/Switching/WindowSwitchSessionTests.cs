using GlazeShell.Core.Switching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlazeShell.Core.Tests.Switching;

[TestClass]
public sealed class WindowSwitchSessionTests
{
    [TestMethod]
    [DataRow(false, 2L)]
    [DataRow(true, 3L)]
    public void InitialSelectionMovesBetweenIndividualWindows(bool backwards, long expected)
    {
        SwitchableWindow[] windows =
        [
            new(1, "Документ", false, "editor.exe"),
            new(2, "Документ", false, "editor.exe"),
            new(3, "Браузер", false),
        ];

        var session = new WindowSwitchSession(windows, foreground: 1, backwards);

        Assert.AreEqual(expected, session.Selected?.Handle);
    }

    [TestMethod]
    public void RecentOrderTracksEachHandleAndKeepsUnrankedWindowsInSnapshotOrder()
    {
        var order = new RecentWindowOrder();
        SwitchableWindow[] windows = [new(1, "Первое", false), new(2, "Второе", false), new(3, "Третье", false)];
        order.Touch(2);
        order.Touch(3);
        order.Touch(2);

        CollectionAssert.AreEqual(new long[] { 2, 3, 1 }, order.Order(windows).Select(window => window.Handle).ToArray());
    }

    [TestMethod]
    public void ClosingTheLastWindowLeavesAnEmptySession()
    {
        var session = new WindowSwitchSession([new(1, "Окно", false)], foreground: 1, backwards: false);

        session.RemoveSelected();

        Assert.HasCount(0, session.Items);
        Assert.IsNull(session.Selected);
        Assert.IsFalse(session.Move(1));
    }
}
