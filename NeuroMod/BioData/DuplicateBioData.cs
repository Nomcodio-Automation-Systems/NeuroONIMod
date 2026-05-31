using Klei.AI;
using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Provides a snapshot-style view over the tracked duplicate bio data.
/// </summary>
/// <pre><paramref name="minion"/> references the live duplicate that owns the queried components.</pre>
/// <post>Public properties return a safe default when the underlying game component is unavailable.</post>
public class DuplicateBioData(MinionIdentity minion)
{
    private readonly MinionIdentity minionIdentity = minion;

    // Lazy accessors for components to avoid null reference during initialization
    private Health? Health => minionIdentity?.GetComponent<Health>();

    private Effects? Effects => minionIdentity?.GetComponent<Effects>();

    private float GetAmountPercentage(AmountInstance? amount)
    {
        if (amount is null)
        {
            return 0f;
        }

        float maxValue = amount.GetMax();
        return maxValue > 0f ? amount.value / maxValue : 0f;
    }

    private AmountInstance? LookupAmount(Amount amount)
    {
        return minionIdentity?.gameObject is not null ? amount.Lookup(minionIdentity.gameObject) : null;
    }

    #region Health Data

    public float HealthPercentage => Health != null && Health.maxHitPoints > 0 ? (Health.hitPoints / Health.maxHitPoints) : 0f;
    public float CurrentHealth => Health?.hitPoints ?? 0f;
    public float MaxHealth => Health?.maxHitPoints ?? 0f;
    public Health.HealthState HealthState => Health?.State ?? Health.HealthState.Dead;
    public bool IsWounded => Health != null && Health.State != Health.HealthState.Perfect && Health.State != Health.HealthState.Alright;
    public bool IsIncapacitated => Health?.IsIncapacitated() ?? false;
    public bool IsDead => Health?.State == Health.HealthState.Dead;

    #endregion Health Data

    #region Hunger/Calorie Data

    public float CaloriePercentage
    {
        get
        {
            return GetAmountPercentage(LookupAmount(Db.Get().Amounts.Calories));
        }
    }

    public float CurrentCalories
    {
        get
        {
            AmountInstance? calories = LookupAmount(Db.Get().Amounts.Calories);
            return calories?.value ?? 0f;
        }
    }

    public float MaxCalories
    {
        get
        {
            AmountInstance? calories = LookupAmount(Db.Get().Amounts.Calories);
            return calories?.GetMax() ?? 0f;
        }
    }

    public bool IsHungry => CaloriePercentage < 0.8f;
    public bool IsStarving => CaloriePercentage < 0.2f;

    #endregion Hunger/Calorie Data

    #region Stamina Data

    public float StaminaPercentage
    {
        get
        {
            return GetAmountPercentage(LookupAmount(Db.Get().Amounts.Stamina));
        }
    }

    public float CurrentStamina
    {
        get
        {
            AmountInstance? stamina = LookupAmount(Db.Get().Amounts.Stamina);
            return stamina?.value ?? 0f;
        }
    }

    public bool IsTired => StaminaPercentage < 0.3f;
    public bool IsExhausted => StaminaPercentage < 0.1f;

    #endregion Stamina Data

    #region Bladder Data

    public float BladderPercentage
    {
        get
        {
            return GetAmountPercentage(LookupAmount(Db.Get().Amounts.Bladder));
        }
    }

    public bool NeedsBathroom => BladderPercentage > 0.8f;

    #endregion Bladder Data

    #region Stress Data

    public float StressPercentage
    {
        get
        {
            return GetAmountPercentage(LookupAmount(Db.Get().Amounts.Stress));
        }
    }

    public bool IsStressed => StressPercentage > 0.6f;
    public bool IsHighlyStressed => StressPercentage > 0.8f;

    #endregion Stress Data

    #region Sickness Data

    public bool IsSick
    {
        get
        {
            if (minionIdentity?.gameObject == null)
            {
                return false;
            }

            Sicknesses sicknesses = minionIdentity.gameObject.GetSicknesses();
            return sicknesses != null && sicknesses.Count > 0;
        }
    }

