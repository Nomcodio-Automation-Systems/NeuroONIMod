using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using NeuroMod.Integration;
using System;
#pragma warning disable CS8602
using System.Collections.Generic;
using System.Linq;

namespace NeuroMod;

/// <summary>
/// Clears the duplicant's currently active errand and releases related tracking state.
/// </summary>
/// <pre>
/// The connected duplicant must still exist and expose a <see cref="ChoreConsumer"/>.
/// </pre>
/// <post>
/// Successful validation describes the errand that will be cleared, and execution stops the chore and tracking state.
/// </post>
public class ClearCurrentErrandAction(MinionIdentity minion) : NeuroAction<ClearCurrentErrandAction.ClearData>
{
    private readonly MinionIdentity neuroMinion = minion;

    /// <summary>
    /// Captures the options and resolved runtime state used to clear the current errand.
    /// </summary>
    /// <pre>
    /// Public properties are populated from the incoming action payload during validation.
    /// </pre>
    /// <post>
    /// Internal properties hold the chore consumer and errand summary needed during execution.
    /// </post>
    public class ClearData
    {
        /// <summary>Gets or sets a value indicating whether the current chore should be force-stopped.</summary>
        /// <pre>The data object represents a parsed clear-errand request.</pre>
        /// <post>The property stores the force-stop flag from the action payload.</post>
        public bool ForceStop { get; set; } = false;

        /// <summary>Gets or sets the human-readable reason for clearing the errand.</summary>
        /// <pre>The data object represents a parsed clear-errand request.</pre>
        /// <post>The property stores the reason string provided by the caller, or the default.</post>
        public string Reason { get; set; } = "Manual clear requested";

        /// <summary>Gets or sets the chore consumer for the Neuro duplicant resolved during validation.</summary>
        /// <pre>Validation has located the duplicant's chore consumer.</pre>
        /// <post>The property holds the reference used to stop the active chore during execution.</post>
        internal ChoreConsumer? Consumer { get; set; }

        /// <summary>Gets or sets the display name of the errand being cleared.</summary>
        /// <pre>Validation has resolved the current chore name.</pre>
        /// <post>The property stores the task name reported in the success message.</post>
        internal string CurrentTask { get; set; } = "none";
    }

    public override string Name => "clear_current_errand";

