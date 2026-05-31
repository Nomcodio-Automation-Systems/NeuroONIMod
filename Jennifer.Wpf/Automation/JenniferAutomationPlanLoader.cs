using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Jennifer.Wpf.Automation;

/// <summary>
/// Loads and normalizes Jennifer automation plans from JSON.
/// </summary>
/// <post>Loaded plans always contain trimmed values, supported priorities, and normalized step aliases.</post>
public static class JenniferAutomationPlanLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Loads a Jennifer automation plan from a JSON file.
    /// </summary>
    /// <param name="filePath">The source JSON file path.</param>
    /// <returns>The normalized automation plan.</returns>
    /// <post>The returned plan is ready to apply to the Jennifer UI.</post>
    public static JenniferAutomationPlan LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A plan file path is required.", nameof(filePath));
        }

        string json = File.ReadAllText(filePath);
        return LoadFromJson(json);
    }

    /// <summary>
    /// Loads a Jennifer automation plan from raw JSON.
    /// </summary>
    /// <param name="json">The raw JSON payload.</param>
    /// <returns>The normalized automation plan.</returns>
    /// <post>The returned plan omits invalid steps and fills supported defaults.</post>
    public static JenniferAutomationPlan LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Automation plan JSON cannot be empty.", nameof(json));
        }

        JenniferAutomationPlan? plan = JsonSerializer.Deserialize<JenniferAutomationPlan>(json, SerializerOptions);
        if (plan is null)
        {
            throw new InvalidDataException("Automation plan JSON could not be parsed.");
        }

        plan.Name = NormalizeOrFallback(plan.Name, "Untitled Jennifer plan");
        plan.Description = NormalizeOrFallback(plan.Description, string.Empty);
        plan.Endpoint = NormalizeOrFallback(plan.Endpoint, string.Empty);
        plan.GameName = NormalizeOrFallback(plan.GameName, NormalizeOrFallback(plan.Game, string.Empty));
        plan.Steps = NormalizeSteps(plan.Steps);

        return plan;
    }

    /// <summary>
    /// Normalizes a Jennifer automation step list.
    /// </summary>
    /// <param name="steps">The raw step list.</param>
    /// <returns>The filtered and normalized step list.</returns>
    /// <post>Only steps with a valid action name remain in the returned list.</post>
    private static List<JenniferAutomationStep> NormalizeSteps(IEnumerable<JenniferAutomationStep>? steps)
    {
        if (steps is null)
        {
            return new List<JenniferAutomationStep>();
        }

        List<JenniferAutomationStep> normalizedSteps = new();
        foreach (JenniferAutomationStep step in steps)
        {
            string actionName = NormalizeOrFallback(step.ActionName, NormalizeOrFallback(step.Action, string.Empty));
            if (string.IsNullOrWhiteSpace(actionName))
            {
                continue;
            }

            normalizedSteps.Add(new JenniferAutomationStep
            {
                Name = NormalizeOrFallback(step.Name, actionName),
                ActionName = actionName,
                State = NormalizeOrFallback(step.State, string.Empty),
                Query = NormalizeOrFallback(step.Query, string.Empty),
                Priority = NormalizePriority(step.Priority),
                Ephemeral = step.Ephemeral,
                ResultSuccess = step.ResultSuccess,
                ResultMessage = NormalizeOrFallback(step.ResultMessage, string.Empty),
            });
        }

        return normalizedSteps;
    }

    /// <summary>
    /// Normalizes a priority string into the Jennifer-supported set.
    /// </summary>
    /// <param name="priority">The raw priority value.</param>
    /// <returns>A supported Jennifer priority value.</returns>
    /// <post>The returned priority is always one of low, medium, high, or critical.</post>
    private static string NormalizePriority(string? priority)
    {
        string value = NormalizeOrFallback(priority, "low").ToLowerInvariant();
        return value switch
        {
            "low" or "medium" or "high" or "critical" => value,
            _ => "low",
        };
    }

    /// <summary>
    /// Trims a string and falls back when the result is empty.
    /// </summary>
    /// <param name="value">The raw string value.</param>
    /// <param name="fallback">The fallback string.</param>
    /// <returns>The trimmed value or the supplied fallback.</returns>
    /// <post>The returned string is never null.</post>
    private static string NormalizeOrFallback(string? value, string fallback)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}