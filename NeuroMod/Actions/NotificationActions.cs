#nullable enable
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace NeuroMod;

/// <summary>
/// Returns the list of active game notifications / alerts currently displayed in the game UI.
/// Covers things like disease outbreaks, low food, blocked errands, duplicant injuries, etc.
/// </summary>
/// <pre>A colony must be loaded.</pre>
/// <post>Returns a snapshot of live notifications without mutating game state.</post>
public class GetNotificationsAction : BaseNeuroAction
{
    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "get_notifications";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "Returns all active game notifications and alerts (e.g. disease, low O2, duplicant injuries, blocked tasks). " +
        "Use this to react to emergencies or to understand what problems the colony is currently facing.";

    /// <summary>Gets the JSON schema (optional format parameter).</summary>
    protected override JsonSchema? Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["format"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object> { "text", "json" }
            }
        }
    };

    /// <summary>
    /// Reads all active notifications from the game's <see cref="global::NotificationManager"/> via reflection.
    /// </summary>
    /// <param name="actionData">Incoming JSON payload.</param>
    /// <param name="parsedData">Always null; result embedded in <see cref="ExecutionResult"/>.</param>
    /// <returns>Success with notification list, or a message stating no active alerts.</returns>
    /// <pre>A valid game world is loaded.</pre>
    /// <post>Game state is unchanged.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;
        try
        {
            string format = actionData.Data?["format"]?.Value<string>() ?? "text";
            List<NotificationEntry> entries = CollectNotifications();

            if (entries.Count == 0)
                return ExecutionResult.Success("No active notifications. The colony appears to be running smoothly.");

            string result = format == "json"
                ? BuildJson(entries)
                : BuildText(entries);

            NeuroLogger.Log($"[GetNotificationsAction] Found {entries.Count} active notifications", "GetNotificationsAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetNotificationsAction] Error: {ex.Message}", "GetNotificationsAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error retrieving notifications: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(object? data) => UniTask.CompletedTask;

    // ── Data types ────────────────────────────────────────────────────────────

    private sealed class NotificationEntry
    {
        public string  Title    { get; }
        public string  Severity { get; }
        public string? Context  { get; }
        public string? Tooltip  { get; }

        public NotificationEntry(string title, string severity, string? context, string? tooltip)
        {
            Title = title; Severity = severity; Context = context; Tooltip = tooltip;
        }
    }

    // ── Collection ────────────────────────────────────────────────────────────

    // Cached FieldInfo for the private 'notifications' list on the game's NotificationManager.
    private static readonly FieldInfo? _notificationsField =
        typeof(global::NotificationManager).GetField("notifications", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? _pendingField =
        typeof(global::NotificationManager).GetField("pendingNotifications", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// Reads the live notification lists from <see cref="NotificationManager"/> via reflection
    /// (both confirmed and pending notifications) since no public enumeration API exists.
    /// </summary>
    private static List<NotificationEntry> CollectNotifications()
    {
        var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<NotificationEntry>();

        global::NotificationManager? nm = global::NotificationManager.Instance;
        if (nm == null) return entries;

        ReadList(_notificationsField, nm, seen, entries);
        ReadList(_pendingField,       nm, seen, entries);

        // Most severe first
        entries.Sort((a, b) => SeverityRank(b.Severity).CompareTo(SeverityRank(a.Severity)));
        return entries;
    }

    private static void ReadList(FieldInfo? field, global::NotificationManager nm,
        HashSet<string> seen, List<NotificationEntry> entries)
    {
        if (field?.GetValue(nm) is not List<global::Notification> list) return;

        foreach (global::Notification n in list)
        {
            if (n == null) continue;

            string title    = StripRichText(n.titleText ?? "Unknown");
            string severity = ClassifySeverity(n.Type);

            // Build tooltip text if a tooltip function is registered.
            string? tooltip = null;
            try
            {
                if (n.ToolTip != null)
                    tooltip = StripRichText(n.ToolTip(list, n.tooltipData));
            }
            catch { }

            // Notifier name carries the context (e.g. "• Neuro", "• Food Storage").
            // Drop internal Unity/game type names that are meaningless to the AI.
            string? rawContext = string.IsNullOrWhiteSpace(n.NotifierName)
                ? null
                : n.NotifierName.TrimStart('•', ' ');
            string? context = IsInternalTypeName(rawContext) ? null : rawContext;

            string key = $"{title}|{context}";
            if (!seen.Add(key)) continue;

            entries.Add(new NotificationEntry(title, severity, context, tooltip));
        }
    }

    // Names that come from internal Unity/game objects and carry no useful meaning for the AI.
    private static readonly HashSet<string> _internalTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "NotificationScreen", "SaveGame", "GameFlowManager", "ClusterManager",
        "WorldContainer", "ClusterGrid",
    };

    private static bool IsInternalTypeName(string? name)
        => name != null && _internalTypeNames.Contains(name);

    // Strips ONI rich-text tags: <link="X">text</link>, <color=#fff>text</color>, <b>, <i>, etc.
    // Also collapses bullet lists (lines starting with •) into a tidy comma-separated list,
    // and removes repeated blank lines.
    private static string StripRichText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Remove XML/HTML-style tags
        string clean = System.Text.RegularExpressions.Regex
            .Replace(input!, @"<[^>]+>", string.Empty);

        // Collapse multiple blank lines and tidy whitespace
        clean = System.Text.RegularExpressions.Regex
            .Replace(clean, @"\r?\n(\s*\r?\n)+", "\n")
            .Trim();

        return clean;
    }

    private static string ClassifySeverity(global::NotificationType type) => type switch
    {
        global::NotificationType.DuplicantThreatening => "critical",
        global::NotificationType.Bad                  => "warning",
        global::NotificationType.BadMinor             => "warning",
        global::NotificationType.Good                 => "good",
        global::NotificationType.MessageImportant     => "info",
        global::NotificationType.Messages             => "info",
        global::NotificationType.Event                => "info",
        global::NotificationType.Tutorial             => "info",
        _                                             => "info",
    };

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 3,
        "warning"  => 2,
        "good"     => 1,
        _          => 0,
    };

    // ── Formatters ────────────────────────────────────────────────────────────

    private static string BuildText(List<NotificationEntry> entries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Active Notifications ({entries.Count}):");
        foreach (NotificationEntry e in entries)
        {
            string ctx = e.Context != null ? $" [{e.Context}]" : string.Empty;
            sb.AppendLine($"  [{e.Severity.ToUpper()}] {e.Title}{ctx}");
            if (!string.IsNullOrWhiteSpace(e.Tooltip))
                sb.AppendLine($"    {e.Tooltip!.Trim()}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildJson(List<NotificationEntry> entries)
    {
        var arr = new JArray();
        foreach (NotificationEntry e in entries)
        {
            var obj = new JObject { ["title"] = e.Title, ["severity"] = e.Severity };
            if (e.Context != null)
                obj["context"] = e.Context;
            if (!string.IsNullOrWhiteSpace(e.Tooltip))
                obj["details"] = e.Tooltip!.Trim();
            arr.Add(obj);
        }
        return arr.ToString();
    }
}

/// <summary>
/// Posts a text notice into the game's notification system.
/// When <c>is_alarm</c> is <see langword="true"/> the notice is raised as a critical alarm
/// (red banner, same priority as a duplicant threat); otherwise it appears as an informational message.
/// </summary>
/// <pre>A colony must be loaded and <see cref="global::NotificationManager"/> must be available.</pre>
/// <post>The notice is visible in the game UI immediately after execution.</post>
public class SetNotificationAction : BaseNeuroAction
{
    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "set_notification";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "Posts a notification or alarm into the game's notification system. " +
        "Set is_alarm to true to raise a critical red alarm (e.g. emergency alert); " +
        "leave it false for a plain informational message.";

    /// <summary>Gets the JSON schema for this action.</summary>
    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Required = new List<string> { "message" },
        Properties = new Dictionary<string, JsonSchema>
        {
            ["message"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
            },
            ["is_alarm"] = new JsonSchema
            {
                Type = JsonSchemaType.Boolean,
            },
        },
    };

    // Carries the validated message and alarm flag between Validate and ExecuteAsync.
    private sealed class NoticeRequest(string message, bool isAlarm)
    {
        public readonly string Message = message;
        public readonly bool IsAlarm = isAlarm;
    }

    /// <summary>
    /// Validates the payload and returns the parsed request; posting happens in <see cref="ExecuteAsync"/>.
    /// </summary>
    /// <param name="actionData">Incoming JSON payload containing <c>message</c> and optional <c>is_alarm</c>.</param>
    /// <param name="parsedData">Receives the validated <see cref="NoticeRequest"/> on success.</param>
    /// <returns>Success with a confirmation string, or failure when the message is missing.</returns>
    /// <pre>A valid game world is loaded.</pre>
    /// <post>Parsed data is ready for execution; no game state has been mutated yet.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;

        string? message = actionData.Data?["message"]?.Value<string>();
        if (string.IsNullOrWhiteSpace(message))
            return ExecutionResult.Failure("Missing required parameter: message");

        bool isAlarm = actionData.Data?["is_alarm"]?.Value<bool>() ?? false;
        string kind = isAlarm ? "alarm" : "notice";

        parsedData = new NoticeRequest(message!.Trim(), isAlarm);
        return ExecutionResult.Success($"Posting {kind}: {message!.Trim()}");
    }

    /// <summary>
    /// Posts the notification into the game UI via a scene <see cref="Notifier"/> component.
    /// </summary>
    /// <param name="data">The <see cref="NoticeRequest"/> produced during validation.</param>
    /// <returns>A completed task.</returns>
    /// <pre>A <see cref="Notifier"/> component is present in the scene.</pre>
    /// <post>A notification entry is visible in the game UI.</post>
    protected override UniTask ExecuteAsync(object? data)
    {
        if (data is not NoticeRequest req) return UniTask.CompletedTask;

        try
        {
            Notifier? notifier = UnityEngine.Object.FindObjectOfType<Notifier>();
            if (notifier == null)
            {
                NeuroLogger.LogWarning("[SetNotificationAction] No Notifier found in scene – notification not posted.", "SetNotificationAction", ActionWindow?.TraceId);
                return UniTask.CompletedTask;
            }

            global::NotificationType notifType = req.IsAlarm
                ? global::NotificationType.DuplicantThreatening
                : global::NotificationType.Messages;

            var notification = new global::Notification(
                req.Message,
                notifType,
                tooltip: (items, _) => req.Message,
                expires: true);

            notifier.Add(notification);

            string kind = req.IsAlarm ? "alarm" : "notice";
            NeuroLogger.Log($"[SetNotificationAction] Posted {kind}: {req.Message}", "SetNotificationAction", ActionWindow?.TraceId);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[SetNotificationAction] Error posting notification: {ex.Message}", "SetNotificationAction", ActionWindow?.TraceId);
        }

        return UniTask.CompletedTask;
    }
}