    protected override string Description =>
        "Clear the current errand (chore) for Neuro duplicate, making them idle and available for new assignments.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["force_stop"] = new JsonSchema { Type = JsonSchemaType.Boolean },
            ["reason"] = new JsonSchema { Type = JsonSchemaType.String, MaxLength = 100 }
        }
    };

    protected override ExecutionResult Validate(ActionJData actionData, out ClearData? parsedData)
    {
        parsedData = null;

        if (neuroMinion == null || neuroMinion.gameObject == null)
            return ExecutionResult.Failure("Neuro duplicate not found or not available");

        bool forceStop = false;
        string reason = "Manual clear requested";

        if (actionData.Data != null)
        {
            forceStop = actionData.Data["force_stop"]?.Value<bool>() ?? false;

            string? reasonValue = actionData.Data["reason"]?.Value<string>();
            if (!string.IsNullOrEmpty(reasonValue))
            {
                if (reasonValue.Length > 100)
                    return ExecutionResult.Failure("Parameter 'reason' cannot exceed 100 characters");
                reason = reasonValue;
            }
        }

        ChoreConsumer? choreConsumer = neuroMinion.GetComponent<ChoreConsumer>();
        if (choreConsumer == null)
            return ExecutionResult.Failure($"Could not clear tasks for {neuroMinion!.GetProperName()} - no task consumer found.");

        string currentTask = "none";
        var driver = choreConsumer.choreDriver;
        if (driver != null && driver.HasChore())
        {
            Chore? currentChore = driver.GetCurrentChore();
            if (currentChore != null)
                currentTask = currentChore.choreType.Name;
        }

        parsedData = new ClearData
        {
            ForceStop = forceStop,
            Reason = reason,
            Consumer = choreConsumer,
            CurrentTask = currentTask
        };

        string message = $"Cleared all tasks for {neuroMinion!.GetProperName()}. Previous task: {currentTask}. Reason: {reason}. They are now idle and available for new assignments.";
        return ExecutionResult.Success(message);
    }

    protected override async UniTask ExecuteAsync(ClearData? parsedData)
    {
        if (parsedData?.Consumer == null || neuroMinion == null || neuroMinion.gameObject == null)
        {
            NeuroLogger.LogError("[ClearTasksAction] Invalid state during execution", "ClearTasksAction", ActionWindow?.TraceId);
            return;
        }

        // Yield to the next frame so StopChore() is not called while KAnimBatchManager is
        // mid-iteration in LateUpdate, which causes an ArgumentOutOfRangeException in the
        // animation batch list.
        await UniTask.Yield();

        try
        {
            try
            {
                parsedData.Consumer.choreDriver.StopChore();
                NeuroLogger.Log("Successfully stopped chore", "ClearTasksAction", ActionWindow?.TraceId);
            }
            catch (System.Exception stopEx)
            {
                NeuroLogger.LogError($"Exception during StopChore (likely harmless Unity UI issue): {stopEx.Message}", "ClearTasksAction", ActionWindow?.TraceId);
            }

            ErrandMonitor? monitor = neuroMinion.GetComponent<ErrandMonitor>();
            if (monitor != null && monitor.HasActiveAssignment)
            {
                monitor.ClearAssignment($"Cleared by user: {parsedData.Reason}");
                NeuroLogger.Log("Cleared ErrandMonitor assignment", "ClearTasksAction", ActionWindow?.TraceId);
            }

            if (ErrandCompletionTracker.Instance.IsTracking())
            {
                ErrandCompletionTracker.Instance.CancelTracking();
                NeuroLogger.Log("Cancelled ErrandCompletionTracker", "ClearTasksAction", ActionWindow?.TraceId);
            }

            ErrandReservationHelper.ClearAll();

            NeuroLogger.Log($"[ClearTasksAction] Cleared tasks for {neuroMinion.GetProperName()} - Previous: {parsedData.CurrentTask}, Reason: {parsedData.Reason}", "ClearTasksAction", ActionWindow?.TraceId);
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[ClearTasksAction] Error clearing tasks: {ex.Message}", "ClearTasksAction", ActionWindow?.TraceId);
        }
    }
}

#pragma warning restore CS8602

/// <summary>
/// Changes the duplicant's personal priority for a chore group or chore type.
/// </summary>
/// <pre>
/// The target duplicant must be available and the requested task type must resolve to a valid chore group.
/// </pre>
/// <post>
/// Successful validation captures the resolved group and current priority, and execution applies the requested value.
/// </post>
public class SetPriorityAction(MinionIdentity minion) : NeuroAction<SetPriorityAction.PriorityData>
{
    private readonly MinionIdentity neuroMinion = minion;

    /// <summary>
    /// Stores the requested priority change and the runtime objects resolved during validation.
    /// </summary>
    /// <pre>
    /// <see cref="TaskType"/> and <see cref="Priority"/> originate from the action payload.
    /// </pre>
    /// <post>
    /// Internal fields contain the chore consumer, target group, and old priority needed for execution and reporting.
    /// </post>
    public class PriorityData
    {
        /// <summary>Gets or sets the chore-group task type requested by the caller.</summary>
        /// <pre>The data object represents a parsed set-priority request.</pre>
        /// <post>The property stores the task-type identifier from the action payload.</post>
        public string TaskType { get; set; } = "";

        /// <summary>Gets or sets the priority level requested by the caller.</summary>
        /// <pre>The data object represents a parsed set-priority request.</pre>
        /// <post>The property stores the priority label (low/normal/high/critical) from the action payload.</post>
        public string Priority { get; set; } = "normal";

