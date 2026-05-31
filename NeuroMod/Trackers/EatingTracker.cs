#nullable enable
using Klei.AI;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Records every meal eaten by the tracked duplicant by subscribing to the
/// <c>EatCompleteEater</c> game event (hash 1121894420) that fires on the
/// duplicant's GameObject when a food item is fully consumed.
/// The tracker keeps a rolling history of the last <see cref="MaxMeals"/> meals
/// with food name, kcal, quality tier, morale effect, and in-game time.
/// </summary>
/// <pre>Must be initialised via <see cref="Attach"/> before use.</pre>
/// <post>History is populated automatically as the duplicant eats; call <see cref="Detach"/> on cleanup.</post>
/// <invariant>At most <see cref="MaxMeals"/> entries are kept; oldest entries are dropped first.</invariant>
public static class EatingTracker
{
    /// <summary>Maximum number of past meals stored.</summary>
    public const int MaxMeals = 10;

    // EatCompleteEater game hash — fires on the duplicant (worker) when eating finishes.
    private const int EatCompleteEaterHash = 1121894420;

    /// <summary>A single recorded meal.</summary>
    public sealed class MealRecord
    {
        /// <summary>Localised food name (e.g. "Mushroom Wrap").</summary>
        public string FoodName    { get; private set; }

        /// <summary>Calories consumed in kcal (already divided by 1000).</summary>
        public float  KcalEaten   { get; private set; }

        /// <summary>Food quality tier (-1 – 5).</summary>
        public int    Quality     { get; private set; }

        /// <summary>Net morale change from this meal's food-quality effect.</summary>
        public int    MoraleEffect { get; private set; }

        /// <summary>Game cycle when the meal occurred.</summary>
        public float  Cycle       { get; private set; }

        /// <summary>In-game hour within the cycle (0–23).</summary>
        public float  Hour        { get; private set; }

        /// <summary>Real UTC ticks of the record (for UI freshness display).</summary>
        public long   RecordedAtTicks { get; private set; }

        /// <summary>
        /// Creates a new MealRecord with all fields set.
        /// </summary>
        public MealRecord(string foodName, float kcalEaten, int quality, int moraleEffect,
                          float cycle, float hour)
        {
            FoodName      = foodName;
            KcalEaten     = kcalEaten;
            Quality       = quality;
            MoraleEffect  = moraleEffect;
            Cycle         = cycle;
            Hour          = hour;
            RecordedAtTicks = global::System.DateTime.UtcNow.Ticks;
        }
    }

    private static readonly List<MealRecord> _history = new List<MealRecord>();
    private static MinionIdentity? _minion;
    private static int _subscriptionHandle;

    /// <summary>Snapshot of recorded meals, newest first.</summary>
    public static IReadOnlyList<MealRecord> History => _history;

    /// <summary>
    /// Attaches the tracker to <paramref name="minion"/>, subscribing to eat events on its GameObject.
    /// Any previous attachment is detached first.
    /// </summary>
    /// <param name="minion">The duplicant to track.</param>
    /// <pre><paramref name="minion"/> is not null and its GameObject is alive.</pre>
    /// <post>Eat events on this minion populate <see cref="History"/>.</post>
    public static void Attach(MinionIdentity minion)
    {
        Detach();
        if (minion == null || minion.gameObject == null) return;
        _minion = minion;
        _history.Clear();
        _subscriptionHandle = minion.gameObject.Subscribe(EatCompleteEaterHash, OnEatComplete);
        NeuroLogger.Log("[EatingTracker] Attached to " + minion.GetProperName(), "EatingTracker");
    }

    /// <summary>
    /// Detaches the tracker from the current duplicant, unsubscribing from eat events.
    /// Safe to call when not attached.
    /// </summary>
    /// <post><see cref="History"/> is preserved but no new meals are recorded.</post>
    public static void Detach()
    {
        if (_minion != null && _minion.gameObject != null)
        {
            try { _minion.gameObject.Unsubscribe(_subscriptionHandle); }
            catch { }
        }
        _minion = null;
        _subscriptionHandle = 0;
    }

