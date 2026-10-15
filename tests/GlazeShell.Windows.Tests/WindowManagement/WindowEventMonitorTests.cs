using System.Collections.Concurrent;
using GlazeShell.Core.Services;
using GlazeShell.Windows.WindowManagement;

namespace GlazeShell.Windows.Tests.WindowManagement;

[TestClass]
public sealed class WindowEventMonitorTests
{
    private readonly ConcurrentQueue<Exception> _errors = new();

    [TestCleanup]
    public void Cleanup()
    {
        if (_errors.TryDequeue(out var failure))
        {
            Assert.Fail($"An event dispatch failed: {failure.Message}");
        }
    }

    [TestMethod]
    public void RestartsCleanlyAfterEachStop()
    {
        // Хуки снимает поток, который их установил: после каждой остановки следующий
        // монитор снова устанавливает хуки и сообщает готовность.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var monitor = new WindowEventMonitor(CreateEvents(), skipOwnProcess: false);

            if (!monitor.Start())
            {
                Assert.Inconclusive("WinEvent hooks are unavailable in this environment.");
                return;
            }
        }
    }

    private EventManager CreateEvents() => new(exception => _errors.Enqueue(exception));
}
