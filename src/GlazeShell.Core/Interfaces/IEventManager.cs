using GlazeShell.Core.Events;

namespace GlazeShell.Core.Interfaces;

public interface IEventManager
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : GlazeEvent;

    void Publish<TEvent>(TEvent glazeEvent)
        where TEvent : GlazeEvent;
}
