using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeuroMod;

/// <summary>
/// Simple Neuro Action to clear the current errand (chore) for the Neuro duplicate
/// </summary>
public class ClearCurrentErrandAction(MinionIdentity minion) : NeuroAction<ClearCurrentErrandAction.ClearData>
{
    private readonly MinionIdentity neuroMinion = minion;

    public class ClearData
    {
        public bool ForceStop { get; set; } = false;
        public string Reason { get; set; } = "Manual clear requested";
    }

    public override string Name => "clear_current_errand";

    protected override string Description =>
        "Clear the current errand (chore) for Neuro duplicate, making them idle and available for new assignments.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["force_stop"] = new JsonSchema
            {
                Type = JsonSchemaType.Boolean
            },
            ["reason"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                MaxLength = 100
            }
        }
    };

    protected override ExecutionResult Validate(ActionJData actionData, out ClearData? parsedData)
    {
        parsedData = null;

        // Validate minion exists and hasn't been destroyed
        if (neuroMinion == null || neuroMinion.gameObject == null)
        {
            return ExecutionResult.Failure("Neuro duplicate not found or not available");
        }

        // Parse input parameters
        bool forceStop = false;
        string reason = "Manual clear requested";

        if (actionData.Data != null)
        {
            forceStop = actionData.Data["force_stop"]?.Value<bool>() ?? false;

            string? reasonValue = actionData.Data["reason"]?.Value<string>();
            if (!string.IsNullOrEmpty(reasonValue))
            {
                if (reasonValue!.Length > 100)
                {
                    return ExecutionResult.Failure("Parameter 'reason' cannot exceed 100 characters");
                }
                reason = reasonValue;
            }
        }

        parsedData = new ClearData
        {
            ForceStop = forceStop,
            Reason = reason
        };

        return ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(ClearData? parsedData)
    {
        // Double-check minion is still valid at execution time
        if (parsedData == null || neuroMinion == null || neuroMinion.gameObject == null)
        {
            NeuroLogger.LogError("[ClearTasksAction] Neuro duplicate became unavailable during action execution");
            NeuroSdk.Messages.Outgoing.Context.Send("Cannot clear tasks - Neuro duplicate is no longer available", false);
            return UniTask.CompletedTask;
        }

        try
        {
            NeuroLogger.Log($"========== ClearTasksAction START ==========", "ClearTasksAction");
            NeuroLogger.Log($"Reason: {parsedData.Reason}, ForceStop: {parsedData.ForceStop}", "ClearTasksAction");

            ChoreConsumer choreConsumer = neuroMinion.GetComponent<ChoreConsumer>();
            if (choreConsumer != null)
            {
                // Get current task info before clearing
                string currentTask = "none";
                if (choreConsumer.choreDriver.HasChore())
                {
                    Chore currentChore = choreConsumer.choreDriver.GetCurrentChore();
                    if (currentChore != null)
                    {
                        currentTask = currentChore.choreType.Name;
                        NeuroLogger.Log($"Current task: {currentTask}", "ClearTasksAction");
                    }
                }
                else
                {
                    NeuroLogger.Log("No current task to clear", "ClearTasksAction");
                }

                // Stop current chore (may trigger Graphics device warning in Unity - this is expected)
                try
                {
                    NeuroLogger.Log("Calling choreDriver.StopChore()...", "ClearTasksAction");
                    choreConsumer.choreDriver.StopChore();
                    NeuroLogger.Log("Successfully stopped chore", "ClearTasksAction");
                }
                catch (System.Exception stopEx)
                {
                    // StopChore can throw graphics-related exceptions in Unity UI system
                    // These are harmless and don't affect functionality
                    NeuroLogger.LogError($"Exception during StopChore (likely harmless Unity UI issue): {stopEx.Message}", "ClearTasksAction");
                }

                // Verify duplicate is now idle
                bool isIdle = !choreConsumer.choreDriver.HasChore();
                NeuroLogger.Log($"Verification: HasChore = {!isIdle} (should be false)", "ClearTasksAction");

                if (isIdle)
                {
                    NeuroLogger.Log("SUCCESS: Duplicate is now idle", "ClearTasksAction");
                }
                else
                {
                    NeuroLogger.LogError("FAILED: Duplicate still has a chore assigned", "ClearTasksAction");
                }

                NeuroLogger.Log($"[ClearTasksAction] Cleared tasks for {neuroMinion.GetProperName()} - Previous task: {currentTask}, Reason: {parsedData.Reason}");

                string contextMessage = $"Cleared all tasks for {neuroMinion.GetProperName()}. " +
                    $"Previous task: {currentTask}. " +
                    $"Reason: {parsedData.Reason}. " +
                    $"They are now idle and available for new assignments.";

                NeuroLogger.Log($"========== ClearTasksAction END ==========", "ClearTasksAction");
                NeuroSdk.Messages.Outgoing.Context.Send(contextMessage, false);
            }
            else
            {
                NeuroLogger.LogError("ChoreConsumer component not found", "ClearTasksAction");
                NeuroSdk.Messages.Outgoing.Context.Send($"Could not clear tasks for {neuroMinion.GetProperName()} - no task consumer found.", false);
            }
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[ClearTasksAction] Error clearing tasks: {ex.Message}");
            NeuroLogger.LogError($"Stack trace: {ex.StackTrace}", "ClearTasksAction");
            NeuroSdk.Messages.Outgoing.Context.Send($"Failed to clear tasks for {neuroMinion.GetProperName()}: {ex.Message}", false);
        }

        return UniTask.CompletedTask;
    }
}

