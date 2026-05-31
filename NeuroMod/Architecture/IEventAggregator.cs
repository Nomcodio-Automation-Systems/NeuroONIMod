using System;
using System.Collections.Generic;

namespace NeuroMod.Architecture
{
    /// <summary>
    /// Defines a simple publish-subscribe event aggregation contract.
    /// </summary>
    /// <pre>Implementations coordinate typed event subscriptions and event dispatch.</pre>
    /// <post>Consumers can subscribe, unsubscribe, and publish events without knowing each other directly.</post>
    public interface IEventAggregator
    {
        /// <summary>
        /// Subscribes a handler to events of the specified type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to observe.</typeparam>
        /// <param name="handler">The handler to invoke when the event is published.</param>
        /// <pre><paramref name="handler"/> is a valid delegate for <typeparamref name="TEvent"/>.</pre>
        /// <post>The implementation tracks the handler for future publications of <typeparamref name="TEvent"/>.</post>
        void Subscribe<TEvent>(Action<TEvent> handler);

        /// <summary>
        /// Removes a previously subscribed handler from events of the specified type.
        /// </summary>
        /// <typeparam name="TEvent">The event type being unsubscribed from.</typeparam>
        /// <param name="handler">The handler to remove.</param>
        /// <pre><paramref name="handler"/> identifies a handler that may have been previously subscribed.</pre>
        /// <post>The implementation no longer invokes the handler for future publications of <typeparamref name="TEvent"/> when present.</post>
        void Unsubscribe<TEvent>(Action<TEvent> handler);

        /// <summary>
        /// Publishes an event to the current subscribers of the specified type.
        /// </summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <param name="event">The event payload to dispatch.</param>
        /// <pre><paramref name="event"/> contains the event data to forward to subscribers.</pre>
        /// <post>All applicable subscribers receive the published event according to the implementation strategy.</post>
        void Publish<TEvent>(TEvent @event);
    }
}
