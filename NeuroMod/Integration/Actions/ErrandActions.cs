using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Action to list available errands (chores) for the Neuro duplicate
/// Shows actual work items in the world, not priority settings
/// </summary>
public class ListErrandsAction(MinionIdentity minion) : NeuroAction<ListErrandsAction.ErrandFilter>
{
    private readonly MinionIdentity _neuroMinion = minion;

    public class ErrandFilter
    {
        public string FilterType { get; set; } = "nearby"; // all, nearby, priority, unassigned
        public int MaxDistance { get; set; } = 50;
        public int MaxResults { get; set; } = 20;
        public List<string> ChoreTypes { get; set; } = [];
    }

    public override string Name => "list_errands";

    protected override string Description =>
        "List available errands (actual work items) that the duplicate can perform. " +
        "This shows specific tasks in the world like 'Mop tile at (25,10)' or 'Build ladder at (30,15)', " +
        "not the priority settings for work categories.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["filter_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = ["all", "nearby", "priority", "unassigned"]
            },
            ["max_distance"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            },
            ["max_results"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            },
            ["chore_types"] = new JsonSchema
            {
                Type = JsonSchemaType.Array,
                Items = new JsonSchema { Type = JsonSchemaType.String }
            }
        }
    };

    protected override ExecutionResult Validate(
        ActionJData actionData,
        out ErrandFilter? parsedData
    )
    {
        parsedData = null;

        if (_neuroMinion == null || _neuroMinion.gameObject == null)
        {
            return ExecutionResult.Failure("Neuro duplicate not found or not available");
        }

        // Parse filter parameters
        string filterType = actionData.Data?["filter_type"]?.Value<string>() ?? "nearby";
        int maxDistance = actionData.Data?["max_distance"]?.Value<int>() ?? 50;
        int maxResults = actionData.Data?["max_results"]?.Value<int>() ?? 20;

        // Validate max_results
        if (maxResults is < 1 or > 100)
        {
            return ExecutionResult.Failure("max_results must be between 1 and 100");
        }

        // Parse chore types filter
        List<string> choreTypes = [];
        if (actionData.Data?["chore_types"] is JArray choreTypesArray)
        {
            foreach (JToken token in choreTypesArray)
            {
                string? choreType = token.Value<string>();
                if (!string.IsNullOrEmpty(choreType))
                {
                    choreTypes.Add(choreType!);
                }
            }
        }

        parsedData = new ErrandFilter
        {
            FilterType = filterType,
            MaxDistance = maxDistance,
            MaxResults = maxResults,
            ChoreTypes = choreTypes
        };

        return ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(ErrandFilter? parsedData)
    {
        if (parsedData == null || _neuroMinion == null || _neuroMinion.gameObject == null)
        {
            NeuroLogger.LogError("[ListErrandsAction] Invalid state during execution");
            return UniTask.CompletedTask;
        }

        try
        {
            NeuroLogger.Log("========== ListErrandsAction START ==========", "ListErrandsAction");
            NeuroLogger.Log($"Filter: {parsedData.FilterType}, MaxDistance: {parsedData.MaxDistance}, MaxResults: {parsedData.MaxResults}", "ListErrandsAction");

            ChoreConsumer? choreConsumer = _neuroMinion.GetComponent<ChoreConsumer>();
            if (choreConsumer == null)
            {
                NeuroLogger.LogError("ChoreConsumer not found", "ListErrandsAction");
                NeuroSdk.Messages.Outgoing.Context.Send("Cannot list errands - ChoreConsumer not found", false);
                return UniTask.CompletedTask;
            }

            Vector3 minionPosition = _neuroMinion.transform.position;
            NeuroLogger.Log($"Minion position: ({minionPosition.x:F1}, {minionPosition.y:F1})", "ListErrandsAction");

            // Get all chores from the global chore provider
            List<ErrandInfo> errands = [];

            if (GlobalChoreProvider.Instance != null)
            {
                // Iterate through all chores in the choreWorldMap
                foreach (KeyValuePair<int, List<Chore>> kvp in GlobalChoreProvider.Instance.choreWorldMap)
                {
                    foreach (Chore chore in kvp.Value)
                    {
                        if (chore == null || chore.target == null || chore.isNull)
                        {
                            continue;
                        }

                        try
                        {
                            // Calculate distance
                            Vector3 chorePosition = chore.target.transform.position;
                            float distance = Vector3.Distance(minionPosition, chorePosition);

                            // Apply distance filter
                            if (parsedData.FilterType == "nearby" && distance > parsedData.MaxDistance)
                            {
                                continue;
                            }

                            // Apply chore type filter
                            if (parsedData.ChoreTypes.Count > 0)
                            {
                                bool matchesType = parsedData.ChoreTypes.Any(ct =>
                                    chore.choreType.Id.Equals(ct, StringComparison.OrdinalIgnoreCase) ||
                                    chore.choreType.Name.Equals(ct, StringComparison.OrdinalIgnoreCase)
                                );
                                if (!matchesType)
                                {
                                    continue;
                                }
                            }

                            // Apply priority filter
                            if (parsedData.FilterType == "priority" && chore.masterPriority.priority_value < 7)
                            {
                                continue;
                            }

                            // Apply unassigned filter
                            if (parsedData.FilterType == "unassigned" && chore.driver != null)
                            {
                                continue;
                            }

                            // Check if this duplicate can perform the chore
                            ChoreGroup? choreGroup = GetChoreGroup(chore.choreType);
                            bool canPerform = choreGroup != null && choreConsumer.IsPermittedByUser(choreGroup);

                            ErrandInfo info = new()
                            {
                                ChoreType = chore.choreType.Name,
                                ChoreGroup = choreGroup?.Name ?? "Unknown",
                                Description = GetChoreDescription(chore),
                                LocationX = (int)chorePosition.x,
                                LocationY = (int)chorePosition.y,
                                Distance = distance,
                                Priority = chore.masterPriority.priority_value,
                                AssignedTo = chore.driver?.GetComponent<MinionIdentity>()?.GetProperName() ?? "",
                                CanPerform = canPerform
                            };

                            errands.Add(info);

                            // Stop if we hit max results
                            if (errands.Count >= parsedData.MaxResults)
                            {
                                goto done_collecting;
                            }
                        }
                        catch (Exception ex)
                        {
                            NeuroLogger.LogError($"Error processing chore: {ex.Message}", "ListErrandsAction");
                        }
                    }
                }
            }
        done_collecting:

            // Sort by distance (closest first)
            errands = errands.OrderBy(e => e.Distance).ToList();

            NeuroLogger.Log($"Found {errands.Count} matching errands", "ListErrandsAction");

            // Build context message
            if (errands.Count == 0)
            {
                string message = $"No errands found matching filter '{parsedData.FilterType}'";
                NeuroLogger.Log(message, "ListErrandsAction");
                NeuroSdk.Messages.Outgoing.Context.Send(message, false);
            }
            else
            {
                // Show summary
                string summary = $"Found {errands.Count} errands:\n";
                int shown = Math.Min(5, errands.Count);
                for (int i = 0; i < shown; i++)
                {
                    ErrandInfo e = errands[i];
                    summary += $"  - {e.ChoreType} at ({e.LocationX},{e.LocationY}) - {e.Distance:F1} tiles away";
                    if (!string.IsNullOrEmpty(e.AssignedTo))
                    {
                        summary += $" (assigned to {e.AssignedTo})";
                    }
                    summary += "\n";
                }
                if (errands.Count > shown)
                {
                    summary += $"  ... and {errands.Count - shown} more";
                }

                NeuroLogger.Log(summary, "ListErrandsAction");
                NeuroSdk.Messages.Outgoing.Context.Send(summary, true);
            }

            NeuroLogger.Log("========== ListErrandsAction END ==========", "ListErrandsAction");
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Error listing errands: {ex.Message}", "ListErrandsAction");
            NeuroLogger.LogError($"Stack trace: {ex.StackTrace}", "ListErrandsAction");
        }

        return UniTask.CompletedTask;
    }

    private static ChoreGroup? GetChoreGroup(ChoreType choreType)
    {
        foreach (ChoreGroup group in Db.Get().ChoreGroups.resources)
        {
            if (group.choreTypes.Contains(choreType))
            {
                return group;
            }
        }
        return null;
    }

    private static string GetChoreDescription(Chore chore)
    {
        try
        {
            if (chore.target != null)
            {
                string targetName = chore.target.name;
                return $"{chore.choreType.Name} {targetName}";
            }
            return chore.choreType.Name;
        }
        catch
        {
            return chore.choreType.Name;
        }
    }

    private class ErrandInfo
    {
        public string ChoreType { get; set; } = "";
        public string ChoreGroup { get; set; } = "";
        public string Description { get; set; } = "";
        public int LocationX { get; set; }
        public int LocationY { get; set; }
        public float Distance { get; set; }
        public int Priority { get; set; }
        public string AssignedTo { get; set; } = "";
        public bool CanPerform { get; set; }
    }
}

