#nullable enable
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using System;
using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Posts a custom message to the game's message log (the bottom-left notification feed).
/// Lets Neuro leave visible notes inside the colony, react to events, or address the colony.
/// </summary>
/// <pre>A colony must be loaded and <see cref="MessageCenter"/> must be available.</pre>
/// <post>A message is posted to the in-game feed; no game simulation state is mutated.</post>
public class TriggerAnnouncementAction : NeuroAction<TriggerAnnouncementAction.AnnouncementRequest>
{
    /// <summary>
    /// Carries the announcement text and optional sender name.
    /// </summary>
    public class AnnouncementRequest
    {
        /// <summary>Gets or sets the message text to display.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Gets or sets an optional sender label shown before the message.</summary>
        public string? Sender { get; set; }
    }

    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "trigger_announcement";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "Posts a custom message to the colony's in-game message log. " +
        "Use this to leave notes for the colony, react to game events in-character, " +
        "or address the duplicants directly. " +
        "Provide 'message' (required) and optionally 'sender' (defaults to 'Neuro').";

    /// <summary>Gets the JSON schema for the announcement request.</summary>
    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Required = new List<string> { "message" },
        Properties = new Dictionary<string, JsonSchema>
        {
            ["message"] = new JsonSchema { Type = JsonSchemaType.String },
            ["sender"]  = new JsonSchema { Type = JsonSchemaType.String },
        }
    };

    /// <summary>
    /// Validates the message text, formats it, and posts it immediately via <see cref="KCrashReporter"/>.
    /// </summary>
    /// <param name="actionData">Incoming JSON payload.</param>
    /// <param name="parsedData">The parsed <see cref="AnnouncementRequest"/> on success.</param>
    /// <returns>Success confirming the message was posted, or failure when the message is missing.</returns>
    /// <pre>A valid game world is loaded.</pre>
    /// <post>A notification is visible in the game's message feed.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out AnnouncementRequest? parsedData)
    {
        parsedData = null;
        try
        {
            string? message = actionData.Data?["message"]?.ToObject<string>();
            if (string.IsNullOrWhiteSpace(message))
                return ExecutionResult.Failure("'message' is required and cannot be empty.");

            if (message!.Length > 300)
                return ExecutionResult.Failure("Message must be 300 characters or fewer.");

            string sender = actionData.Data?["sender"]?.ToObject<string>() ?? "Neuro";

            parsedData = new AnnouncementRequest { Message = message, Sender = sender };
            return ExecutionResult.Success($"Announcement queued: [{sender}] {message}");
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[TriggerAnnouncementAction] Validate error: {ex.Message}", "TriggerAnnouncementAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error validating announcement: {ex.Message}");
        }
    }

    /// <summary>
    /// Posts the announcement to the in-game message center.
    /// </summary>
    /// <param name="parsedData">The validated announcement request.</param>
    /// <returns>A completed task.</returns>
    /// <pre><see cref="MessageCenter"/> is available in the scene.</pre>
    /// <post>The message is visible in the colony's message feed.</post>
    protected override UniTask ExecuteAsync(AnnouncementRequest? parsedData)
    {
        if (parsedData == null) return UniTask.CompletedTask;
        try
        {
            string full = $"[{parsedData.Sender}] {parsedData.Message}";

            // Post a temporary in-game notification via a Notifier on any game object
            Notifier? notifier = UnityEngine.Object.FindObjectOfType<Notifier>();
            if (notifier != null)
            {
                global::Notification n = new global::Notification(
                    full,
                    (global::NotificationType)4, // Neutral
                    tooltip: (items, _) => full,
                    expires: true);
                notifier.Add(n);
            }

            NeuroLogger.Log($"[TriggerAnnouncementAction] Posted: {full}", "TriggerAnnouncementAction", ActionWindow?.TraceId);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[TriggerAnnouncementAction] Execute error: {ex.Message}", "TriggerAnnouncementAction", ActionWindow?.TraceId);
        }
        return UniTask.CompletedTask;
    }
}
