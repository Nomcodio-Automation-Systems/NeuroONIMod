#nullable enable

using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NeuroSdk.Websocket;

namespace NeuroSdk.Messages.API;

[PublicAPI]
/// <summary>
/// Defines the lifecycle for validating, reporting, and executing incoming websocket messages.
/// </summary>
/// <pre>Implementations are registered against incoming commands and receive the raw parsed message payload.</pre>
/// <post>Each handled message can be validated, reported, and executed through a uniform interface.</post>
public interface IIncomingMessageHandler
{
    /// <summary>
    /// Determines whether this handler is responsible for the supplied command.
    /// </summary>
    /// <param name="command">The incoming command name.</param>
    /// <returns><see langword="true"/> when this handler can process <paramref name="command"/>.</returns>
    /// <pre><paramref name="command"/> contains the incoming websocket command identifier.</pre>
    /// <post>The result indicates whether this handler should be selected for the command.</post>
    bool CanHandle(string command);

    /// <summary>
    /// Validates an incoming message and optionally produces parsed execution data.
    /// </summary>
    /// <param name="command">The incoming command name.</param>
    /// <param name="messageData">The raw parsed payload wrapper.</param>
    /// <param name="parsedData">Receives parsed execution data when validation succeeds.</param>
    /// <returns>The validation result for the message.</returns>
    /// <pre><paramref name="messageData"/> reflects the payload already extracted from the websocket message envelope.</pre>
    /// <post>The returned result describes whether execution may proceed and <paramref name="parsedData"/> contains any parsed state needed for execution.</post>
    ExecutionResult Validate(string command, MessageJData messageData, out object? parsedData);

    /// <summary>
    /// Reports the result of handling a previously validated message.
    /// </summary>
    /// <param name="parsedData">The parsed execution state produced during validation.</param>
    /// <param name="result">The final execution result.</param>
    /// <pre><paramref name="parsedData"/> matches the state produced by <see cref="Validate(string, MessageJData, out object?)"/> for the same message.</pre>
    /// <post>Any required outward-facing reporting side effects for the handled message have been triggered.</post>
    void ReportResult(object? parsedData, ExecutionResult result);

    /// <summary>
    /// Executes the behavior associated with a previously validated message.
    /// </summary>
    /// <param name="parsedData">The parsed execution state produced during validation.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    /// <pre><paramref name="parsedData"/> matches the state produced by <see cref="Validate(string, MessageJData, out object?)"/> for the same message.</pre>
    /// <post>The returned task completes when the message side effects have finished executing.</post>
    UniTask ExecuteAsync(object? parsedData);
}

[PublicAPI]
/// <summary>
/// Base implementation for incoming message handlers that do not need parsed execution state.
/// </summary>
/// <pre>Derived types validate and execute commands without carrying typed parsed data between the phases.</pre>
/// <post>The explicit interface implementation bridges the untyped handler contract to the simplified derived API.</post>
public abstract class IncomingMessageHandler : IIncomingMessageHandler
{
    public abstract bool CanHandle(string command);

    protected abstract ExecutionResult Validate(string command, MessageJData messageData);

    protected abstract void ReportResult(ExecutionResult result);

    protected abstract UniTask ExecuteAsync();

    /// <summary>
    /// Adapts the simplified validation contract to the untyped handler interface.
    /// </summary>
    /// <param name="command">The incoming command name.</param>
    /// <param name="messageData">The raw parsed payload wrapper.</param>
    /// <param name="parsedData">Receives null because this handler does not carry parsed state.</param>
    /// <returns>The validation result for the message.</returns>
    /// <pre>The derived handler does not require parsed state to flow into execution.</pre>
    /// <post><paramref name="parsedData"/> is null and the returned result matches the derived validation result.</post>
    ExecutionResult IIncomingMessageHandler.Validate(string command, MessageJData messageData, out object? parsedData)
    {
        ExecutionResult result = Validate(command, messageData);
        parsedData = null;
        return result;
    }

    /// <summary>
    /// Adapts result reporting to the simplified derived reporting contract.
    /// </summary>
    /// <param name="parsedData">Unused parsed state.</param>
    /// <param name="result">The final execution result.</param>
    /// <pre>This handler shape does not require parsed state during result reporting.</pre>
    /// <post>The derived reporting method has been invoked with <paramref name="result"/>.</post>
    void IIncomingMessageHandler.ReportResult(object? parsedData, ExecutionResult result)
    {
        ReportResult(result);
    }

    /// <summary>
    /// Adapts execution to the simplified derived execution contract.
    /// </summary>
    /// <param name="parsedData">Unused parsed state.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    /// <pre>This handler shape does not require parsed state during execution.</pre>
    /// <post>The returned task matches the derived execution task.</post>
    UniTask IIncomingMessageHandler.ExecuteAsync(object? parsedData)
    {
        return ExecuteAsync();
    }
}

[PublicAPI]
/// <summary>
/// Base implementation for incoming message handlers that carry typed parsed state between phases.
/// </summary>
/// <typeparam name="T">The typed parsed state passed from validation into reporting and execution.</typeparam>
/// <pre>Derived types validate commands into a typed payload that is safe to cast back from the untyped handler interface.</pre>
/// <post>The explicit interface implementation bridges the untyped handler contract to the typed derived API.</post>
public abstract class IncomingMessageHandler<T> : IIncomingMessageHandler
{
    public abstract bool CanHandle(string command);

    protected abstract ExecutionResult Validate(string command, MessageJData messageData, out T? parsedData);

    protected abstract void ReportResult(T? parsedData, ExecutionResult result);

    protected abstract UniTask ExecuteAsync(T? parsedData);

    /// <summary>
    /// Adapts typed validation to the untyped handler interface.
    /// </summary>
    /// <param name="command">The incoming command name.</param>
    /// <param name="messageData">The raw parsed payload wrapper.</param>
    /// <param name="parsedData">Receives the typed parsed state boxed as an object.</param>
    /// <returns>The validation result for the message.</returns>
    /// <pre>The derived handler produces parsed state of type <typeparamref name="T"/> when validation succeeds.</pre>
    /// <post><paramref name="parsedData"/> contains the typed parsed state boxed for the interface contract.</post>
    ExecutionResult IIncomingMessageHandler.Validate(string command, MessageJData messageData, out object? parsedData)
    {
        ExecutionResult result = Validate(command, messageData, out T? tParsedData);
        parsedData = tParsedData;
        return result;
    }

    /// <summary>
    /// Adapts typed result reporting to the untyped handler interface.
    /// </summary>
    /// <param name="parsedData">The boxed parsed state.</param>
    /// <param name="result">The final execution result.</param>
    /// <pre><paramref name="parsedData"/> is either null or an instance of <typeparamref name="T"/> produced during validation.</pre>
    /// <post>The derived reporting method has been invoked with the typed parsed state and final result.</post>
    void IIncomingMessageHandler.ReportResult(object? parsedData, ExecutionResult result)
    {
        ReportResult((T?)parsedData, result);
    }

    /// <summary>
    /// Adapts typed execution to the untyped handler interface.
    /// </summary>
    /// <param name="parsedData">The boxed parsed state.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    /// <pre><paramref name="parsedData"/> is either null or an instance of <typeparamref name="T"/> produced during validation.</pre>
    /// <post>The returned task matches the derived execution task for the typed parsed state.</post>
    UniTask IIncomingMessageHandler.ExecuteAsync(object? parsedData)
    {
        return ExecuteAsync((T?)parsedData);
    }
}