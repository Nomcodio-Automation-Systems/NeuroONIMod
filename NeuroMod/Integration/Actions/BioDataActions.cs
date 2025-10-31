using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Utilities;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeuroMod;

/// <summary>
/// Advanced bio data retrieval action with filtering and detailed reporting capabilities
/// </summary>
public class GetBioDataAction : BaseNeuroAction
{
    public override string Name => "get_biodata";
    protected override string Description => "Advanced bio data retrieval with filtering and detailed reporting capabilities";

    protected override JsonSchema? Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["data_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = ["health", "nutrition", "stress", "environment", "skills", "all"]
            },
            ["detail_level"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = ["basic", "detailed", "full"]
            },
            ["include_history"] = new JsonSchema
            {
                Type = JsonSchemaType.Boolean
            },
            ["format"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = ["text", "json", "structured"]
            }
        },
        Required = ["data_type"]
    };

    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;

        try
        {
            MinionIdentity? minion = NeuroIntegrationBridge.Instance?.GetNeuroMinion();
            if (minion == null)
            {
                return ExecutionResult.Failure("No duplicant connected to Neuro system.");
            }

            // Parse and validate input parameters
            string dataType = "all";
            string detailLevel = "basic";
            bool includeHistory = false;
            string format = "text";

            if (actionData.Data is not null and JObject dataObj)
            {
                // Validate and extract data_type
                if (dataObj["data_type"] != null)
                {
                    string? requestedType = dataObj["data_type"]?.Value<string>()?.ToLower();
                    string[] validTypes = ["health", "nutrition", "stress", "environment", "skills", "all"];
                    if (!string.IsNullOrEmpty(requestedType) && validTypes.Contains(requestedType))
                    {
                        dataType = requestedType!;
                    }
                    else
                    {
                        return ExecutionResult.Failure($"Invalid data_type '{requestedType}'. Valid options: {string.Join(", ", validTypes)}");
                    }
                }

                // Validate and extract detail_level
                if (dataObj["detail_level"] != null)
                {
                    string? requestedLevel = dataObj["detail_level"]?.Value<string>()?.ToLower();
                    string[] validLevels = ["basic", "detailed", "full"];
                    if (!string.IsNullOrEmpty(requestedLevel) && validLevels.Contains(requestedLevel))
                    {
                        detailLevel = requestedLevel!;
                    }
                    else
                    {
                        return ExecutionResult.Failure($"Invalid detail_level '{requestedLevel}'. Valid options: {string.Join(", ", validLevels)}");
                    }
                }

                // Extract boolean parameters
                if (dataObj["include_history"] != null)
                {
                    includeHistory = dataObj["include_history"]?.Value<bool>() ?? false;
                }

                // Validate and extract format
                if (dataObj["format"] != null)
                {
                    string? requestedFormat = dataObj["format"]?.Value<string>()?.ToLower();
                    string[] validFormats = ["text", "json", "structured"];
                    if (!string.IsNullOrEmpty(requestedFormat) && validFormats.Contains(requestedFormat))
                    {
                        format = requestedFormat!;
                    }
                    else
                    {
                        return ExecutionResult.Failure($"Invalid format '{requestedFormat}'. Valid options: {string.Join(", ", validFormats)}");
                    }
                }
            }

            parsedData = new BioDataQueryData
            {
                Minion = minion,
                DataType = dataType,
                DetailLevel = detailLevel,
                IncludeHistory = includeHistory,
                Format = format
            };

            return ExecutionResult.Success("Bio data query validated successfully");
        }
        catch (Exception ex)
        {
            return ExecutionResult.Failure($"Error validating bio data query: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(object? data)
    {
        try
        {
            if (data is not BioDataQueryData queryData)
            {
                NeuroSdk.Messages.Outgoing.Context.Send("Invalid bio data query parameters", false);
                return UniTask.CompletedTask;
            }

            DuplicateBioData bioData = new(queryData.Minion);

            // Generate bio data response based on parameters
            string response = GenerateBioDataResponse(queryData.Minion, bioData,
                queryData.DataType, queryData.DetailLevel, queryData.IncludeHistory, queryData.Format);

            NeuroSdk.Messages.Outgoing.Context.Send(response, false);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetBioDataAction] Error executing bio data query: {ex.Message}");
            NeuroSdk.Messages.Outgoing.Context.Send($"Error retrieving bio data: {ex.Message}", false);
        }

        return UniTask.CompletedTask;
    }

    private string GenerateBioDataResponse(MinionIdentity minion, DuplicateBioData bioData,
        string dataType, string detailLevel, bool includeHistory, string format)
    {
        Dictionary<string, object> responseData = [];

        // Collect data based on requested type
        if (dataType is "health" or "all")
        {
            Dictionary<string, object> healthData = new()
            {
                ["health_percentage"] = Math.Round(bioData.HealthPercentage * 100, 1),
                ["is_sick"] = bioData.IsSick,
                ["oxygen_percentage"] = Math.Round(bioData.OxygenPercentage * 100, 1)
            };

            if (detailLevel != "basic")
            {
                healthData["health_status"] = GetHealthStatus(bioData.HealthPercentage);
                healthData["oxygen_status"] = GetOxygenStatus(bioData.OxygenPercentage);
            }

            responseData["health"] = healthData;
        }

        if (dataType is "nutrition" or "all")
        {
            Dictionary<string, object> nutritionData = new()
            {
                ["calorie_percentage"] = Math.Round(bioData.CaloriePercentage * 100, 1),
            };

            if (detailLevel != "basic")
            {
                nutritionData["nutrition_status"] = GetNutritionStatus(bioData.CaloriePercentage);
                nutritionData["hunger_level"] = GetHungerLevel(bioData.CaloriePercentage);
            }

            responseData["nutrition"] = nutritionData;
        }

        if (dataType is "stress" or "all")
        {
            Dictionary<string, object> stressData = new()
            {
                ["stress_percentage"] = Math.Round(bioData.StressPercentage * 100, 1)
            };

            if (detailLevel != "basic")
            {
                stressData["stress_level"] = GetStressLevel(bioData.StressPercentage);
                stressData["mental_break_risk"] = GetMentalBreakRisk(bioData.StressPercentage);
            }

            responseData["stress"] = stressData;
        }

        if (dataType is "environment" or "all")
        {
            Dictionary<string, object> envData = new()
            {
                ["body_temperature"] = Math.Round(bioData.BodyTemperature, 1),
                ["is_overheating"] = bioData.IsOverheating,
                ["is_freezing"] = bioData.IsFreezing
            };

            if (detailLevel != "basic")
            {
                envData["environment_status"] = GetEnvironmentStatus(bioData);
            }

            responseData["environment"] = envData;
        }

        if (dataType is "skills" or "all")
        {
            Dictionary<string, object> skillsData = new()
            {
                ["skills_note"] = "Skills data available through game systems"
            };

            if (detailLevel != "basic")
            {
                skillsData["detailed_skills"] = "Detailed skill information would be implemented here";
            }

            responseData["skills"] = skillsData;
        }

        // Add metadata
        responseData["duplicant_name"] = minion.GetProperName();
        responseData["timestamp"] = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        if (includeHistory && detailLevel != "basic")
        {
            responseData["history_note"] = "Bio data trends and history tracking would be implemented here";
        }

        // Format response
        return FormatBioDataResponse(responseData, format, detailLevel);
    }

    private string FormatBioDataResponse(Dictionary<string, object> data, string format, string detailLevel)
    {
        return format switch
        {
            "json" => Jason.Serialize(data),
            "structured" => FormatStructuredResponse(data, detailLevel),
            _ => FormatTextResponse(data, detailLevel),
        };
    }

    private string FormatTextResponse(Dictionary<string, object> data, string detailLevel)
    {
        StringBuilder response = new();
        response.AppendLine($"Bio Data Report for {data["duplicant_name"]} ({data["timestamp"]})");
        response.AppendLine(new string('=', 50));

        if (data.ContainsKey("health"))
        {
            Dictionary<string, object> health = (Dictionary<string, object>)data["health"];
            response.AppendLine($"Health: {health["health_percentage"]}%");
            if (detailLevel != "basic" && health.ContainsKey("health_status"))
            {
                response.AppendLine($"  Status: {health["health_status"]}");
            }
            response.AppendLine($"Oxygen: {health["oxygen_percentage"]}%");
            response.AppendLine($"Sick: {health["is_sick"]}");
            response.AppendLine();
        }

        if (data.ContainsKey("nutrition"))
        {
            Dictionary<string, object> nutrition = (Dictionary<string, object>)data["nutrition"];
            response.AppendLine($"Calories: {nutrition["calorie_percentage"]}%");
            if (detailLevel != "basic" && nutrition.ContainsKey("nutrition_status"))
            {
                response.AppendLine($"  Status: {nutrition["nutrition_status"]}");
            }
            response.AppendLine();
        }

        if (data.ContainsKey("stress"))
        {
            Dictionary<string, object> stress = (Dictionary<string, object>)data["stress"];
            response.AppendLine($"Stress: {stress["stress_percentage"]}%");
            if (detailLevel != "basic" && stress.ContainsKey("stress_level"))
            {
                response.AppendLine($"  Level: {stress["stress_level"]}");
                response.AppendLine($"  Mental Break Risk: {stress["mental_break_risk"]}");
            }
            response.AppendLine();
        }

        if (data.ContainsKey("environment"))
        {
            Dictionary<string, object> env = (Dictionary<string, object>)data["environment"];
            response.AppendLine($"Body Temperature: {env["body_temperature"]}°K");
            response.AppendLine($"Overheating: {env["is_overheating"]}");
            response.AppendLine($"Freezing: {env["is_freezing"]}");
            response.AppendLine();
        }

        return response.ToString().Trim();
    }

    private string FormatStructuredResponse(Dictionary<string, object> data, string detailLevel)
    {
        StringBuilder response = new();
        response.AppendLine("=== DUPLICANT BIO DATA REPORT ===");
        response.AppendLine($"Name: {data["duplicant_name"]}");
        response.AppendLine($"Time: {data["timestamp"]}");
        response.AppendLine();

        foreach (KeyValuePair<string, object> section in data.Where(kvp => kvp.Key is not "duplicant_name" and not "timestamp"))
        {
            if (section.Value is Dictionary<string, object> sectionData)
            {
                response.AppendLine($"[{section.Key.ToUpper()}]");
                foreach (KeyValuePair<string, object> item in sectionData)
                {
                    response.AppendLine($"  {item.Key}: {item.Value}");
                }
                response.AppendLine();
            }
        }

        return response.ToString().Trim();
    }

    private string GetEnvironmentStatus(DuplicateBioData bioData)
    {
        return bioData.IsOverheating ? "Overheating" : bioData.IsFreezing ? "Freezing" : "Normal";
    }

    // Helper methods for status descriptions
    private string GetHealthStatus(float healthPercentage)
    {
        return healthPercentage < 0.3f ? "Critical" : healthPercentage < 0.5f ? "Poor" : healthPercentage < 0.8f ? "Fair" : "Good";
    }

    private string GetOxygenStatus(float oxygenPercentage)
    {
        return oxygenPercentage < 0.3f ? "Suffocating" : oxygenPercentage < 0.6f ? "Low Oxygen" : "Normal";
    }

    private string GetNutritionStatus(float caloriePercentage)
    {
        return caloriePercentage < 0.2f
            ? "Starving"
            : caloriePercentage < 0.4f ? "Very Hungry" : caloriePercentage < 0.6f ? "Hungry" : "Well Fed";
    }

    private string GetHungerLevel(float caloriePercentage)
    {
        return caloriePercentage < 0.2f ? "Emergency" : caloriePercentage < 0.4f ? "High" : caloriePercentage < 0.6f ? "Moderate" : "Low";
    }

    private string GetStressLevel(float stressPercentage)
    {
        return stressPercentage > 0.8f ? "Critical" : stressPercentage > 0.6f ? "High" : stressPercentage > 0.4f ? "Moderate" : "Low";
    }

    private string GetMentalBreakRisk(float stressPercentage)
    {
        return stressPercentage > 0.8f ? "Imminent" : stressPercentage > 0.6f ? "High" : stressPercentage > 0.4f ? "Moderate" : "Low";
    }
}

/// <summary>
/// Data structure for bio data query parameters
/// </summary>
public class BioDataQueryData
{
    public MinionIdentity Minion { get; set; } = null!;
    public string DataType { get; set; } = null!;
    public string DetailLevel { get; set; } = null!;
    public bool IncludeHistory { get; set; }
    public string Format { get; set; } = null!;
}