using System;
using System.Collections.Generic;
using System.Linq;
using Jennifer.Wpf.Automation;
using Jennifer.Wpf.Contracts;
using Jennifer.Wpf.Parsing;

namespace Jennifer.Wpf.Session;

/// <summary>
/// Provides testable Jennifer session logic that is independent of WPF controls.
/// </summary>
/// <post>The returned values can be applied by the WPF shell without duplicating decision logic in event handlers.</post>
public static class JenniferSessionCoordinator
{
    /// <summary>
    /// Interprets an incoming message and produces the Jennifer-side action or log result.
    /// </summary>
    /// <param name="rawMessage">The raw websocket or TCP message.</param>
    /// <param name="receivedAt">The receive timestamp.</param>
    /// <returns>The parsed Jennifer message result.</returns>
    /// <post>Action messages return a normalized incoming action with fallback id and name values.</post>
    public static JenniferIncomingMessageResult ProcessIncomingMessage(string rawMessage, DateTimeOffset receivedAt)
    {
        JenniferWsMessage message = JenniferWsMessageParser.Parse(rawMessage);
        return message.Kind switch
        {
            JenniferWsMessageKind.ReRegisterAll => new JenniferIncomingMessageResult
            {
                Kind = message.Kind,
                LogMessage = "[WS] Received actions/reregister_all.",
            },
            JenniferWsMessageKind.ActionsRegister => new JenniferIncomingMessageResult
            {
                Kind = message.Kind,
                GameRegisteredActionNames = message.RegisteredActionNames,
                GameName = message.GameName,
                LogMessage = $"[WS] Game registered {message.RegisteredActionNames.Count} action(s): [{string.Join(", ", message.RegisteredActionNames)}].",
            },
            JenniferWsMessageKind.ActionsUnregister => new JenniferIncomingMessageResult
            {
                Kind = message.Kind,
                GameUnregisteredActionNames = message.UnregisteredActionNames,
                LogMessage = $"[WS] Game unregistered {message.UnregisteredActionNames.Count} action(s).",
            },
            JenniferWsMessageKind.ActionsForce => new JenniferIncomingMessageResult
            {
                Kind = message.Kind,
                ForceSelectedActionName = message.ForceActionNames.Count > 0 ? message.ForceActionNames[0] : null,
                ForceCandidateNames = message.ForceActionNames,
                LogMessage = message.ForceActionNames.Count > 0
                    ? $"[WS] actions/force — candidates: [{string.Join(", ", message.ForceActionNames)}]."
                    : "[WS] actions/force received but contained no action names.",
            },
            JenniferWsMessageKind.Action => new JenniferIncomingMessageResult
            {
                Kind = message.Kind,
                IncomingAction = new JenniferIncomingAction
                {
                    Id = string.IsNullOrWhiteSpace(message.ActionId) ? Guid.NewGuid().ToString("N") : message.ActionId!,
                    Name = string.IsNullOrWhiteSpace(message.ActionName) ? "unknown" : message.ActionName!,
                    Data = message.ActionData,
                    Raw = rawMessage,
                    ReceivedAt = receivedAt,
                },
                LogMessage = $"[WS] Incoming action '{(string.IsNullOrWhiteSpace(message.ActionName) ? "unknown" : message.ActionName)}' ({(string.IsNullOrWhiteSpace(message.ActionId) ? "generated" : message.ActionId)}).",
            },
            JenniferWsMessageKind.Generic => new JenniferIncomingMessageResult
            {
                Kind = message.Kind,
                LogMessage = $"[WS] {rawMessage}",
            },
            JenniferWsMessageKind.ActionResult => new JenniferIncomingMessageResult
            {
                Kind = message.Kind,
                ActionResultId = message.ActionResultId,
                ActionResultSuccess = message.ActionResultSuccess,
                ActionResultMessage = message.ActionResultMessage,
                LogMessage = message.ActionResultSuccess
                    ? $"[Result] Action '{message.ActionResultId}' — success."
                    : $"[Result] Action '{message.ActionResultId}' — failure: {message.ActionResultMessage ?? "(no message)"}",
            },
            _ => new JenniferIncomingMessageResult
            {
                Kind = message.Kind,
                LogMessage = $"[Raw] {rawMessage}",
            },
        };
    }

    /// <summary>
    /// Inserts or replaces a pending action while keeping the newest item first.
    /// </summary>
    /// <param name="existingActions">The current pending actions.</param>
    /// <param name="incomingAction">The incoming action to add.</param>
    /// <returns>The updated pending action order.</returns>
    /// <post>The returned list contains the incoming action once at the front of the list.</post>
    public static IReadOnlyList<JenniferIncomingAction> UpsertPendingAction(IEnumerable<JenniferIncomingAction> existingActions, JenniferIncomingAction incomingAction)
    {
        ArgumentNullException.ThrowIfNull(existingActions);
        ArgumentNullException.ThrowIfNull(incomingAction);

        List<JenniferIncomingAction> merged = [incomingAction];
        merged.AddRange(existingActions.Where(action => !string.Equals(action.Id, incomingAction.Id, StringComparison.OrdinalIgnoreCase)));
        return merged;
    }

