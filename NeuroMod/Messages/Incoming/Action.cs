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
public sealed class Action : IncomingMessageHandler<Action.ParsedData>
{
    public class ParsedData(string id)
    {
        public readonly string Id = id;
        public INeuroAction? Action;
        public object? Data;
    }

    public override bool CanHandle(string command)
    {
        return command == "action";
    }

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

    protected override void ReportResult(ParsedData? parsedData, ExecutionResult result)
    {
        if (parsedData == null)
        {
            Debug.LogError($"ReportResult received null data. It probably could not be parsed in the action. Received result: {result.Message}");
            return;
        }

        WebsocketConnection.Instance!.Send(new ActionResult(parsedData.Id, result));
    }

    protected override UniTask ExecuteAsync(ParsedData? parsedData)
    {
        return parsedData!.Action!.ExecuteAsync(parsedData.Data!);
    }
}