    // ── Event handler ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the game when the tracked duplicant finishes eating.
    /// <paramref name="data"/> is the <see cref="Edible"/> that was consumed.
    /// </summary>
    private static void OnEatComplete(object data)
    {
        try
        {
            Edible? edible = data as Edible;
            if (edible == null) return;

            EdiblesManager.FoodInfo? info = edible.FoodInfo;
            if (info == null) return;

            float caloriesConsumed = GetCaloriesConsumed(edible);

            // Morale from food quality effect
            int morale = GetMoraleForQuality(info.Quality);

            float cycle = GameClock.Instance?.GetCycle() ?? 0f;
            float hour  = GameClock.Instance != null
                ? (float)(GameClock.Instance.GetTime() % 600.0 / 600.0 * 24.0)
                : 0f;

            var record = new MealRecord(
                foodName:     StripRichText(edible.GetProperName()),
                kcalEaten:    caloriesConsumed / 1000f,
                quality:      info.Quality,
                moraleEffect: morale,
                cycle:        cycle,
                hour:         hour
            );

            // Prepend so newest is first; keep rolling window.
            _history.Insert(0, record);
            if (_history.Count > MaxMeals)
                _history.RemoveAt(_history.Count - 1);

            NeuroLogger.Log(
                $"[EatingTracker] Ate: {record.FoodName} ({record.KcalEaten:F0} kcal, quality {record.Quality}, morale {record.MoraleEffect:+0;-0;0}) @ cycle {record.Cycle:F1} h{record.Hour:F0}",
                "EatingTracker");
        }
        catch (global::System.Exception ex)
        {
            NeuroLogger.LogError("[EatingTracker] Error in OnEatComplete: " + ex.Message, "EatingTracker");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the calories actually consumed during this meal.
    /// <c>caloriesConsumed</c> is a public field on <see cref="Edible"/> that accumulates
    /// eaten units during <c>OnWork</c> and is set to NaN AFTER the EatCompleteEater event fires,
    /// so it is valid at the point this handler runs.
    /// </summary>
    private static float GetCaloriesConsumed(Edible edible)
    {
        // caloriesConsumed is public — no reflection needed.
        // It holds the raw internal calorie units consumed (1 kcal = 1000 units).
        float v = edible.caloriesConsumed;
        if (!float.IsNaN(v) && v > 0f) return v;

        // Fallback: totalConsumableCalories (whole item size) if somehow not set.
        return edible.FoodInfo?.CaloriesPerUnit * edible.Units ?? 0f;
    }

    /// <summary>Removes ONI rich-text tags (e.g. &lt;link="…"&gt;…&lt;/link&gt;) from a display string.</summary>
    private static string StripRichText(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // Remove <link="..."> and </link> wrappers; keep the inner text.
        s = global::System.Text.RegularExpressions.Regex.Replace(s, @"<link=[^>]*>", "");
        s = global::System.Text.RegularExpressions.Regex.Replace(s, @"</link>", "");
        // Remove any remaining Unity rich-text tags e.g. <color=…>, <b>, <i>.
        s = global::System.Text.RegularExpressions.Regex.Replace(s, @"<[^>]+>", "");
        return s.Trim();
    }

    /// <summary>Returns the net morale contribution for the given food quality tier.</summary>
    private static int GetMoraleForQuality(int quality)
    {
        try
        {
            string effectId = Edible.GetEffectForFoodQuality(quality);
            string moraleId = Db.Get().Attributes.QualityOfLife.Id;
            foreach (AttributeModifier mod in Db.Get().effects.Get(effectId).SelfModifiers)
            {
                if (mod.AttributeId == moraleId)
                    return Mathf.RoundToInt(mod.Value);
            }
        }
        catch { }
        return 0;
    }
}
