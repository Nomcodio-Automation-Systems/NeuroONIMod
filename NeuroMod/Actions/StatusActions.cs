using Cysharp.Threading.Tasks;
using Klei.AI;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using NeuroSdk.Websocket;

namespace NeuroMod;

/// <summary>
/// Retrieves status information for the currently connected Neuro duplicant.
/// Supports filtering by data category and choosing between text or JSON output.
/// Replaces the separate <c>get_biodata</c> action which had overlapping responsibility.
/// </summary>
/// <pre>
/// The action must be bound to a live duplicant before validation runs.
/// </pre>
/// <post>
/// Successful validation returns a status report and does not mutate duplicant state.
/// </post>
public class GetStatusAction(MinionIdentity minion) : NeuroAction<GetStatusAction.StatusQuery>
{
    private readonly MinionIdentity neuroMinion = minion;

    /// <summary>
    /// Describes the filters that control which status categories are returned and how they are formatted.
    /// </summary>
    /// <pre>Property values are populated from the incoming JSON payload.</pre>
    /// <post>A populated instance drives both section selection and output formatting.</post>
    public class StatusQuery
    {
        /// <summary>
        /// Gets or sets the status category to return.
        /// </summary>
        /// <value>One of <c>all</c>, <c>health</c>, <c>nutrition</c>, <c>stress</c>, <c>task</c>, <c>environment</c>, <c>skills</c>.</value>
        public string DataType { get; set; } = "all";

        /// <summary>
        /// Gets or sets the output format.
        /// </summary>
        /// <value><c>text</c> for a human-readable report; <c>json</c> for a structured JSON object.</value>
        public string Format { get; set; } = "text";

        /// <summary>
        /// Gets or sets the verbosity level.
        /// </summary>
        /// <value>One of <c>basic</c>, <c>detailed</c>.</value>
        public string DetailLevel { get; set; } = "basic";
    }

    public override string Name => "get_status";

