using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace NeuroMod;

/// <summary>
/// Simple action to get the current schedule information for Neuro duplicate
/// </summary>
public class GetNeuroScheduleAction(MinionIdentity minion) : NeuroAction<GetNeuroScheduleAction.ScheduleQuery>
{
    private readonly MinionIdentity neuroMinion = minion;

    public class ScheduleQuery
    {
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

        // Validate minion exists and hasn't been destroyed
        return neuroMinion == null || neuroMinion.gameObject == null
            ? ExecutionResult.Failure("Neuro duplicate not found")
            : ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(ScheduleQuery? parsedData)
    {
        NeuroLogger.Log("ExecuteAsync called for GetNeuroScheduleAction", "GetNeuroScheduleAction");

        // Double-check minion is still valid at execution time
        if (parsedData == null)
        {
            NeuroLogger.LogError("parsedData is NULL", "GetNeuroScheduleAction");
            NeuroSdk.Messages.Outgoing.Context.Send("Cannot get schedule - Internal error (parsedData null)", false);
            return UniTask.CompletedTask;
        }

        if (neuroMinion == null)
        {
            NeuroLogger.LogError("neuroMinion is NULL", "GetNeuroScheduleAction");
            NeuroSdk.Messages.Outgoing.Context.Send("Cannot get schedule - Neuro duplicate reference is null", false);
            return UniTask.CompletedTask;
        }

        if (neuroMinion.gameObject == null)
        {
            NeuroLogger.LogError("neuroMinion.gameObject is NULL", "GetNeuroScheduleAction");
            NeuroSdk.Messages.Outgoing.Context.Send("Cannot get schedule - Neuro duplicate gameObject is null", false);
            return UniTask.CompletedTask;
        }

        try
        {
            string duplicateName = neuroMinion.GetProperName();
            NeuroLogger.Log($"Getting schedule for {duplicateName}", "GetNeuroScheduleAction");

            Schedulable? schedulable = neuroMinion.GetComponent<Schedulable>();
            if (schedulable == null)
            {
                NeuroLogger.LogError($"No Schedulable component found on {duplicateName}", "GetNeuroScheduleAction");
                NeuroSdk.Messages.Outgoing.Context.Send($"Cannot get schedule - {duplicateName} has no Schedulable component", false);
                return UniTask.CompletedTask;
            }

            NeuroLogger.Log("Schedulable component found", "GetNeuroScheduleAction");

            // Get current time of day (0-23 hours)
            if (GameClock.Instance == null)
            {
                NeuroLogger.LogError("GameClock.Instance is NULL", "GetNeuroScheduleAction");
                NeuroSdk.Messages.Outgoing.Context.Send("Cannot get schedule - GameClock not available", false);
                return UniTask.CompletedTask;
            }

            float cycleTime = GameClock.Instance.GetTimeSinceStartOfCycle();
            int currentHour = UnityEngine.Mathf.FloorToInt(cycleTime / 600f); // 600 seconds per hour in ONI

            NeuroLogger.Log($"Current cycle time: {cycleTime}, Hour: {currentHour}", "GetNeuroScheduleAction");

            // Get current activity and schedule name
            string currentActivity = "Unknown";
            string scheduleName = "Unknown";

            Schedule currentSchedule = schedulable.GetSchedule();
            if (currentSchedule == null)
            {
                NeuroLogger.LogWarning("schedulable.GetSchedule() returned NULL", "GetNeuroScheduleAction");
            }
            else
            {
                scheduleName = currentSchedule.name ?? "Unnamed Schedule";
                NeuroLogger.Log($"Schedule name: {scheduleName}", "GetNeuroScheduleAction");

                ScheduleBlock currentBlock = currentSchedule.GetCurrentScheduleBlock();
                if (currentBlock == null)
                {
                    NeuroLogger.LogWarning("GetCurrentScheduleBlock() returned NULL", "GetNeuroScheduleAction");
                }
                else
                {
                    currentActivity = currentBlock.name ?? "Unnamed Activity";
                    NeuroLogger.Log($"Current block: {currentActivity}", "GetNeuroScheduleAction");
                }
            }

            // Check if this is Neuro's dedicated schedule
            if (NeuroScheduleManager.Instance == null)
            {
                NeuroLogger.LogWarning("NeuroScheduleManager.Instance is NULL", "GetNeuroScheduleAction");
            }
            else
            {
                Schedule? neuroSchedule = NeuroScheduleManager.Instance.GetNeuroSchedule();
                if (neuroSchedule == null)
                {
                    NeuroLogger.LogWarning("GetNeuroSchedule() returned NULL", "GetNeuroScheduleAction");
                }
                else if (neuroSchedule.name == scheduleName)
                {
                    scheduleName = $"{scheduleName} (Dedicated)";
                    NeuroLogger.Log("Using dedicated Neuro schedule", "GetNeuroScheduleAction");
                }
            }

            string contextMessage = $"Neuro Schedule: '{scheduleName}' - Hour: {currentHour}/24 - Current Activity: {currentActivity}";
            NeuroLogger.Log($"Success: {contextMessage}", "GetNeuroScheduleAction");

            NeuroSdk.Messages.Outgoing.Context.Send(contextMessage, false);
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"Exception in GetNeuroScheduleAction: {ex.Message}", "GetNeuroScheduleAction");
            NeuroLogger.LogError($"Stack trace: {ex.StackTrace}", "GetNeuroScheduleAction");
            NeuroSdk.Messages.Outgoing.Context.Send($"Error getting schedule: {ex.Message}", false);
        }

