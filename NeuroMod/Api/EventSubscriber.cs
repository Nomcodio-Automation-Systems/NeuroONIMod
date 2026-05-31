using System;
using UnityEngine;
using NativeWebSocket;
using System.Runtime.CompilerServices;

namespace NeuroMod.Api
{
    /// <summary>
    /// Helper to wrap event callbacks with aggressive debug logging and anti-spam.
    /// Use Wrap to get a safe callback to subscribe.
    /// </summary>
    /// <pre>Wrapped callbacks are valid delegates that may throw during event execution.</pre>
    /// <post>Returned delegates log throttled entry and exit messages and swallow callback exceptions after reporting them.</post>
    public static class EventSubscriber
    {
        private static readonly TimeSpan DefaultThrottle = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Wraps a parameterless callback with throttled logging and exception protection.
        /// </summary>
        /// <param name="tag">Logical tag used for diagnostics and throttling.</param>
        /// <param name="callback">Callback to execute safely.</param>
        /// <param name="caller">Caller member name captured automatically for diagnostics.</param>
        /// <param name="file">Caller file path captured automatically for diagnostics.</param>
        /// <param name="line">Caller line number captured automatically for diagnostics.</param>
        /// <returns>A safe wrapper delegate around <paramref name="callback"/>.</returns>
        /// <pre><paramref name="tag"/> consistently identifies the subscribed event source.</pre>
        /// <post>The returned delegate never propagates callback exceptions to the event source.</post>
        public static global::System.Action Wrap(string tag, global::System.Action callback,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (callback == null) return () => { };

            return () =>
            {
                try
                {
                    if (LogThrottler.ShouldLog($"EventEnter:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event enter: {tag}", tag, caller, file, line);

                    callback();

                    if (LogThrottler.ShouldLog($"EventExit:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event exit: {tag}", tag, caller, file, line);
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogException(ex, $"EventSubscriber({tag})", tag);
                }
            };
        }

        /// <summary>
        /// Wraps a single-argument callback with throttled logging and exception protection.
        /// </summary>
        /// <typeparam name="T">Argument type expected by the callback.</typeparam>
        /// <param name="tag">Logical tag used for diagnostics and throttling.</param>
        /// <param name="callback">Callback to execute safely.</param>
        /// <param name="caller">Caller member name captured automatically for diagnostics.</param>
        /// <param name="file">Caller file path captured automatically for diagnostics.</param>
        /// <param name="line">Caller line number captured automatically for diagnostics.</param>
        /// <returns>A safe wrapper delegate around <paramref name="callback"/>.</returns>
        /// <pre><paramref name="callback"/> accepts the event payload supplied by the source.</pre>
        /// <post>The returned delegate logs entry and exit transitions when not throttled and reports failures safely.</post>
        public static global::System.Action<T> Wrap<T>(string tag, global::System.Action<T> callback,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (callback == null) return _ => { };

            return (arg) =>
            {
                try
                {
                    if (LogThrottler.ShouldLog($"EventEnter:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event enter: {tag}", tag, caller, file, line);

                    callback(arg);

                    if (LogThrottler.ShouldLog($"EventExit:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event exit: {tag}", tag, caller, file, line);
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogException(ex, $"EventSubscriber({tag})", tag);
                }
            };
        }

        /// <summary>
        /// Wraps a NativeWebSocket open handler with throttled logging and exception protection.
        /// </summary>
        /// <param name="tag">Logical tag used for diagnostics and throttling.</param>
        /// <param name="callback">Open callback to execute safely.</param>
        /// <param name="caller">Caller member name captured automatically for diagnostics.</param>
        /// <param name="file">Caller file path captured automatically for diagnostics.</param>
        /// <param name="line">Caller line number captured automatically for diagnostics.</param>
        /// <returns>A websocket-compatible open handler.</returns>
        /// <pre><paramref name="callback"/> represents a websocket open lifecycle callback.</pre>
        /// <post>The returned handler matches <see cref="WebSocketOpenEventHandler"/> and protects the caller from callback exceptions.</post>
        public static WebSocketOpenEventHandler WrapWebsocketOpen(string tag, global::System.Action callback,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (callback == null) return () => { };
            return () =>
            {
                try
                {
                    if (LogThrottler.ShouldLog($"EventEnter:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event enter: {tag}", tag, caller, file, line);

                    callback();

                    if (LogThrottler.ShouldLog($"EventExit:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event exit: {tag}", tag, caller, file, line);
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogException(ex, $"EventSubscriber({tag})", tag);
                }
            };
        }

        /// <summary>
        /// Wraps a NativeWebSocket message handler with throttled logging and exception protection.
        /// </summary>
        /// <param name="tag">Logical tag used for diagnostics and throttling.</param>
        /// <param name="callback">Message callback to execute safely.</param>
        /// <param name="caller">Caller member name captured automatically for diagnostics.</param>
        /// <param name="file">Caller file path captured automatically for diagnostics.</param>
        /// <param name="line">Caller line number captured automatically for diagnostics.</param>
        /// <returns>A websocket-compatible message handler.</returns>
        /// <pre><paramref name="callback"/> accepts websocket payload bytes supplied by the socket.</pre>
        /// <post>The returned handler preserves websocket delegate shape while logging and protecting against callback failures.</post>
        public static WebSocketMessageEventHandler WrapWebsocketMessage(string tag, global::System.Action<byte[]> callback,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (callback == null) return _ => { };
            return (bytes) =>
            {
                try
                {
                    if (LogThrottler.ShouldLog($"EventEnter:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event enter: {tag}", tag, caller, file, line);

                    callback(bytes);

                    if (LogThrottler.ShouldLog($"EventExit:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event exit: {tag}", tag, caller, file, line);
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogException(ex, $"EventSubscriber({tag})", tag);
                }
            };
        }

        /// <summary>
        /// Wraps a NativeWebSocket error handler with throttled logging and exception protection.
        /// </summary>
        /// <param name="tag">Logical tag used for diagnostics and throttling.</param>
        /// <param name="callback">Error callback to execute safely.</param>
        /// <param name="caller">Caller member name captured automatically for diagnostics.</param>
        /// <param name="file">Caller file path captured automatically for diagnostics.</param>
        /// <param name="line">Caller line number captured automatically for diagnostics.</param>
        /// <returns>A websocket-compatible error handler.</returns>
        /// <pre><paramref name="callback"/> accepts websocket error text emitted by the socket implementation.</pre>
        /// <post>The returned handler reports callback exceptions without destabilizing the websocket event pipeline.</post>
        public static WebSocketErrorEventHandler WrapWebsocketError(string tag, global::System.Action<string> callback,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (callback == null) return _ => { };
            return (error) =>
            {
                try
                {
                    if (LogThrottler.ShouldLog($"EventEnter:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event enter: {tag}", tag, caller, file, line);

                    callback(error);

                    if (LogThrottler.ShouldLog($"EventExit:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event exit: {tag}", tag, caller, file, line);
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogException(ex, $"EventSubscriber({tag})", tag);
                }
            };
        }

        /// <summary>
        /// Wraps a NativeWebSocket close handler with throttled logging and exception protection.
        /// </summary>
        /// <param name="tag">Logical tag used for diagnostics and throttling.</param>
        /// <param name="callback">Close callback to execute safely.</param>
        /// <param name="caller">Caller member name captured automatically for diagnostics.</param>
        /// <param name="file">Caller file path captured automatically for diagnostics.</param>
        /// <param name="line">Caller line number captured automatically for diagnostics.</param>
        /// <returns>A websocket-compatible close handler.</returns>
        /// <pre><paramref name="callback"/> accepts the close code emitted when the socket disconnects.</pre>
        /// <post>The returned handler keeps websocket close processing resilient to callback failures.</post>
        public static WebSocketCloseEventHandler WrapWebsocketClose(string tag, global::System.Action<WebSocketCloseCode> callback,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (callback == null) return _ => { };
            return (code) =>
            {
                try
                {
                    if (LogThrottler.ShouldLog($"EventEnter:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event enter: {tag}", tag, caller, file, line);

                    callback(code);

                    if (LogThrottler.ShouldLog($"EventExit:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event exit: {tag}", tag, caller, file, line);
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogException(ex, $"EventSubscriber({tag})", tag);
                }
            };
        }

        /// <summary>
        /// Wraps a two-argument callback with throttled logging and exception protection.
        /// </summary>
        /// <typeparam name="T1">First argument type.</typeparam>
        /// <typeparam name="T2">Second argument type.</typeparam>
        /// <param name="tag">Logical tag used for diagnostics and throttling.</param>
        /// <param name="callback">Callback to execute safely.</param>
        /// <param name="caller">Caller member name captured automatically for diagnostics.</param>
        /// <param name="file">Caller file path captured automatically for diagnostics.</param>
        /// <param name="line">Caller line number captured automatically for diagnostics.</param>
        /// <returns>A safe wrapper delegate around <paramref name="callback"/>.</returns>
        /// <pre><paramref name="callback"/> accepts the two-part event payload produced by the source.</pre>
        /// <post>The returned delegate logs entry and exit transitions when allowed and swallows callback exceptions after logging them.</post>
        public static global::System.Action<T1, T2> Wrap<T1, T2>(string tag, global::System.Action<T1, T2> callback,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (callback == null) return (_, __) => { };

            return (a, b) =>
            {
                try
                {
                    if (LogThrottler.ShouldLog($"EventEnter:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event enter: {tag}", tag, caller, file, line);

                    callback(a, b);

                    if (LogThrottler.ShouldLog($"EventExit:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event exit: {tag}", tag, caller, file, line);
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogException(ex, $"EventSubscriber({tag})", tag);
                }
            };
        }

        /// <summary>
        /// Wraps a standard <see cref="EventHandler"/> with throttled logging and exception protection.
        /// </summary>
        /// <param name="tag">Logical tag used for diagnostics and throttling.</param>
        /// <param name="handler">Handler to execute safely.</param>
        /// <param name="caller">Caller member name captured automatically for diagnostics.</param>
        /// <param name="file">Caller file path captured automatically for diagnostics.</param>
        /// <param name="line">Caller line number captured automatically for diagnostics.</param>
        /// <returns>A safe wrapper delegate around <paramref name="handler"/>.</returns>
        /// <pre><paramref name="handler"/> matches a standard sender/args event signature.</pre>
        /// <post>The returned handler maintains the original signature and prevents exceptions from escaping the event source.</post>
        public static global::System.EventHandler WrapEventHandler(string tag, global::System.EventHandler handler,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (handler == null) return (_, __) => { };

            return (sender, args) =>
            {
                try
                {
                    if (LogThrottler.ShouldLog($"EventEnter:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event enter: {tag}", tag, caller, file, line);

                    handler(sender, args);

                    if (LogThrottler.ShouldLog($"EventExit:{tag}", DefaultThrottle))
                        NeuroLogger.LogDebug($"Event exit: {tag}", tag, caller, file, line);
                }
                catch (Exception ex)
                {
                    NeuroLogger.LogException(ex, $"EventSubscriber({tag})", tag);
                }
            };
        }
    }
}
