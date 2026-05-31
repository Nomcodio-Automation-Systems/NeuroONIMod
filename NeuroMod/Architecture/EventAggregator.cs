using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NeuroMod.Architecture
{
    /// <summary>
    /// Provides a lightweight thread-safe event aggregator for loosely coupled communication within the mod.
    /// </summary>
    /// <pre>Event subscribers provide handlers for concrete event types before matching events are published.</pre>
    /// <post>Published events are dispatched to a snapshot of the subscribed handlers for the requested event type.</post>
    public sealed class EventAggregator : IEventAggregator
    {
        private static readonly Lazy<EventAggregator> _instance = new(() => new EventAggregator());

        /// <summary>
        /// Gets the singleton event aggregator instance.
        /// </summary>
        /// <pre>No input is required.</pre>
        /// <post>The returned value is the shared event aggregator instance.</post>
        public static EventAggregator Instance => _instance.Value;

        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

        private EventAggregator() { }

        /// <summary>
        /// Subscribes a handler to events of the specified type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to subscribe to.</typeparam>
        /// <param name="handler">The handler to invoke when an event of the specified type is published.</param>
        /// <pre><paramref name="handler"/> references a valid delegate for the requested event type.</pre>
        /// <post>The handler is stored and will be included in subsequent event dispatch snapshots for <typeparamref name="TEvent"/>.</post>
        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            var list = _handlers.GetOrAdd(typeof(TEvent), _ => new List<Delegate>());
            lock (list)
            {
                list.Add(handler);
            }
        }

        /// <summary>
        /// Unsubscribes a handler from events of the specified type.
        /// </summary>
        /// <typeparam name="TEvent">The event type previously subscribed to.</typeparam>
        /// <param name="handler">The handler to remove from the subscription list.</param>
        /// <pre><paramref name="handler"/> identifies a previously registered delegate or a no-op removal is acceptable.</pre>
        /// <post>The handler is removed from future dispatches for <typeparamref name="TEvent"/> when present.</post>
        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                lock (list)
                {
                    list.Remove(handler);
                }
            }
        }

        /// <summary>
        /// Publishes an event to all subscribed handlers of the specified type.
        /// </summary>
        /// <typeparam name="TEvent">The event type being published.</typeparam>
        /// <param name="event">The event payload to dispatch.</param>
        /// <pre><paramref name="event"/> contains the event data to send to subscribed handlers.</pre>
        /// <post>Each subscribed handler for <typeparamref name="TEvent"/> is invoked against a stable snapshot of the current subscriptions.</post>
        public void Publish<TEvent>(TEvent @event)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                Delegate[] snapshot;
                lock (list)
                {
                    snapshot = list.ToArray();
                }

                foreach (var d in snapshot)
                {
                    try
                    {
                        ((Action<TEvent>)d).Invoke(@event);
                    }
                    catch { /* swallow exceptions to avoid breaking publisher */ }
                }
            }
        }
    }
}