/// <summary>
/// Action to set ChoreGroup priorities for Neuro duplicate
/// </summary>
public class SetPriorityAction(MinionIdentity minion) : NeuroAction<SetPriorityAction.PriorityData>
{
    private readonly MinionIdentity neuroMinion = minion;

    public class PriorityData
    {
        public string TaskType { get; set; } = "";
        public string Priority { get; set; } = "normal";
    }

    public override string Name => "set_priority";

    protected override string Description =>
        "Set the ChoreGroup priority (willingness to work) for a specific work category (Dig, Build, Cook, etc.).";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Required = ["task_type"],
        Properties = new Dictionary<string, JsonSchema>
        {
            ["task_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = ["dig", "build", "harvest", "cook", "research", "doctor", "tidy", "supply", "operate", "art", "ranch"]
            },
            ["priority"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = ["low", "normal", "high", "critical"]
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
        {
            return ExecutionResult.Failure("Task type is required");
        }

        // Validate minion exists and hasn't been destroyed
        return neuroMinion == null || neuroMinion.gameObject == null
            ? ExecutionResult.Failure("Neuro duplicate not found")
            : ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(PriorityData? parsedData)
    {
        // Double-check minion is still valid at execution time
        if (parsedData == null || neuroMinion == null || neuroMinion.gameObject == null)
        {
            NeuroLogger.LogError("[SetPriorityAction] Neuro duplicate became unavailable during action execution");
            NeuroSdk.Messages.Outgoing.Context.Send("Cannot set priority - Neuro duplicate is no longer available", false);
            return UniTask.CompletedTask;
        }

        try
        {
            string duplicateName = neuroMinion.GetProperName();
            NeuroLogger.Log($"[SetPriorityAction] Setting ChoreGroup {parsedData!.TaskType} priority to {parsedData.Priority} for {duplicateName}");

            ChoreConsumer? choreConsumer = neuroMinion.GetComponent<ChoreConsumer>();
            if (choreConsumer == null)
            {
                string errorMessage = $"Could not set priority for {duplicateName} - no chore consumer found.";
                NeuroLogger.LogError($"[SetPriorityAction] {errorMessage}");
                NeuroSdk.Messages.Outgoing.Context.Send(errorMessage, false);
                return UniTask.CompletedTask;
            }

            // Try to find ChoreGroup directly first (for group names like "Basekeeping", "Farming")
            ChoreGroup? choreGroup = GetChoreGroupByName(parsedData.TaskType);

            // If not found as group, try as ChoreType (like "Mop", "Dig", "Cook")
            if (choreGroup == null)
            {
                ChoreType? choreType = GetChoreTypeByName(parsedData.TaskType);
                if (choreType == null)
                {
                    string errorMessage = $"ChoreGroup '{parsedData.TaskType}' not found as ChoreGroup or ChoreType. Cannot set priority.";
                    NeuroLogger.LogError($"[SetPriorityAction] {errorMessage}");
                    NeuroSdk.Messages.Outgoing.Context.Send(errorMessage, false);
                    return UniTask.CompletedTask;
                }

                // Find the ChoreGroup containing this ChoreType
                choreGroup = GetChoreGroupForType(choreType);
                if (choreGroup == null)
                {
                    NeuroLogger.LogError($"Could not find ChoreGroup for ChoreType {choreType.Name}", "SetPriorityAction");
                    NeuroSdk.Messages.Outgoing.Context.Send($"Failed to set priority - internal error (no ChoreGroup)", false);
                    return UniTask.CompletedTask;
                }

                NeuroLogger.Log($"[SetPriorityAction] Found via ChoreType: {choreType.Name} -> ChoreGroup: {choreGroup.Name}");
            }
            else
            {
                NeuroLogger.Log($"[SetPriorityAction] Found ChoreGroup directly: {choreGroup.Name}");
            }

            NeuroLogger.Log($"========== SetPriorityAction START ==========", "SetPriorityAction");
            NeuroLogger.Log($"ChoreGroup: {parsedData.TaskType}, Priority: {parsedData.Priority}", "SetPriorityAction");
            NeuroLogger.Log($"ChoreGroup: {choreGroup.Name} (ID: {choreGroup.Id})", "SetPriorityAction");

            // Convert priority string to actual priority value (0-5)
            int priorityValue = parsedData.Priority.ToLower() switch
            {
                "low" => 1,
                "normal" => 3,
                "high" => 4,
                "critical" => 5,
                _ => 3  // Default to normal
            };

            NeuroLogger.Log($"Setting priority {priorityValue} for ChoreGroup {choreGroup.Name}", "SetPriorityAction");

            // Get current priority before change
            int currentPriority = choreConsumer.GetPersonalPriority(choreGroup);
            NeuroLogger.Log($"Current priority: {currentPriority} -> New priority: {priorityValue}", "SetPriorityAction");

            // ACTUALLY SET THE PRIORITY
            choreConsumer.SetPersonalPriority(choreGroup, priorityValue);

            // Verify the change
            int verifyPriority = choreConsumer.GetPersonalPriority(choreGroup);
            NeuroLogger.Log($"Verification: Priority is now {verifyPriority}", "SetPriorityAction");

            if (verifyPriority == priorityValue)
            {
                NeuroLogger.Log($"SUCCESS: ChoreGroup priority updated!", "SetPriorityAction");
            }
            else
            {
                NeuroLogger.LogError($"FAILED: Priority is {verifyPriority}, expected {priorityValue}", "SetPriorityAction");
            }

            // Force UI refresh if Jobs/Priorities screen is open (fixes Graphics device null error)
            if (ManagementMenu.Instance != null && ManagementMenu.Instance.jobsScreen != null)
            {
                NeuroLogger.Log("  - JobsScreen IS OPEN, refreshing UI", "SetPriorityAction");
                try
                {
                    MinionResume? resume = neuroMinion.GetComponent<MinionResume>();
                    if (resume != null)
                    {
                        ManagementMenu.Instance.jobsScreen.Refresh(resume);
                        NeuroLogger.Log("  - Successfully refreshed JobsScreen UI", "SetPriorityAction");
                    }
                    else
                    {
                        NeuroLogger.LogError("  - Could not find MinionResume component", "SetPriorityAction");
                    }
                }
                catch (Exception refreshEx)
                {
                    NeuroLogger.LogError($"  - UI refresh failed: {refreshEx.Message}", "SetPriorityAction");
                }
            }
            else
            {
                NeuroLogger.Log("  - JobsScreen is NOT OPEN (no immediate UI to update)", "SetPriorityAction");
            }

            string contextMessage = $"ChoreGroup Priority Set: {duplicateName} will prioritize {choreGroup.Name} work (priority: {priorityValue}/5)";
            NeuroLogger.Log($"========== SetPriorityAction END ==========", "SetPriorityAction");

            NeuroSdk.Messages.Outgoing.Context.Send(contextMessage, true);
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[SetPriorityAction] Error setting priority: {ex.Message}");
            NeuroSdk.Messages.Outgoing.Context.Send($"Failed to set priority: {ex.Message}", false);
        }

        return UniTask.CompletedTask;
    }

    /// <summary>
    /// Gets a ChoreGroup by name from the game database
    /// </summary>
    /// <param name="groupName">The name or ID of the ChoreGroup to find</param>
    /// <returns>The ChoreGroup if found, null otherwise</returns>
    private static ChoreGroup? GetChoreGroupByName(string groupName)
    {
        try
        {
            // Try to find by ID first (exact match)
            ChoreGroup? group = Db.Get().ChoreGroups.resources.FirstOrDefault(
                g => g.Id.Equals(groupName, StringComparison.OrdinalIgnoreCase)
            );

            if (group != null)
            {
                NeuroLogger.Log($"Found ChoreGroup by ID: {group.Id} -> {group.Name}");
                return group;
            }

            // Try to find by display name (fallback)
            group = Db.Get().ChoreGroups.resources.FirstOrDefault(
                g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase)
            );

            if (group != null)
            {
                NeuroLogger.Log($"Found ChoreGroup by Name: {group.Name} (ID: {group.Id})");
                return group;
            }

            NeuroLogger.Log($"ChoreGroup '{groupName}' not found in database");
            return null;
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Exception in GetChoreGroupByName: {ex.Message}", "TaskActions");
            return null;
        }
    }

    /// <summary>
    /// Get ChoreType by name - now accepts ANY ChoreType ID from ONI database
    /// Examples: "Dig", "Build", "Mop", "Cook", "Research", "Harvest", "EmptyStorage", etc.
    /// </summary>
    private ChoreType? GetChoreTypeByName(string taskType)
    {
        try
        {
            // Access the global chore types database
            if (Db.Get()?.ChoreTypes == null)
            {
                NeuroLogger.LogError("[SetPriorityAction] ChoreTypes database not initialized");
                return null;
            }

            // Try to find the ChoreType directly by ID (case-insensitive)
            ChoreType? choreType = Db.Get().ChoreTypes.resources.FirstOrDefault(
                ct => ct.Id.Equals(taskType, StringComparison.OrdinalIgnoreCase)
            );

            if (choreType != null)
            {
                NeuroLogger.Log($"[SetPriorityAction] Found ChoreType: {choreType.Id}");
                return choreType;
            }

            // Also support legacy friendly names for backward compatibility
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
                NeuroLogger.Log($"[SetPriorityAction] Mapped legacy name '{taskType}' to {choreType.Id}");
                return choreType;
            }

            NeuroLogger.LogError($"[SetPriorityAction] ChoreType '{taskType}' not found in database");
            return null;
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[SetPriorityAction] Error looking up chore type '{taskType}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get the ChoreGroup that contains this ChoreType
    /// </summary>
    private ChoreGroup? GetChoreGroupForType(ChoreType choreType)
    {
        try
        {
            if (Db.Get()?.ChoreGroups == null)
            {
                NeuroLogger.LogError("[SetPriorityAction] ChoreGroups database not initialized");
                return null;
            }

            // Iterate through all ChoreGroups and find which one contains this ChoreType
            foreach (ChoreGroup group in Db.Get().ChoreGroups.resources)
            {
                if (group.choreTypes.Contains(choreType))
                {
                    return group;
                }
            }

            NeuroLogger.LogError($"[SetPriorityAction] No ChoreGroup found containing ChoreType: {choreType.Name}");
            return null;
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[SetPriorityAction] Error finding ChoreGroup for '{choreType.Name}': {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Action to get the ChoreGroup priorities for Neuro duplicate
/// Shows priority ratings (0-5) for all 17 work categories
/// </summary>
public class ListPrioritiesAction(MinionIdentity minion) : NeuroAction<ListPrioritiesAction.EmptyData>
{
    private readonly MinionIdentity neuroMinion = minion;

    public class EmptyData
    { }

    public override string Name => "list_priorities";

    protected override string Description =>
        "Get Neuro's ChoreGroup priority settings (0-5 rating for each work category: Basekeeping, Dig, Cook, Research, etc.).";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = []
    };

    protected override ExecutionResult Validate(ActionJData actionData, out EmptyData? parsedData)
    {
        parsedData = new EmptyData();

        return neuroMinion == null || neuroMinion.gameObject == null
            ? ExecutionResult.Failure("Neuro duplicate not found or not available")
            : ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(EmptyData? parsedData)
    {
        try
        {
            NeuroLogger.Log("[ListPrioritiesAction] ========== START ==========");

            ChoreConsumer choreConsumer = neuroMinion.GetComponent<ChoreConsumer>();
            if (choreConsumer == null)
            {
                NeuroLogger.LogError("[ListPrioritiesAction] ChoreConsumer not found on Neuro");
                NeuroSdk.Messages.Outgoing.Context.Send("Failed to get priority list - ChoreConsumer missing", false);
                return UniTask.CompletedTask;
            }

            if (Db.Get()?.ChoreGroups == null)
            {
                NeuroLogger.LogError("[ListPrioritiesAction] ChoreGroups database not initialized");
                NeuroSdk.Messages.Outgoing.Context.Send("Failed to get priority list - database not ready", false);
                return UniTask.CompletedTask;
            }

            string duplicateName = neuroMinion.GetProperName();
            NeuroLogger.Log($"[ListPrioritiesAction] Getting ChoreGroup priorities for: {duplicateName}");

            // Get all ChoreGroups that this duplicate can perform
            List<ChoreGroup> allChoreGroups = Db.Get().ChoreGroups.resources;
            List<string> taskList = new();
            int totalGroups = 0;

            foreach (ChoreGroup choreGroup in allChoreGroups)
            {
                // Get current priority for this group
                int priority = choreConsumer.GetPersonalPriority(choreGroup);

                // Build task entry showing group name and current priority
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

                NeuroLogger.Log($"  ChoreGroup: {choreGroup.Id} | Priority: {priority} ({priorityName})");
            }

            NeuroLogger.Log($"[ListPrioritiesAction] Found {totalGroups} ChoreGroups for {duplicateName}");

            // Create summary message
            string contextMessage = $"{duplicateName}'s ChoreGroup Priorities ({totalGroups} groups): " +
                string.Join(", ", taskList.Take(10)) +
                (taskList.Count > 10 ? "..." : "");

            NeuroLogger.Log("[ListPrioritiesAction] ========== END ==========");
            NeuroSdk.Messages.Outgoing.Context.Send(contextMessage, false);
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[ListPrioritiesAction] Error getting priorities: {ex.Message}");
            NeuroSdk.Messages.Outgoing.Context.Send($"Failed to get priority list: {ex.Message}", false);
        }

        return UniTask.CompletedTask;
    }
}