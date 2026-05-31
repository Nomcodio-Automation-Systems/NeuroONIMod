#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.API;
using NeuroSdk.Messages.Outgoing;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using NeuroMod;

namespace NeuroSdk.Websocket;

[PublicAPI]
/// <summary>
/// Thread-safe outgoing message queue for websocket transport.
/// </summary>
/// <pre>
/// Producers may enqueue outgoing SDK messages from different runtime callbacks.
/// </pre>
/// <post>
/// Messages are buffered, merged when possible, and made available for websocket dispatch.
/// </post>
public class MessageQueue : MonoBehaviour
{
    protected readonly List<OutgoingMessageBuilder> Messages = [new Startup()];

    /// <summary>
    /// Gets the current number of queued outgoing messages.
    /// </summary>
    /// <pre>
    /// The queue may be accessed concurrently by producers and consumers.
    /// </pre>
    /// <post>
    /// The current queue length is returned.
    /// </post>
    public virtual int Count
    {
        get
        {
            lock (Messages)
            {
                return Messages.Count;
            }
        }
    }

    /// <summary>
    /// Enqueues an outgoing message, merging with an existing builder when possible.
    /// </summary>
    /// <param name="message">The outgoing message builder to enqueue.</param>
    /// <pre>
    /// <paramref name="message"/> contains a valid outgoing SDK message builder.
    /// </pre>
    /// <post>
    /// The message is merged into an existing entry or appended to the queue.
    /// </post>
    public virtual void Enqueue(OutgoingMessageBuilder message)
    {
        lock (Messages)
        {
            foreach (OutgoingMessageBuilder existingMessage in Messages)
            {
                if (existingMessage.Merge(message))
                {
                    NeuroLogger.LogDebug($"Merged outgoing message into existing builder", "MessageQueue");
                    return;
                }
            }

            Messages.Add(message);
            NeuroLogger.LogDebug($"Enqueued outgoing message. Queue size now: {Messages.Count}", "MessageQueue");
        }
    }

    /// <summary>
    /// Dequeues the next outgoing message builder.
    /// </summary>
    /// <returns>The next queued builder, or <see langword="null"/> when the queue is empty.</returns>
    /// <pre>
    /// The queue may or may not contain pending messages.
    /// </pre>
    /// <post>
    /// The head of the queue is removed and returned when one exists.
    /// </post>
    public virtual OutgoingMessageBuilder? Dequeue()
    {
        lock (Messages)
        {
            if (Messages.Count == 0)
            {
                return null;
            }

            OutgoingMessageBuilder message = Messages[0];
            Messages.RemoveAt(0);

            NeuroLogger.LogDebug($"Dequeued outgoing message. Queue size now: {Messages.Count}", "MessageQueue");
            return message;
        }
    }
}