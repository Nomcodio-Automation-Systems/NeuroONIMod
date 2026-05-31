#nullable enable

using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NeuroSdk.Actions;
using NeuroSdk.Messages.API;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System;

namespace NeuroSdk.Messages.Incoming;

[UsedImplicitly]
/// <summary>
/// Handles incoming action execution requests from the Neuro websocket protocol.
/// </summary>
/// <pre>Incoming messages contain an action id, a registered action name, and optional stringified JSON data.</pre>
/// <post>Validated action requests are dispatched to the registered action implementation and their results are reported back to Neuro.</post>
public sealed class Action : IncomingMessageHandler<Action.ParsedData>
{
    /// <summary>
    /// Carries the parsed execution state for an incoming action request.
    /// </summary>
    /// <pre>The action request contained a non-empty action id and validation has begun or completed.</pre>
    /// <post>The instance holds the action id plus the resolved action and parsed payload once validation succeeds.</post>
    public class ParsedData(string id)
    {
        /// <summary>
        /// Gets the protocol action id that must be echoed back in the result.
        /// </summary>
        public readonly string Id = id;

        /// <summary>
        /// Gets or sets the resolved registered action implementation.
        /// </summary>
        public INeuroAction? Action;

        /// <summary>
        /// Gets or sets the parsed payload produced during action validation.
        /// </summary>
        public object? Data;
    }

    /// <summary>
    /// Determines whether this handler is responsible for the action command.
    /// </summary>
    /// <param name="command">The incoming command name.</param>
    /// <returns><see langword="true"/> when the command is <c>action</c>.</returns>
    /// <pre><paramref name="command"/> contains the incoming websocket command identifier.</pre>
    /// <post>The result indicates whether this handler should process the message.</post>
    public override bool CanHandle(string command)
    {
        return command == "action";
    }

    /// <summary>
    /// Validates an incoming action request and resolves the registered action plus parsed payload.
    /// </summary>
    /// <param name="command">The incoming command name.</param>
    /// <param name="messageData">The raw parsed payload wrapper.</param>
    /// <param name="parsedData">Receives the parsed action execution state when validation succeeds.</param>
    /// <returns>The validation result for the action request.</returns>
    /// <pre><paramref name="messageData"/> contains the action id, name, and optional stringified payload expected by the Neuro action protocol.</pre>
    /// <post>On success <paramref name="parsedData"/> contains the action id, resolved action, and parsed action payload.</post>
    protected override ExecutionResult Validate(string command, MessageJData messageData, out ParsedData? parsedData)
    {
        if (messageData.Data == null)
        {
            parsedData = null;
            return ExecutionResult.VedalFailure(Strings.ActionFailedNoData);
        }

        string? id = messageData.Data["id"]?.Value<string>();

        if (id is null or "")
        {
            parsedData = null;
            return ExecutionResult.VedalFailure(Strings.ActionFailedNoId);
        }

        parsedData = new ParsedData(id);

        try
        {
            string? name = messageData.Data["name"]?.Value<string>();
            string? stringifiedData = messageData.Data["data"]?.Value<string>();

            if (name is null or "")
            {
                return ExecutionResult.VedalFailure(Strings.ActionFailedNoName);
            }

            INeuroAction? registeredAction = NeuroActionHandler.GetRegistered(name);
            if (registeredAction == null)
            {
                return NeuroActionHandler.IsRecentlyUnregistered(name)
                    ? ExecutionResult.Failure(Strings.ActionFailedUnregistered)
                    : ExecutionResult.Failure(Strings.ActionFailedUnknownAction.Format(name));
            }
            parsedData.Action = registeredAction;

            if (!ActionJData.TryParse(stringifiedData, out ActionJData? jData))
            {
                return ExecutionResult.Failure(Strings.ActionFailedInvalidJson);
            }

            ExecutionResult actionValidationResult = registeredAction.Validate(jData!, out object? parsedActionData);
            parsedData.Data = parsedActionData;

            return actionValidationResult;
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception caught while validating action {id}");
            Debug.LogError(e);

            return ExecutionResult.Failure(Strings.ActionFailedCaughtException.Format(e.Message));
        }
    }

    /// <summary>
    /// Sends the final action result back through the Neuro API client.
    /// </summary>
    /// <param name="parsedData">The parsed action execution state.</param>
    /// <param name="result">The final execution result.</param>
    /// <pre><paramref name="parsedData"/> contains the action id for the request being completed.</pre>
    /// <post>An <c>action/result</c> message has been enqueued unless parsing failed so early that no id was available.</post>
    protected override void ReportResult(ParsedData? parsedData, ExecutionResult result)
    {
        if (parsedData == null)
        {
            Debug.LogError($"ReportResult received null data. It probably could not be parsed in the action. Received result: {result.Message}");
            return;
        }

        NeuroMod.Integration.Api.ApiClient.Send(new ActionResult(parsedData.Id, result));
    }

    /// <summary>
    /// Executes the resolved registered action using the parsed payload produced during validation.
    /// Any unhandled exception is caught and reported back as a failed <c>action/result</c> so the
    /// caller receives the exception details instead of a silent failure.
    /// </summary>
    /// <param name="parsedData">The parsed action execution state.</param>
    /// <returns>A task representing the action execution.</returns>
    /// <pre><paramref name="parsedData"/> contains a resolved action instance and the payload produced during validation.</pre>
    /// <post>The returned task completes when the registered action has finished executing, or an error result has been sent on exception.</post>
    protected override async UniTask ExecuteAsync(ParsedData? parsedData)
    {
        try
        {
            await parsedData!.Action!.ExecuteAsync(parsedData.Data!);
        }
        catch (Exception e)
        {
            string message = $"Action '{parsedData!.Action?.Name ?? "unknown"}' threw {e.GetType().Name}: {e.Message}";
            Debug.LogError($"[NeuroMod] {message}");
            Debug.LogException(e);
            ReportResult(parsedData, ExecutionResult.Failure(message));
        }
    }
}