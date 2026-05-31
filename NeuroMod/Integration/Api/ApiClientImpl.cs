using System;
using System.Runtime.CompilerServices;
using NeuroSdk.Websocket;

namespace NeuroMod.Integration.Api
{
    /// <summary>
    /// Default implementation of <see cref="IApiClient"/>. Mirrors previous
    /// static ApiClient behavior but is instance-based to allow swapping in tests.
    /// </summary>
    /// <pre>
    /// The runtime either has a websocket connection available or explicitly tolerates dropped messages.
    /// </pre>
    /// <post>
    /// Callers can send context and SDK messages without depending on websocket implementation details.
    /// </post>
    public class ApiClientImpl : IApiClient
    {
        // Test seam: when set, this override will be invoked instead of sending
        // to the real WebsocketConnection. Tests should set and clear this.
        public Action<NeuroSdk.Messages.Outgoing.Context>? TestSendOverride;

        /// <summary>
        /// Sends a context message using the SDK or the test override when present.
        /// </summary>
        /// <param name="message">The context payload to send.</param>
        /// <param name="isHighPriority">Whether this is a high priority message.</param>
        /// <param name="caller">Caller member name for diagnostics.</param>
        /// <param name="file">Caller file path for diagnostics.</param>
        /// <param name="line">Caller source line for diagnostics.</param>
        /// <pre>
        /// <paramref name="message"/> is valid for the context channel and not intended as an action result body.
        /// </pre>
        /// <post>
        /// The message has been handed to the override or websocket connection, or logged as dropped.
        /// </post>
        public void SendContext(string message, bool isHighPriority = false, string caller = "", string file = "", int line = 0)
        {
            NeuroMod.NeuroLogger.LogDebug($"ApiClientImpl.SendContext: {message}", "ApiClientImpl", caller, file, line);
            try
            {
                var builder = new NeuroSdk.Messages.Outgoing.Context(message, isHighPriority);

                if (TestSendOverride != null)
                {
                    NeuroMod.NeuroLogger.LogDebug("ApiClientImpl.SendContext: using TestSendOverride", "ApiClientImpl", caller, file, line);
                    TestSendOverride(builder);
                    return;
                }

                if (NeuroSdk.Websocket.WebsocketConnection.Instance != null)
                {
                    NeuroMod.NeuroLogger.LogDebug("ApiClientImpl.SendContext: using WebsocketConnection.Instance.Send", "ApiClientImpl", caller, file, line);
                    NeuroSdk.Websocket.WebsocketConnection.Instance.Send(builder);
                    return;
                }

                NeuroMod.NeuroLogger.LogWarning("ApiClientImpl.SendContext: WebsocketConnection instance not available - dropping context message", "ApiClientImpl");
            }
            catch (Exception ex)
            {
                NeuroMod.NeuroLogger.LogError($"Failed to queue context message: {ex.Message}", "ApiClientImpl");
            }
        }

        /// <summary>
        /// Builds and returns a <see cref="WsMessage"/> for a context payload.
        /// </summary>
        /// <param name="message">The context payload text.</param>
        /// <param name="silent">Whether to mark the message as silent.</param>
        /// <param name="caller">Caller member name for diagnostics.</param>
        /// <param name="file">Caller file path for diagnostics.</param>
        /// <param name="line">Caller source line for diagnostics.</param>
        /// <returns>A websocket-ready <see cref="WsMessage"/>.</returns>
        /// <pre>
        /// <paramref name="message"/> can be represented as a context websocket payload.
        /// </pre>
        /// <post>
        /// A websocket message is returned without sending any transport traffic.
        /// </post>
        public WsMessage BuildContextMessage(string message, bool silent = false, string caller = "", string file = "", int line = 0)
        {
            NeuroMod.NeuroLogger.LogDebug($"ApiClientImpl.BuildContextMessage: {message}", "ApiClientImpl", caller, file, line);
            var builder = new NeuroSdk.Messages.Outgoing.Context(message, silent);
            return builder.GetWsMessage();
        }

        /// <summary>
        /// Sends an SDK-provided outgoing message using the websocket connection
        /// or the TrySend fallback when the instance is not available.
        /// </summary>
        /// <param name="messageBuilder">SDK message builder instance.</param>
        /// <param name="caller">Caller member name for diagnostics.</param>
        /// <param name="file">Caller file path for diagnostics.</param>
        /// <param name="line">Caller source line for diagnostics.</param>
        /// <pre>
        /// <paramref name="messageBuilder"/> contains a valid outgoing SDK message builder.
        /// </pre>
        /// <post>
        /// The websocket connection has been asked to send the message or a drop was logged.
        /// </post>
        public void Send(NeuroSdk.Messages.API.OutgoingMessageBuilder messageBuilder, string caller = "", string file = "", int line = 0)
        {
            try
            {
                if (NeuroSdk.Websocket.WebsocketConnection.Instance != null)
                {
                    NeuroMod.NeuroLogger.LogDebug("ApiClientImpl.Send: using WebsocketConnection.Instance.Send", "ApiClientImpl", caller, file, line);
                    NeuroSdk.Websocket.WebsocketConnection.Instance.Send(messageBuilder);
                    return;
                }

                NeuroMod.NeuroLogger.LogWarning("ApiClientImpl.Send: WebsocketConnection instance not available - dropping message", "ApiClientImpl");
            }
            catch (Exception ex)
            {
                NeuroMod.NeuroLogger.LogError($"Failed to send message: {ex.Message}", "ApiClientImpl");
            }
        }

        /// <summary>
        /// Sends an SDK message immediately (non-queued) if the instance supports it,
        /// otherwise falls back to a best-effort TrySendImmediate call.
        /// </summary>
        /// <param name="messageBuilder">SDK message builder instance.</param>
        /// <param name="caller">Caller member name for diagnostics.</param>
        /// <param name="file">Caller file path for diagnostics.</param>
        /// <param name="line">Caller source line for diagnostics.</param>
        /// <pre>
        /// <paramref name="messageBuilder"/> identifies a message that should bypass the normal queue.
        /// </pre>
        /// <post>
        /// Immediate delivery has been attempted or a failure to do so has been logged.
        /// </post>
        public void SendImmediate(NeuroSdk.Messages.API.OutgoingMessageBuilder messageBuilder, string caller = "", string file = "", int line = 0)
        {
            try
            {
                if (NeuroSdk.Websocket.WebsocketConnection.Instance != null)
                {
                    NeuroMod.NeuroLogger.LogDebug("ApiClientImpl.SendImmediate: using WebsocketConnection.Instance.SendImmediate", "ApiClientImpl", caller, file, line);
                    NeuroSdk.Websocket.WebsocketConnection.Instance.SendImmediate(messageBuilder);
                    return;
                }

                NeuroMod.NeuroLogger.LogWarning("ApiClientImpl.SendImmediate: WebsocketConnection instance not available - cannot send immediate message", "ApiClientImpl");
            }
            catch (Exception ex)
            {
                NeuroMod.NeuroLogger.LogError($"Failed to send immediate message: {ex.Message}", "ApiClientImpl");
            }
        }
    }
}
