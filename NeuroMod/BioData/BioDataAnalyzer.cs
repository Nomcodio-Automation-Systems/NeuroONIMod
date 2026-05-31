using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NeuroMod;

/// <summary>
/// Provides aggregate analysis utilities over the current duplicate bio data snapshot.
/// </summary>
/// <pre>The patch cache can resolve bio data for the live duplicates currently in the colony.</pre>
/// <post>All analysis methods operate on a point-in-time snapshot and do not mutate live game state.</post>
public static class BioDataAnalyzer
{
    /// <summary>
    /// Aggregates the current colony-wide health statistics.
    /// </summary>
    /// <returns>A snapshot of the colony health metrics derived from the currently tracked duplicates.</returns>
    /// <pre>The bio-data patch layer has been initialized.</pre>
    /// <post>The returned statistics object always contains non-null counters and normalized averages.</post>
    public static ColonyHealthStats AnalyzeColonyHealth()
    {
        Dictionary<MinionIdentity, DuplicateBioData> allBioData = DuplicateBioDataPatches.GetAllBioData();
        ColonyHealthStats stats = new();

        if (allBioData.Count == 0)
        {
            return stats;
        }

        stats.TotalDuplicates = allBioData.Count;

        foreach (KeyValuePair<MinionIdentity, DuplicateBioData> kvp in allBioData)
        {
            DuplicateBioData bioData = kvp.Value;

            // Health statistics
            stats.AverageHealth += bioData.HealthPercentage;
            if (bioData.HealthPercentage < 0.3f)
            {
                stats.CriticalHealthCount++;
            }

            if (bioData.IsWounded)
            {
                stats.WoundedCount++;
            }

            if (bioData.IsDead)
            {
                stats.DeadCount++;
            }

            // Nutrition statistics
            stats.AverageCalories += bioData.CaloriePercentage;
            if (bioData.IsHungry)
            {
                stats.HungryCount++;
            }

            if (bioData.IsStarving)
            {
                stats.StarvingCount++;
            }

            // Stamina statistics
            stats.AverageStamina += bioData.StaminaPercentage;
            if (bioData.IsTired)
            {
                stats.TiredCount++;
            }

            if (bioData.IsExhausted)
            {
                stats.ExhaustedCount++;
            }

            // Stress statistics
            stats.AverageStress += bioData.StressPercentage;
            if (bioData.IsStressed)
            {
                stats.StressedCount++;
            }

            if (bioData.IsHighlyStressed)
            {
                stats.HighlyStressedCount++;
            }

            // Other conditions
            if (bioData.IsSick)
            {
                stats.SickCount++;
            }

            if (bioData.NeedsBathroom)
            {
                stats.BathroomNeedCount++;
            }

            if (bioData.IsOverheating)
            {
                stats.OverheatingCount++;
            }

            if (bioData.IsFreezing)
            {
                stats.FreezingCount++;
            }
        }

        // Calculate averages
        stats.AverageHealth /= allBioData.Count;
        stats.AverageCalories /= allBioData.Count;
        stats.AverageStamina /= allBioData.Count;
        stats.AverageStress /= allBioData.Count;

        return stats;
    }

    /// <summary>
    /// Builds the set of immediate alerts that should be surfaced to callers.
    /// </summary>
    /// <returns>A severity-sorted list of alerts for the current colony snapshot.</returns>
    /// <pre>The current bio data snapshot can be enumerated without mutating game state.</pre>
    /// <post>The returned list is ordered from highest severity to lowest severity.</post>
    public static List<DuplicateAlert> GetImmediateAlerts()
    {
        List<DuplicateAlert> alerts = [];
        Dictionary<MinionIdentity, DuplicateBioData> allBioData = DuplicateBioDataPatches.GetAllBioData();

        foreach (KeyValuePair<MinionIdentity, DuplicateBioData> kvp in allBioData)
        {
            MinionIdentity minion = kvp.Key;
            DuplicateBioData bioData = kvp.Value;

            // Critical health
            if (bioData.HealthPercentage < 0.2f && !bioData.IsDead)
            {
                alerts.Add(new DuplicateAlert
                {
                    Minion = minion,
                    AlertType = AlertType.CriticalHealth,
                    Severity = AlertSeverity.Critical,
                    Message = $"Critical health: {bioData.HealthPercentage:P1}",
                    Value = bioData.HealthPercentage
                });
            }

            // Starvation
            if (bioData.IsStarving)
            {
                alerts.Add(new DuplicateAlert
                {
                    Minion = minion,
                    AlertType = AlertType.Starvation,
                    Severity = AlertSeverity.Critical,
                    Message = $"Starving: {bioData.CaloriePercentage:P1}",
                    Value = bioData.CaloriePercentage
                });
            }

            // High stress
            if (bioData.IsHighlyStressed)
            {
                alerts.Add(new DuplicateAlert
                {
                    Minion = minion,
                    AlertType = AlertType.HighStress,
                    Severity = AlertSeverity.High,
                    Message = $"High stress: {bioData.StressPercentage:P1}",
                    Value = bioData.StressPercentage
                });
            }

            // Sickness
            if (bioData.IsSick)
            {
                alerts.Add(new DuplicateAlert
                {
                    Minion = minion,
                    AlertType = AlertType.Sickness,
                    Severity = AlertSeverity.Medium,
                    Message = $"Sick: {string.Join(", ", bioData.CurrentSicknesses)}",
                    Value = 1.0f
                });
            }

            // Temperature issues
            if (bioData.IsOverheating || bioData.IsFreezing)
            {
                alerts.Add(new DuplicateAlert
                {
                    Minion = minion,
                    AlertType = AlertType.Temperature,
                    Severity = AlertSeverity.Medium,
                    Message = $"Temperature: {bioData.BodyTemperature:F1}K",
                    Value = bioData.BodyTemperature
                });
            }
        }

        // Sort by severity and value
        return [.. alerts.OrderByDescending(a => (int)a.Severity).ThenBy(a => a.Value)];
    }

