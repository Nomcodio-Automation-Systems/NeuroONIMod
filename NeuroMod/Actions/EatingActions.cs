#nullable enable
using Cysharp.Threading.Tasks;
using Klei.AI;
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
/// Returns Neuro's current hunger state and her meal history (what she ate, when,
/// how many kcal, and the morale effect of each meal).
/// Meal data is collected live via <see cref="EatingTracker"/> so it is always accurate.
/// </summary>
/// <pre>The Neuro duplicant must exist with an <see cref="Amounts"/> component, and
/// <see cref="EatingTracker"/> must have been attached.</pre>
/// <post>Returns a hunger + meal-history snapshot without mutating game state.</post>
public class GetEatingInfoAction(MinionIdentity minion) : BaseNeuroAction
{
    private readonly MinionIdentity neuroMinion = minion;

    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "get_eating_info";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "Returns Neuro's current hunger level and her recent meal history: what she ate, " +
        "when she ate it (in-game cycle and hour), how many kcal each meal provided, " +
        "and the morale bonus or penalty from each food's quality. " +
        "Use this to react to hunger or to know what Neuro has been eating lately.";

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
    /// Builds the hunger + meal-history response.
    /// </summary>
    /// <param name="actionData">Incoming JSON payload.</param>
    /// <param name="parsedData">Always null; result embedded in return value.</param>
    /// <returns>Success with eating info, or failure when calorie data is unavailable.</returns>
    /// <pre>Neuro duplicant is alive and has Amounts.</pre>
    /// <post>Game state unchanged.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;
        try
        {
            if (neuroMinion == null || neuroMinion.gameObject == null)
                return ExecutionResult.Failure("Neuro duplicant not found.");

            string format = actionData.Data?["format"]?.Value<string>() ?? "text";

            // ── Current hunger ────────────────────────────────────────────────
            AmountInstance? calorieAmount = Db.Get().Amounts.Calories.Lookup(neuroMinion.gameObject);
            if (calorieAmount == null)
                return ExecutionResult.Failure("Calorie data is not available for this duplicant.");

            // ONI internal unit: 1 kcal = 1000 units
            const float KcalScale = 1000f;
            float current = calorieAmount.value / KcalScale;
            float max     = calorieAmount.GetMax() / KcalScale;
            float pct     = max > 0f ? current / max : 0f;

            // Hunger label matches ONI's own CalorieMonitor thresholds
            string hungerLabel = pct < 0.1f ? "STARVING"
                               : pct < 0.3f ? "hungry"
                               : pct < 0.6f ? "peckish"
                               : "satisfied";

            // kcal/s drain rate (positive = gaining, negative = losing)
            float rateKcalPerSec = (calorieAmount.GetDelta() / KcalScale);

            // Current food quality effect (affects morale)
            string dietQuality = GetDietQuality(neuroMinion);

            // ── Meal history from tracker ─────────────────────────────────────
            IReadOnlyList<EatingTracker.MealRecord> meals = EatingTracker.History;

            NeuroLogger.Log(
                $"[GetEatingInfoAction] hunger={hungerLabel} ({pct:P0}) rate={rateKcalPerSec:+0.##;-0.##;0} kcal/s meals={meals.Count}",
                "GetEatingInfoAction", ActionWindow?.TraceId);

            string result = format == "json"
                ? BuildJson(current, max, pct, hungerLabel, rateKcalPerSec, dietQuality, meals)
                : BuildText(current, max, pct, hungerLabel, rateKcalPerSec, dietQuality, meals);

            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[GetEatingInfoAction] Error: {ex.Message}", "GetEatingInfoAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error retrieving eating info: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(object? data) => UniTask.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns the current diet quality label from active food-quality effects.</summary>
    private static string GetDietQuality(MinionIdentity minion)
    {
        try
        {
            Effects? effects = minion.GetComponent<Effects>();
            if (effects == null) return "unknown";
            if (effects.HasEffect("FoodQuality5")) return "amazing (+3 morale)";
            if (effects.HasEffect("FoodQuality4")) return "great (+2 morale)";
            if (effects.HasEffect("FoodQuality3")) return "good (+1 morale)";
            if (effects.HasEffect("FoodQuality2")) return "mediocre (0 morale)";
            if (effects.HasEffect("FoodQuality1")) return "poor (-1 morale)";
            if (effects.HasEffect("FoodQuality0")) return "awful (-2 morale)";
        }
        catch { }
        return "unknown";
    }

    // ── Formatters ────────────────────────────────────────────────────────────

    private static string BuildText(float cur, float max, float pct, string hungerLabel,
                                    float rateKcalPerSec, string dietQuality,
                                    IReadOnlyList<EatingTracker.MealRecord> meals)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Hunger: {hungerLabel}  ({cur:F0} / {max:F0} kcal, {pct:P0})");
        sb.AppendLine($"Calorie drain: {rateKcalPerSec:+0.##;-0.##;0} kcal/s   Current diet quality: {dietQuality}");

        if (meals.Count == 0)
        {
            sb.AppendLine("No meals recorded yet this session.");
        }
        else
        {
            sb.AppendLine($"Recent meals ({meals.Count}):");
            foreach (EatingTracker.MealRecord m in meals)
            {
                string moraleStr = m.MoraleEffect >= 0
                    ? $"+{m.MoraleEffect} morale"
                    : $"{m.MoraleEffect} morale";
                string timeAgo = FormatTimeAgo(m.RecordedAtTicks);
                sb.AppendLine($"  • {m.FoodName}  —  {m.KcalEaten:F0} kcal, quality {m.Quality}, {moraleStr}");
                sb.AppendLine($"    Cycle {m.Cycle:F1} hour {m.Hour:F0}  ({timeAgo})");
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildJson(float cur, float max, float pct, string hungerLabel,
                                    float rateKcalPerSec, string dietQuality,
                                    IReadOnlyList<EatingTracker.MealRecord> meals)
    {
        var mealArr = new JArray();
        foreach (EatingTracker.MealRecord m in meals)
        {
            mealArr.Add(new JObject
            {
                ["food"]         = m.FoodName,
                ["kcal"]         = Mathf.RoundToInt(m.KcalEaten),
                ["quality"]      = m.Quality,
                ["morale_effect"] = m.MoraleEffect,
                ["cycle"]        = Math.Round(m.Cycle, 1),
                ["hour"]         = Mathf.RoundToInt(m.Hour),
                ["time_ago"]     = FormatTimeAgo(m.RecordedAtTicks),
            });
        }

        return new JObject
        {
            ["calories_current"] = Mathf.RoundToInt(cur),
            ["calories_max"]     = Mathf.RoundToInt(max),
            ["calories_pct"]     = Mathf.RoundToInt(pct * 100f),
            ["hunger_state"]     = hungerLabel,
            ["calorie_rate_per_sec"] = Math.Round(rateKcalPerSec, 3),
            ["current_diet_quality"] = dietQuality,
            ["recent_meals"]     = mealArr,
        }.ToString();
    }

    /// <summary>Returns a human-readable "X min ago" / "just now" label relative to real time.</summary>
    private static string FormatTimeAgo(long recordedAtTicks)
    {
        double secs = (global::System.DateTime.UtcNow.Ticks - recordedAtTicks) / (double)global::System.TimeSpan.TicksPerSecond;
        if (secs < 90)   return "just now";
        if (secs < 3600) return $"{(int)(secs / 60)} min ago";
        return $"{(int)(secs / 3600)} h ago";
    }
}

