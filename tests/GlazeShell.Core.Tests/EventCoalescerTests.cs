using System;
using System.Collections.Concurrent;
using System.Threading;
using GlazeShell.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class EventCoalescerTests
{
    private const int WaitTimeoutMilliseconds = 10000;
    private const int QuiescenceMilliseconds = 400;

    private readonly ConcurrentQueue<Exception> _errors = new();

    private void OnError(Exception exception) => _errors.Enqueue(exception);

    [TestMethod]
    public void RequestExecutesOnceAfterDelay()
    {
        var executed = 0;
        using var signal = new ManualResetEventSlim(false);
        using var coalescer = new EventCoalescer(
            TimeSpan.FromMilliseconds(50),
            () =>
            {
                Interlocked.Increment(ref executed);
                signal.Set();
            },
            OnError);

        coalescer.Request();
        Assert.IsFalse(signal.Wait(0), "The action must not run before the delay elapses.");

        Assert.IsTrue(signal.Wait(WaitTimeoutMilliseconds), "The action was not executed after the delay.");
        Assert.AreEqual(1, Volatile.Read(ref executed));
    }

    [TestMethod]
    public void MultipleRequestsCoalesceToSingleExecution()
    {
        var executed = 0;
        using var signal = new ManualResetEventSlim(false);
        using var coalescer = new EventCoalescer(
            TimeSpan.FromMilliseconds(100),
            () =>
            {
                Interlocked.Increment(ref executed);
                signal.Set();
            },
            OnError);

        coalescer.Request();
        coalescer.Request();
        coalescer.Request();

        Assert.IsTrue(signal.Wait(WaitTimeoutMilliseconds), "The action was not executed.");

        // Даём запас времени: если запросы не были объединены, последующие такты дали бы
        // дополнительные выполнения, и счётчик превысил бы единицу.
        Thread.Sleep(QuiescenceMilliseconds);
        Assert.AreEqual(1, Volatile.Read(ref executed), "The requests were not coalesced into a single execution.");
    }

    [TestMethod]
    public void RequestDuringActionSchedulesAnotherExecution()
    {
        var executed = 0;
        using var first = new ManualResetEventSlim(false);
        using var second = new ManualResetEventSlim(false);
        using var coalescer = new EventCoalescer(
            TimeSpan.FromMilliseconds(50),
            () =>
            {
                if (Interlocked.Increment(ref executed) == 1)
                {
                    first.Set();
                }
                else
                {
                    second.Set();
                }
            },
            OnError);

        coalescer.Request();
        Assert.IsTrue(first.Wait(WaitTimeoutMilliseconds), "The first execution did not happen.");

        // Запрос, пришедший во время выполнения, не теряется: он планирует следующий проход.
        coalescer.Request();
        Assert.IsTrue(second.Wait(WaitTimeoutMilliseconds), "A request during execution was dropped.");
        Assert.AreEqual(2, Volatile.Read(ref executed));
    }

    [TestMethod]
    public void FlushExecutesPendingActionImmediately()
    {
        var executed = 0;
        using var coalescer = new EventCoalescer(
            TimeSpan.FromSeconds(30),
            () => Interlocked.Increment(ref executed),
            OnError);

        coalescer.Request();
        coalescer.Flush();

        Assert.AreEqual(1, Volatile.Read(ref executed), "Flush must run the pending action without waiting for the delay.");
    }

    [TestMethod]
    public void FlushDoesNothingWhenNothingIsPending()
    {
        var executed = 0;
        using var coalescer = new EventCoalescer(
            TimeSpan.FromMilliseconds(50),
            () => Interlocked.Increment(ref executed),
            OnError);

        coalescer.Flush();

        Assert.AreEqual(0, Volatile.Read(ref executed));
    }

    [TestMethod]
    public void CancelPreventsExecution()
    {
        var executed = 0;
        using var coalescer = new EventCoalescer(
            TimeSpan.FromMilliseconds(100),
            () => Interlocked.Increment(ref executed),
            OnError);

        coalescer.Request();
        coalescer.Cancel();
        Thread.Sleep(QuiescenceMilliseconds);

        Assert.AreEqual(0, Volatile.Read(ref executed), "A cancelled action must not run.");
    }

    [TestMethod]
    public void RequestAfterCancelStillExecutes()
    {
        var executed = 0;
        using var signal = new ManualResetEventSlim(false);
        using var coalescer = new EventCoalescer(
            TimeSpan.FromMilliseconds(50),
            () =>
            {
                Interlocked.Increment(ref executed);
                signal.Set();
            },
            OnError);

        coalescer.Request();
        coalescer.Cancel();
        coalescer.Request();

        Assert.IsTrue(signal.Wait(WaitTimeoutMilliseconds), "Cancel must not disable the coalescer permanently.");
        Assert.AreEqual(1, Volatile.Read(ref executed));
    }

    [TestMethod]
    public void DisposeIsIdempotentAndStopsPendingExecution()
    {
        var executed = 0;
        var coalescer = new EventCoalescer(
            TimeSpan.FromMilliseconds(100),
            () => Interlocked.Increment(ref executed),
            OnError);

        coalescer.Request();
        coalescer.Dispose();
        coalescer.Dispose();
        Thread.Sleep(QuiescenceMilliseconds);

        Assert.AreEqual(0, Volatile.Read(ref executed), "A disposed coalescer must not run a pending action.");
    }

    [TestMethod]
    public void RequestAfterDisposeIsIgnored()
    {
        var executed = 0;
        var coalescer = new EventCoalescer(
            TimeSpan.FromMilliseconds(50),
            () => Interlocked.Increment(ref executed),
            OnError);

        coalescer.Dispose();
        coalescer.Request();
        Thread.Sleep(QuiescenceMilliseconds);

        Assert.AreEqual(0, Volatile.Read(ref executed));
    }

    [TestMethod]
    public void ActionFailureIsRoutedToErrorHandlerAndDoesNotEscape()
    {
        using var signal = new ManualResetEventSlim(false);
        using var coalescer = new EventCoalescer(
            TimeSpan.FromMilliseconds(50),
            () => throw new InvalidOperationException("test"),
            exception =>
            {
                _errors.Enqueue(exception);
                signal.Set();
            });

        coalescer.Request();

        Assert.IsTrue(signal.Wait(WaitTimeoutMilliseconds), "The error handler was not invoked.");
        Assert.HasCount(1, _errors);
        Assert.IsInstanceOfType<InvalidOperationException>(_errors.ToArray()[0]);
    }

    [TestMethod]
    public void ActionKeepsRunningAfterFailure()
    {
        var executed = 0;
        using var recovered = new ManualResetEventSlim(false);
        using var coalescer = new EventCoalescer(
            TimeSpan.FromMilliseconds(50),
            () =>
            {
                if (Interlocked.Increment(ref executed) == 1)
                {
                    throw new InvalidOperationException("first pass fails");
                }

                recovered.Set();
            },
            OnError);

        coalescer.Request();
        Assert.IsTrue(WaitFor(() => !_errors.IsEmpty, WaitTimeoutMilliseconds), "The first failure was not reported.");

        coalescer.Request();
        Assert.IsTrue(recovered.Wait(WaitTimeoutMilliseconds), "The coalescer must keep working after a failure.");
    }

    [TestMethod]
    public void FallbackErrorHandlerDoesNotThrow()
    {
        var handler = EventCoalescer.FallbackErrorHandler("Test");

        handler(new InvalidOperationException("swallowed"));
    }

    [TestMethod]
    public void RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new EventCoalescer(TimeSpan.Zero, null!, OnError));
        Assert.Throws<ArgumentNullException>(() => new EventCoalescer(TimeSpan.Zero, () => { }, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventCoalescer(TimeSpan.FromSeconds(-1), () => { }, OnError));
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMilliseconds)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;

        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return condition();
    }
}
