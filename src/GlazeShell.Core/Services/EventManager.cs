using GlazeShell.Core.Events;
using GlazeShell.Core.Interfaces;

namespace GlazeShell.Core.Services;

public sealed class EventManager : IEventManager
{
    private readonly Dictionary<Type, List<Subscription>> _subscriptions = [];
    private readonly Action<Exception>? _exceptionHandler;
    private readonly object _sync = new();

    public EventManager(Action<Exception>? exceptionHandler = null)
    {
        _exceptionHandler = exceptionHandler;
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : GlazeEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);
        var subscription = new Subscription(
            this,
            eventType,
            value => handler((TEvent)value));

        lock (_sync)
        {
            if (!_subscriptions.TryGetValue(eventType, out var subscriptions))
            {
                subscriptions = [];
                _subscriptions.Add(eventType, subscriptions);
            }

            subscriptions.Add(subscription);
        }

        return subscription;
    }

    public bool HasSubscribers<TEvent>()
        where TEvent : GlazeEvent
    {
        lock (_sync)
        {
            return _subscriptions.TryGetValue(typeof(TEvent), out var subscriptions) && subscriptions.Count > 0;
        }
    }

    public void Publish<TEvent>(TEvent glazeEvent)
        where TEvent : GlazeEvent
    {
        ArgumentNullException.ThrowIfNull(glazeEvent);

        var eventType = glazeEvent.GetType();
        Subscription[] subscriptions;

        lock (_sync)
        {
            if (!_subscriptions.TryGetValue(eventType, out var registeredSubscriptions))
            {
                return;
            }

            subscriptions = registeredSubscriptions.ToArray();
        }

        foreach (var subscription in subscriptions)
        {
            try
            {
                subscription.Invoke(glazeEvent);
            }
            catch (Exception exception)
            {
                if (_exceptionHandler is null)
                {
                    throw;
                }

                _exceptionHandler(exception);
            }
        }
    }

    private void Remove(Type eventType, Subscription subscription)
    {
        lock (_sync)
        {
            if (!_subscriptions.TryGetValue(eventType, out var subscriptions))
            {
                return;
            }

            subscriptions.Remove(subscription);
            if (subscriptions.Count == 0)
            {
                _subscriptions.Remove(eventType);
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly EventManager _owner;
        private readonly Type _eventType;
        private readonly Action<GlazeEvent> _handler;
        private int _disposed;

        public Subscription(EventManager owner, Type eventType, Action<GlazeEvent> handler)
        {
            _owner = owner;
            _eventType = eventType;
            _handler = handler;
        }

        public void Invoke(GlazeEvent glazeEvent)
        {
            _handler(glazeEvent);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.Remove(_eventType, this);
            }
        }
    }
}
