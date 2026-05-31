using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace NeuroMod;

/// <summary>
/// Action to get the current schedule information for the Neuro duplicate.
/// </summary>
/// <remarks>
/// Returns the current activity block, schedule name and hour-of-day. Use the
/// `include_details` flag to request additional information where supported.
/// </remarks>
/// <pre>
/// The connected duplicant must still exist and expose schedule data when validation runs.
/// </pre>
/// <post>
/// Validation confirms the schedule can be queried, and execution emits a human-readable schedule summary.
/// </post>
public class GetNeuroScheduleAction(MinionIdentity minion) : NeuroAction<GetNeuroScheduleAction.ScheduleQuery>
{
    private readonly MinionIdentity neuroMinion = minion;

    /// <summary>
    /// Describes optional flags that control how much schedule information is returned.
    /// </summary>
    /// <pre>The request payload has been parsed for the get-schedule action.</pre>
    /// <post>The instance stores the optional flags used to shape the response.</post>
    public class ScheduleQuery
    {
        /// <summary>
        /// Gets or sets a value indicating whether additional schedule details should be included.
        /// </summary>
        /// <pre>The query object represents a parsed get-schedule request.</pre>
        /// <post>The property stores whether the response should include additional schedule details.</post>
        public bool IncludeDetails { get; set; } = false;
    }

    public override string Name => "get_schedule";

