#nullable enable

using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using System;

namespace NeuroSdk.Actions;

[PublicAPI]
public abstract class BaseNeuroAction : INeuroAction
{
    /// <summary>
    /// Current action window that this action is a part of.
    /// </summary>
    public ActionWindow? ActionWindow { get; private set; }

    protected BaseNeuroAction()
    {
        ActionWindow = null;
    }

    [Obsolete("Setting the action window is now handled by the Neuro SDK. Please use the parameterless constructor instead.")]
    protected BaseNeuroAction(ActionWindow? actionWindow)
    {
        ActionWindow = actionWindow;
    }

    public abstract string Name { get; }
    protected abstract string Description { get; }
    protected abstract JsonSchema? Schema { get; }

    public virtual bool CanAddToActionWindow(ActionWindow actionWindow)
    {
        return true;
    }

    ExecutionResult INeuroAction.Validate(ActionJData actionData, out object? parsedData)
    {
        ExecutionResult result = Validate(actionData, out parsedData);

        return ActionWindow != null ? ActionWindow.Result(result) : result;
    }

    UniTask INeuroAction.ExecuteAsync(object? data)
    {
        return ExecuteAsync(data);
    }

    public virtual WsAction GetWsAction()
    {
        return new WsAction(Name, Description, Schema);
    }

    protected abstract ExecutionResult Validate(ActionJData actionData, out object? parsedData);

    protected abstract UniTask ExecuteAsync(object? data);

    void INeuroAction.SetActionWindow(ActionWindow actionWindow)
    {
        if (ActionWindow != null)
        {
            if (ActionWindow != actionWindow)
            {
                Debug.LogError("Cannot set the action window for this action, it is already set.");
            }

            return;
        }

        ActionWindow = actionWindow;
    }
}