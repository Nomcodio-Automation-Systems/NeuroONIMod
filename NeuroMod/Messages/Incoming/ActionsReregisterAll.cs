using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NeuroSdk.Actions;
using NeuroSdk.Messages.API;
using NeuroSdk.Websocket;

namespace NeuroSdk.Messages.Incoming;

[UsedImplicitly]
public sealed class ActionsReregisterAll : IncomingMessageHandler
{
    public override bool CanHandle(string command)
    {
        return command == "actions/reregister_all";
    }

    protected override ExecutionResult Validate(string command, MessageJData messageData)
    {
        return ExecutionResult.Success();
    }

    protected override void ReportResult(ExecutionResult result)
    {
    }

    protected override UniTask ExecuteAsync()
    {
        NeuroActionHandler.ResendRegisteredActions();
        return UniTask.CompletedTask;
    }
}