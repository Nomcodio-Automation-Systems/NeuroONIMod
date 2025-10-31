using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Simple Neuro Action to get basic status information for the Neuro duplicate
/// </summary>
public class GetStatusAction(MinionIdentity minion) : NeuroAction<GetStatusAction.StatusQuery>
{
    private readonly MinionIdentity neuroMinion = minion;

    public class StatusQuery
    {
        public string QueryType { get; set; } = "basic";
        public bool IncludeEnvironment { get; set; } = false;
        public bool IncludeSkills { get; set; } = false;
    }

    public override string Name => "get_status";

    protected override string Description =>
        "Get current status information for Neuro duplicate including health, stress, hunger, current task, and optional details like environment and skills.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["query_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = ["basic", "detailed", "minimal"]
            },
            ["include_environment"] = new JsonSchema
            {
                Type = JsonSchemaType.Boolean
            },
            ["include_skills"] = new JsonSchema
            {
                Type = JsonSchemaType.Boolean
            }
        }
    };

    protected override ExecutionResult Validate(ActionJData actionData, out StatusQuery? parsedData)
    {
        parsedData = null;

        // Validate minion exists and hasn't been destroyed
        if (neuroMinion == null || neuroMinion.gameObject == null)
        {
            return ExecutionResult.Failure("Neuro duplicate not found or not available");
        }

        // Validate and parse input data
        if (actionData.Data == null)
        {
            // Use defaults if no data provided
            parsedData = new StatusQuery();
            return ExecutionResult.Success();
        }

        // Parse query type
        string? queryType = actionData.Data["query_type"]?.Value<string>();
        if (queryType is not null and not "basic" and not "detailed" and not "minimal")
        {
            return ExecutionResult.Failure("Invalid parameter 'query_type'. Must be 'basic', 'detailed', or 'minimal'.");
        }

        // Parse boolean options
        bool includeEnvironment = actionData.Data["include_environment"]?.Value<bool>() ?? false;
        bool includeSkills = actionData.Data["include_skills"]?.Value<bool>() ?? false;

        parsedData = new StatusQuery
        {
            QueryType = queryType ?? "basic",
            IncludeEnvironment = includeEnvironment,
            IncludeSkills = includeSkills
        };

        return ExecutionResult.Success();
    }

    protected override UniTask ExecuteAsync(StatusQuery? parsedData)
    {
        // Double-check minion is still valid at execution time
        if (parsedData == null || neuroMinion == null || neuroMinion.gameObject == null)
        {
            NeuroLogger.LogError("[GetStatusAction] Neuro duplicate became unavailable during action execution");
            NeuroSdk.Messages.Outgoing.Context.Send("Cannot get status - Neuro duplicate is no longer available", false);
            return UniTask.CompletedTask;
        }

        try
        {
            DuplicateBioData bioData = new(neuroMinion);

            string statusMessage = BuildStatusMessage(bioData, parsedData);

            NeuroSdk.Messages.Outgoing.Context.Send(statusMessage, false);

            NeuroLogger.Log($"[GetStatusAction] Retrieved {parsedData.QueryType} status for {neuroMinion.GetProperName()}");
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[GetStatusAction] Error retrieving status: {ex.Message}");
        }

        return UniTask.CompletedTask;
    }

    private string BuildStatusMessage(DuplicateBioData bioData, StatusQuery query)
    {
        string message = $"Status Report for {neuroMinion.GetProperName()}:\n";

        // Always include basic stats
        message += $"Health: {bioData.HealthPercentage:P1} ({bioData.HealthState})\n";
        message += $"Stress: {bioData.StressPercentage:P1}\n";
        message += $"Calories: {bioData.CaloriePercentage:P1}\n";

        // Include stamina for basic and detailed
        if (query.QueryType != "minimal")
        {
            message += $"Stamina: {bioData.StaminaPercentage:P1}\n";
        }

        // Add location data (always included unless minimal)
        if (query.QueryType != "minimal")
        {
            message += AddLocationInfo();
        }

        // Add current activity
        ChoreConsumer choreConsumer = neuroMinion.GetComponent<ChoreConsumer>();
        if (choreConsumer?.choreDriver.HasChore() == true)
        {
            Chore currentChore = choreConsumer.choreDriver.GetCurrentChore();
            if (currentChore != null)
            {
                message += $"Current Task: {currentChore.choreType.Name}";

                // Add priority for detailed
                if (query.QueryType == "detailed")
                {
                    message += $" (Priority: {currentChore.masterPriority.priority_value})";
                }
                message += "\n";
            }
        }
        else
        {
            message += "Status: Idle\n";
        }

        // Add environment data if requested
        if (query.IncludeEnvironment && query.QueryType != "minimal")
        {
            message += AddEnvironmentInfo();
        }

        // Add skills if requested
        if (query.IncludeSkills && query.QueryType == "detailed")
        {
            message += AddSkillsInfo();
        }

        return message.TrimEnd('\n');
    }

    private string AddLocationInfo()
    {
        try
        {
            // Validate minion exists and has a transform
            if (neuroMinion == null || neuroMinion.transform == null)
            {
                return "Location: Data unavailable (minion not found)\n";
            }

            Vector3 worldPos = neuroMinion.transform.position;
            int cell = Grid.PosToCell(worldPos);

            // Validate cell is valid
            if (!Grid.IsValidCell(cell))
            {
                return "Location: Data unavailable (invalid cell)\n";
            }

            // Get grid coordinates (cell position)
            int gridX = Grid.CellToXY(cell).x;
            int gridY = Grid.CellToXY(cell).y;

            string locationInfo = $"Location: Grid ({gridX}, {gridY})";

            // Try to get camera/screen position
            try
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
                    locationInfo += $", Screen ({screenPos.x:F0}, {screenPos.y:F0})";
                }
            }
            catch (System.Exception cameraEx)
            {
                // Camera conversion failed, continue without screen coords
                NeuroLogger.LogWarning($"[GetStatusAction] Camera conversion failed: {cameraEx.Message}");
            }

            // Add room information
            try
            {
                if (Game.Instance != null && Game.Instance.roomProber != null && neuroMinion.gameObject != null)
                {
                    Room room = Game.Instance.roomProber.GetRoomOfGameObject(neuroMinion.gameObject);
                    if (room != null && room.roomType != null)
                    {
                        locationInfo += $", Room: {room.roomType.Name}";
                    }
                    else
                    {
                        locationInfo += ", Room: Outside/None";
                    }
                }
                else
                {
                    locationInfo += ", Room: Unknown";
                }
            }
            catch (System.Exception roomEx)
            {
                // Room detection failed, continue without room info
                NeuroLogger.LogWarning($"[GetStatusAction] Room detection failed: {roomEx.Message}");
                locationInfo += ", Room: Unknown";
            }

            return locationInfo + "\n";
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[GetStatusAction] Error getting location: {ex.Message}");
            return "Location: Data unavailable\n";
        }
    }

    private string AddEnvironmentInfo()
    {
        try
        {
            int cell = Grid.PosToCell(neuroMinion.transform.position);
            float temperature = Grid.Temperature[cell];

            string envInfo = $"Environment: Temperature {temperature:F1}°C";

            Room room = Game.Instance.roomProber.GetRoomOfGameObject(neuroMinion.gameObject);
            if (room != null)
            {
                envInfo += $", Room: {room.roomType.Name}";
            }

            return envInfo + "\n";
        }
        catch
        {
            return "Environment: Data unavailable\n";
        }
    }

    private string AddSkillsInfo()
    {
        try
        {
            MinionResume resumeSkill = neuroMinion.GetComponent<MinionResume>();
            if (resumeSkill != null)
            {
                int totalSkillPoints = (int)resumeSkill.TotalExperienceGained;
                int availablePoints = resumeSkill.AvailableSkillpoints;
                return $"Skills: {totalSkillPoints} total XP, {availablePoints} available skill points\n";
            }
        }
        catch
        {
            // Ignore skill errors
        }

        return "";
    }
}