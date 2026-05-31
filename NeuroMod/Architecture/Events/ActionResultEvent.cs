using System;

namespace NeuroMod.Architecture.Events
{
    /// <summary>
    /// Represents the outcome of an executed action within a traced workflow.
    /// </summary>
    /// <pre>The action execution has completed and produced a success flag and message.</pre>
    /// <post>A new immutable event instance can be published to interested consumers.</post>
    public sealed class ActionResultEvent
    {
        /// <summary>
        /// Gets the trace identifier that correlates this result to the originating workflow.
        /// </summary>
        /// <pre>The event instance was created from one completed action execution.</pre>
        /// <post>The property returns the trace identifier captured for this action result.</post>
        public string TraceId { get; }

        /// <summary>
        /// Gets a value indicating whether the action completed successfully.
        /// </summary>
        /// <pre>The event instance was created from one completed action execution.</pre>
        /// <post>The property returns the success flag captured for this action result.</post>
        public bool Successful { get; }

        /// <summary>
        /// Gets the result message returned by the action execution.
        /// </summary>
        /// <pre>The event instance was created from one completed action execution.</pre>
        /// <post>The property returns the execution message captured for this action result.</post>
        public string Message { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionResultEvent"/> class.
        /// </summary>
        /// <param name="traceId">The trace identifier associated with the action execution.</param>
        /// <param name="successful"><see langword="true"/> when the action succeeded; otherwise, <see langword="false"/>.</param>
        /// <param name="message">The execution result message for diagnostics or user feedback.</param>
        /// <pre><paramref name="traceId"/> identifies the originating workflow and <paramref name="message"/> contains the final action feedback.</pre>
        /// <post>The event stores the provided execution outcome for downstream processing.</post>
        public ActionResultEvent(string traceId, bool successful, string message)
        {
            if (string.IsNullOrWhiteSpace(traceId))
            {
                throw new ArgumentException("Trace identifier cannot be null or whitespace.", nameof(traceId));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            TraceId = traceId;
            Successful = successful;
            Message = message;
        }
    }
}