        return UniTask.CompletedTask;
    }
}

/// <summary>
/// Action to set a specific schedule type for Neuro duplicate
/// </summary>
public class SetNeuroScheduleAction(MinionIdentity minion) : NeuroAction<SetNeuroScheduleAction.ScheduleData>
{
    private readonly MinionIdentity neuroMinion = minion;

    public class ScheduleData
    {
        public string ScheduleType { get; set; } = "";
    }

    public override string Name => "set_schedule";

    protected override string Description =>
        "Set a specific schedule type for Neuro duplicate (work focused, research focused, etc.).";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Required = ["schedule_type"],
        Properties = new Dictionary<string, JsonSchema>
        {
            ["schedule_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = ["default", "work_focused", "research_focused", "night_shift", "early_bird", "recreation_focused", "bathing_focused"]
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
        {
            return ExecutionResult.Failure("Schedule type is required");
        }

        // Validate minion exists and hasn't been destroyed
        return neuroMinion == null || neuroMinion.gameObject == null
            ? ExecutionResult.Failure("Neuro duplicate not found")
            : ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(ScheduleData? parsedData)
    {
        NeuroLogger.Log("ExecuteAsync called for SetNeuroScheduleAction", "SetNeuroScheduleAction");

        // Double-check minion is still valid at execution time
        if (parsedData == null)
        {
            NeuroLogger.LogError("parsedData is NULL", "SetNeuroScheduleAction");
            NeuroSdk.Messages.Outgoing.Context.Send("Cannot set schedule - Internal error (parsedData null)", false);
            return UniTask.CompletedTask;
        }

        NeuroLogger.Log($"Schedule type requested: {parsedData.ScheduleType}", "SetNeuroScheduleAction");

        if (neuroMinion == null)
        {
            NeuroLogger.LogError("neuroMinion is NULL", "SetNeuroScheduleAction");
            NeuroSdk.Messages.Outgoing.Context.Send("Cannot set schedule - Neuro duplicate reference is null", false);
            return UniTask.CompletedTask;
        }

        if (neuroMinion.gameObject == null)
        {
            NeuroLogger.LogError("neuroMinion.gameObject is NULL", "SetNeuroScheduleAction");
            NeuroSdk.Messages.Outgoing.Context.Send("Cannot set schedule - Neuro duplicate gameObject is null", false);
            return UniTask.CompletedTask;
        }

        try
        {
            string duplicateName = neuroMinion.GetProperName();
            NeuroLogger.Log($"Setting schedule {parsedData.ScheduleType} for {duplicateName}", "SetNeuroScheduleAction");

            Schedulable? schedulable = neuroMinion.GetComponent<Schedulable>();
            if (schedulable == null)
            {
                string errorMessage = $"No Schedulable component found on {duplicateName}. Cannot assign schedule.";
                NeuroLogger.LogError(errorMessage, "SetNeuroScheduleAction");
                NeuroSdk.Messages.Outgoing.Context.Send(errorMessage, false);
                return UniTask.CompletedTask;
            }

            NeuroLogger.Log("Schedulable component found, creating target schedule...", "SetNeuroScheduleAction");

            // Validate that schedule type exists and create it
            Schedule? targetSchedule = GetScheduleByType(parsedData.ScheduleType);
            if (targetSchedule == null)
            {
                string errorMessage = $"GetScheduleByType returned NULL for '{parsedData.ScheduleType}'. This schedule type may not be available.";
                NeuroLogger.LogError(errorMessage, "SetNeuroScheduleAction");
                NeuroSdk.Messages.Outgoing.Context.Send(errorMessage, false);
                return UniTask.CompletedTask;
            }

            NeuroLogger.Log($"Target schedule created: {targetSchedule.name}", "SetNeuroScheduleAction");

            // Validate that the schedule has valid blocks
            List<ScheduleBlock> blocks = targetSchedule.GetBlocks();
            if (blocks == null)
            {
                string errorMessage = $"Schedule '{parsedData.ScheduleType}' has NULL blocks. Cannot assign invalid schedule.";
                NeuroLogger.LogError(errorMessage, "SetNeuroScheduleAction");
                NeuroSdk.Messages.Outgoing.Context.Send(errorMessage, false);
                return UniTask.CompletedTask;
            }

            if (blocks.Count == 0)
            {
                string errorMessage = $"Schedule '{parsedData.ScheduleType}' has ZERO blocks. Cannot assign invalid schedule.";
                NeuroLogger.LogError(errorMessage, "SetNeuroScheduleAction");
                NeuroSdk.Messages.Outgoing.Context.Send(errorMessage, false);
                return UniTask.CompletedTask;
            }

            NeuroLogger.Log($"Target schedule has {blocks.Count} blocks", "SetNeuroScheduleAction");

            // Use the NeuroScheduleManager to update Neuro's dedicated schedule
            if (NeuroScheduleManager.Instance == null)
            {
                NeuroLogger.LogError("NeuroScheduleManager.Instance is NULL, using fallback method", "SetNeuroScheduleAction");

                // Fallback to old system if NeuroScheduleManager isn't available
                DuplicateScheduleControlPatches.SetCustomSchedule(schedulable, targetSchedule);

                string contextMessage = $"Schedule Assignment: Set {duplicateName} to {parsedData.ScheduleType} schedule (fallback mode).";
                NeuroLogger.Log(contextMessage, "SetNeuroScheduleAction");
                NeuroSdk.Messages.Outgoing.Context.Send(contextMessage, false);
            }
            else
            {
                NeuroLogger.Log("Using NeuroScheduleManager.UpdateNeuroSchedule", "SetNeuroScheduleAction");
                NeuroScheduleManager.Instance.UpdateNeuroSchedule(targetSchedule);

                // No need to manually call OnScheduleChanged - UpdateNeuroSchedule uses SetBlockGroup which calls Changed()
                // Changed() automatically notifies all assigned Schedulables and triggers UI refresh

                string contextMessage = $"Schedule Assignment: Set {duplicateName} to {parsedData.ScheduleType} schedule. Their daily routine has been updated.";
                NeuroLogger.Log($"Success: {contextMessage}", "SetNeuroScheduleAction");
                NeuroSdk.Messages.Outgoing.Context.Send(contextMessage, false);
            }
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"Exception in SetNeuroScheduleAction: {ex.Message}", "SetNeuroScheduleAction");
            NeuroLogger.LogError($"Stack trace: {ex.StackTrace}", "SetNeuroScheduleAction");
            NeuroSdk.Messages.Outgoing.Context.Send($"Failed to set schedule for {neuroMinion.GetProperName()}: {ex.Message}", false);
        }

        return UniTask.CompletedTask;
    }

    /// <summary>
    /// Get Schedule by type with runtime validation
    /// </summary>
    private Schedule? GetScheduleByType(string scheduleType)
    {
        try
        {
            // Validate that ScheduleManager exists
            if (ScheduleManager.Instance == null)
            {
                NeuroLogger.LogError("[SetNeuroScheduleAction] ScheduleManager not initialized");
                return null;
            }

            // Try to use the custom schedule factory system if available
            Schedule? schedule = null;
            schedule = scheduleType.ToLower() switch
            {
                "work_focused" => CustomScheduleFactory.CreateWorkFocusedSchedule(),
                "research_focused" => CustomScheduleFactory.CreateResearchFocusedSchedule(),
                "recreation_focused" => CustomScheduleFactory.CreateRestFocusedSchedule("Recreation Focused"),// Use rest focused as closest available alternative
                "bathing_focused" => CustomScheduleFactory.CreateBathingFocusedSchedule(),
                "night_shift" => CustomScheduleFactory.CreateNightShiftSchedule(),
                "early_bird" => CustomScheduleFactory.CreateBalancedSchedule("Early Bird"),// Use balanced schedule as alternative for early bird
                _ => ScheduleManager.Instance?.GetSchedules()?.FirstOrDefault(),// Use the default schedule from the schedule manager
            };

            // Validate the created schedule
            if (schedule == null)
            {
                NeuroLogger.LogError($"[SetNeuroScheduleAction] Failed to create schedule for type: {scheduleType}");
                return null;
            }

            return schedule;
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[SetNeuroScheduleAction] Error creating schedule {scheduleType}: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Action to get all available schedule types that can be assigned to Neuro duplicate
/// </summary>
public class GetAvailableSchedulesAction : BaseNeuroAction
{
    public override string Name => "list_schedules";

    protected override string Description =>
        "Get a list of all available schedule types and activity blocks for Neuro duplicate.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = []
    };

    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;
        return ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(object? parsedData)
    {
        try
        {
            NeuroLogger.Log("[GetAvailableSchedulesAction] Getting available schedule types");

            // Define available schedule types based on the existing system
            var availableSchedules = new
            {
                schedule_types = new[]
                {
                    new { name = "default", description = "Standard work/sleep/recreation balance" },
                    new { name = "work_focused", description = "Emphasis on work tasks, minimal recreation" },
                    new { name = "research_focused", description = "Prioritize research and learning" },
                    new { name = "night_shift", description = "Work during night hours, sleep during day" },
                    new { name = "early_bird", description = "Start work very early, sleep early" },
                    new { name = "recreation_focused", description = "More time for recreation and stress relief" },
                    new { name = "bathing_focused", description = "Extended time for hygiene and bathing activities" }
                },
                activity_types = new[]
                {
                    new { name = "work", description = "General work and labor tasks" },
                    new { name = "sleep", description = "Rest and sleep time" },
                    new { name = "recreation", description = "Fun and stress relief activities" },
                    new { name = "eat", description = "Meal time and eating" },
                    new { name = "hygiene", description = "Personal hygiene and bathroom breaks" }
                },
                time_info = new
                {
                    cycle_length = "24 hours (600 seconds per game hour)",
                    description = "Each activity block represents 1 hour of the day cycle"
                }
            };

            string contextMessage = "Available Schedules: " + string.Join(", ", availableSchedules.schedule_types.Select(s => s.name)) +
                " | Activity Types: " + string.Join(", ", availableSchedules.activity_types.Select(a => a.name));

            NeuroLogger.Log($"[GetAvailableSchedulesAction] {contextMessage}");
            NeuroSdk.Messages.Outgoing.Context.Send(contextMessage, false);
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[GetAvailableSchedulesAction] Error getting available schedules: {ex.Message}");
        }

        return UniTask.CompletedTask;
    }
}

/// <summary>
/// Action to create a custom schedule with specific hour allocations for each activity type
/// </summary>
public class SetCustomScheduleAction(MinionIdentity minion) : NeuroAction<SetCustomScheduleAction.CustomScheduleData>
{
    private readonly MinionIdentity neuroMinion = minion;

    public class CustomScheduleData
    {
        public int WorkHours { get; set; } = 16;
        public int RecreationHours { get; set; } = 4;
        public int SleepHours { get; set; } = 4;
        public int BathingHours { get; set; } = 0;
    }

    public override string Name => "set_custom_schedule";

    protected override string Description =>
        "Create a custom schedule for Neuro with specific hour allocations for work, recreation, sleep, and bathing. All hours must add up to 24.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["work_hours"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer,
                Minimum = 0,
                Maximum = 24
            },
            ["recreation_hours"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer,
                Minimum = 0,
                Maximum = 24
            },
            ["sleep_hours"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer,
                Minimum = 0,
                Maximum = 24
            },
            ["bathing_hours"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer,
                Minimum = 0,
                Maximum = 24
            }
        }
    };

