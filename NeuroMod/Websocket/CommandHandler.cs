#nullable enable

using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NeuroSdk.Messages.API;
using NeuroSdk.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using NeuroMod;

namespace NeuroSdk.Websocket;

[PublicAPI]
/// <summary>
/// Dispatches incoming websocket commands to registered message handlers.
/// </summary>
/// <pre>
/// Incoming handler components are discoverable under the current Unity transform hierarchy.
/// </pre>
/// <post>
/// Matching handlers validate, report, and optionally execute incoming commands.
/// </post>
public class CommandHandler : MonoBehaviour
{
    protected readonly List<IIncomingMessageHandler> Handlers = [];

    /// <summary>
    /// Discovers and registers incoming message handlers in the current transform hierarchy.
    /// </summary>
    /// <pre>
    /// The Unity object graph is initialized enough to discover handler components.
    /// </pre>
    /// <post>
    /// <see cref="Handlers"/> contains the discovered incoming message handlers.
    /// </post>
    public virtual void Start()
    {
        Handlers.AddRange(ReflectionHelpers.GetAllInDomain<IIncomingMessageHandler>(transform));
    }

    /// <summary>
    /// Routes an incoming websocket command to all matching handlers.
    /// </summary>
    /// <param name="command">The incoming command name.</param>
    /// <param name="data">The incoming JSON payload.</param>
    /// <pre>
    /// <paramref name="command"/> and <paramref name="data"/> describe a decoded inbound websocket message.
    /// </pre>
    /// <post>
    /// Matching handlers have validated the command, reported the result, and started execution when validation succeeded.
    /// </post>
    public virtual void Handle(string command, MessageJData data)
    {
        foreach (IIncomingMessageHandler handler in Handlers)
        {
            if (!handler.CanHandle(command))
            {
                continue;
            }

            ExecutionResult validationResult;
            object? parsedData;
            try
            {
                validationResult = handler.Validate(command, data, out parsedData);
            }
            catch (Exception e)
            {
                NeuroLogger.LogError("Caught exception during validation at WebsocketConnection level - this is bad.", "CommandHandler");
                NeuroLogger.LogException(e, "CommandHandler.Validate", "CommandHandler");

                validationResult = ExecutionResult.Failure(Strings.MessageHandlerFailedCaughtException.Format(e.Message));
                parsedData = null;
            }

            if (!validationResult.Successful)
            {
                NeuroLogger.LogWarning("Received unsuccessful execution result when handling a message", "CommandHandler");
                NeuroLogger.LogWarning(validationResult.Message ?? "<no message>", "CommandHandler");
                NeuroLogger.LogDebug(StackTraceUtility.ExtractStackTrace(), "CommandHandler");
            }

            handler.ReportResult(parsedData, validationResult);

            if (validationResult.Successful)
            {
                ExecuteHandlerAsync(handler, parsedData).Forget();
            }
        }
    }

    /// <summary>
    /// Executes a handler asynchronously and catches any unhandled exception so it is
    /// surfaced through <see cref="NeuroLogger"/> instead of being silently swallowed
    /// by UniTask's <c>Forget()</c>.
    /// </summary>
    /// <param name="handler">The handler to execute.</param>
    /// <param name="parsedData">The validated parsed data to pass to the handler.</param>
    /// <pre><paramref name="handler"/> has already been validated successfully.</pre>
    /// <post>Any exception thrown during execution is logged with full details.</post>
    private static async UniTask ExecuteHandlerAsync(IIncomingMessageHandler handler, object? parsedData)
    {
        try
        {
            await handler.ExecuteAsync(parsedData);
        }
        catch (Exception e)
        {
            NeuroLogger.LogError($"Unhandled exception during action execution: {e.GetType().Name} – {e.Message}", "CommandHandler");
            NeuroLogger.LogException(e, "CommandHandler.ExecuteAsync", "CommandHandler");
        }
    }
}
