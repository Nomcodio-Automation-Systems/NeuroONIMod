using NeuroSdk.Websocket;

namespace NeuroMod.Integration.Api
{
    /// <summary>
    /// Abstraction for sending messages via the Neuro SDK. Use this interface to
    /// decouple higher-level code from the concrete websocket implementation and
    /// enable test doubles in unit tests.
    /// </summary>
    /// <pre>
    /// Implementations must preserve the distinction between action results and out-of-band context messages.
    /// </pre>
    /// <post>
    /// Callers can route websocket traffic through a stable interface without depending on a concrete transport class.
    /// </post>
    public interface IApiClient
    {
        /// <summary>
        /// Sends a simple context string to the Neuro server.
        /// </summary>
        /// <param name="message">Human-readable context message to send.</param>
        /// <param name="isHighPriority">When true the message should be treated as high priority.</param>
        /// <param name="caller">Optional calling member name for diagnostic tracing.</param>
        /// <param name="file">Optional calling file path for diagnostic tracing.</param>
        /// <param name="line">Optional calling source line for diagnostic tracing.</param>
        /// <pre>
        /// <paramref name="message"/> contains a user-facing context update that is valid for the context channel.
        /// </pre>
        /// <post>
        /// The context message has been handed to the configured transport or dropped according to implementation policy.
        /// </post>
        void SendContext(string message, bool isHighPriority = false, string caller = "", string file = "", int line = 0);

        /// <summary>
        /// Builds a websocket message object for a context message without sending it.
        /// </summary>
        /// <param name="message">The context payload.</param>
        /// <param name="silent">Whether to mark the message as silent (no UI notification).</param>
        /// <param name="caller">Caller member for tracing.</param>
        /// <param name="file">Caller file path for tracing.</param>
        /// <param name="line">Caller line number for tracing.</param>
        /// <returns>A <see cref="WsMessage"/> representing the context payload.</returns>
        /// <pre>
        /// <paramref name="message"/> is valid for serialization as a context websocket message.
        /// </pre>
        /// <post>
        /// A websocket message is returned without sending any network traffic.
        /// </post>
        WsMessage BuildContextMessage(string message, bool silent = false, string caller = "", string file = "", int line = 0);

        /// <summary>
        /// Sends an SDK-built outgoing message. The caller may construct the message
        /// using the SDK's <c>OutgoingMessageBuilder</c> API.
        /// </summary>
        /// <param name="messageBuilder">Builder for the outgoing message.</param>
        /// <param name="caller">Caller member for tracing.</param>
        /// <param name="file">Caller file path for tracing.</param>
        /// <param name="line">Caller line number for tracing.</param>
        /// <pre>
        /// <paramref name="messageBuilder"/> already describes a fully formed SDK message.
        /// </pre>
        /// <post>
        /// The outgoing SDK message has been handed to the transport layer.
        /// </post>
        void Send(NeuroSdk.Messages.API.OutgoingMessageBuilder messageBuilder, string caller = "", string file = "", int line = 0);

        /// <summary>
        /// Sends an SDK-built outgoing message immediately, bypassing any queuing.
        /// </summary>
        /// <param name="messageBuilder">Builder for the outgoing message.</param>
        /// <param name="caller">Caller member for tracing.</param>
        /// <param name="file">Caller file path for tracing.</param>
        /// <param name="line">Caller line number for tracing.</param>
        /// <pre>
        /// <paramref name="messageBuilder"/> identifies a message that should bypass the normal queue.
        /// </pre>
        /// <post>
        /// The implementation has attempted immediate delivery of the outgoing SDK message.
        /// </post>
        void SendImmediate(NeuroSdk.Messages.API.OutgoingMessageBuilder messageBuilder, string caller = "", string file = "", int line = 0);
    }
}
