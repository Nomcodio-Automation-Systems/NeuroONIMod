using Klei.AI;
using System.Collections.Generic;

namespace NeuroMod;

/// <summary>
/// Container class for all duplicate bio data
/// </summary>
public class DuplicateBioData(MinionIdentity minion)
{
    private readonly MinionIdentity minionIdentity = minion;

    // Lazy accessors for components to avoid null reference during initialization
    private Health? Health => minionIdentity?.GetComponent<Health>();

    private Effects? Effects => minionIdentity?.GetComponent<Effects>();

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
            if (minionIdentity?.gameObject == null)
            {
                return 0f;
            }

            AmountInstance calories = Db.Get().Amounts.Calories.Lookup(minionIdentity.gameObject);
            return calories != null ? (calories.value / calories.GetMax()) : 0f;
        }
    }

    public float CurrentCalories
    {
        get
        {
            if (minionIdentity?.gameObject == null)
            {
                return 0f;
            }

            AmountInstance calories = Db.Get().Amounts.Calories.Lookup(minionIdentity.gameObject);
            return calories?.value ?? 0f;
        }
    }

    public float MaxCalories
    {
        get
        {
            if (minionIdentity?.gameObject == null)
            {
                return 0f;
            }

            AmountInstance calories = Db.Get().Amounts.Calories.Lookup(minionIdentity.gameObject);
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
            if (minionIdentity?.gameObject == null)
            {
                return 0f;
            }

            AmountInstance stamina = Db.Get().Amounts.Stamina.Lookup(minionIdentity.gameObject);
            return stamina != null ? (stamina.value / stamina.GetMax()) : 0f;
        }
    }

    public float CurrentStamina
    {
        get
        {
            if (minionIdentity?.gameObject == null)
            {
                return 0f;
            }

            AmountInstance stamina = Db.Get().Amounts.Stamina.Lookup(minionIdentity.gameObject);
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
            if (minionIdentity?.gameObject == null)
            {
                return 0f;
            }

            AmountInstance bladder = Db.Get().Amounts.Bladder.Lookup(minionIdentity.gameObject);
            return bladder != null ? (bladder.value / bladder.GetMax()) : 0f;
        }
    }

    public bool NeedsBathroom => BladderPercentage > 0.8f;

    #endregion Bladder Data

    #region Stress Data

    public float StressPercentage
    {
        get
        {
            if (minionIdentity?.gameObject == null)
            {
                return 0f;
            }

            AmountInstance stress = Db.Get().Amounts.Stress.Lookup(minionIdentity.gameObject);
            return stress != null ? (stress.value / stress.GetMax()) : 0f;
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
                        sicknesses.Add(sickness.modifier.Name);
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
            if (minionIdentity?.gameObject == null)
            {
                return 0f;
            }

            AmountInstance breath = Db.Get().Amounts.Breath.Lookup(minionIdentity.gameObject);
            return breath != null ? (breath.value / breath.GetMax()) : 0f;
        }
    }

    public bool NeedsOxygen => OxygenPercentage < 0.2f;

    #endregion Oxygen Data

    #region Temperature Data

    public float BodyTemperature
    {
        get
        {
            if (minionIdentity?.gameObject == null)
            {
                return 0f;
            }

            AmountInstance temperature = Db.Get().Amounts.Temperature.Lookup(minionIdentity.gameObject);
            return temperature?.value ?? 0f;
        }
    }

    public bool IsOverheating => BodyTemperature > 310f; // > 37�C
    public bool IsFreezing => BodyTemperature < 290f; // < 17�C

    #endregion Temperature Data

    #region Update Methods

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

    public string GetHealthSummary()
    {
        return $"Health: {HealthPercentage:P1} ({HealthState})";
    }

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

    public override string ToString()
    {
        return $"{minionIdentity.GetProperName()}: {GetHealthSummary()} | Needs: {GetNeedsSummary()}";
    }

    #endregion Summary Methods
}