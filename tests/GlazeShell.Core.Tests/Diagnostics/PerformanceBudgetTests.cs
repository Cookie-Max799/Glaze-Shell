using GlazeShell.Core.Diagnostics;

namespace GlazeShell.Core.Tests.Diagnostics;

[TestClass]
public sealed class PerformanceBudgetTests
{
    [TestMethod]
    public void DefaultBudgetMatchesTheFeatureMap()
    {
        var budget = PerformanceBudgets.Default;

        Assert.AreEqual(1.0, budget.IdleCpuPercent);
        Assert.AreEqual(200L * 1024 * 1024, budget.IdleMemoryBytes);
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), budget.GlanceOpen);
    }

    [TestMethod]
    public void MeasurementsInsideTheBudgetPass()
    {
        var measurement = new PerformanceMeasurement(
            idleCpuPercent: 0.4,
            idleMemoryBytes: 150L * 1024 * 1024,
            glanceOpen: TimeSpan.FromMilliseconds(42));

        var report = PerformanceBudgetEvaluator.Evaluate(PerformanceBudgets.Default, measurement);

        Assert.IsTrue(report.IsWithinBudget);
        Assert.HasCount(3, report.Checks);
    }

    [TestMethod]
    public void MeasurementsOutsideTheBudgetFail()
    {
        var measurement = new PerformanceMeasurement(
            idleCpuPercent: 4.5,
            idleMemoryBytes: 400L * 1024 * 1024,
            glanceOpen: TimeSpan.FromMilliseconds(350));

        var report = PerformanceBudgetEvaluator.Evaluate(PerformanceBudgets.Default, measurement);

        Assert.IsFalse(report.IsWithinBudget);
        Assert.IsTrue(report.Checks.All(static check => !check.IsWithinBudget));
    }

    [TestMethod]
    public void MissingGlanceMeasurementIsSkippedInsteadOfCountedAsZero()
    {
        var measurement = new PerformanceMeasurement(
            idleCpuPercent: 0.2,
            idleMemoryBytes: 100L * 1024 * 1024);

        var report = PerformanceBudgetEvaluator.Evaluate(PerformanceBudgets.Default, measurement);

        Assert.HasCount(2, report.Checks);
        Assert.IsTrue(report.IsWithinBudget);
        Assert.DoesNotContain("glance", report.ToLogString());
    }

    [TestMethod]
    public void OneOverBudgetMeasurementIsEnoughToFail()
    {
        var measurement = new PerformanceMeasurement(
            idleCpuPercent: 0.1,
            idleMemoryBytes: 100L * 1024 * 1024,
            glanceOpen: TimeSpan.FromMilliseconds(101));

        var report = PerformanceBudgetEvaluator.Evaluate(PerformanceBudgets.Default, measurement);

        Assert.IsFalse(report.IsWithinBudget);
        Assert.HasCount(3, report.Checks);
    }

    [TestMethod]
    public void LogStringContainsValuesAndVerdict()
    {
        var measurement = new PerformanceMeasurement(
            idleCpuPercent: 0.25,
            idleMemoryBytes: 120L * 1024 * 1024,
            glanceOpen: TimeSpan.FromMilliseconds(30));

        var report = PerformanceBudgetEvaluator.Evaluate(PerformanceBudgets.Default, measurement);
        var text = report.ToLogString();

        StringAssert.Contains(text, "idle cpu");
        StringAssert.Contains(text, "0.25");
        StringAssert.Contains(text, "within budget");
    }

    [TestMethod]
    public void EmptyReportExplainsThatNothingWasMeasured()
    {
        var report = new PerformanceBudgetReport([]);

        Assert.IsTrue(report.IsWithinBudget);
        StringAssert.Contains(report.ToLogString(), "nothing was measured");
    }
}
