using Cysharp.Threading.Tasks;
using Klei.AI;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Lists all living duplicants currently in the colony with a brief status snapshot for each.
/// Useful for getting an overview of which duplicants exist and their names before querying details.
/// </summary>
/// <pre>At least one MinionIdentity must be present in the scene.</pre>
/// <post>Returns a summary of all colony duplicants without mutating game state.</post>
public class ListDuplicantsAction : BaseNeuroAction
{
    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "list_duplicants";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "List all duplicants currently alive in the colony. " +
        "Returns each duplicate's name, health, stress, current task, and location. " +
        "Use this to find out who else is in the colony before calling get_duplicant_info for detailed data.";

    /// <summary>Gets the JSON schema for the list-duplicants request.</summary>
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
    /// Validates the request, scans all minions, and returns the list immediately.
    /// </summary>
    /// <param name="actionData">Incoming JSON action payload.</param>
    /// <param name="parsedData">Always null; output is returned in the ExecutionResult.</param>
    /// <returns>Success with the duplicant list, or failure if no minions are found.</returns>
    /// <pre>A valid game instance with living duplicants must exist.</pre>
    /// <post>On success the result contains a list of all colony duplicants; game state is unchanged.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;

        try
        {
            List<MinionIdentity> minions = Components.LiveMinionIdentities.Items
                .Where(m => m != null && m.gameObject != null)
                .ToList();

            if (minions.Count == 0)
                return ExecutionResult.Failure("No duplicants found in the colony.");

            string format = actionData.Data?["format"]?.Value<string>() ?? "text";

            string result = format == "json"
                ? BuildJsonList(minions)
                : BuildTextList(minions);

            NeuroLogger.Log($"[ListDuplicantsAction] Listed {minions.Count} duplicants", "ListDuplicantsAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[ListDuplicantsAction] Error: {ex.Message}", "ListDuplicantsAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error listing duplicants: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(object? data) => UniTask.CompletedTask;

    private static string BuildTextList(List<MinionIdentity> minions)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Colony Duplicants ({minions.Count}):");
        foreach (MinionIdentity m in minions)
        {
            DuplicateBioData bio = new(m);
            ChoreConsumer? cc = m.GetComponent<ChoreConsumer>();
            string task = cc?.choreDriver.HasChore() == true
                ? cc.choreDriver.GetCurrentChore()?.choreType.Name ?? "unknown"
                : "idle";
            sb.AppendLine($"  {m.GetProperName()} — HP {bio.HealthPercentage:P0}  Stress {bio.StressPercentage:P0}  Task: {task}");
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static string BuildJsonList(List<MinionIdentity> minions)
    {
        var arr = new JArray();
        foreach (MinionIdentity m in minions)
        {
            DuplicateBioData bio = new(m);
            ChoreConsumer? cc = m.GetComponent<ChoreConsumer>();
            string task = cc?.choreDriver.HasChore() == true
                ? cc.choreDriver.GetCurrentChore()?.choreType.Name ?? "unknown"
                : "idle";
            arr.Add(new JObject
            {
                ["name"]        = m.GetProperName(),
                ["health_pct"]  = Math.Round(bio.HealthPercentage * 100, 1),
                ["stress_pct"]  = Math.Round(bio.StressPercentage * 100, 1),
                ["current_task"] = task
            });
        }
        return new JObject { ["duplicants"] = arr }.ToString();
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Returns detailed status information for a named duplicant other than the Neuro duplicant.
/// Mirrors the data categories of <see cref="GetStatusAction"/> but targets any colony member by name.
/// </summary>
/// <pre>The named duplicant must exist and be alive in the colony.</pre>
/// <post>Returns a status report for the requested duplicant without mutating game state.</post>
public class GetDuplicantInfoAction : BaseNeuroAction
{
    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "get_duplicant_info";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "Get detailed status information about a specific duplicant by name. " +
        "Use list_duplicants first to find available names. " +
        "Supports the same data_type categories as get_status (health / nutrition / stress / task / skills).";

    /// <summary>Gets the JSON schema for the get-duplicant-info request.</summary>
    protected override JsonSchema? Schema => new()
    {
        Type = JsonSchemaType.Object,
        Required = new List<string> { "name" },
        Properties = new Dictionary<string, JsonSchema>
        {
            ["name"] = new JsonSchema { Type = JsonSchemaType.String },
            ["data_type"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object> { "all", "health", "nutrition", "stress", "task", "skills" }
            },
            ["format"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object> { "text", "json" }
            }
        }
    };

    /// <summary>
    /// Validates the request by locating the named duplicant and building the status report.
    /// </summary>
    /// <param name="actionData">Incoming JSON action payload.</param>
    /// <param name="parsedData">Always null; output is returned in the ExecutionResult.</param>
    /// <returns>Success with the status report, or failure if the duplicant is not found.</returns>
    /// <pre><paramref name="actionData"/> must contain a valid <c>name</c> field matching a live colony duplicant.</pre>
    /// <post>On success the result contains the requested status data; game state is unchanged.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;

        string? requestedName = actionData.Data?["name"]?.Value<string>();
        if (string.IsNullOrWhiteSpace(requestedName))
            return ExecutionResult.Failure("Parameter 'name' is required.");

        MinionIdentity? target = Components.LiveMinionIdentities.Items
            .FirstOrDefault(m => m != null && m.gameObject != null &&
                string.Equals(m.GetProperName(), requestedName, StringComparison.OrdinalIgnoreCase));

        if (target == null)
            return ExecutionResult.Failure($"Duplicant '{requestedName}' not found. Use list_duplicants to see available names.");

        string dataType    = actionData.Data?["data_type"]?.Value<string>() ?? "all";
        string format      = actionData.Data?["format"]?.Value<string>()    ?? "text";

        try
        {
            DuplicateBioData bio = new(target);
            string result = format == "json"
                ? BuildJsonReport(target, bio, dataType)
                : BuildTextReport(target, bio, dataType);

            NeuroLogger.Log($"[GetDuplicantInfoAction] Retrieved {dataType} info for {target.GetProperName()}", "GetDuplicantInfoAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetDuplicantInfoAction] Error: {ex.Message}", "GetDuplicantInfoAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error retrieving info for '{requestedName}': {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(object? data) => UniTask.CompletedTask;

    private static string BuildTextReport(MinionIdentity m, DuplicateBioData bio, string dataType)
    {
        bool all = dataType == "all";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Info for {m.GetProperName()}:");

        if (all || dataType == "health")
            sb.AppendLine($"  Health: {bio.HealthPercentage:P1} ({bio.HealthState})  Sick: {bio.IsSick}");

        if (all || dataType == "nutrition")
            sb.AppendLine($"  Calories: {bio.CaloriePercentage:P1}  Hungry: {bio.IsHungry}");

        if (all || dataType == "stress")
            sb.AppendLine($"  Stress: {bio.StressPercentage:P1}");

        if (all || dataType == "task")
        {
            ChoreConsumer? cc = m.GetComponent<ChoreConsumer>();
            string task = cc?.choreDriver.HasChore() == true
                ? cc.choreDriver.GetCurrentChore()?.choreType.Name ?? "unknown"
                : "idle";
            sb.AppendLine($"  Task: {task}");
        }

        if (all || dataType == "skills")
        {
            MinionResume? resume = m.GetComponent<MinionResume>();
            if (resume != null)
                sb.AppendLine($"  Skills: {(int)resume.TotalExperienceGained} XP, {resume.AvailableSkillpoints} points available");
        }

        return sb.ToString().TrimEnd('\n');
    }

    private static string BuildJsonReport(MinionIdentity m, DuplicateBioData bio, string dataType)
    {
        bool all = dataType == "all";
        var root = new JObject { ["name"] = m.GetProperName() };

        if (all || dataType == "health")
            root["health"] = new JObject
            {
                ["health_pct"] = Math.Round(bio.HealthPercentage * 100, 1),
                ["state"]      = bio.HealthState.ToString(),
                ["is_sick"]    = bio.IsSick
            };

        if (all || dataType == "nutrition")
            root["nutrition"] = new JObject
            {
                ["calorie_pct"] = Math.Round(bio.CaloriePercentage * 100, 1),
                ["is_hungry"]   = bio.IsHungry
            };

        if (all || dataType == "stress")
            root["stress"] = new JObject { ["stress_pct"] = Math.Round(bio.StressPercentage * 100, 1) };

        if (all || dataType == "task")
        {
            ChoreConsumer? cc = m.GetComponent<ChoreConsumer>();
            string task = cc?.choreDriver.HasChore() == true
                ? cc.choreDriver.GetCurrentChore()?.choreType.Name ?? "unknown"
                : "idle";
            root["task"] = new JObject { ["name"] = task };
        }

        if (all || dataType == "skills")
        {
            MinionResume? resume = m.GetComponent<MinionResume>();
            if (resume != null)
                root["skills"] = new JObject
                {
                    ["total_xp"]         = (int)resume.TotalExperienceGained,
                    ["available_points"] = resume.AvailableSkillpoints
                };
        }

        return root.ToString();
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Returns general information about the current colony: its name, current cycle, number of duplicants, and elapsed time.
/// Useful as a quick orientation query before issuing more specific commands.
/// </summary>
/// <pre>A colony save must be loaded and GameClock must be available.</pre>
/// <post>Returns colony metadata without mutating game state.</post>
/// <summary>
/// Returns a full colony snapshot: identity fields (name, cycle, hour, duplicant count) plus
/// health indicators (average stress, active alerts, food in storage, O2 concentration, power balance).
/// Replaces both the former get_colony_info and get_colony_overview actions.
/// </summary>
/// <invariant>Game state is never mutated; all reads are best-effort with catch fallbacks.</invariant>
public class GetColonyInfoAction : BaseNeuroAction
{
    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "get_colony_info";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "Returns a full snapshot of the colony: name, cycle, hour, duplicant count, average stress, " +
        "active alert count, total food in storage, average O2 concentration, and power balance. " +
        "Call this at the start of a session or whenever you need a broad situational update.";

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
    /// Validates the request, collects all colony data, and returns the result.
    /// </summary>
    /// <param name="actionData">Incoming JSON action payload.</param>
    /// <param name="parsedData">Always null; output is embedded in the ExecutionResult.</param>
    /// <returns>Success with colony snapshot, or failure if the game is not yet loaded.</returns>
    /// <pre>GameClock.Instance must not be null.</pre>
    /// <post>On success the result contains a full colony snapshot; game state is unchanged.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;

        try
        {
            if (GameClock.Instance == null)
                return ExecutionResult.Failure("Game clock is not available. Make sure a colony is loaded.");

            string format = actionData.Data?["format"]?.Value<string>() ?? "text";

            string colonyName  = GetColonyName();
            int    cycle       = GameClock.Instance.GetCycle() + 1; // 0-based internally
            int    hour        = Mathf.FloorToInt(GameClock.Instance.GetTimeSinceStartOfCycle() / 600f);
            int    dupeCount   = Components.LiveMinionIdentities.Count;
            float  avgStress   = CalculateAverageStress();
            int    alertCount  = CountActiveAlerts();
            float  totalFood   = SumFoodCalories();
            float  avgO2       = AverageO2Concentration();
            float  powerBalance = CalculatePowerBalance();

            string result = format == "json"
                ? BuildJson(colonyName, cycle, hour, dupeCount, avgStress, alertCount, totalFood, avgO2, powerBalance)
                : BuildText(colonyName, cycle, hour, dupeCount, avgStress, alertCount, totalFood, avgO2, powerBalance);

            NeuroLogger.Log($"[GetColonyInfoAction] Colony={colonyName} Cycle={cycle} Dupes={dupeCount} Stress={avgStress:P0}", "GetColonyInfoAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetColonyInfoAction] Error: {ex.Message}", "GetColonyInfoAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error retrieving colony info: {ex.Message}");
        }
    }

    /// <summary>Executes the action asynchronously (no async work required).</summary>
    /// <param name="data">Unused parsed data.</param>
    /// <returns>A completed task.</returns>
    /// <pre>Validate has already run successfully.</pre>
    /// <post>No game state is mutated.</post>
    protected override UniTask ExecuteAsync(object? data) => UniTask.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Reads the colony name from the active save.</summary>
    /// <returns>The colony name or "Unknown Colony" when unavailable.</returns>
    /// <pre>May be called before a save is fully loaded.</pre>
    /// <post>Never throws; returns a safe default.</post>
    private static string GetColonyName()
    {
        try { return SaveGame.Instance?.BaseName ?? "Unknown Colony"; }
        catch { return "Unknown Colony"; }
    }

    /// <summary>Calculates the mean stress ratio across all live duplicants.</summary>
    /// <returns>Value in [0, 1]; 0 when no duplicants are present.</returns>
    /// <pre>Components.LiveMinionIdentities is accessible.</pre>
    /// <post>Returns a best-effort average; individual failures are silently skipped.</post>
    private static float CalculateAverageStress()
    {
        var minions = Components.LiveMinionIdentities.Items
            .Where(m => m != null && m.gameObject != null)
            .ToList();
        if (minions.Count == 0) return 0f;

        float total = 0f;
        foreach (MinionIdentity m in minions)
        {
            try
            {
                Amounts? amounts = m.GetComponent<Amounts>();
                AmountInstance? stress = amounts?.Get(Db.Get().Amounts.Stress);
                if (stress != null)
                    total += stress.value / stress.GetMax();
            }
            catch { }
        }
        return total / minions.Count;
    }

    /// <summary>Counts all live notifications across all Notifier components in the scene.</summary>
    /// <returns>Total active alert count; 0 on error.</returns>
    /// <pre>The Unity scene is fully loaded.</pre>
    /// <post>Never throws.</post>
    private static int CountActiveAlerts()
    {
        try
        {
            int count = 0;
            foreach (Notifier notifier in UnityEngine.Object.FindObjectsOfType<Notifier>())
            {
                if (notifier == null) continue;
                count += notifier.GetComponentsInChildren<global::Notification>(true).Length;
            }
            return count;
        }
        catch { return 0; }
    }

    /// <summary>Sums the calories of all edible items in every Storage component in the scene.</summary>
    /// <returns>Total kcal; 0 on error.</returns>
    /// <pre>The Unity scene is fully loaded.</pre>
    /// <post>Never throws.</post>
    private static float SumFoodCalories()
    {
        try
        {
            float total = 0f;
            foreach (Storage storage in UnityEngine.Object.FindObjectsOfType<Storage>())
            {
                if (storage == null || storage.gameObject == null) continue;
                foreach (GameObject item in storage.items)
                {
                    if (item == null || !item.HasTag(GameTags.Edible)) continue;
                    Edible? edible = item.GetComponent<Edible>();
                    if (edible != null) total += edible.Calories;
                }
            }
            return total;
        }
        catch { return 0f; }
    }

    /// <summary>Estimates the fraction of sampled grid cells that contain oxygen.</summary>
    /// <returns>Value in [0, 1]; 0 on error or empty grid.</returns>
    /// <pre>Grid data is initialized.</pre>
    /// <post>Never throws; samples every 50th cell for performance.</post>
    private static float AverageO2Concentration()
    {
        try
        {
            int sampled = 0, o2Count = 0;
            SimHashes o2Hash = SimHashes.Oxygen;
            for (int i = 0; i < Grid.CellCount; i += 50)
            {
                if (!Grid.IsValidCell(i)) continue;
                Element el = Grid.Element[i];
                if (el != null && el.id == o2Hash) o2Count++;
                sampled++;
            }
            return sampled > 0 ? (float)o2Count / sampled : 0f;
        }
        catch { return 0f; }
    }

    /// <summary>Calculates overall power balance (generated minus consumed) across all circuits.</summary>
    /// <returns>Watt surplus (positive) or deficit (negative); 0 on error.</returns>
    /// <pre>Game.Instance.circuitManager is accessible.</pre>
    /// <post>Never throws.</post>
    private static float CalculatePowerBalance()
    {
        try
        {
            CircuitManager? cm = Game.Instance?.circuitManager;
            if (cm == null) return 0f;

            float generated = 0f, consumed = 0f;
            var seen = new HashSet<ushort>();
            foreach (Generator gen in UnityEngine.Object.FindObjectsOfType<Generator>())
            {
                if (gen == null) continue;
                ushort id = cm.GetCircuitID((ICircuitConnected)gen);
                if (id == ushort.MaxValue || !seen.Add(id)) continue;
                generated += cm.GetWattsGeneratedByCircuit(id);
                consumed  += cm.GetWattsUsedByCircuit(id);
            }
            return generated - consumed;
        }
        catch { return 0f; }
    }

    /// <summary>Builds a compact text summary of the colony snapshot.</summary>
    private static string BuildText(string name, int cycle, int hour, int dupes,
        float stress, int alerts, float food, float o2, float power)
    {
        string stressLabel = stress > 0.8f ? "CRITICAL" : stress > 0.5f ? "high" : stress > 0.25f ? "moderate" : "low";
        string powerLabel  = power >= 0 ? $"+{Mathf.RoundToInt(power)}W surplus" : $"{Mathf.RoundToInt(power)}W deficit";
        string alertLabel  = alerts == 0 ? "none" : alerts.ToString();

        return $"=== Colony: {name} ===\n" +
               $"Cycle {cycle}, Hour {hour}/24\n" +
               $"Duplicants: {dupes}   Avg stress: {stress:P0} ({stressLabel})\n" +
               $"Active alerts: {alertLabel}\n" +
               $"Food in storage: {Mathf.RoundToInt(food)} kcal\n" +
               $"Avg O2 concentration: {o2:P0}\n" +
               $"Power balance: {powerLabel}";
    }

    /// <summary>Builds a compact JSON object of the colony snapshot.</summary>
    private static string BuildJson(string name, int cycle, int hour, int dupes,
        float stress, int alerts, float food, float o2, float power)
    {
        return new JObject
        {
            ["colony_name"]     = name,
            ["cycle"]           = cycle,
            ["hour"]            = hour,
            ["duplicants"]      = dupes,
            ["avg_stress_pct"]  = Mathf.RoundToInt(stress * 100f),
            ["active_alerts"]   = alerts,
            ["food_kcal"]       = Mathf.RoundToInt(food),
            ["avg_o2_pct"]      = Mathf.RoundToInt(o2 * 100f),
            ["power_balance_w"] = Mathf.RoundToInt(power),
        }.ToString();
    }
}
