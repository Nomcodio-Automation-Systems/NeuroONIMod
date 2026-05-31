#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.API;

namespace NeuroSdk.Messages.Outgoing;

[PublicAPI]
/// <summary>
/// Builds the outbound websocket message that sends textual context to Neuro.
/// </summary>
/// <pre>Callers provide a context message and whether it should be delivered silently.</pre>
/// <post>The builder serializes to a <c>context</c> payload containing the message and silence flag.</post>
public sealed class Context : OutgoingMessageBuilder
{
    private readonly string message;
    private readonly bool silent;

    /// <summary>
    /// Creates a context-message builder.
    /// </summary>
    /// <param name="message">The context text to send.</param>
    /// <param name="silent">Whether the remote side should treat the context as silent.</param>
    /// <pre><paramref name="message"/> contains the context text intended for the remote side.</pre>
    /// <post>The builder holds the supplied message and silence flag for later serialization.</post>
    public Context(string message, bool silent = false)
    {
        this.message = message;
        this.silent = silent;
    }

    /// <summary>
    /// Gets the websocket command name for context messages.
    /// </summary>
    protected override string Command => "context";

    /// <summary>
    /// Gets the payload object containing the context message fields.
    /// </summary>
    /// <pre>The builder holds the message text and silence flag supplied at construction time.</pre>
    /// <post>The returned anonymous payload contains the exact outbound fields expected by the protocol.</post>
    protected override object? Data => new { message = message, silent = silent };

    /// <summary>
    /// Gets whether this context message is marked as silent.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned value matches the silence flag supplied at construction time.</post>
    public bool IsSilent => silent;
}
