using System.Collections.Generic;
using GlazeShell.Core.Switching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlazeShell.Core.Tests.Switching;

[TestClass]
public sealed class RecentApplicationOrderTests
{
    [TestMethod]
    public void TouchMovesApplicationToTheFront()
    {
        var order = new RecentApplicationOrder();

        Assert.IsTrue(order.Touch("a"));
        Assert.IsTrue(order.Touch("b"));
        Assert.IsTrue(order.Touch("c"));

        Assert.AreEqual("c,b,a", string.Join(",", order.Ids));

        Assert.IsTrue(order.Touch("a"), "A touched application must become the most recent.");
        Assert.AreEqual("a,c,b", string.Join(",", order.Ids));
    }

    [TestMethod]
    public void TouchingTheFirstApplicationChangesNothing()
    {
        var order = new RecentApplicationOrder();
        order.Touch("a");
        order.Touch("b");

        Assert.IsFalse(order.Touch("b"), "The most recent application is already first.");
        Assert.AreEqual("b,a", string.Join(",", order.Ids));
    }

    [TestMethod]
    public void TouchIgnoresEmptyIdentifiers()
    {
        var order = new RecentApplicationOrder();

        Assert.IsFalse(order.Touch(null));
        Assert.IsFalse(order.Touch("   "));
        Assert.IsEmpty(order.Ids);
    }

    [TestMethod]
    public void TouchKeepsTheListBounded()
    {
        var order = new RecentApplicationOrder();

        for (var i = 0; i < RecentApplicationOrder.MaxItems + 5; i++)
        {
            order.Touch($"app-{i}");
        }

        Assert.HasCount(RecentApplicationOrder.MaxItems, order.Ids);
        Assert.AreEqual($"app-{RecentApplicationOrder.MaxItems + 4}", order.Ids[0]);
    }

    [TestMethod]
    public void RemoveForgetsApplication()
    {
        var order = new RecentApplicationOrder();
        order.Touch("a");
        order.Touch("b");

        Assert.IsTrue(order.Remove("a"));
        Assert.IsFalse(order.Remove("a"), "Removing an unknown application reports no change.");
        Assert.AreEqual("b", string.Join(",", order.Ids));
    }

    [TestMethod]
    public void OrderPutsRecentFirstAndKeepsUnknownApplications()
    {
        var order = new RecentApplicationOrder();
        order.Touch("b");
        order.Touch("c");

        var snapshot = new List<string> { "a", "b", "c", "d" };

        var ordered = order.Order(snapshot, value => value);

        Assert.AreEqual("c,b,a,d", string.Join(",", ordered));
    }
}
