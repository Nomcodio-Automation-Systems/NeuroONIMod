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

/// <summary>
/// Returns the current active research technology and its progress, plus a list of
/// recently completed techs if available. Helps Neuro keep track of what's being studied.
/// </summary>
/// <pre>A colony must be loaded. <see cref="Research.Instance"/> must be available.</pre>
/// <post>Returns a snapshot of research state without mutating game state.</post>
public class GetCurrentResearchAction : BaseNeuroAction
{
    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "get_current_research";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "Returns the currently active research technology and how far along it is " +
        "(research points invested vs total needed), plus a list of the most recently completed techs. " +
        "Use this to track scientific progress or to know when a tech is about to finish.";

    /// <summary>Gets the JSON schema (optional format parameter).</summary>
    protected override JsonSchema? Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["include_completed"] = new JsonSchema { Type = JsonSchemaType.Boolean },
            ["format"]            = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object> { "text", "json" }
            }
        }
    };

    /// <summary>
    /// Reads the active research target and returns progress data.
    /// </summary>
    /// <param name="actionData">Incoming JSON payload.</param>
    /// <param name="parsedData">Always null; result embedded in <see cref="ExecutionResult"/>.</param>
    /// <returns>Success with research info, or a message when no research is active.</returns>
    /// <pre><see cref="Research.Instance"/> is not null.</pre>
    /// <post>Game state unchanged.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;
        try
        {
            if (Research.Instance == null)
                return ExecutionResult.Failure("Research system is not available.");

            bool inclCompleted = actionData.Data?["include_completed"]?.Value<bool>() ?? false;
            string format      = actionData.Data?["format"]?.Value<string>()           ?? "text";

            Tech? active = Research.Instance.GetActiveResearch()?.tech;

            // Gather completed techs
            List<(string Name, float Total)> completed = new();
            if (inclCompleted)
            {
                foreach (TechInstance ti in Research.Instance.GetResearchQueue())
                {
                    if (ti?.IsComplete() == true)
                        completed.Add((ToDisplayText(ti.tech.Name), ti.tech.costsByResearchTypeID.Values.Sum()));
                }
            }

            if (active == null)
            {
                string noActive = "No active research. The colony is not currently studying anything.";
                if (completed.Count > 0)
                    noActive += $"\nCompleted techs: {string.Join(", ", completed.Select(c => c.Name))}";
                return ExecutionResult.Success(noActive);
            }

            TechInstance? activeTI = Research.Instance.GetActiveResearch();
            float invested = activeTI != null ? GetInvestedPoints(activeTI) : 0f;
            float total    = active.costsByResearchTypeID.Values.Sum();
            float pct      = total > 0 ? invested / total : 0f;

            string activeName = ToDisplayText(active.Name);

            NeuroLogger.Log($"[GetCurrentResearchAction] Active: {activeName} {pct:P0}", "GetCurrentResearchAction", ActionWindow?.TraceId);

            string result = format == "json"
                ? BuildJson(activeName, invested, total, pct, completed)
                : BuildText(activeName, invested, total, pct, completed);

            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetCurrentResearchAction] Error: {ex.Message}", "GetCurrentResearchAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error retrieving research status: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(object? data) => UniTask.CompletedTask;

    private static float GetInvestedPoints(TechInstance ti)
    {
        try
        {
            float sum = 0f;
            foreach (float v in ti.progressInventory.PointsByTypeID.Values)
                sum += v;
            return sum;
        }
        catch { return 0f; }
    }

    /// <summary>
    /// Converts ONI UI-rich text into plain readable text for action responses.
    /// </summary>
    /// <param name="value">The raw string that may contain markup.</param>
    /// <returns>A trimmed plain-text representation safe for logs and responses.</returns>
    /// <pre><paramref name="value"/> may be null or contain rich-text tags.</pre>
    /// <post>Returns a non-null plain string with markup removed.</post>
    private static string ToDisplayText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return System.Text.RegularExpressions.Regex.Replace(value, @"<[^>]+>", string.Empty).Trim();
    }

    private static string BuildText(string name, float invested, float total, float pct,
                                    List<(string Name, float Total)> completed)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Active Research: {name}");
        sb.AppendLine($"  Progress: {invested:F0} / {total:F0} pts  ({pct:P0})");
        if (completed.Count > 0)
        {
            sb.AppendLine($"Recently Completed:");
            int skip = completed.Count > 5 ? completed.Count - 5 : 0;
            foreach (var entry in completed.Skip(skip))
                sb.AppendLine($"  \u2713 {entry.Name}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildJson(string name, float invested, float total, float pct,
                                    List<(string Name, float Total)> completed)
    {
        var obj = new JObject
        {
            ["active_research"]  = name,
            ["points_invested"]  = invested,
            ["points_needed"]    = total,
            ["progress_pct"]     = Mathf.RoundToInt(pct * 100f),
        };
        if (completed.Count > 0)
        {
            int skip = completed.Count > 5 ? completed.Count - 5 : 0;
            obj["recently_completed"] = new JArray(completed.Skip(skip).Select(c => c.Name));
        }
        return obj.ToString();
    }
}