    protected override ExecutionResult Validate(ActionJData actionData, out CustomScheduleData? parsedData)
    {
        parsedData = new CustomScheduleData
        {
            WorkHours = actionData.Data?["work_hours"]?.Value<int>() ?? 16,
            RecreationHours = actionData.Data?["recreation_hours"]?.Value<int>() ?? 4,
            SleepHours = actionData.Data?["sleep_hours"]?.Value<int>() ?? 4,
            BathingHours = actionData.Data?["bathing_hours"]?.Value<int>() ?? 0
        };

        if (neuroMinion == null || neuroMinion.gameObject == null)
        {
            return ExecutionResult.Failure("Neuro duplicate not found");
        }

        int total = parsedData.WorkHours + parsedData.RecreationHours + parsedData.SleepHours + parsedData.BathingHours;
        return total != 24
            ? ExecutionResult.Failure($"Hours must add up to 24. Got: {total} (work: {parsedData.WorkHours}, recreation: {parsedData.RecreationHours}, sleep: {parsedData.SleepHours}, bathing: {parsedData.BathingHours})")
            : ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(CustomScheduleData? parsedData)
    {
        try
        {
            if (parsedData == null || neuroMinion == null)
            {
                NeuroSdk.Messages.Outgoing.Context.Send("Cannot set custom schedule - invalid data", false);
                return UniTask.CompletedTask;
            }

            NeuroLogger.Log($"Creating custom schedule: work={parsedData.WorkHours}h, recreation={parsedData.RecreationHours}h, sleep={parsedData.SleepHours}h, bathing={parsedData.BathingHours}h", "SetCustomScheduleAction");

            // Create custom schedule
            Schedule? customSchedule = CustomScheduleFactory.CreateCustomSchedule(
                "Custom Schedule",
                parsedData.WorkHours,
                parsedData.RecreationHours,
                parsedData.SleepHours,
                parsedData.BathingHours
            );

            if (customSchedule == null)
            {
                NeuroSdk.Messages.Outgoing.Context.Send("Failed to create custom schedule", false);
                return UniTask.CompletedTask;
            }

            // Use NeuroScheduleManager to update
            if (NeuroScheduleManager.Instance != null)
            {
                NeuroScheduleManager.Instance.UpdateNeuroSchedule(customSchedule);

                string contextMessage = $"Custom Schedule Set: {parsedData.WorkHours}h work, {parsedData.RecreationHours}h recreation, {parsedData.SleepHours}h sleep, {parsedData.BathingHours}h bathing";
                NeuroLogger.Log(contextMessage, "SetCustomScheduleAction");
                NeuroSdk.Messages.Outgoing.Context.Send(contextMessage, false);
            }
            else
            {
                NeuroLogger.LogError("NeuroScheduleManager.Instance is NULL", "SetCustomScheduleAction");
                NeuroSdk.Messages.Outgoing.Context.Send("Failed to set schedule - manager not available", false);
            }
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"Exception in SetCustomScheduleAction: {ex.Message}", "SetCustomScheduleAction");
            NeuroSdk.Messages.Outgoing.Context.Send($"Failed to set custom schedule: {ex.Message}", false);
        }

        return UniTask.CompletedTask;
    }
}