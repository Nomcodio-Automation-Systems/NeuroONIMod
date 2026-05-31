using System;
using System.Collections.Generic;

namespace NeuroMod.Architecture.Events
{
    /// <summary>
    /// Represents a notification that a window and its available actions were registered.
    /// </summary>
    /// <pre>The caller has completed window registration and collected the exposed action names.</pre>
    /// <post>A new immutable event instance can be dispatched to subscribers.</post>
    public sealed class WindowRegisteredEvent
    {
        /// <summary>
        /// Gets the trace identifier that correlates this registration event to the originating workflow.
        /// </summary>
        /// <pre>The event instance was created with registration metadata from one workflow.</pre>
        /// <post>The property returns the trace identifier captured for this registration event.</post>
        public string TraceId { get; }

        /// <summary>
        /// Gets the action names that were available when the window was registered.
        /// </summary>
        /// <pre>The event instance was created with registration metadata from one workflow.</pre>
        /// <post>The property returns the immutable list of action names captured during registration.</post>
        public IReadOnlyList<string> ActionNames { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowRegisteredEvent"/> class.
        /// </summary>
        /// <param name="traceId">The trace identifier associated with the window registration flow.</param>
        /// <param name="actionNames">The action names exposed by the registered window.</param>
        /// <pre><paramref name="traceId"/> is expected to identify the current trace and <paramref name="actionNames"/> contains the registered action names.</pre>
        /// <post>The event preserves the supplied registration data for downstream consumers.</post>
        public WindowRegisteredEvent(string traceId, IReadOnlyList<string> actionNames)
        {
            if (string.IsNullOrWhiteSpace(traceId))
            {
                throw new ArgumentException(
                    "Trace identifier cannot be null or whitespace.",
                    nameof(traceId)
                );
            }

            if (actionNames == null)
            {
                throw new ArgumentNullException(nameof(actionNames));
            }

            TraceId = traceId;
            ActionNames = actionNames;
        }
    }
}
