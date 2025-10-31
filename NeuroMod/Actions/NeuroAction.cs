#nullable enable

using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NeuroSdk.Websocket;
using System;

namespace NeuroSdk.Actions;

/// <summary>
/// Represents a NeuroAction with no parsed state
/// </summary>
[PublicAPI]
public abstract class NeuroAction : BaseNeuroAction
{
    protected NeuroAction()
    {
    }

    [Obsolete("Setting the action window is now handled by the Neuro SDK. Please use the parameterless constructor instead.")]
    protected NeuroAction(ActionWindow? actionWindow) : base(actionWindow)
    {
    }

    protected abstract ExecutionResult Validate(ActionJData actionData);

    protected abstract UniTask ExecuteAsync();

    protected sealed override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        ExecutionResult result = Validate(actionData);
        parsedData = null;
        return result;
    }

    protected sealed override UniTask ExecuteAsync(object? data)
    {
        return ExecuteAsync();
    }
}

/// <summary>
/// Represents a NeuroAction with a parsed state
/// </summary>
/// <typeparam name="TData">The type of the state parameter passed between <see cref="Validate(NeuroSdk.Actions.ActionJData,out TData?)"/> and <see cref="ExecuteAsync(TData?)"/></typeparam>
[PublicAPI]
public abstract class NeuroAction<TData> : BaseNeuroAction
{
    protected NeuroAction()
    {
    }

    [Obsolete("This way of setting the action window is obsolete. Please use the parameterless constructor instead.")]
    protected NeuroAction(ActionWindow? actionWindow) : base(actionWindow)
    {
    }

    protected abstract ExecutionResult Validate(ActionJData actionData, out TData? parsedData);

    protected abstract UniTask ExecuteAsync(TData? parsedData);

    protected sealed override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        ExecutionResult result = Validate(actionData, out TData? tParsedData);
        parsedData = tParsedData;
        return result;
    }

    protected sealed override UniTask ExecuteAsync(object? parsedData)
    {
        return ExecuteAsync((TData?)parsedData);
    }
}

/// <summary>
/// Represents a NeuroAction with a parsed state that is a value type.
/// Use this instead of <see cref="NeuroAction{TData}"/> when using primite types or structs to ensure proper nullability.
/// </summary>
/// <typeparam name="TData">The type of the state parameter passed between <see cref="NeuroAction{TData}.Validate(NeuroSdk.Actions.ActionJData,out TData?)"/> and <see cref="NeuroAction{TData}.ExecuteAsync(TData?)"/></typeparam>
[PublicAPI]
public abstract class NeuroActionS<TData> : NeuroAction<TData?> where TData : struct
{
    protected NeuroActionS()
    {
    }

    [Obsolete("Setting the action window is now handled by the Neuro SDK. Please use the parameterless constructor instead.")]
    protected NeuroActionS(ActionWindow? actionWindow) : base(actionWindow)
    {
    }
}