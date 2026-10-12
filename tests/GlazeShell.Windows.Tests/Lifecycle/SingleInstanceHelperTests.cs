using System.Globalization;
using GlazeShell.Windows.Lifecycle;
using GlazeShell.Windows.Tests.WindowManagement;

namespace GlazeShell.Windows.Tests.Lifecycle;

[TestClass]
[DoNotParallelize] // Работает с реальными окнами и named mutex: выполняется вне параллельной группы.
public sealed class SingleInstanceHelperTests
{
    private static string UniqueMutexName() =>
        "GlazeShell.Test.SingleInstance." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    [TestMethod]
    public void AcquireReturnsLeaseForFirstInstance()
    {
        using var lease = SingleInstanceHelper.Acquire(UniqueMutexName());
        Assert.IsNotNull(lease);
    }

    [TestMethod]
    public void AcquireReturnsNullForSameNameFromAnotherThread()
    {
        var name = UniqueMutexName();
        using var first = SingleInstanceHelper.Acquire(name);
        Assert.IsNotNull(first);

        SingleInstanceLease? second = null;
        var thread = new Thread(() => second = SingleInstanceHelper.Acquire(name));
        thread.Start();
        thread.Join();

        // Named mutex реентерабелен внутри одного потока, поэтому второй экземпляр
        // проверяется с другого потока: именно так его увидит второй процесс.
        Assert.IsNull(second);
    }

    [TestMethod]
    public void AcquireSucceedsAgainAfterLeaseIsDisposed()
    {
        var name = UniqueMutexName();
        var first = SingleInstanceHelper.Acquire(name);
        Assert.IsNotNull(first);
        first!.Dispose();

        using var second = SingleInstanceHelper.Acquire(name);
        Assert.IsNotNull(second);
    }

    [TestMethod]
    public void LeaseDisposeIsIdempotent()
    {
        var lease = SingleInstanceHelper.Acquire(UniqueMutexName());
        Assert.IsNotNull(lease);

        lease!.Dispose();
        lease.Dispose();
    }

    [TestMethod]
    public void DistinctNamesDoNotConflict()
    {
        using var first = SingleInstanceHelper.Acquire(UniqueMutexName());
        using var second = SingleInstanceHelper.Acquire(UniqueMutexName());

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
    }

    [TestMethod]
    public void FindOwnVisibleWindowReturnsZeroWithoutOwnWindow()
    {
        Assert.AreEqual(0, SingleInstanceHelper.FindOwnVisibleWindow());
    }

    [TestMethod]
    public void FindOwnVisibleWindowFindsOwnTestWindow()
    {
        using var window = new Win32TestWindow("GlazeShell single instance window");
        Assert.AreEqual(window.Handle, SingleInstanceHelper.FindOwnVisibleWindow());
    }

    [TestMethod]
    public void ActivateExistingInstanceDoesNotThrowWhenOwnWindowExists()
    {
        using var window = new Win32TestWindow("GlazeShell single instance activation");
        SingleInstanceHelper.ActivateExistingInstance();
    }
}