    /// <summary>
    /// Ranks duplicates from least healthy to most healthy according to the analyzer score.
    /// </summary>
    /// <returns>The duplicate rankings ordered by ascending health score.</returns>
    /// <pre>The analyzer score weights are accepted as the current colony-health heuristic.</pre>
    /// <post>The first returned entry represents the weakest overall health score in the current snapshot.</post>
    public static List<DuplicateHealthRanking> RankDuplicatesByHealth()
    {
        List<DuplicateHealthRanking> rankings = [];
        Dictionary<MinionIdentity, DuplicateBioData> allBioData = DuplicateBioDataPatches.GetAllBioData();

        foreach (KeyValuePair<MinionIdentity, DuplicateBioData> kvp in allBioData)
        {
            MinionIdentity minion = kvp.Key;
            DuplicateBioData bioData = kvp.Value;

            DuplicateHealthRanking ranking = new()
            {
                Minion = minion,
                HealthScore = CalculateOverallHealthScore(bioData),
                HealthPercentage = bioData.HealthPercentage,
                CaloriePercentage = bioData.CaloriePercentage,
                StaminaPercentage = bioData.StaminaPercentage,
                StressPercentage = bioData.StressPercentage,
                Issues = GetHealthIssues(bioData)
            };

            rankings.Add(ranking);
        }

        return [.. rankings.OrderBy(r => r.HealthScore)];
    }

    /// <summary>
    /// Calculate overall health score (lower is worse)
    /// </summary>
    /// <param name="bioData">The duplicate bio-data snapshot to score.</param>
    /// <returns>A normalized health score between 0 and 1.</returns>
    /// <pre><paramref name="bioData"/> contains the health-related values to aggregate into a score.</pre>
    /// <post>A clamped score describing the duplicate's overall condition is returned.</post>
    private static float CalculateOverallHealthScore(DuplicateBioData bioData)
    {
        float score = 0f;

        // Health weight: 40%
        score += bioData.HealthPercentage * 0.4f;

        // Calories weight: 25%
        score += bioData.CaloriePercentage * 0.25f;

        // Stamina weight: 15%
        score += bioData.StaminaPercentage * 0.15f;

        // Stress weight: 20% (inverted - lower stress is better)
        score += (1.0f - bioData.StressPercentage) * 0.2f;

        // Penalties for specific conditions
        if (bioData.IsSick)
        {
            score -= 0.3f;
        }

        if (bioData.IsOverheating || bioData.IsFreezing)
        {
            score -= 0.2f;
        }

        if (bioData.NeedsOxygen)
        {
            score -= 0.4f;
        }

        return Mathf.Clamp01(score);
    }

    /// <summary>
    /// Get list of health issues for a duplicate
    /// </summary>
    /// <param name="bioData">The duplicate bio-data snapshot to inspect.</param>
    /// <returns>A list of human-readable health issues currently affecting the duplicate.</returns>
    /// <pre><paramref name="bioData"/> contains the condition flags used to build the issue list.</pre>
    /// <post>The returned list contains one entry per detected issue and may be empty when no issues are present.</post>
    private static List<string> GetHealthIssues(DuplicateBioData bioData)
    {
        List<string> issues = [];

        if (bioData.HealthPercentage < 0.5f)
        {
            issues.Add("Low Health");
        }

        if (bioData.IsHungry)
        {
            issues.Add("Hungry");
        }

        if (bioData.IsTired)
        {
            issues.Add("Tired");
        }

        if (bioData.IsStressed)
        {
            issues.Add("Stressed");
        }

        if (bioData.IsSick)
        {
            issues.Add("Sick");
        }

        if (bioData.NeedsBathroom)
        {
            issues.Add("Needs Bathroom");
        }

        if (bioData.IsOverheating)
        {
            issues.Add("Overheating");
        }

        if (bioData.IsFreezing)
        {
            issues.Add("Freezing");
        }

        if (bioData.NeedsOxygen)
        {
            issues.Add("Low Oxygen");
        }

        return issues;
    }