    public List<string> CurrentSicknesses
    {
        get
        {
            List<string> sicknesses = [];
            if (minionIdentity?.gameObject != null)
            {
                Sicknesses sicknessInstances = minionIdentity.gameObject.GetSicknesses();
                if (sicknessInstances != null)
                {
                    foreach (SicknessInstance? sickness in sicknessInstances)
                    {
                        if (sickness is not null)
                        {
                            sicknesses.Add(sickness.modifier.Name);
                        }
                    }
                }
            }
            return sicknesses;
        }
    }

    #endregion Sickness Data

    #region Effects Data

    public List<string> CurrentEffects
    {
        get
        {
            List<string> effectList = [];
            if (Effects != null)
            {
                // Use reflection or try different approach since direct enumeration isn't available
                try
                {
                    // Try to access effects through the modifiers if available
                    Modifiers modifiers = Effects.GetComponent<Modifiers>();
                    if (modifiers != null)
                    {
                        // This is a simplified approach - actual implementation may vary
                        effectList.Add("Effects present"); // Placeholder
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Could not enumerate effects: {ex.Message}");
                }
            }
            return effectList;
        }
    }

    #endregion Effects Data

    #region Oxygen Data

    public float OxygenPercentage
    {
        get
        {
            return GetAmountPercentage(LookupAmount(Db.Get().Amounts.Breath));
        }
    }

    public bool NeedsOxygen => OxygenPercentage < 0.2f;

    #endregion Oxygen Data

    #region Temperature Data

    public float BodyTemperature
    {
        get
        {
            AmountInstance? temperature = LookupAmount(Db.Get().Amounts.Temperature);
            return temperature?.value ?? 0f;
        }
    }

    public bool IsOverheating => BodyTemperature > 310f; // Approximately above 37C
    public bool IsFreezing => BodyTemperature < 290f; // Approximately below 17C

    #endregion Temperature Data

    #region Update Methods

    /// <summary>
    /// Forces the current snapshot to touch each tracked metric once.
    /// </summary>
    /// <pre>The instance is associated with a duplicate that may or may not be fully initialized.</pre>
    /// <post>No exception is thrown for missing game components; unavailable values remain at their safe defaults.</post>
    public void UpdateAllData()
    {
        // Force refresh all cached values by accessing properties
        var _ = new
        {
            HealthPercentage,
            CaloriePercentage,
            StaminaPercentage,
            BladderPercentage,
            StressPercentage,
            OxygenPercentage,
            BodyTemperature,
            IsSick
        };
    }

    #endregion Update Methods

    #region Summary Methods

    /// <summary>
    /// Builds a short health-focused summary for the duplicate.
    /// </summary>
    /// <pre>The snapshot may contain fallback values when health is not yet initialized.</pre>
    /// <post>The returned string always contains the current health percentage and health state.</post>
    public string GetHealthSummary()
    {
        return $"Health: {HealthPercentage:P1} ({HealthState})";
    }

    /// <summary>
    /// Builds a compact list of unmet needs inferred from the tracked bio data.
    /// </summary>
    /// <pre>The snapshot values have already been normalized into percentages or safe defaults.</pre>
    /// <post>Returns <c>All Good</c> only when no monitored need currently crosses its warning threshold.</post>
    public string GetNeedsSummary()
    {
        List<string> needs = [];

        if (IsHungry)
        {
            needs.Add("Hungry");
        }

        if (IsTired)
        {
            needs.Add("Tired");
        }

        if (NeedsBathroom)
        {
            needs.Add("Bathroom");
        }

        if (IsStressed)
        {
            needs.Add("Stressed");
        }

        if (NeedsOxygen)
        {
            needs.Add("Oxygen");
        }

        if (IsOverheating)
        {
            needs.Add("Hot");
        }

        if (IsFreezing)
        {
            needs.Add("Cold");
        }

        if (IsSick)
        {
            needs.Add("Sick");
        }

        return needs.Count > 0 ? string.Join(", ", needs) : "All Good";
    }

    /// <summary>
    /// Formats the duplicate name together with the current health and need summaries.
    /// </summary>
    /// <pre>The owning duplicate identity is still available to provide a display name.</pre>
    /// <post>The returned string is suitable for logs and diagnostic output.</post>
    public override string ToString()
    {
        return $"{minionIdentity.GetProperName()}: {GetHealthSummary()} | Needs: {GetNeedsSummary()}";
    }

    #endregion Summary Methods
}