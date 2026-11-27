using System.Collections.Generic;
using GlazeShell.Core.Switching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlazeShell.Core.Tests.Switching;

[TestClass]
public sealed class ApplicationSwitchSessionTests
{
    private static SwitchableApplication Application(string id, params string[] windowTitles)
    {
        var windows = new List<SwitchableWindow>();

        for (var i = 0; i < windowTitles.Length; i++)
        {
            windows.Add(new SwitchableWindow(i + 1, windowTitles[i], false));
        }

        return new SwitchableApplication(id, id, 1, null, windows);
    }

    private static ApplicationSwitchSession Session(params SwitchableApplication[] applications) =>
        new(applications);

    [TestMethod]
    public void SessionStartsOnThePreviousApplication()
    {
        var session = Session(Application("a", "A"), Application("b", "B"), Application("c", "C"));

        Assert.AreEqual(1, session.SelectedIndex);
        Assert.AreEqual("b", session.Selected!.Id);
    }

    [TestMethod]
    public void SessionWithoutSelectNextStartsOnTheFirstApplication()
    {
        var session = new ApplicationSwitchSession(
            new[] { Application("a", "A"), Application("b", "B") },
            selectNext: false);

        Assert.AreEqual(0, session.SelectedIndex);
    }

    [TestMethod]
    public void SingleApplicationIsSelected()
    {
        var session = Session(Application("a", "A"));

        Assert.AreEqual(0, session.SelectedIndex);
    }

    [TestMethod]
    public void MoveWrapsAroundAndResetsWindowExpansion()
    {
        var session = Session(Application("a", "A", "A1"), Application("b", "B"), Application("c", "C"));

        Assert.IsTrue(session.ToggleWindows());
        Assert.IsTrue(session.WindowsExpanded);

        Assert.IsTrue(session.Move(1));
        Assert.AreEqual(2, session.SelectedIndex);
        Assert.IsFalse(session.WindowsExpanded, "Changing application collapses the window list.");

        Assert.IsTrue(session.Move(1));
        Assert.AreEqual(0, session.SelectedIndex, "Selection wraps to the first application.");

        Assert.IsTrue(session.Move(-1));
        Assert.AreEqual(2, session.SelectedIndex, "Selection wraps backwards too.");
    }

    [TestMethod]
    public void MoveWithZeroDoesNothing()
    {
        var session = Session(Application("a", "A"), Application("b", "B"));

        Assert.IsFalse(session.Move(0));
    }

    [TestMethod]
    public void SelectValidatesIndex()
    {
        var session = Session(Application("a", "A"), Application("b", "B"));

        Assert.IsFalse(session.Select(1), "Selecting the current application changes nothing.");
        Assert.IsFalse(session.Select(2));
        Assert.IsFalse(session.Select(-1));
        Assert.IsTrue(session.Select(0));
        Assert.AreEqual("a", session.Selected!.Id);
    }

    [TestMethod]
    public void ToggleWindowsExpandsOnlyWhenThereAreWindows()
    {
        var empty = Session(
            new SwitchableApplication("empty", "Empty", 1, null, new List<SwitchableWindow>()),
            Application("b", "B"));
        empty.Select(0);

        Assert.IsFalse(empty.ToggleWindows(), "An application without windows cannot expand.");

        var session = Session(Application("a", "A1", "A2"), Application("b", "B"));
        session.Select(0);
        Assert.IsTrue(session.ToggleWindows());
        Assert.AreEqual(0, session.SelectedWindowIndex);
        Assert.AreEqual("A1", session.SelectedWindow!.Title);

        Assert.IsTrue(session.ToggleWindows());
        Assert.IsFalse(session.WindowsExpanded);
    }

    [TestMethod]
    public void MoveWindowCyclesWithinTheApplication()
    {
        var session = Session(Application("a", "A1", "A2", "A3"), Application("b", "B"));
        session.Select(0);
        session.ToggleWindows();

        Assert.IsTrue(session.MoveWindow(1));
        Assert.AreEqual(1, session.SelectedWindowIndex);

        Assert.IsTrue(session.MoveWindow(-1));
        Assert.AreEqual(0, session.SelectedWindowIndex);

        Assert.IsTrue(session.MoveWindow(-1));
        Assert.AreEqual(2, session.SelectedWindowIndex, "Window selection wraps.");
    }

    [TestMethod]
    public void MoveWindowRequiresExpandedList()
    {
        var session = Session(Application("a", "A", "A1", "A2"), Application("b", "B"));

        Assert.IsFalse(session.MoveWindow(1));
    }

    [TestMethod]
    public void RemoveSelectedKeepsSelectionValid()
    {
        var session = Session(Application("a", "A"), Application("b", "B"), Application("c", "C"));
        session.Select(2);

        var removed = session.RemoveSelected();

        Assert.AreEqual("c", removed!.Id);
        Assert.HasCount(2, session.Items);
        Assert.AreEqual(1, session.SelectedIndex, "Selection moves to the neighbour on the left.");

        session.RemoveSelected();
        session.RemoveSelected();

        Assert.IsTrue(session.IsEmpty);
        Assert.IsNull(session.Selected);
        Assert.IsNull(session.RemoveSelected());
    }
}
