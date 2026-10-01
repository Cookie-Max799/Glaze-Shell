using GlazeShell.Core.Events;

namespace GlazeShell.Core.Interfaces;

public interface IEventManager
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : GlazeEvent;

    void Publish<TEvent>(TEvent glazeEvent)
        where TEvent : GlazeEvent;

    /// <summary>
    /// Показывает, есть ли активные подписки именно на указанный тип события.
    /// Источник событий использует это, чтобы не выполнять фоновую работу, когда
    /// никто не слушает: подписка на производный тип в подсчёт не входит,
    /// а <see cref="GlazeEvent"/> учитывает только подписку на базовый тип.
    /// </summary>
    bool HasSubscribers<TEvent>()
        where TEvent : GlazeEvent;
}