    /// <summary>
    /// Produces human-readable recommendations based on the current colony health snapshot.
    /// </summary>
    /// <returns>A non-empty list of recommendations or a single healthy-status message.</returns>
    /// <pre>The colony statistics are computed from the current live snapshot.</pre>
    /// <post>The returned list contains at least one recommendation string.</post>
    public static List<string> GetHealthRecommendations()
    {
        List<string> recommendations = [];
        ColonyHealthStats stats = AnalyzeColonyHealth();

        if (stats.TotalDuplicates == 0)
        {
            recommendations.Add("No duplicates found to analyze");
            return recommendations;
        }

        // Health recommendations
        if (stats.CriticalHealthCount > 0)
        {
            recommendations.Add($"URGENT: {stats.CriticalHealthCount} duplicates need medical attention");
        }

        if (stats.AverageHealth < 0.7f)
        {
            recommendations.Add("Colony health is below optimal - consider medical facilities");
        }

        // Nutrition recommendations
        if (stats.StarvingCount > 0)
        {
            recommendations.Add($"URGENT: {stats.StarvingCount} duplicates are starving");
        }

        if (stats.AverageCalories < 0.6f)
        {
            recommendations.Add("Food production may be insufficient");
        }

        // Stress recommendations
        if (stats.HighlyStressedCount > 0)
        {
            recommendations.Add($"WARNING: {stats.HighlyStressedCount} duplicates are highly stressed");
        }

        if (stats.AverageStress > 0.6f)
        {
            recommendations.Add("Consider adding recreation facilities");
        }

        // General recommendations
        if (stats.SickCount > 0)
        {
            recommendations.Add($"{stats.SickCount} duplicates are sick - check air quality");
        }

        if (stats.OverheatingCount > 0 || stats.FreezingCount > 0)
        {
            recommendations.Add("Temperature control needed in some areas");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Colony health looks good!");
        }

        return recommendations;
    }
}

#region Data Structures

public class ColonyHealthStats
{
    public int TotalDuplicates { get; set; }
    public float AverageHealth { get; set; }
    public float AverageCalories { get; set; }
    public float AverageStamina { get; set; }
    public float AverageStress { get; set; }

    public int CriticalHealthCount { get; set; }
    public int WoundedCount { get; set; }
    public int DeadCount { get; set; }
    public int HungryCount { get; set; }
    public int StarvingCount { get; set; }
    public int TiredCount { get; set; }
    public int ExhaustedCount { get; set; }
    public int StressedCount { get; set; }
    public int HighlyStressedCount { get; set; }
    public int SickCount { get; set; }
    public int DirtyCount { get; set; }
    public int BathroomNeedCount { get; set; }
    public int OverheatingCount { get; set; }
    public int FreezingCount { get; set; }

    /// <summary>
    /// Formats the aggregate health statistics for diagnostic output.
    /// </summary>
    /// <pre>The statistics instance has already been populated by the analyzer.</pre>
    /// <post>The returned string contains the total duplicate count and the most relevant health counters.</post>
    public override string ToString()
    {
        return $"Colony Health: {TotalDuplicates} duplicates, " +
               $"Avg Health: {AverageHealth:P1}, " +
               $"Critical: {CriticalHealthCount}, " +
               $"Stressed: {StressedCount}, " +
               $"Sick: {SickCount}";
    }
}

public class DuplicateAlert
{
    public MinionIdentity Minion { get; set; } = null!;
    public AlertType AlertType { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public float Value { get; set; }

    /// <summary>
    /// Formats the alert for logs and diagnostics.
    /// </summary>
    /// <pre><see cref="Minion"/> and <see cref="Message"/> have been populated by the analyzer.</pre>
    /// <post>The returned string always includes the severity, duplicate name, and alert message.</post>
    public override string ToString()
    {
        return $"[{Severity}] {Minion.GetProperName()}: {Message}";
    }
}

public class DuplicateHealthRanking
{
    public MinionIdentity Minion { get; set; } = null!;
    public float HealthScore { get; set; }
    public float HealthPercentage { get; set; }
    public float CaloriePercentage { get; set; }
    public float StaminaPercentage { get; set; }
    public float StressPercentage { get; set; }
    public List<string> Issues { get; set; } = [];

    /// <summary>
    /// Formats the health ranking entry for logs and diagnostics.
    /// </summary>
    /// <pre><see cref="Minion"/> and the score fields have been populated by the analyzer.</pre>
    /// <post>The returned string contains the duplicate name, score, and collected issue labels.</post>
    public override string ToString()
    {
        return $"{Minion.GetProperName()}: Score {HealthScore:F2} " +
               $"(H:{HealthPercentage:P0} C:{CaloriePercentage:P0} " +
               $"S:{StaminaPercentage:P0} St:{StressPercentage:P0}) " +
               $"Issues: {string.Join(", ", Issues)}";
    }
}

public enum AlertType
{
    CriticalHealth,
    Starvation,
    HighStress,
    Sickness,
    Temperature,
    Oxygen,
    Other
}

public enum AlertSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

#endregion Data Structures