/// <summary>
/// Action to get information about the duplicate's current errand
/// </summary>
public class GetCurrentErrandAction(MinionIdentity minion) : NeuroAction<GetCurrentErrandAction.EmptyData>
{
    private readonly MinionIdentity _neuroMinion = minion;

    public class EmptyData
    { }

    public override string Name => "get_current_errand";

    protected override string Description =>
        "Get detailed information about what errand (task) the duplicate is currently performing.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = []
    };

    protected override ExecutionResult Validate(
        ActionJData actionData,
        out EmptyData? parsedData
    )
    {
        parsedData = new EmptyData();

        return _neuroMinion == null || _neuroMinion.gameObject == null
            ? ExecutionResult.Failure("Neuro duplicate not found or not available")
            : ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(EmptyData? parsedData)
    {
        if (_neuroMinion == null || _neuroMinion.gameObject == null)
        {
            NeuroLogger.LogError("[GetCurrentErrandAction] Invalid state during execution");
            return UniTask.CompletedTask;
        }

        try
        {
            NeuroLogger.Log("========== GetCurrentErrandAction START ==========", "GetCurrentErrandAction");

            ChoreConsumer? choreConsumer = _neuroMinion.GetComponent<ChoreConsumer>();
            if (choreConsumer == null)
            {
                NeuroLogger.LogError("ChoreConsumer not found", "GetCurrentErrandAction");
                NeuroSdk.Messages.Outgoing.Context.Send("Cannot get current errand - ChoreConsumer not found", false);
                return UniTask.CompletedTask;
            }

            bool hasChore = choreConsumer.choreDriver.HasChore();
            NeuroLogger.Log($"Has current chore: {hasChore}", "GetCurrentErrandAction");

            if (!hasChore)
            {
                string message = $"{_neuroMinion.GetProperName()} is currently idle (no active errand)";
                NeuroLogger.Log(message, "GetCurrentErrandAction");
                NeuroSdk.Messages.Outgoing.Context.Send(message, false);
            }
            else
            {
                Chore? currentChore = choreConsumer.choreDriver.GetCurrentChore();
                if (currentChore != null)
                {
                    string choreType = currentChore.choreType.Name;
                    ChoreGroup? choreGroup = GetChoreGroup(currentChore.choreType);
                    string groupName = choreGroup?.Name ?? "Unknown";

                    Vector3 targetPos = currentChore.target != null
                        ? currentChore.target.transform.position
                        : Vector3.zero;

                    int priority = currentChore.masterPriority.priority_value;

                    string message = $"{_neuroMinion.GetProperName()} is currently doing: {choreType} ({groupName})\n" +
                        $"Location: ({(int)targetPos.x}, {(int)targetPos.y})\n" +
                        $"Priority: {priority}/9";

                    NeuroLogger.Log($"Current errand: {choreType} at ({(int)targetPos.x},{(int)targetPos.y})", "GetCurrentErrandAction");
                    NeuroLogger.Log($"ChoreGroup: {groupName}, Priority: {priority}", "GetCurrentErrandAction");

                    NeuroSdk.Messages.Outgoing.Context.Send(message, true);
                }
                else
                {
                    NeuroLogger.LogError("HasChore returned true but GetCurrentChore returned null", "GetCurrentErrandAction");
                }
            }

            NeuroLogger.Log("========== GetCurrentErrandAction END ==========", "GetCurrentErrandAction");
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Error getting current errand: {ex.Message}", "GetCurrentErrandAction");
            NeuroLogger.LogError($"Stack trace: {ex.StackTrace}", "GetCurrentErrandAction");
        }

        return UniTask.CompletedTask;
    }

    private static ChoreGroup? GetChoreGroup(ChoreType choreType)
    {
        foreach (ChoreGroup group in Db.Get().ChoreGroups.resources)
        {
            if (group.choreTypes.Contains(choreType))
            {
                return group;
            }
        }
        return null;
    }
}