    protected override string Description =>
        "Get current schedule information for Neuro duplicate including current activity and time blocks.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["include_details"] = new JsonSchema
            {
                Type = JsonSchemaType.Boolean
            }
        }
    };

    protected override ExecutionResult Validate(ActionJData actionData, out ScheduleQuery? parsedData)
    {
        parsedData = new ScheduleQuery
        {
            IncludeDetails = actionData.Data?["include_details"]?.Value<bool>() ?? false
        };

        if (neuroMinion == null || neuroMinion.gameObject == null)
        {
            return ExecutionResult.Failure("Neuro duplicate not found");
        }

        try
        {
            string duplicateName = neuroMinion.GetProperName();
            NeuroLogger.Log($"Getting schedule for {duplicateName}", "GetNeuroScheduleAction", ActionWindow?.TraceId);

            Schedulable? schedulable = neuroMinion.GetComponent<Schedulable>();
            if (schedulable == null)
            {
                string errorMessage = $"Cannot get schedule - {duplicateName} has no Schedulable component";
                NeuroLogger.LogError(errorMessage, "GetNeuroScheduleAction", ActionWindow?.TraceId);
                return ExecutionResult.Failure(errorMessage);
            }

            if (GameClock.Instance == null)
            {
                const string errorMessage = "Cannot get schedule - GameClock not available";
                NeuroLogger.LogError(errorMessage, "GetNeuroScheduleAction", ActionWindow?.TraceId);
                return ExecutionResult.Failure(errorMessage);
            }

            float cycleTime = GameClock.Instance.GetTimeSinceStartOfCycle();
            int currentHour = UnityEngine.Mathf.FloorToInt(cycleTime / 600f);

            NeuroLogger.Log($"Current cycle time: {cycleTime}, Hour: {currentHour}", "GetNeuroScheduleAction", ActionWindow?.TraceId);

            string currentActivity = "Unknown";
            string scheduleName = "Unknown";

            Schedule currentSchedule = schedulable.GetSchedule();
            if (currentSchedule == null)
            {
                NeuroLogger.LogWarning("schedulable.GetSchedule() returned NULL", "GetNeuroScheduleAction", ActionWindow?.TraceId);
            }
            else
            {
                scheduleName = currentSchedule.name ?? "Unnamed Schedule";
                NeuroLogger.Log($"Schedule name: {scheduleName}", "GetNeuroScheduleAction", ActionWindow?.TraceId);

                ScheduleBlock currentBlock = currentSchedule.GetCurrentScheduleBlock();
                if (currentBlock == null)
                {
                    NeuroLogger.LogWarning("GetCurrentScheduleBlock() returned NULL", "GetNeuroScheduleAction", ActionWindow?.TraceId);
                }
                else
                {
                    currentActivity = currentBlock.name ?? "Unnamed Activity";
                    NeuroLogger.Log($"Current block: {currentActivity}", "GetNeuroScheduleAction", ActionWindow?.TraceId);
                }
            }

            if (NeuroScheduleManager.Instance == null)
            {
                NeuroLogger.LogWarning("NeuroScheduleManager.Instance is NULL", "GetNeuroScheduleAction", ActionWindow?.TraceId);
            }
            else
            {
                Schedule? neuroSchedule = NeuroScheduleManager.Instance.GetNeuroSchedule();
                if (neuroSchedule == null)
                {
                    NeuroLogger.LogWarning("GetNeuroSchedule() returned NULL", "GetNeuroScheduleAction", ActionWindow?.TraceId);
                }
                else if (neuroSchedule.name == scheduleName)
                {
                    scheduleName = $"{scheduleName} (Dedicated)";
                    NeuroLogger.Log("Using dedicated Neuro schedule", "GetNeuroScheduleAction", ActionWindow?.TraceId);
                }
            }

            // Build per-hour breakdown — one entry per hour (0-23).
            // Each hour covers 600 seconds of cycle time; the schedule block active at the
            // mid-point of each hour window is used to name that hour.
            var hourLines = new System.Text.StringBuilder();
            if (currentSchedule != null)
            {
                List<ScheduleBlock> blocks = currentSchedule.GetBlocks();
                if (blocks != null && blocks.Count > 0)
                {
                    // Each ScheduleBlock represents exactly one hour slot (600 s).
                    string[] hourMap = new string[24];
                    for (int slot = 0; slot < 24; slot++)
                        hourMap[slot] = slot < blocks.Count ? (blocks[slot].name ?? "Unknown") : "Unknown";

                    hourLines.AppendLine("Hourly breakdown:");
                    for (int h = 0; h < 24; h++)
                    {
                        string marker = h == currentHour ? " ← now" : string.Empty;
                        hourLines.AppendLine($"  Hour {h,2}: {hourMap[h]}{marker}");
                    }
                }
            }

            string contextMessage = string.Concat(
                $"Neuro Schedule: '{scheduleName}' | Hour: {currentHour}/24 | Current Activity: {currentActivity}\n",
                hourLines.ToString().TrimEnd());

            NeuroLogger.Log($"Success: schedule={scheduleName} hour={currentHour}", "GetNeuroScheduleAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(contextMessage);
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"Exception in GetNeuroScheduleAction: {ex.Message}", "GetNeuroScheduleAction", ActionWindow?.TraceId);
            NeuroLogger.LogError($"Stack trace: {ex.StackTrace}", "GetNeuroScheduleAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error getting schedule: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(ScheduleQuery? parsedData)
    {
        return UniTask.CompletedTask;
    }
}

/// <summary>
/// Action to assign a predefined schedule type to the Neuro duplicate.
/// </summary>
/// <remarks>
/// Validates the requested schedule exists and uses NeuroScheduleManager when available,
/// falling back to legacy assignment when necessary.
/// </remarks>
/// <pre>
/// The requested schedule type must be recognized and the duplicant must expose a <see cref="Schedulable"/> component.
/// </pre>
/// <post>
/// Successful execution assigns a schedule through the manager or fallback API and reports the resulting mode.
/// </post>
public class SetNeuroScheduleAction(MinionIdentity minion) : NeuroAction<SetNeuroScheduleAction.ScheduleData>
{
    private readonly MinionIdentity neuroMinion = minion;

    /// <summary>
    /// Carries the requested schedule type for <see cref="SetNeuroScheduleAction"/>.
    /// </summary>
    /// <pre>The request payload has been parsed for the set-schedule action.</pre>
    /// <post>The instance stores the requested schedule type together with runtime objects resolved during validation.</post>
    public class ScheduleData
    {
        /// <summary>
        /// Gets or sets the requested schedule type identifier.
        /// </summary>
        /// <pre>The data object represents a parsed set-schedule request.</pre>
        /// <post>The property stores the requested schedule type supplied by the caller.</post>
        public string ScheduleType { get; set; } = "";

        /// <summary>
        /// Gets or sets the resolved schedulable component for the target duplicant.
        /// </summary>
        /// <pre>Validation may resolve the target duplicant's schedulable component.</pre>
        /// <post>The property stores the resolved schedulable component when validation succeeds.</post>
        internal Schedulable? Schedulable { get; set; }

        /// <summary>
        /// Gets or sets the resolved schedule instance to apply.
        /// </summary>
        /// <pre>Validation may resolve the requested schedule template.</pre>
        /// <post>The property stores the resolved schedule when validation succeeds.</post>
        internal Schedule? ResolvedSchedule { get; set; }

        /// <summary>
        /// Gets or sets the resolved duplicate name used for result messages.
        /// </summary>
        /// <pre>Validation may resolve the current display name of the target duplicant.</pre>
        /// <post>The property stores the duplicate name used by subsequent result messages and logging.</post>
        internal string DuplicateName { get; set; } = "";

        /// <summary>
        /// Gets or sets a value indicating whether execution should use the fallback assignment path.
        /// </summary>
        /// <pre>Validation determines whether the Neuro schedule manager is available.</pre>
        /// <post>The property stores whether execution should use the fallback schedule-assignment path.</post>
        internal bool UseFallbackMode { get; set; }
    }

    public override string Name => "set_schedule";

    protected override string Description =>
        "Set a specific schedule type for Neuro duplicate (work focused, research focused, etc.).";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Required = new List<string>{ "schedule_type" },
        Properties = new Dictionary<string, JsonSchema>
        {
            ["schedule_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object>{ "default", "work_focused", "research_focused", "night_shift", "early_bird", "recreation_focused", "bathing_focused" }
            }
        }
    };

    protected override ExecutionResult Validate(ActionJData actionData, out ScheduleData? parsedData)
    {
        parsedData = new ScheduleData
        {
            ScheduleType = actionData.Data?["schedule_type"]?.Value<string>() ?? ""
        };

        if (string.IsNullOrEmpty(parsedData.ScheduleType))
            return ExecutionResult.Failure("Schedule type is required");

        if (neuroMinion == null || neuroMinion.gameObject == null)
        {
            return ExecutionResult.Failure("Neuro duplicate not found");
        }

        try
        {
            string duplicateName = neuroMinion.GetProperName();
            parsedData.DuplicateName = duplicateName;
            NeuroLogger.Log($"Setting schedule {parsedData.ScheduleType} for {duplicateName}", "SetNeuroScheduleAction", ActionWindow?.TraceId);

            Schedulable? schedulable = neuroMinion.GetComponent<Schedulable>();
            if (schedulable == null)
            {
                string errorMessage = $"No Schedulable component found on {duplicateName}. Cannot assign schedule.";
                NeuroLogger.LogError(errorMessage, "SetNeuroScheduleAction", ActionWindow?.TraceId);
                return ExecutionResult.Failure(errorMessage);
            }

            parsedData.Schedulable = schedulable;

            Schedule? targetSchedule = GetScheduleByType(parsedData.ScheduleType);
            if (targetSchedule == null)
            {
                string errorMessage = $"GetScheduleByType returned NULL for '{parsedData.ScheduleType}'. This schedule type may not be available.";
                NeuroLogger.LogError(errorMessage, "SetNeuroScheduleAction", ActionWindow?.TraceId);
                return ExecutionResult.Failure(errorMessage);
            }

            parsedData.ResolvedSchedule = targetSchedule;

            List<ScheduleBlock> blocks = targetSchedule.GetBlocks();
            if (blocks == null)
            {
                string errorMessage = $"Schedule '{parsedData.ScheduleType}' has NULL blocks. Cannot assign invalid schedule.";
                NeuroLogger.LogError(errorMessage, "SetNeuroScheduleAction", ActionWindow?.TraceId);
                return ExecutionResult.Failure(errorMessage);
            }

            if (blocks.Count == 0)
            {
                string errorMessage = $"Schedule '{parsedData.ScheduleType}' has ZERO blocks. Cannot assign invalid schedule.";
                NeuroLogger.LogError(errorMessage, "SetNeuroScheduleAction", ActionWindow?.TraceId);
                return ExecutionResult.Failure(errorMessage);
            }

            parsedData.UseFallbackMode = NeuroScheduleManager.Instance == null;

            string resultMessage = parsedData.UseFallbackMode
                ? $"Schedule Assignment: Set {duplicateName} to {parsedData.ScheduleType} schedule (fallback mode)."
                : $"Schedule Assignment: Set {duplicateName} to {parsedData.ScheduleType} schedule. Their daily routine has been updated.";

            NeuroLogger.Log($"Validated schedule assignment: {resultMessage}", "SetNeuroScheduleAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(resultMessage);
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"Exception in SetNeuroScheduleAction.Validate: {ex.Message}", "SetNeuroScheduleAction", ActionWindow?.TraceId);
            NeuroLogger.LogError($"Stack trace: {ex.StackTrace}", "SetNeuroScheduleAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Failed to set schedule for {neuroMinion.GetProperName()}: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(ScheduleData? parsedData)
    {
        if (parsedData?.Schedulable == null || parsedData.ResolvedSchedule == null)
        {
            NeuroLogger.LogError("ExecuteAsync called without resolved schedule data", "SetNeuroScheduleAction", ActionWindow?.TraceId);
            return UniTask.CompletedTask;
        }

        try
        {
            if (parsedData.UseFallbackMode)
            {
                NeuroLogger.LogError("NeuroScheduleManager.Instance is NULL, using fallback method", "SetNeuroScheduleAction", ActionWindow?.TraceId);
                ScheduleOverrideApi.SetCustomSchedule(parsedData.Schedulable, parsedData.ResolvedSchedule);
            }
            else
            {
                NeuroLogger.Log("Using NeuroScheduleManager.UpdateNeuroSchedule", "SetNeuroScheduleAction", ActionWindow?.TraceId);
                NeuroScheduleManager.Instance!.UpdateNeuroSchedule(parsedData.ResolvedSchedule);
            }
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"Exception in SetNeuroScheduleAction.ExecuteAsync: {ex.Message}", "SetNeuroScheduleAction", ActionWindow?.TraceId);
            NeuroLogger.LogError($"Stack trace: {ex.StackTrace}", "SetNeuroScheduleAction", ActionWindow?.TraceId);
        }

        return UniTask.CompletedTask;
    }

    private Schedule? GetScheduleByType(string scheduleType)
    {
        try
        {
            if (ScheduleManager.Instance == null)
            {
                NeuroLogger.LogError("[SetNeuroScheduleAction] ScheduleManager not initialized", "SetNeuroScheduleAction", ActionWindow?.TraceId);
                return null;
            }

            Schedule? schedule = scheduleType.ToLower() switch
            {
                "work_focused" => CustomScheduleFactory.CreateWorkFocusedSchedule(),
                "research_focused" => CustomScheduleFactory.CreateResearchFocusedSchedule(),
                "recreation_focused" => CustomScheduleFactory.CreateRestFocusedSchedule("Recreation Focused"),
                "bathing_focused" => CustomScheduleFactory.CreateBathingFocusedSchedule(),
                "night_shift" => CustomScheduleFactory.CreateNightShiftSchedule(),
                "early_bird" => CustomScheduleFactory.CreateEarlyBirdSchedule(),
                _ => ScheduleManager.Instance?.GetSchedules()?.FirstOrDefault(),
            };

            if (schedule == null)
            {
                NeuroLogger.LogError($"[SetNeuroScheduleAction] Failed to create schedule for type: {scheduleType}", "SetNeuroScheduleAction", ActionWindow?.TraceId);
                return null;
            }

            return schedule;
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[SetNeuroScheduleAction] Error creating schedule {scheduleType}: {ex.Message}", "SetNeuroScheduleAction", ActionWindow?.TraceId);
            return null;
        }
    }
}

/// <summary>
/// Lists the schedule templates and activity block types supported by the mod.
/// </summary>
/// <pre>
/// No payload is required and the action does not depend on a live duplicant reference.
/// </pre>
/// <post>
/// Execution reports the currently supported schedule and activity names through the action context channel.
/// </post>
public class GetAvailableSchedulesAction : BaseNeuroAction
{
    public override string Name => "list_schedules";

    protected override string Description =>
        "Get a list of all available schedule types and activity blocks for Neuro duplicate.";

    protected override JsonSchema? Schema => null;

    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        string resultMessage = "Available Schedules: default, work_focused, research_focused, night_shift, early_bird, recreation_focused, bathing_focused | Activity Types: work, sleep, recreation, eat, hygiene";
        parsedData = resultMessage;
        return ExecutionResult.Success(resultMessage);
    }

    protected override UniTask ExecuteAsync(object? parsedData)
    {
        return UniTask.CompletedTask;
    }
}

/// <summary>
/// Builds and assigns a custom Neuro schedule by specifying the activity for each of the 24 hours.
/// </summary>
/// <remarks>
/// Each hour slot (hour_0 … hour_23) accepts one of <c>"work"</c>, <c>"sleep"</c>,
/// <c>"recreation"</c>, or <c>"bathing"</c>, giving full per-hour control over the schedule.
/// </remarks>
/// <pre>
/// All 24 hour fields must be present and the connected duplicant must still exist.
/// </pre>
/// <post>
/// Successful execution creates a custom schedule from the supplied per-hour activities and applies
/// it through the Neuro schedule manager.
/// </post>
public class SetCustomScheduleAction(MinionIdentity minion) : NeuroAction<SetCustomScheduleAction.CustomScheduleData>
{
    private readonly MinionIdentity neuroMinion = minion;

    private static readonly string[] HourKeys = Enumerable.Range(0, 24).Select(i => $"hour_{i}").ToArray();

    /// <summary>
    /// Stores the per-hour activity list and the resolved schedule built during validation.
    /// </summary>
    /// <pre>The request payload has been parsed for the set-custom-schedule action.</pre>
    /// <post>The instance stores the per-hour activities together with the schedule resolved during validation.</post>
    public class CustomScheduleData
    {
        /// <summary>
        /// Gets or sets the 24-element list of per-hour activity names.
        /// </summary>
        /// <pre>The data object represents a parsed custom-schedule request.</pre>
        /// <post>Each element is one of "work", "sleep", "recreation", or "bathing".</post>
        public List<string> HourActivities { get; set; } = new(24);

        /// <summary>
        /// Gets or sets the schedule resolved from the per-hour activity list.
        /// </summary>
        /// <pre>Validation may build a schedule instance from the per-hour activities.</pre>
        /// <post>The property stores the resolved custom schedule when validation succeeds.</post>
        internal Schedule? ResolvedSchedule { get; set; }
    }

    public override string Name => "set_custom_schedule";

    protected override string Description =>
        "Create a fully custom schedule for Neuro by specifying the activity for each of the 24 hours of the cycle. " +
        "Each hour slot (hour_0 … hour_23) accepts one of: work, sleep, recreation, bathing.";

    protected override JsonSchema Schema
    {
        get
        {
            var hourEnum = new List<object> { "work", "sleep", "recreation", "bathing" };
            var properties = new Dictionary<string, JsonSchema>(24);
            var required   = new List<string>(24);
            for (int i = 0; i < 24; i++)
            {
                string key = $"hour_{i}";
                properties[key] = new JsonSchema { Type = JsonSchemaType.String, Enum = hourEnum };
                required.Add(key);
            }
            return new JsonSchema
            {
                Type = JsonSchemaType.Object,
                Required = required,
                Properties = properties
            };
        }
    }

    protected override ExecutionResult Validate(ActionJData actionData, out CustomScheduleData? parsedData)
    {
        parsedData = new CustomScheduleData();

        if (neuroMinion == null || neuroMinion.gameObject == null)
            return ExecutionResult.Failure("Neuro duplicate not found");

        var activities = new List<string>(24);
        for (int i = 0; i < 24; i++)
        {
            string key = HourKeys[i];
            string? value = actionData.Data?[key]?.Value<string>()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(value))
                value = "work";
            activities.Add(value!);
        }

        parsedData.HourActivities = activities;

        try
        {
            if (NeuroScheduleManager.Instance == null)
            {
                NeuroLogger.LogError("NeuroScheduleManager.Instance is NULL", "SetCustomScheduleAction", ActionWindow?.TraceId);
                return ExecutionResult.Failure("Failed to set schedule - manager not available");
            }

            Schedule? customSchedule = CustomScheduleFactory.CreateHourlySchedule("Custom Schedule", activities);
            if (customSchedule == null)
                return ExecutionResult.Failure("Failed to create custom schedule from the supplied hour activities");

            parsedData.ResolvedSchedule = customSchedule;

            // Build a human-readable summary (e.g. "work×14, sleep×6, recreation×4")
            string summary = activities
                .GroupBy(a => a)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key}×{g.Count()}")
                .Aggregate((a, b) => $"{a}, {b}");

            string resultMessage = $"Custom Schedule Set: {summary}";
            NeuroLogger.Log($"Validated custom schedule assignment: {resultMessage}", "SetCustomScheduleAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(resultMessage);
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"Exception in SetCustomScheduleAction.Validate: {ex.Message}", "SetCustomScheduleAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Failed to set custom schedule: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(CustomScheduleData? parsedData)
    {
        try
        {
            if (parsedData?.ResolvedSchedule == null)
            {
                NeuroLogger.LogError("ExecuteAsync called without resolved custom schedule", "SetCustomScheduleAction", ActionWindow?.TraceId);
                return UniTask.CompletedTask;
            }

            NeuroScheduleManager.Instance!.UpdateNeuroSchedule(parsedData.ResolvedSchedule);
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"Exception in SetCustomScheduleAction.ExecuteAsync: {ex.Message}", "SetCustomScheduleAction", ActionWindow?.TraceId);
        }

        return UniTask.CompletedTask;
    }
}