        /// <summary>Gets or sets the chore consumer for the Neuro duplicant resolved during validation.</summary>
        /// <pre>Validation has located the duplicant's chore consumer.</pre>
        /// <post>The property holds the reference used to apply the priority change during execution.</post>
        internal ChoreConsumer? Consumer { get; set; }

        /// <summary>Gets or sets the chore group resolved from <see cref="TaskType"/> during validation.</summary>
        /// <pre>Validation has mapped the task type to a chore group.</pre>
        /// <post>The property holds the target group whose personal priority is being changed.</post>
        internal ChoreGroup? ResolvedGroup { get; set; }

        /// <summary>Gets or sets the numeric priority value mapped from <see cref="Priority"/>.</summary>
        /// <pre>The priority label has been converted to its integer equivalent.</pre>
        /// <post>The property stores the integer value applied to the chore group.</post>
        internal int PriorityValue { get; set; }

        /// <summary>Gets or sets the previous personal priority before the change, used for rollback reporting.</summary>
        /// <pre>Validation has read the current personal priority for the resolved group.</pre>
        /// <post>The property stores the old value so the result message can show what changed.</post>
        internal int OldPriority { get; set; }
    }

    public override string Name => "set_priority";

    protected override string Description =>
        "Set the ChoreGroup priority (willingness to work) for a specific work category (Dig, Build, Cook, etc.).";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Required = new List<string>{ "task_type" },
        Properties = new Dictionary<string, JsonSchema>
        {
            ["task_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object>{ "dig", "build", "harvest", "cook", "research", "doctor", "tidy", "supply", "operate", "art", "ranch" }
            },
            ["priority"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object>{ "low", "normal", "high", "critical" }
            }
        }
    };

    protected override ExecutionResult Validate(ActionJData actionData, out PriorityData? parsedData)
    {
        parsedData = new PriorityData
        {
            TaskType = actionData.Data?["task_type"]?.Value<string>() ?? "",
            Priority = actionData.Data?["priority"]?.Value<string>() ?? "normal"
        };

        if (string.IsNullOrEmpty(parsedData.TaskType))
            return ExecutionResult.Failure("Task type is required");

        if (neuroMinion == null || neuroMinion.gameObject == null)
            return ExecutionResult.Failure("Neuro duplicate not found");

        string duplicateName = neuroMinion.GetProperName();

        ChoreConsumer? choreConsumer = neuroMinion.GetComponent<ChoreConsumer>();
        if (choreConsumer == null)
            return ExecutionResult.Failure($"Could not set priority for {duplicateName} - no chore consumer found.");

        ChoreGroup? choreGroup = GetChoreGroupByName(parsedData.TaskType);

        if (choreGroup == null)
        {
            ChoreType? choreType = GetChoreTypeByName(parsedData.TaskType);
            if (choreType == null)
                return ExecutionResult.Failure($"ChoreGroup '{parsedData.TaskType}' not found as ChoreGroup or ChoreType. Cannot set priority.");

            choreGroup = GetChoreGroupForType(choreType);
            if (choreGroup == null)
                return ExecutionResult.Failure($"Failed to set priority - no ChoreGroup found for ChoreType '{parsedData.TaskType}'");
        }

        int priorityValue = parsedData.Priority.ToLower() switch
        {
            "low" => 1,
            "normal" => 3,
            "high" => 4,
            "critical" => 5,
            _ => 3
        };

        int currentPriority = choreConsumer.GetPersonalPriority(choreGroup);

        parsedData.Consumer = choreConsumer;
        parsedData.ResolvedGroup = choreGroup;
        parsedData.PriorityValue = priorityValue;
        parsedData.OldPriority = currentPriority;

        string message = $"ChoreGroup Priority Set: {duplicateName} will prioritize {choreGroup.Name} work (priority: {priorityValue}/5, was {currentPriority}/5)";
        NeuroLogger.Log(message, "SetPriorityAction", ActionWindow?.TraceId);
        return ExecutionResult.Success(message);
    }

    protected override UniTask ExecuteAsync(PriorityData? parsedData)
    {
        if (parsedData?.Consumer == null || parsedData.ResolvedGroup == null ||
            neuroMinion == null || neuroMinion.gameObject == null)
        {
            NeuroLogger.LogError("[SetPriorityAction] Invalid state during execution", "SetPriorityAction", ActionWindow?.TraceId);
            return UniTask.CompletedTask;
        }

        try
        {
            parsedData.Consumer.SetPersonalPriority(parsedData.ResolvedGroup, parsedData.PriorityValue);

            if (ManagementMenu.Instance != null && ManagementMenu.Instance.jobsScreen != null)
            {
                try
                {
                    MinionResume? resume = neuroMinion.GetComponent<MinionResume>();
                    if (resume != null)
                        ManagementMenu.Instance.jobsScreen.Refresh(resume);
                }
                catch (Exception refreshEx)
                {
                    NeuroLogger.LogError($"UI refresh failed: {refreshEx.Message}", "SetPriorityAction", ActionWindow?.TraceId);
                }
            }

            NeuroLogger.Log($"Priority set: {parsedData.ResolvedGroup.Name} = {parsedData.PriorityValue}", "SetPriorityAction", ActionWindow?.TraceId);
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[SetPriorityAction] Error setting priority: {ex.Message}", "SetPriorityAction", ActionWindow?.TraceId);
        }

        return UniTask.CompletedTask;
    }

    private static ChoreGroup? GetChoreGroupByName(string groupName)
    {
        try
        {
            ChoreGroup? group = Db.Get().ChoreGroups.resources.FirstOrDefault(
                g => g.Id.Equals(groupName, StringComparison.OrdinalIgnoreCase)
            );

            if (group != null)
            {
                NeuroLogger.Log($"Found ChoreGroup by ID: {group.Id} -> {group.Name}", "GetChoreGroupByName", null);
                return group;
            }

            group = Db.Get().ChoreGroups.resources.FirstOrDefault(
                g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase)
            );

            if (group != null)
            {
                NeuroLogger.Log($"Found ChoreGroup by Name: {group.Name} (ID: {group.Id})", "GetChoreGroupByName", null);
                return group;
            }

            NeuroLogger.Log($"ChoreGroup '{groupName}' not found in database", "GetChoreGroupByName", null);
            return null;
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Exception in GetChoreGroupByName: {ex.Message}", "GetChoreGroupByName", null);
            return null;
        }
    }

    private ChoreType? GetChoreTypeByName(string taskType)
    {
        try
        {
            if (Db.Get()?.ChoreTypes == null)
            {
                NeuroLogger.LogError("[SetPriorityAction] ChoreTypes database not initialized");
                return null;
            }

            ChoreType? choreType = Db.Get().ChoreTypes.resources.FirstOrDefault(
                ct => ct.Id.Equals(taskType, StringComparison.OrdinalIgnoreCase)
            );

            if (choreType != null)
            {
                NeuroLogger.Log($"[SetPriorityAction] Found ChoreType: {choreType.Id}", "SetPriorityAction", ActionWindow?.TraceId);
                return choreType;
            }

            choreType = taskType.ToLower() switch
            {
                "harvest" => Db.Get().ChoreTypes.Ranch,
                "tidy" => Db.Get().ChoreTypes.EmptyStorage,
                "supply" => Db.Get().ChoreTypes.FetchCritical,
                "operate" => Db.Get().ChoreTypes.PowerTinker,
                _ => null
            };

            if (choreType != null)
            {
                NeuroLogger.Log($"[SetPriorityAction] Mapped legacy name '{taskType}' to {choreType.Id}", "SetPriorityAction", ActionWindow?.TraceId);
                return choreType;
            }

            NeuroLogger.LogError($"[SetPriorityAction] ChoreType '{taskType}' not found in database", "SetPriorityAction", ActionWindow?.TraceId);
            return null;
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[SetPriorityAction] Error looking up chore type '{taskType}': {ex.Message}", "SetPriorityAction", ActionWindow?.TraceId);
            return null;
        }
    }

    private ChoreGroup? GetChoreGroupForType(ChoreType choreType)
    {
        try
        {
            if (Db.Get()?.ChoreGroups == null)
            {
                NeuroLogger.LogError("[SetPriorityAction] ChoreGroups database not initialized", "SetPriorityAction", ActionWindow?.TraceId);
                return null;
            }

            foreach (ChoreGroup group in Db.Get().ChoreGroups.resources)
            {
                if (group.choreTypes.Contains(choreType))
                {
                    return group;
                }
            }

            NeuroLogger.LogError($"[SetPriorityAction] No ChoreGroup found containing ChoreType: {choreType.Name}", "SetPriorityAction", ActionWindow?.TraceId);
            return null;
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[SetPriorityAction] Error finding ChoreGroup for '{choreType.Name}': {ex.Message}", "SetPriorityAction", ActionWindow?.TraceId);
            return null;
        }
    }
}

