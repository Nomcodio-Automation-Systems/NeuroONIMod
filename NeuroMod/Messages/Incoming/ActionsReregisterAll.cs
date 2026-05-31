using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NeuroSdk.Actions;
using NeuroSdk.Messages.API;
using NeuroSdk.Websocket;

namespace NeuroSdk.Messages.Incoming;

[UsedImplicitly]
/// <summary>
/// Handles requests to re-register all currently known actions with Neuro.
/// </summary>
/// <pre>The websocket protocol has requested that the game resend its current action registry.</pre>
/// <post>All currently registered actions have been re-announced to the remote side.</post>
public sealed class ActionsReregisterAll : IncomingMessageHandler
{
    /// <summary>
    /// Determines whether this handler is responsible for the re-register-all command.
    /// </summary>
    /// <param name="command">The incoming command name.</param>
    /// <returns><see langword="true"/> when the command is <c>actions/reregister_all</c>.</returns>
    /// <pre><paramref name="command"/> contains the incoming websocket command identifier.</pre>
    /// <post>The result indicates whether this handler should process the message.</post>
    public override bool CanHandle(string command)
    {
        return command == "actions/reregister_all";
    }

    /// <summary>
    /// Accepts the re-register-all command without additional payload validation.
    /// </summary>
    /// <param name="command">The incoming command name.</param>
    /// <param name="messageData">The raw parsed payload wrapper.</param>
    /// <returns>A successful validation result.</returns>
    /// <pre>The command does not require additional payload data.</pre>
    /// <post>The returned result always indicates success.</post>
    protected override ExecutionResult Validate(string command, MessageJData messageData)
    {
        return ExecutionResult.Success();
    }

    /// <summary>
    /// Reports the result of the re-register-all command.
    /// </summary>
    /// <param name="result">The final execution result.</param>
    /// <pre>The command does not require an outward-facing result message.</pre>
    /// <post>No reporting side effects occur.</post>
    protected override void ReportResult(ExecutionResult result)
    {
    }

    /// <summary>
    /// Re-sends all currently registered actions to the remote side.
    /// </summary>
    /// <returns>A completed task after the resend has been triggered.</returns>
    /// <pre>The local action registry contains the actions that should be re-announced.</pre>
    /// <post>All currently registered actions have been offered to the resend pipeline.</post>
    protected override UniTask ExecuteAsync()
    {
        NeuroActionHandler.ResendRegisteredActions();
        return UniTask.CompletedTask;
    }
}