/// <summary>
/// Action to boost the priority of a specific ChoreGroup to maximum
/// Finds the nearest available errand of the specified type and boosts its ChoreGroup to priority 5 (critical)
/// </summary>
public class AssignErrandAction(MinionIdentity minion) : NeuroAction<AssignErrandAction.AssignData>
{
    private readonly MinionIdentity _neuroMinion = minion;

    public class AssignData
    {
        public string? ErrandType { get; set; }
        public int MaxDistance { get; set; } = 50;
        public int? TargetX { get; set; }
        public int? TargetY { get; set; }
    }

    public override string Name => "assign_errand";

    protected override string Description =>
        "Boost the priority of a specific ChoreGroup to maximum (5). " +
        "Finds the nearest available errand of the specified type and sets its ChoreGroup priority to critical. " +
        "Use set_priority to manually adjust priorities later if needed.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Required = ["errand_type"],
        Properties = new Dictionary<string, JsonSchema>
        {
            ["errand_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String
            },
            ["max_distance"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            },
            ["target_x"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            },
            ["target_y"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            }
        }
    };

    protected override ExecutionResult Validate(ActionJData actionData, out AssignData? parsedData)
    {
        parsedData = new AssignData
        {
            ErrandType = actionData.Data?["errand_type"]?.Value<string>(),
            MaxDistance = actionData.Data?["max_distance"]?.Value<int>() ?? 50,
            TargetX = actionData.Data?["target_x"]?.Value<int>(),
            TargetY = actionData.Data?["target_y"]?.Value<int>()
        };

        if (string.IsNullOrEmpty(parsedData.ErrandType))
        {
            return ExecutionResult.Failure("errand_type is required");
        }

        return _neuroMinion == null || _neuroMinion.gameObject == null
            ? ExecutionResult.Failure("Neuro duplicate not found")
            : ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(AssignData? parsedData)
    {
        if (parsedData == null || _neuroMinion == null)
        {
            NeuroSdk.Messages.Outgoing.Context.Send("Failed to boost priority - invalid data", false);
            return UniTask.CompletedTask;
        }

        try
        {
            NeuroLogger.Log($"========== AssignErrandAction START ==========", "AssignErrand");
            NeuroLogger.Log($"Errand type: {parsedData.ErrandType}, Max distance: {parsedData.MaxDistance}", "AssignErrand");

            ChoreConsumer? choreConsumer = _neuroMinion.GetComponent<ChoreConsumer>();
            if (choreConsumer == null)
            {
                string errorMsg = "ChoreConsumer not found on Neuro";
                NeuroLogger.LogError(errorMsg, "AssignErrand");
                NeuroSdk.Messages.Outgoing.Context.Send($"Failed to boost priority: {errorMsg}", false);
                return UniTask.CompletedTask;
            }

            // Find matching chore type
            ChoreType? choreType = FindChoreType(parsedData.ErrandType!);
            if (choreType == null)
            {
                string errorMsg = $"Chore type '{parsedData.ErrandType}' not found";
                NeuroLogger.LogError(errorMsg, "AssignErrand");
                NeuroSdk.Messages.Outgoing.Context.Send($"Failed to boost priority: {errorMsg}", false);
                return UniTask.CompletedTask;
            }

            // Find the ChoreGroup containing this chore type
            ChoreGroup? choreGroup = GetChoreGroup(choreType);
            if (choreGroup == null)
            {
                string errorMsg = $"ChoreGroup not found for chore type '{parsedData.ErrandType}'";
                NeuroLogger.LogError(errorMsg, "AssignErrand");
                NeuroSdk.Messages.Outgoing.Context.Send($"Failed to boost priority: {errorMsg}", false);
                return UniTask.CompletedTask;
            }

            // Find nearby chore of this type (just for verification and better feedback)
            Chore? targetChore = FindNearestChore(choreType, parsedData);
            if (targetChore == null)
            {
                string errorMsg = $"No available {parsedData.ErrandType} errands found within {parsedData.MaxDistance} tiles";
                NeuroLogger.LogError(errorMsg, "AssignErrand");
                NeuroSdk.Messages.Outgoing.Context.Send(errorMsg, false);
                return UniTask.CompletedTask;
            }

            // Get chore location for logging
            Vector3 chorePos = targetChore.target.transform.position;
            Vector3 neuroPos = _neuroMinion.transform.position;
            float distance = Vector3.Distance(neuroPos, chorePos);

            // Get current priority
            int oldPriority = choreConsumer.GetPersonalPriority(choreGroup);

            // Boost priority to maximum (5 = critical)
            const int maxPriority = 5;
            choreConsumer.SetPersonalPriority(choreGroup, maxPriority);

            string description = $"{parsedData.ErrandType} at ({chorePos.x:F0}, {chorePos.y:F0}) - {distance:F1} tiles away";
            string contextMsg = $"Boosted {choreGroup.Name} priority from {oldPriority} to {maxPriority}. Nearest errand: {description}";

            NeuroLogger.Log(contextMsg, "AssignErrand");
            NeuroSdk.Messages.Outgoing.Context.Send(contextMsg, true);

            NeuroLogger.Log($"========== AssignErrandAction END ==========", "AssignErrand");
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Error boosting priority: {ex.Message}", "AssignErrand");
            NeuroLogger.LogError($"Stack trace: {ex.StackTrace}", "AssignErrand");
            NeuroSdk.Messages.Outgoing.Context.Send($"Failed to boost priority: {ex.Message}", false);
        }

        return UniTask.CompletedTask;
    }

    private Chore? FindNearestChore(ChoreType choreType, AssignData parsedData)
    {
        if (GlobalChoreProvider.Instance == null)
        {
            return null;
        }

        Vector3 neuroPos = _neuroMinion.transform.position;
        Chore? nearestChore = null;
        float nearestDistance = float.MaxValue;

        // Check if we have a specific target location
        bool hasTargetLocation = parsedData.TargetX.HasValue && parsedData.TargetY.HasValue;
        Vector3 targetPos = hasTargetLocation
            ? new Vector3(parsedData.TargetX!.Value, parsedData.TargetY!.Value, 0)
            : neuroPos;

        foreach (KeyValuePair<int, List<Chore>> kvp in GlobalChoreProvider.Instance.choreWorldMap)
        {
            foreach (Chore chore in kvp.Value)
            {
                if (chore == null || chore.target == null || chore.isComplete)
                {
                    continue;
                }

                // Check chore type
                if (chore.choreType != choreType)
                {
                    continue;
                }

                // Check if assigned
                if (chore.driver != null)
                {
                    continue; // Skip already assigned chores
                }

                Vector3 chorePos = chore.target.transform.position;
                float distance = Vector3.Distance(targetPos, chorePos);

                // Check distance
                if (distance > parsedData.MaxDistance)
                {
                    continue;
                }

                // Track nearest
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestChore = chore;
                }
            }
        }

        if (nearestChore != null)
        {
            NeuroLogger.Log($"Found nearest chore: {choreType.Name} at distance {nearestDistance:F1}", "AssignErrand");
        }

        return nearestChore;
    }

    private static ChoreType? FindChoreType(string typeName)
    {
        return Db.Get()?.ChoreTypes == null
            ? null
            : Db.Get().ChoreTypes.resources.FirstOrDefault(
            ct => ct.Id.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                  ct.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static ChoreGroup? GetChoreGroup(ChoreType choreType)
    {
        if (Db.Get()?.ChoreGroups == null)
        {
            return null;
        }

        foreach (ChoreGroup group in Db.Get().ChoreGroups.resources)
        {
            if (group.choreTypes.Contains(choreType))
            {
                return group;
            }
        }
        return null;
    }
}