/// <summary>
/// Lists the duplicant's current personal priority values for all known chore groups.
/// </summary>
/// <pre>
/// The duplicant and chore database must be available when validation runs.
/// </pre>
/// <post>
/// Successful validation returns a summarized priority snapshot and execution performs no further work.
/// </post>
public class ListPrioritiesAction(MinionIdentity minion) : NeuroAction<ListPrioritiesAction.EmptyData>
{
    private readonly MinionIdentity neuroMinion = minion;

    /// <summary>
    /// Represents the empty payload accepted by <see cref="ListPrioritiesAction"/>.
    /// </summary>
    /// <pre>The list-priorities action accepts no structured payload.</pre>
    /// <post>The type serves only as a marker indicating successful parsing of an empty request.</post>
    public class EmptyData { }

    public override string Name => "list_priorities";

    protected override string Description =>
        "Get Neuro's ChoreGroup priority settings (0-5 rating for each work category: Basekeeping, Dig, Cook, Research, etc.).";

    protected override JsonSchema? Schema => null;

    protected override ExecutionResult Validate(ActionJData actionData, out EmptyData? parsedData)
    {
        parsedData = new EmptyData();

        if (neuroMinion == null || neuroMinion.gameObject == null)
            return ExecutionResult.Failure("Neuro duplicate not found or not available");

        try
        {
            ChoreConsumer choreConsumer = neuroMinion.GetComponent<ChoreConsumer>();
            if (choreConsumer == null)
                return ExecutionResult.Failure("Failed to get priority list - ChoreConsumer missing");

            if (Db.Get()?.ChoreGroups == null)
                return ExecutionResult.Failure("Failed to get priority list - database not ready");

            string duplicateName = neuroMinion.GetProperName();

            List<ChoreGroup> allChoreGroups = Db.Get().ChoreGroups.resources;
            List<string> taskList = new List<string>();
            int totalGroups = 0;

            foreach (ChoreGroup choreGroup in allChoreGroups)
            {
                int priority = choreConsumer.GetPersonalPriority(choreGroup);

                string priorityName = priority switch
                {
                    0 => "disabled",
                    1 => "very_low",
                    2 => "low",
                    3 => "normal",
                    4 => "high",
                    5 => "critical",
                    _ => $"custom_{priority}"
                };

                string taskEntry = $"{choreGroup.Id}:{priorityName}({priority})";
                taskList.Add(taskEntry);
                totalGroups++;
            }

            string contextMessage = $"{duplicateName}'s ChoreGroup Priorities ({totalGroups} groups): " +
                string.Join(", ", taskList.Take(10)) +
                (taskList.Count > 10 ? "..." : "");

            NeuroLogger.Log($"[ListPrioritiesAction] Found {totalGroups} ChoreGroups for {duplicateName}", "ListPrioritiesAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(contextMessage);
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[ListPrioritiesAction] Error getting priorities: {ex.Message}", "ListPrioritiesAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Failed to get priority list: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(EmptyData? parsedData)
    {
        return UniTask.CompletedTask;
    }
}
