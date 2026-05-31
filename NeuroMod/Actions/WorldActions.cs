#nullable enable
using Cysharp.Threading.Tasks;
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

// ─── get_geysers ─────────────────────────────────────────────────────────────

/// <summary>
/// Returns all geysers the colony has discovered so far, including geyser type,
/// element output, average output rate, and whether they are currently active.
/// </summary>
/// <pre>A colony must be loaded.</pre>
/// <post>Returns a snapshot of discovered geysers without mutating game state.</post>
public class GetGeysersAction : BaseNeuroAction
{
    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "get_geysers";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "Returns all geysers and natural vents discovered by the colony so far. " +
        "For each geyser: type name, element it emits (e.g. steam, natural gas, water), " +
        "estimated average output in kg/cycle, and whether it is currently erupting or dormant. " +
        "Useful for planning resource pipelines and understanding the asteroid's natural resources.";

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
    /// Scans all <see cref="Geyser"/> objects in the scene and returns their status.
    /// </summary>
    /// <param name="actionData">Incoming JSON payload.</param>
    /// <param name="parsedData">Always null; result embedded in <see cref="ExecutionResult"/>.</param>
    /// <returns>Success with geyser list, or info message when none are discovered.</returns>
    /// <pre>A valid game world is loaded.</pre>
    /// <post>Game state unchanged.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;
        try
        {
            string format  = actionData.Data?["format"]?.Value<string>() ?? "text";
            List<GeyserEntry> geysers = CollectGeysers();

            if (geysers.Count == 0)
                return ExecutionResult.Success("No geysers discovered yet. Explore more of the asteroid!");

            string result = format == "json"
                ? BuildJson(geysers)
                : BuildText(geysers);

            NeuroLogger.Log($"[GetGeysersAction] Found {geysers.Count} geysers", "GetGeysersAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetGeysersAction] Error: {ex.Message}", "GetGeysersAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error retrieving geysers: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(object? data) => UniTask.CompletedTask;

    // ── Data ──────────────────────────────────────────────────────────────────

    private sealed class GeyserEntry
    {
        public string Name     { get; }
        public string Element  { get; }
        public float  AvgKgCycle { get; }
        public bool   IsErupting { get; }

        public GeyserEntry(string name, string element, float avgKgCycle, bool erupting)
        {
            Name = name; Element = element; AvgKgCycle = avgKgCycle; IsErupting = erupting;
        }
    }

    private static List<GeyserEntry> CollectGeysers()
    {
        var results = new List<GeyserEntry>();
        foreach (Geyser g in UnityEngine.Object.FindObjectsOfType<Geyser>())
        {
            if (g == null || g.gameObject == null) continue;

            // Only include geysers the player has discovered (revealed on map)
            Studyable? studyable = g.GetComponent<Studyable>();
            // Undiscovered geysers have no revealed element — skip them
            GeyserConfigurator? cfg = g.GetComponent<GeyserConfigurator>();
            if (cfg == null) continue;

            string name    = g.GetProperName() ?? "Unknown Geyser";
            string element = TryGetElement(cfg);
            float  avg     = TryGetAvgOutput(cfg);
            bool   erupt   = IsErupting(g);

            results.Add(new GeyserEntry(name, element, avg, erupt));
        }
        return results;
    }

    private static string TryGetElement(GeyserConfigurator cfg)
    {
        try
        {
            GeyserConfigurator.GeyserType? t = GeyserConfigurator.FindType(cfg.presetType);
            return t?.element.ToString() ?? cfg.presetType.ToString();
        }
        catch { return "unknown"; }
    }

    private static bool IsErupting(Geyser g)
    {
        try
        {
            // The geyser emits when RemainingActiveTime > 0 and it has not gone dormant
            return !g.ShouldGoDormant() && g.gameObject.GetComponent<ElementEmitter>() != null;
        }
        catch { return false; }
    }

    private static float TryGetAvgOutput(GeyserConfigurator cfg)
    {
        try
        {
            GeyserConfigurator.GeyserInstanceConfiguration ic = cfg.MakeConfiguration();
            ic.Init();
            return ic.GetMassPerCycle() * ic.GetYearPercent();
        }
        catch { return 0f; }
    }

    private static string BuildText(List<GeyserEntry> geysers)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Discovered Geysers ({geysers.Count}):");
        foreach (GeyserEntry g in geysers)
        {
            string status = g.IsErupting ? "ERUPTING" : "dormant";
            sb.AppendLine($"  {g.Name} ({g.Element}) — ~{g.AvgKgCycle:F1} kg/cycle  [{status}]");
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildJson(List<GeyserEntry> geysers)
    {
        var arr = new JArray(geysers.Select(g => new JObject
        {
            ["name"]         = g.Name,
            ["element"]      = g.Element,
            ["avg_kg_cycle"] = g.AvgKgCycle,
            ["is_erupting"]  = g.IsErupting,
        }));
        return arr.ToString();
    }
}

// ─── get_power_status ────────────────────────────────────────────────────────

/// <summary>
/// Returns the power balance for each electrical circuit in the colony:
/// how much is being generated, how much is consumed, and whether the circuit is overloaded.
/// </summary>
/// <pre>A colony must be loaded and <see cref="Game.Instance.circuitManager"/> must be available.</pre>
/// <post>Returns a snapshot without mutating game state.</post>
public class GetPowerStatusAction : BaseNeuroAction
{
    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "get_power_status";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "Returns the power status of every electrical circuit in the colony: " +
        "wattage generated, wattage consumed, surplus or deficit, and whether the circuit is overloaded. " +
        "Useful for knowing if we're about to lose power or have capacity to spare.";

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
    /// Reads circuit manager data and reports per-circuit power balance.
    /// </summary>
    /// <param name="actionData">Incoming JSON payload.</param>
    /// <param name="parsedData">Always null; result embedded in <see cref="ExecutionResult"/>.</param>
    /// <returns>Success with power status, or failure when circuit data is unavailable.</returns>
    /// <pre><see cref="Game.Instance"/> and circuitManager are not null.</pre>
    /// <post>Game state unchanged.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;
        try
        {
            if (Game.Instance?.circuitManager == null)
                return ExecutionResult.Failure("Circuit manager is not available.");

            string format = actionData.Data?["format"]?.Value<string>() ?? "text";

            List<CircuitSummary> circuits = CollectCircuits();

            if (circuits.Count == 0)
                return ExecutionResult.Success("No electrical circuits found in the colony.");

            string result = format == "json"
                ? BuildJson(circuits)
                : BuildText(circuits);

            NeuroLogger.Log($"[GetPowerStatusAction] {circuits.Count} circuits", "GetPowerStatusAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetPowerStatusAction] Error: {ex.Message}", "GetPowerStatusAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error retrieving power status: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(object? data) => UniTask.CompletedTask;

    private sealed class CircuitSummary
    {
        public int    Id       { get; }
        public float  Generated { get; }
        public float  Consumed  { get; }

        public CircuitSummary(int id, float gen, float con)
        { Id = id; Generated = gen; Consumed = con; }
    }

    private static List<CircuitSummary> CollectCircuits()
    {
        var results = new List<CircuitSummary>();
        CircuitManager? cm = Game.Instance?.circuitManager;
        if (cm == null) return results;

        // ONI uses ushort circuit IDs; scan all generators and collect unique IDs
        var seen = new HashSet<ushort>();
        foreach (Generator gen in UnityEngine.Object.FindObjectsOfType<Generator>())
        {
            if (gen == null) continue;
            ushort id = cm.GetCircuitID((ICircuitConnected)gen);
            if (id == ushort.MaxValue || !seen.Add(id)) continue;
            float generated = cm.GetWattsGeneratedByCircuit(id);
            float consumed  = cm.GetWattsUsedByCircuit(id);
            results.Add(new CircuitSummary(id, generated, consumed));
        }
        return results;
    }

    private static string BuildText(List<CircuitSummary> circuits)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Power Status ({circuits.Count} circuit(s)):");
        float totalGen = 0f, totalCon = 0f;
        int ci = 1;
        foreach (CircuitSummary c in circuits)
        {
            float balance = c.Generated - c.Consumed;
            totalGen += c.Generated; totalCon += c.Consumed;
            string balStr = balance >= 0 ? $"+{balance:F0}W" : $"{balance:F0}W";
            sb.AppendLine($"  Circuit {ci++}: {c.Generated:F0}W gen  {c.Consumed:F0}W used  ({balStr})");
        }
        sb.AppendLine($"  ── Total: {totalGen:F0}W generated  {totalCon:F0}W consumed  ({(totalGen - totalCon):+0;-0;0}W balance)");
        return sb.ToString().TrimEnd();
    }

    private static string BuildJson(List<CircuitSummary> circuits)
    {
        var arr = new JArray(circuits.Select((c, i) => new JObject
        {
            ["circuit"]       = i + 1,
            ["generated_w"]   = c.Generated,
            ["consumed_w"]    = c.Consumed,
            ["balance_w"]     = c.Generated - c.Consumed,
        }));
        return arr.ToString();
    }
}
