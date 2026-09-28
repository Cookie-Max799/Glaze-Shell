using Microsoft.UI.Dispatching;

namespace GlazeShell.App.Presentation;

public static class DispatcherQueueExtensions
{
    public static Task EnqueueAsync(this DispatcherQueue dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(action);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var queued = dispatcher.TryEnqueue(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        if (!queued)
        {
            completion.SetException(new InvalidOperationException("The dispatcher queue is shutting down."));
        }

        return completion.Task;
    }
}