    protected override string Description =>
        "Get status information for the Neuro duplicant. " +
        "Use data_type to focus on a specific category (health / nutrition / stress / task / environment / skills / reactions) or 'all' for a full report. " +
        "Set format to 'json' for structured data or 'text' for a readable summary. " +
        "detail_level 'detailed' adds extra fields like priority, room name, skill points, and active stress reactions.";

    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["data_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object>{ "all", "health", "nutrition", "stress", "task", "environment", "skills", "reactions" }
            },
            ["format"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object>{ "text", "json" }
            },
            ["detail_level"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object>{ "basic", "detailed" }
            }
        }
    };

    protected override ExecutionResult Validate(ActionJData actionData, out StatusQuery? parsedData)
    {
        parsedData = null;

        if (neuroMinion == null || neuroMinion.gameObject == null)
            return ExecutionResult.Failure("Neuro duplicate not found or not available");

        parsedData = new StatusQuery
        {
            DataType    = actionData.Data?["data_type"]?.Value<string>()    ?? "all",
            Format      = actionData.Data?["format"]?.Value<string>()       ?? "text",
            DetailLevel = actionData.Data?["detail_level"]?.Value<string>() ?? "basic"
        };

        string dt = parsedData.DataType;
        if (dt != "all" && dt != "health" && dt != "nutrition" && dt != "stress"
            && dt != "task" && dt != "environment" && dt != "skills" && dt != "reactions")
            return ExecutionResult.Failure($"Invalid data_type '{dt}'");

        try
        {
            DuplicateBioData bioData = new(neuroMinion);
            string result = parsedData.Format == "json"
                ? BuildJsonReport(bioData, parsedData)
                : BuildTextReport(bioData, parsedData);

            NeuroLogger.Log($"[GetStatusAction] Retrieved {parsedData.DataType}/{parsedData.DetailLevel} status for {neuroMinion.GetProperName()}", "GetStatusAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetStatusAction] Error retrieving status: {ex.Message}", "GetStatusAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error retrieving status: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(StatusQuery? parsedData) => UniTask.CompletedTask;

    // ── Text report ───────────────────────────────────────────────────────────

    private string BuildTextReport(DuplicateBioData bioData, StatusQuery query)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Status for {neuroMinion.GetProperName()}:");

        bool all = query.DataType == "all";

        if (all || query.DataType == "health")
            AppendHealthText(sb, bioData, query.DetailLevel);

        if (all || query.DataType == "nutrition")
            AppendNutritionText(sb, bioData, query.DetailLevel);

        if (all || query.DataType == "stress")
            AppendStressText(sb, bioData, query.DetailLevel);

        if (all || query.DataType == "task")
            AppendTaskText(sb, query.DetailLevel);

        if (all || query.DataType == "environment")
            sb.Append(AddEnvironmentInfo());

        if (all || query.DataType == "skills")
            sb.Append(AddSkillsInfo());

        if (all || query.DataType == "reactions")
            sb.Append(AddReactionsInfo(query.DetailLevel));

        return sb.ToString().TrimEnd('\n');
    }

    private void AppendHealthText(System.Text.StringBuilder sb, DuplicateBioData bio, string detail)
    {
        sb.AppendLine($"Health: {bio.HealthPercentage:P1} ({bio.HealthState})");
        if (detail == "detailed")
        {
            sb.AppendLine($"  HP: {bio.CurrentHealth:F0} / {bio.MaxHealth:F0}");
            sb.AppendLine($"  Oxygen: {bio.OxygenPercentage:P1}{(bio.NeedsOxygen ? " ⚠ low" : "")}");
            sb.AppendLine($"  Wounded: {bio.IsWounded}  Incapacitated: {bio.IsIncapacitated}");
            if (bio.IsSick)
                sb.AppendLine($"  Sicknesses: {string.Join(", ", bio.CurrentSicknesses)}");
        }
    }

    private void AppendNutritionText(System.Text.StringBuilder sb, DuplicateBioData bio, string detail)
    {
        sb.AppendLine($"Calories: {bio.CaloriePercentage:P1}");
        sb.AppendLine($"Stamina: {bio.StaminaPercentage:P1}{(bio.IsExhausted ? " ⚠ exhausted" : bio.IsTired ? " ⚠ tired" : "")}");
        bool relieving = IsRelievingItself();
        sb.AppendLine($"Bladder: {bio.BladderPercentage:P1}{(relieving ? " ⚠ relieving" : bio.NeedsBathroom ? " ⚠ urgent" : "")}");
        if (detail == "detailed")
        {
            sb.AppendLine($"  Calories: {bio.CurrentCalories:F0} / {bio.MaxCalories:F0} kcal");
            sb.AppendLine($"  Hungry: {bio.IsHungry}  Starving: {bio.IsStarving}");
            sb.AppendLine($"  Relieving: {relieving}");
        }
    }

    private void AppendStressText(System.Text.StringBuilder sb, DuplicateBioData bio, string detail)
    {
        sb.AppendLine($"Stress: {bio.StressPercentage:P1}");
        if (detail == "detailed")
            sb.AppendLine($"  Mental-break risk: {(bio.StressPercentage > 0.8f ? "high" : bio.StressPercentage > 0.5f ? "medium" : "low")}");
    }

    private void AppendTaskText(System.Text.StringBuilder sb, string detail)
    {
        ChoreConsumer choreConsumer = neuroMinion.GetComponent<ChoreConsumer>();
        if (choreConsumer?.choreDriver.HasChore() == true)
        {
            Chore currentChore = choreConsumer.choreDriver.GetCurrentChore();
            if (currentChore != null)
            {
                string taskName = GetEnrichedTaskName(currentChore);
                sb.Append($"Task: {taskName}");
                if (detail == "detailed")
                    sb.Append($" (priority {currentChore.masterPriority.priority_value})");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("Task: Idle");
        }

        if (detail == "detailed")
            sb.Append(AddLocationInfo());
    }

    /// <summary>
    /// Returns a human-readable task name that enriches generic chore type names (e.g. "Socialize")
    /// with information about the target building so Neuro can tell apart e.g. party-phone calls
    /// from free-roam socializing.
    /// </summary>
    /// <param name="chore">The current chore being performed.</param>
    /// <returns>An enriched task label string.</returns>
    private static string GetEnrichedTaskName(Chore chore)
    {
        string baseName = chore.choreType?.Name ?? "Unknown";

        // For social chores, try to identify the target building.
        if (ContainsIgnoreCase(chore.choreType?.Id ?? string.Empty, "Socialize") ||
            ContainsIgnoreCase(baseName, "Socialize"))
        {
            string? building = TryGetChoreBuildingName(chore);
            if (building != null)
                return $"Socializing ({building})";
            return "Socializing";
        }

        return baseName;
    }

    /// <summary>
    /// Attempts to resolve the name of the building the chore is targeting.
    /// Returns <see langword="null"/> when no meaningful building name can be found.
    /// </summary>
    private static string? TryGetChoreBuildingName(Chore chore)
    {
        try
        {
            GameObject? target = chore.target?.gameObject;
            if (target == null) return null;

            // Check for a Telephone component (party phone).
            if (target.GetComponent<Telephone>() != null)
                return "Party Phone";

            // Fall back to the building def name if a building is present.
            Building? building = target.GetComponent<Building>();
            if (building?.Def?.Name is { } name && !string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch { }
        return null;
    }

    // ── JSON report ───────────────────────────────────────────────────────────

    private string BuildJsonReport(DuplicateBioData bioData, StatusQuery query)
    {
        bool all = query.DataType == "all";
        bool detailed = query.DetailLevel == "detailed";

        var root = new JObject { ["duplicate"] = neuroMinion.GetProperName() };

        if (all || query.DataType == "health")
        {
            var h = new JObject
            {
                ["health_pct"]     = Math.Round(bioData.HealthPercentage * 100, 1),
                ["state"]          = bioData.HealthState.ToString(),
                ["oxygen_pct"]     = Math.Round(bioData.OxygenPercentage * 100, 1),
                ["needs_oxygen"]   = bioData.NeedsOxygen,
                ["is_sick"]        = bioData.IsSick,
            };
            if (detailed)
            {
                h["hp"]              = Math.Round(bioData.CurrentHealth, 1);
                h["max_hp"]          = Math.Round(bioData.MaxHealth, 1);
                h["is_wounded"]      = bioData.IsWounded;
                h["is_incapacitated"]= bioData.IsIncapacitated;
                if (bioData.IsSick)
                    h["sicknesses"]  = new Newtonsoft.Json.Linq.JArray(bioData.CurrentSicknesses.ToArray());
            }
            root["health"] = h;
        }

        if (all || query.DataType == "nutrition")
        {
            var n = new JObject
            {
                ["calorie_pct"]   = Math.Round(bioData.CaloriePercentage * 100, 1),
                ["stamina_pct"]   = Math.Round(bioData.StaminaPercentage * 100, 1),
                ["bladder_pct"]   = Math.Round(bioData.BladderPercentage * 100, 1),
                ["needs_bathroom"]= bioData.NeedsBathroom,
                ["is_relieving"]  = IsRelievingItself(),
                ["is_tired"]      = bioData.IsTired,
                ["is_exhausted"]  = bioData.IsExhausted,
            };
            if (detailed)
            {
                n["calories"]    = Math.Round(bioData.CurrentCalories, 0);
                n["max_calories"]= Math.Round(bioData.MaxCalories, 0);
                n["is_hungry"]   = bioData.IsHungry;
                n["is_starving"] = bioData.IsStarving;
            }
            root["nutrition"] = n;
        }

        if (all || query.DataType == "stress")
        {
            var s = new JObject
            {
                ["stress_pct"] = Math.Round(bioData.StressPercentage * 100, 1)
            };
            if (detailed)
                s["mental_break_risk"] = bioData.StressPercentage > 0.8f ? "high" : bioData.StressPercentage > 0.5f ? "medium" : "low";
            root["stress"] = s;
        }

        if (all || query.DataType == "task")
        {
            ChoreConsumer choreConsumer = neuroMinion.GetComponent<ChoreConsumer>();
            if (choreConsumer?.choreDriver.HasChore() == true)
            {
                Chore? chore = choreConsumer.choreDriver.GetCurrentChore();
                string taskName = chore != null ? GetEnrichedTaskName(chore) : "unknown";
                var t = new JObject { ["name"] = taskName };
                if (detailed && chore != null)
                    t["priority"] = chore.masterPriority.priority_value;
                root["task"] = t;
            }
            else
            {
                root["task"] = new JObject { ["name"] = "idle" };
            }
        }

        if (all || query.DataType == "reactions")
        {
            root["reactions"] = BuildReactionsJson(detailed);
        }

        if (all || query.DataType == "environment")
        {
            root["environment"] = BuildEnvironmentJson(detailed);
        }

        if (all || query.DataType == "skills")
        {
            root["skills"] = BuildSkillsJson(detailed);
        }

        return root.ToString();
    }

    // ── Location / environment helpers (shared with text and task section) ───

    /// <summary>
    /// Returns <see langword="true"/> when the duplicate is currently using a toilet/outhouse.
    /// </summary>
    /// <returns><see langword="true"/> if the active chore type name contains "Toilet" or "Outhouse".</returns>
    private bool IsRelievingItself()
    {
        try
        {
            ChoreConsumer? cc = neuroMinion.GetComponent<ChoreConsumer>();
            if (cc?.choreDriver.HasChore() != true) return false;
            string? name = cc.choreDriver.GetCurrentChore()?.choreType?.Name;
            return name != null &&
                   (name.IndexOf("Toilet", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Outhouse", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }
        catch { return false; }
    }

    private string AddLocationInfo()
    {
        try
        {
            if (neuroMinion == null || neuroMinion.transform == null)
            {
                return "Location: Data unavailable (minion not found)\n";
            }

            Vector3 worldPos = neuroMinion.transform.position;
            int cell = Grid.PosToCell(worldPos);

            if (!Grid.IsValidCell(cell))
            {
                return "Location: Data unavailable (invalid cell)\n";
            }

            int gridX = Grid.CellToXY(cell).x;
            int gridY = Grid.CellToXY(cell).y;

            string locationInfo = $"Location: Grid ({gridX}, {gridY})";

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
                NeuroLogger.LogWarning($"[GetStatusAction] Camera conversion failed: {cameraEx.Message}", "GetStatusAction", ActionWindow?.TraceId);
            }

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
                NeuroLogger.LogWarning($"[GetStatusAction] Room detection failed: {roomEx.Message}", "GetStatusAction", ActionWindow?.TraceId);
                locationInfo += ", Room: Unknown";
            }

            return locationInfo + "\n";
        }
        catch (System.Exception ex)
        {
            NeuroLogger.LogError($"[GetStatusAction] Error getting location: {ex.Message}", "GetStatusAction", ActionWindow?.TraceId);
            return "Location: Data unavailable\n";
        }
    }

    private string AddEnvironmentInfo()
    {
        try
        {
            int cell = Grid.PosToCell(neuroMinion.transform.position);
            float ambientTemp = Grid.Temperature[cell] - 273.15f;

            DuplicateBioData bioData = new(neuroMinion);
            float bodyTemp = bioData.BodyTemperature - 273.15f;
            string bodyTempWarning = bioData.IsOverheating ? " ⚠ overheating" : bioData.IsFreezing ? " ⚠ freezing" : "";

            string envInfo = $"Environment: Ambient {ambientTemp:F1}°C, Body {bodyTemp:F1}°C{bodyTempWarning}";

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
            MinionResume? resumeSkill = neuroMinion.GetComponent<MinionResume>();
            if (resumeSkill != null)
            {
                int totalXp = (int)resumeSkill.TotalExperienceGained;
                int available = resumeSkill.AvailableSkillpoints;
                return $"Skills: {totalXp} XP, {available} skill points available\n";
            }
        }
        catch
        {
        }

        return "";
    }

    /// <summary>
    /// Builds a <see cref="JObject"/> for the environment section of the JSON status report.
    /// </summary>
    /// <param name="detailed">When <see langword="true"/>, grid coordinates and screen position are included.</param>
    private JObject BuildEnvironmentJson(bool detailed)
    {
        try
        {
            int cell = Grid.PosToCell(neuroMinion.transform.position);
            float ambientTemp = Grid.Temperature[cell] - 273.15f;

            DuplicateBioData bioData = new(neuroMinion);
            float bodyTemp = bioData.BodyTemperature - 273.15f;

            var env = new JObject
            {
                ["ambient_temp_c"] = Math.Round(ambientTemp, 1),
                ["body_temp_c"]    = Math.Round(bodyTemp, 1),
                ["is_overheating"] = bioData.IsOverheating,
                ["is_freezing"]    = bioData.IsFreezing,
            };

            try
            {
                Room room = Game.Instance.roomProber.GetRoomOfGameObject(neuroMinion.gameObject);
                env["room"] = room?.roomType?.Name ?? "outside";
            }
            catch { env["room"] = "unknown"; }

            if (detailed && Grid.IsValidCell(cell))
            {
                var xy = Grid.CellToXY(cell);
                env["grid_x"] = xy.x;
                env["grid_y"] = xy.y;
            }

            return env;
        }
        catch
        {
            return new JObject { ["error"] = "unavailable" };
        }
    }

    /// <summary>
    /// Builds a <see cref="JObject"/> for the skills section of the JSON status report.
    /// </summary>
    /// <param name="detailed">When <see langword="true"/>, mastered-skill names are included.</param>
    private JObject BuildSkillsJson(bool detailed)
    {
        try
        {
            MinionResume? resume = neuroMinion.GetComponent<MinionResume>();
            if (resume == null)
                return new JObject { ["error"] = "unavailable" };

            var skills = new JObject
            {
                ["total_xp"]         = (int)resume.TotalExperienceGained,
                ["available_points"] = resume.AvailableSkillpoints,
            };

            if (detailed)
            {
                var mastered = new Newtonsoft.Json.Linq.JArray();
                foreach (var kvp in resume.MasteryBySkillID)
                {
                    if (kvp.Value)
                        mastered.Add(kvp.Key);
                }
                skills["mastered_skills"] = mastered;
            }

            return skills;
        }
        catch
        {
            return new JObject { ["error"] = "unavailable" };
        }
    }

    // ── Reactions helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Collects the duplicant's current active reactions and mental-break state as a text block.
    /// </summary>
    /// <param name="detailLevel">Verbosity level – <c>detailed</c> adds individual stress-reaction names.</param>
    /// <returns>A human-readable reactions summary line.</returns>
    private string AddReactionsInfo(string detailLevel)
    {
        try
        {
            var reactions = CollectActiveReactions(detailLevel == "detailed");
            if (reactions.Count == 0)
                return "Reactions: none\n";

            return $"Reactions: {string.Join(", ", reactions)}\n";
        }
        catch
        {
            return "Reactions: unavailable\n";
        }
    }

    /// <summary>
    /// Builds a <see cref="JObject"/> describing the duplicant's current reactions for JSON output.
    /// </summary>
    /// <param name="detailed">When <see langword="true"/>, individual reaction names are included.</param>
    private JObject BuildReactionsJson(bool detailed)
    {
        try
        {
            List<string> active = CollectActiveReactions(detailed);
            var obj = new JObject
            {
                ["active"] = new Newtonsoft.Json.Linq.JArray(active.ToArray()),
                ["is_in_mental_break"] = IsInMentalBreak(),
                ["is_vomiting"] = IsDoingChoreType("Vomit"),
                ["is_overjoyed"] = HasEffect("Overjoyed"),
            };

            if (detailed)
            {
                obj["stress_reactions"] = new Newtonsoft.Json.Linq.JArray(GetStressReactionNames().ToArray());
            }

            return obj;
        }
        catch
        {
            return new JObject { ["active"] = new Newtonsoft.Json.Linq.JArray() };
        }
    }

    /// <summary>
    /// Returns a list of currently active reaction labels for the duplicant.
    /// </summary>
    /// <param name="includeStressReactions">When <see langword="true"/>, individual stress-reaction names are appended.</param>
    private List<string> CollectActiveReactions(bool includeStressReactions)
    {
        var list = new List<string>();

        if (IsInMentalBreak())
            list.Add("mental break");

        if (IsDoingChoreType("Vomit"))
            list.Add("vomiting");

        if (HasEffect("Overjoyed"))
            list.Add("overjoyed");

        if (HasEffect("SevereStress"))
            list.Add("severe stress");

        if (HasEffect("StressReaction"))
            list.Add("stress reaction");

        if (includeStressReactions)
        {
            foreach (string reaction in GetStressReactionNames())
            {
                if (!list.Contains(reaction))
                    list.Add(reaction);
            }
        }

        return list;
    }

    private bool IsInMentalBreak()
    {
        try
        {
            ChoreConsumer? cc = neuroMinion.GetComponent<ChoreConsumer>();
            if (cc?.choreDriver.HasChore() == true)
            {
                Chore? chore = cc.choreDriver.GetCurrentChore();
                string id = chore?.choreType?.Id ?? string.Empty;
                return ContainsIgnoreCase(id, "breakdown")
                    || ContainsIgnoreCase(id, "vomit")
                    || ContainsIgnoreCase(id, "cry")
                    || ContainsIgnoreCase(id, "pace");
            }
        }
        catch { }
        return false;
    }

    private bool IsDoingChoreType(string choreTypeName)
    {
        try
        {
            ChoreConsumer? cc = neuroMinion.GetComponent<ChoreConsumer>();
            if (cc?.choreDriver.HasChore() == true)
            {
                Chore? chore = cc.choreDriver.GetCurrentChore();
                return ContainsIgnoreCase(chore?.choreType?.Id ?? string.Empty, choreTypeName);
            }
        }
        catch { }
        return false;
    }

    private bool HasEffect(string effectId)
    {
        try
        {
            Effects? effects = neuroMinion.GetComponent<Effects>();
            return effects?.HasEffect(effectId) == true;
        }
        catch { }
        return false;
    }

    private List<string> GetStressReactionNames()    {
        var names = new List<string>();
        try
        {
            // Stress reactions in ONI are chore types under the StressBehaviours group
            // We surface the active chore name if it looks like a stress reaction
            ChoreConsumer? cc = neuroMinion.GetComponent<ChoreConsumer>();
            if (cc?.choreDriver.HasChore() == true)
            {
                Chore? chore = cc.choreDriver.GetCurrentChore();
                if (chore?.choreType != null)
                {
                    // ONI stress-reaction chore ids are: CryItOut, Vomit, Narcolepsy, Breakdown, Pacing, Binge*
                    string id = chore.choreType.Id ?? string.Empty;
                    if (ContainsIgnoreCase(id, "CryItOut")) names.Add("cry it out");
                    if (ContainsIgnoreCase(id, "Vomit")) names.Add("vomiting");
                    if (ContainsIgnoreCase(id, "Narcolepsy")) names.Add("narcolepsy");
                    if (ContainsIgnoreCase(id, "Breakdown")) names.Add("breakdown");
                    if (ContainsIgnoreCase(id, "Pacing")) names.Add("pacing");
                    if (ContainsIgnoreCase(id, "Binge")) names.Add("binge eating");
                    if (ContainsIgnoreCase(id, "Singing")) names.Add("singing");
                    if (ContainsIgnoreCase(id, "Jubilant")) names.Add("jubilant");
                }
            }
        }
        catch { }
        return names;
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
