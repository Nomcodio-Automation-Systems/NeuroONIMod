using System;

namespace NeuroMod.Architecture.Events
{
    /// <summary>
    /// Represents a notification that an action was added to the current traced workflow.
    /// </summary>
    /// <pre>The trace identifier and action name have been validated by the caller.</pre>
    /// <post>A new immutable event instance is available for downstream consumers.</post>
    public sealed class ActionAddedEvent
    {
        /// <summary>
        /// Gets the trace identifier that correlates this event with the originating workflow.
        /// </summary>
        /// <pre>The event instance was created with one action-registration payload.</pre>
        /// <post>The property returns the trace identifier captured for the action-added notification.</post>
        public string TraceId { get; }

        /// <summary>
        /// Gets the name of the action that was added.
        /// </summary>
        /// <pre>The event instance was created with one action-registration payload.</pre>
        /// <post>The property returns the action name captured for the notification.</post>
        public string ActionName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionAddedEvent"/> class.
        /// </summary>
        /// <param name="traceId">The trace identifier associated with the action registration flow.</param>
        /// <param name="actionName">The name of the action that was added.</param>
        /// <pre><paramref name="traceId"/> and <paramref name="actionName"/> are expected to be non-empty values.</pre>
        /// <post>The event stores the supplied identifiers without further mutation.</post>
        public ActionAddedEvent(string traceId, string actionName)
        {
            if (string.IsNullOrWhiteSpace(traceId))
            {
                throw new ArgumentException("Trace identifier cannot be null or whitespace.", nameof(traceId));
            }

            if (string.IsNullOrWhiteSpace(actionName))
            {
                throw new ArgumentException("Action name cannot be null or whitespace.", nameof(actionName));
            }

            TraceId = traceId;
            ActionName = actionName;
        }
    }
}
