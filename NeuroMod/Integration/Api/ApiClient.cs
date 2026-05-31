using System;
using System.Runtime.CompilerServices;
using NeuroSdk.Websocket;

namespace NeuroMod.Integration.Api
{
    /// <summary>
    /// Minimal API client wrapper to centralize outgoing message creation and sending.
    /// Use this static facade to reduce direct usages of SDK types across the codebase
    /// and make higher-level call sites easier to test and debug by swapping
    /// the underlying <see cref="IApiClient"/> implementation.
    /// </summary>
    /// <pre>
    /// <see cref="Instance"/> points to the active transport implementation for the current runtime.
    /// </pre>
    /// <post>
    /// Production code can send SDK and context messages through a single stable facade.
    /// </post>
    public static class ApiClient
    {
        /// <summary>
        /// Pluggable instance used for API operations. Tests can replace this with
        /// a mock implementation to intercept sends.
        /// </summary>
        /// <pre>A transport implementation is available for the current runtime or test context.</pre>
        /// <post>The property returns the active API client implementation used for outgoing operations.</post>
        public static IApiClient Instance { get; set; } = new ApiClientImpl();

        /// <summary>
        /// Backwards-compatible test seam that maps to the default implementation
        /// when present. Prefer replacing <see cref="Instance"/> with a mock in tests.
        /// </summary>
        /// <pre>The active client may or may not be the default <see cref="ApiClientImpl"/>.</pre>
        /// <post>Reads and writes pass through to the default implementation when it is active; otherwise setters are ignored.</post>
        public static Action<NeuroSdk.Messages.Outgoing.Context>? TestSendOverride
        {
            get => (Instance as ApiClientImpl)?.TestSendOverride;
            set
            {
                if (Instance is ApiClientImpl impl)
                    impl.TestSendOverride = value;
            }
        }

        /// <summary>
        /// Sends a simple textual context message via the configured API client.
        /// </summary>
        /// <param name="message">Context string to send.</param>
        /// <param name="isHighPriority">When true, server may treat message with higher priority.</param>
        /// <param name="caller">Automatically supplied caller member name for diagnostics.</param>
        /// <param name="file">Automatically supplied caller file path for diagnostics.</param>
        /// <param name="line">Automatically supplied caller line number for diagnostics.</param>
        /// <pre>
        /// <paramref name="message"/> is intended for the out-of-band context channel rather than the action result body.
        /// </pre>
        /// <post>
        /// The active <see cref="IApiClient"/> has been asked to transmit the context update.
        /// </post>
        public static void SendContext(string message, bool isHighPriority = false,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            Instance.SendContext(message, isHighPriority, caller, file, line);
        }

        /// <summary>
        /// Helper: build a context websocket message without sending it.
        /// </summary>
        /// <param name="message">Context payload text.</param>
        /// <param name="silent">Whether the message should be silent.</param>
        /// <returns>A <see cref="WsMessage"/> containing the encoded context.</returns>
        /// <pre>
        /// <paramref name="message"/> is valid for the context message schema.
        /// </pre>
        /// <post>
        /// A websocket payload is returned without modifying transport state.
        /// </post>
        public static WsMessage BuildContextMessage(string message, bool silent = false)
        {
            return BuildContextMessage(message, silent, caller: "", file: "", line: 0);
        }

        /// <summary>
        /// Builds a context message with caller diagnostics attached.
        /// </summary>
        /// <param name="message">Context payload text.</param>
        /// <param name="silent">Whether the message should be silent.</param>
        /// <param name="caller">Caller member name supplied for tracing.</param>
        /// <param name="file">Caller file path supplied for tracing.</param>
        /// <param name="line">Caller source line supplied for tracing.</param>
        /// <returns>A <see cref="WsMessage"/> representing the context payload.</returns>
        /// <pre>
        /// <paramref name="message"/> and diagnostics are ready to be encoded into a context payload.
        /// </pre>
        /// <post>
        /// A websocket message instance is returned without sending it.
        /// </post>
        public static WsMessage BuildContextMessage(string message, bool silent,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            return Instance.BuildContextMessage(message, silent, caller, file, line);
        }

        /// <summary>
        /// Sends an SDK message using the configured API client.
        /// </summary>
        /// <param name="messageBuilder">SDK-provided builder for the outgoing message.</param>
        /// <param name="caller">Caller member name supplied for tracing.</param>
        /// <param name="file">Caller file path supplied for tracing.</param>
        /// <param name="line">Caller source line supplied for tracing.</param>
        /// <pre>
        /// <paramref name="messageBuilder"/> contains a valid SDK message description.
        /// </pre>
        /// <post>
        /// The active <see cref="IApiClient"/> has been asked to send the outgoing SDK message.
        /// </post>
        public static void Send(NeuroSdk.Messages.API.OutgoingMessageBuilder messageBuilder,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            Instance.Send(messageBuilder, caller, file, line);
        }

        /// <summary>
        /// Sends an SDK message immediately, bypassing the usual queue.
        /// </summary>
        /// <param name="messageBuilder">SDK-provided builder for the outgoing message.</param>
        /// <param name="caller">Caller member name supplied for tracing.</param>
        /// <param name="file">Caller file path supplied for tracing.</param>
        /// <param name="line">Caller source line supplied for tracing.</param>
        /// <pre>
        /// <paramref name="messageBuilder"/> should be safe to send immediately.
        /// </pre>
        /// <post>
        /// The active <see cref="IApiClient"/> has attempted immediate delivery.
        /// </post>
        public static void SendImmediate(NeuroSdk.Messages.API.OutgoingMessageBuilder messageBuilder,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            Instance.SendImmediate(messageBuilder, caller, file, line);
        }
    }
}