    /// <summary>
    /// Removes a pending action by id.
    /// </summary>
    /// <param name="existingActions">The current pending actions.</param>
    /// <param name="incomingActionId">The id to remove.</param>
    /// <returns>The updated pending action order.</returns>
    /// <post>The returned list omits the action with the supplied id.</post>
    public static IReadOnlyList<JenniferIncomingAction> RemovePendingAction(IEnumerable<JenniferIncomingAction> existingActions, string incomingActionId)
    {
        ArgumentNullException.ThrowIfNull(existingActions);

        return existingActions
            .Where(action => !string.Equals(action.Id, incomingActionId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Finds the automatic reply Jennifer should send for a matching incoming action.
    /// </summary>
    /// <param name="plan">The loaded automation plan.</param>
    /// <param name="autoReplyEnabled">Whether auto-reply is enabled.</param>
    /// <param name="incomingAction">The incoming action to match.</param>
    /// <returns>The automation reply, or <c>null</c> when no reply should be sent.</returns>
    /// <post>A non-null reply represents the exact success and message values Jennifer should send.</post>
    public static JenniferAutomationReply? FindAutomationReply(JenniferAutomationPlan? plan, bool autoReplyEnabled, JenniferIncomingAction incomingAction)
    {
        ArgumentNullException.ThrowIfNull(incomingAction);

        if (!autoReplyEnabled || plan is null)
        {
            return null;
        }

        JenniferAutomationStep? matchingStep = plan.Steps.FirstOrDefault(
            step => string.Equals(step.ActionName, incomingAction.Name, StringComparison.OrdinalIgnoreCase));

        return matchingStep is null
            ? null
            : new JenniferAutomationReply
            {
                ActionName = incomingAction.Name,
                ResultSuccess = matchingStep.ResultSuccess,
                ResultMessage = matchingStep.ResultMessage,
            };
    }

    /// <summary>
    /// Builds the Jennifer transport plan for a force-action request.
    /// </summary>
    /// <param name="isWebSocketConnected">Whether Jennifer currently has an open websocket.</param>
    /// <param name="gameName">The optional game name. May be null or empty when not configured.</param>
    /// <param name="actionNames">The requested action names.</param>
    /// <param name="state">The optional state payload.</param>
    /// <param name="query">The optional force query.</param>
    /// <param name="priority">The requested priority.</param>
    /// <param name="ephemeral">Whether the request is ephemeral.</param>
    /// <returns>The transport plan Jennifer should execute.</returns>
    /// <post>The returned plan contains either a websocket payload, a compatibility message, or a rejection reason.</post>
    public static JenniferForceRequestPlan BuildForceRequestPlan(bool isWebSocketConnected, string? gameName, IEnumerable<string> actionNames, string state, string query, string priority, bool ephemeral)
    {
        ArgumentNullException.ThrowIfNull(actionNames);

        string[] normalizedActions = actionNames
            .Select(NormalizeText)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedActions.Length == 0)
        {
            return new JenniferForceRequestPlan
            {
                Mode = JenniferForceRequestMode.None,
                ActionNames = normalizedActions,
                Priority = NormalizeText(priority),
                LogMessage = "[Force] No valid action names were provided.",
            };
        }

        if (isWebSocketConnected)
        {
            return new JenniferForceRequestPlan
            {
                Mode = JenniferForceRequestMode.WebSocket,
                ActionNames = normalizedActions,
                Priority = NormalizeText(priority),
                WebSocketPayload = JenniferRandyContractPayloadFactory.CreateActionsForcePayload(
                    NormalizeText(gameName),
                    normalizedActions,
                    NormalizeText(state),
                    NormalizeText(query),
                    NormalizeText(priority),
                    ephemeral),
            };
        }

        if (normalizedActions.Length > 1)
        {
            return new JenniferForceRequestPlan
            {
                Mode = JenniferForceRequestMode.None,
                ActionNames = normalizedActions,
                Priority = NormalizeText(priority),
                LogMessage = "[Force] Connect to WebSocket before sending multiple actions at once.",
            };
        }

        return new JenniferForceRequestPlan
        {
            Mode = JenniferForceRequestMode.CompatibilityTcp,
            ActionNames = normalizedActions,
            Priority = NormalizeText(priority),
            CompatibilityMessage = normalizedActions[0],
        };
    }

    private static string NormalizeText(string? text)
    {
        return text?.Trim() ?? string.Empty;
    }
}