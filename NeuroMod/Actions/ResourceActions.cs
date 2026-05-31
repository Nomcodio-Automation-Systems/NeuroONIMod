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
/// Lists resources available in the colony's storage, optionally filtered by category or minimum quantity.
/// Helps Neuro understand what materials are on hand for crafting, building, or rationing decisions.
/// </summary>
/// <pre>The game must be running and at least one storage object must exist when validation executes.</pre>
/// <post>Returns a snapshot of accessible colony resources without mutating game state.</post>
public class ListResourcesAction : BaseNeuroAction
{
    /// <summary>Gets the protocol name for this action.</summary>
    public override string Name => "list_resources";

    /// <summary>Gets the human-readable description registered with the Neuro SDK.</summary>
    protected override string Description =>
        "List resources currently available in colony storage. " +
        "Use category to narrow results (food / material / gas / liquid / medical / all). " +
        "Use name_filter to search for a specific resource by partial name (e.g. 'algae', 'iron'). " +
        "Set min_quantity to hide trace amounts. " +
        "Returns each resource's name, total stored amount, and unit. " +
        "Useful for checking if there are enough supplies for tasks.";

    /// <summary>Gets the JSON schema for the list-resources request.</summary>
    protected override JsonSchema? Schema => new()
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, JsonSchema>
        {
            ["category"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object> { "all", "food", "material", "gas", "liquid", "medical" }
            },
            ["name_filter"] = new JsonSchema
            {
                Type = JsonSchemaType.String
            },
            ["min_quantity"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            },
            ["max_results"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer
            },
            ["format"] = new JsonSchema
            {
                Type = JsonSchemaType.String,
                Enum = new List<object> { "text", "json" }
            }
        }
    };

    /// <summary>
    /// Validates the request, scans colony storages, and returns matching resource entries.
    /// </summary>
    /// <param name="actionData">Incoming JSON action payload.</param>
    /// <param name="parsedData">Always null; output is returned in the ExecutionResult.</param>
    /// <returns>Success with resource list, or failure if an error occurs.</returns>
    /// <pre>The game world must be loaded.</pre>
    /// <post>On success the result contains resource data sorted by quantity descending; game state is unchanged.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out object? parsedData)
    {
        parsedData = null;

        try
        {
            string category   = actionData.Data?["category"]?.Value<string>()    ?? "all";
            string nameFilter = actionData.Data?["name_filter"]?.Value<string>()  ?? "";
            float  minQty     = actionData.Data?["min_quantity"]?.Value<float>()  ?? 1f;
            int    maxResults = actionData.Data?["max_results"]?.Value<int>()     ?? 50;
            string format     = actionData.Data?["format"]?.Value<string>()       ?? "text";

            if (maxResults < 1 || maxResults > 200)
                return ExecutionResult.Failure("max_results must be between 1 and 200.");

            List<ResourceEntry> entries = CollectResources(category, nameFilter, minQty, maxResults);

            if (entries.Count == 0)
                return ExecutionResult.Success($"No resources found for category '{category}' with min quantity {minQty}.");

            string result = format == "json"
                ? BuildJsonList(entries, category)
                : BuildTextList(entries, category);

            NeuroLogger.Log($"[ListResourcesAction] Listed {entries.Count} resources (category={category}, name_filter='{nameFilter}')", "ListResourcesAction", ActionWindow?.TraceId);
            return ExecutionResult.Success(result);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"[ListResourcesAction] Error: {ex.Message}", "ListResourcesAction", ActionWindow?.TraceId);
            return ExecutionResult.Failure($"Error listing resources: {ex.Message}");
        }
    }

    protected override UniTask ExecuteAsync(object? data) => UniTask.CompletedTask;

    // ── Data collection ───────────────────────────────────────────────────────

    private sealed class ResourceEntry
    {
        public string Name     { get; }
        public float  Amount   { get; }
        public string Unit     { get; }
        public string Category { get; }

        public ResourceEntry(string name, float amount, string unit, string category)
        {
            Name = name; Amount = amount; Unit = unit; Category = category;
        }
    }

    private static List<ResourceEntry> CollectResources(string category, string nameFilter, float minQty, int maxResults)
    {
        // Aggregate quantities per resource tag across all storage objects
        Dictionary<Tag, float> totals = new Dictionary<Tag, float>();

        foreach (Storage storage in UnityEngine.Object.FindObjectsOfType<Storage>())
        {
            if (storage == null || storage.gameObject == null)
                continue;

            foreach (GameObject item in storage.items)
            {
                if (item == null)
                    continue;

                PrimaryElement pe = item.GetComponent<PrimaryElement>();
                if (pe == null || pe.Mass < 0.001f)
                    continue;

                Tag tag = item.PrefabID();
                if (totals.ContainsKey(tag))
                    totals[tag] += pe.Mass;
                else
                    totals[tag] = pe.Mass;
            }
        }

        var results = new List<ResourceEntry>();

        foreach (KeyValuePair<Tag, float> kv in totals)
        {
            if (kv.Value < minQty)
                continue;

            GameObject prefab = Assets.TryGetPrefab(kv.Key);
            if (prefab == null)
                continue;

            string entryCategory = ClassifyPrefab(prefab);
            if (category != "all" && entryCategory != category)
                continue;

            string name = prefab.GetProperName();
            if (string.IsNullOrWhiteSpace(name))
                name = kv.Key.ProperName();

            if (!string.IsNullOrEmpty(nameFilter) &&
                name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            string unit = entryCategory == "food" ? "kcal" : "kg";
            results.Add(new ResourceEntry(name, kv.Value, unit, entryCategory));

            if (results.Count >= maxResults)
                break;
        }

        results.Sort((a, b) => b.Amount.CompareTo(a.Amount));
        return results;
    }

    private static string ClassifyPrefab(GameObject prefab)
    {
        if (prefab.HasTag(GameTags.Edible) || prefab.HasTag(GameTags.CookingIngredient) || prefab.HasTag(GameTags.Seed))
            return "food";

        if (prefab.HasTag(GameTags.Medicine))
            return "medical";

        Element? element = prefab.GetComponent<PrimaryElement>()?.Element;
        if (element != null)
        {
            if (element.state == Element.State.Gas)    return "gas";
            if (element.state == Element.State.Liquid) return "liquid";
        }

        return "material";
    }

    // ── Formatters ────────────────────────────────────────────────────────────

    private static string BuildTextList(List<ResourceEntry> entries, string category)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Colony Resources — {category} ({entries.Count} items):");
        foreach (ResourceEntry e in entries)
            sb.AppendLine($"  {e.Name}: {e.Amount:F1} {e.Unit}");
        return sb.ToString().TrimEnd('\n');
    }

    private static string BuildJsonList(List<ResourceEntry> entries, string category)
    {
        var arr = new JArray();
        foreach (ResourceEntry e in entries)
            arr.Add(new JObject
            {
                ["name"]     = e.Name,
                ["amount"]   = Math.Round(e.Amount, 1),
                ["unit"]     = e.Unit,
                ["category"] = e.Category
            });
        return new JObject { ["category"] = category, ["resources"] = arr }.ToString();
    }
}
