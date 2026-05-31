#nullable enable

using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NeuroSdk.Websocket;
using System;

namespace NeuroSdk.Actions;

/// <summary>
/// Represents a Neuro action with no parsed state.
/// </summary>
/// <pre>Derived actions do not require a parsed payload between validation and execution.</pre>
/// <post>The action exposes a simplified validation and execution model without parsed state transfer.</post>
[PublicAPI]
public abstract class NeuroAction : BaseNeuroAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NeuroAction"/> class.
    /// </summary>
    /// <pre>The action is created without an owning action window.</pre>
    /// <post>The action is ready for SDK-managed association and execution.</post>
    protected NeuroAction()
    {
    }

    [Obsolete("Setting the action window is now handled by the Neuro SDK. Please use the parameterless constructor instead.")]
    /// <summary>
    /// Initializes a new instance of the <see cref="NeuroAction"/> class with an initial action window.
    /// </summary>
    /// <param name="actionWindow">The initial owning action window.</param>
    /// <pre>This overload is retained for backward compatibility.</pre>
    /// <post>The action starts with the supplied action window association.</post>
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
/// Represents a Neuro action with a parsed payload.
/// </summary>
/// <typeparam name="TData">The type of the state parameter passed between <see cref="Validate(NeuroSdk.Actions.ActionJData,out TData?)"/> and <see cref="ExecuteAsync(TData?)"/></typeparam>
/// <pre>Derived actions parse incoming payloads into <typeparamref name="TData"/> before execution.</pre>
/// <post>The parsed payload is carried from validation to execution through the base implementation.</post>
[PublicAPI]
public abstract class NeuroAction<TData> : BaseNeuroAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NeuroAction{TData}"/> class.
    /// </summary>
    /// <pre>The action is created without an owning action window.</pre>
    /// <post>The action is ready for payload parsing and SDK-managed association.</post>
    protected NeuroAction()
    {
    }

    [Obsolete("This way of setting the action window is obsolete. Please use the parameterless constructor instead.")]
    /// <summary>
    /// Initializes a new instance of the <see cref="NeuroAction{TData}"/> class with an initial action window.
    /// </summary>
    /// <param name="actionWindow">The initial owning action window.</param>
    /// <pre>This overload is retained for backward compatibility.</pre>
    /// <post>The action starts with the supplied action window association.</post>
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
/// <pre>Derived actions use nullable value-type payloads to preserve absence semantics safely.</pre>
/// <post>The action inherits the generic parsed-payload flow while preserving value-type nullability.</post>
[PublicAPI]
public abstract class NeuroActionS<TData> : NeuroAction<TData?> where TData : struct
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NeuroActionS{TData}"/> class.
    /// </summary>
    /// <pre>The action is created without an owning action window.</pre>
    /// <post>The action is ready for SDK-managed association using nullable value-type payloads.</post>
    protected NeuroActionS()
    {
    }

    [Obsolete("Setting the action window is now handled by the Neuro SDK. Please use the parameterless constructor instead.")]
    /// <summary>
    /// Initializes a new instance of the <see cref="NeuroActionS{TData}"/> class with an initial action window.
    /// </summary>
    /// <param name="actionWindow">The initial owning action window.</param>
    /// <pre>This overload is retained for backward compatibility.</pre>
    /// <post>The action starts with the supplied action window association.</post>
    protected NeuroActionS(ActionWindow? actionWindow) : base(actionWindow)
    {
    }
}