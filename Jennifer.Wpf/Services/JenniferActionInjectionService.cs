using Jennifer.Wpf.Config.ActionInjection;
using Jennifer.Wpf.Parsing;
using System.Collections.Generic;
using System.Text.Json;

namespace Jennifer.Wpf.Services;

/// <summary>
/// Converts the persisted <see cref="JenniferActionInjectionConfig"/> into
/// <see cref="JenniferDiscoveredAction"/> instances that Jennifer merges into the
/// full action catalog and injects into the server-side registration payload.
/// </summary>
/// <post>The returned actions are tagged with source <c>"injection"</c> so the UI can distinguish them.</post>
public static class JenniferActionInjectionService
{
    private static readonly JsonDocumentOptions _jsonOptions = new() { AllowTrailingCommas = true };

    /// <summary>
    /// Loads the injection config from disk and converts its entries to discovered actions.
    /// </summary>
    /// <returns>The list of injected actions ready for catalog merging.</returns>
    /// <post>An empty list is returned when no config file exists or the file is empty.</post>
    public static List<JenniferDiscoveredAction> LoadInjectedActions()
    {
        JenniferActionInjectionConfig config = JenniferActionInjectionStore.EnsureDefaultExists();
        return ConvertToDiscoveredActions(config);
    }

    /// <summary>
    /// Converts <paramref name="config"/> entries to <see cref="JenniferDiscoveredAction"/> instances.
    /// </summary>
    /// <param name="config">The injection config to convert.</param>
    /// <returns>The converted discovered actions.</returns>
    public static List<JenniferDiscoveredAction> ConvertToDiscoveredActions(JenniferActionInjectionConfig config)
    {
        var result = new List<JenniferDiscoveredAction>();

        foreach (InjectedAction injected in config.Actions)
        {
            if (string.IsNullOrWhiteSpace(injected.Name))
                continue;

            var action = new JenniferDiscoveredAction
            {
                Name        = injected.Name.Trim(),
                Description = injected.Description ?? string.Empty,
                HasSchema   = !string.IsNullOrWhiteSpace(injected.SchemaJson),
                Source      = "injection",
                Parameters  = ParseParameters(injected.SchemaJson),
            };

            result.Add(action);
        }

        return result;
    }

    /// <summary>
    /// Parses a JSON schema string into a list of <see cref="JenniferActionParameter"/> objects.
    /// Only top-level object properties are extracted; nested schemas become a single parameter with no enum.
    /// </summary>
    /// <param name="schemaJson">The raw JSON schema string.</param>
    /// <returns>The extracted parameters, or an empty list when the schema is absent or unparsable.</returns>
    private static List<JenniferActionParameter> ParseParameters(string? schemaJson)
    {
        var parameters = new List<JenniferActionParameter>();

        if (string.IsNullOrWhiteSpace(schemaJson))
            return parameters;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(schemaJson, _jsonOptions);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("properties", out JsonElement props))
                return parameters;

            // Collect required fields
            var required = new HashSet<string>();
            if (root.TryGetProperty("required", out JsonElement req) && req.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement r in req.EnumerateArray())
                    if (r.ValueKind == JsonValueKind.String)
                        required.Add(r.GetString()!);
            }

            foreach (JsonProperty prop in props.EnumerateObject())
            {
                var param = new JenniferActionParameter
                {
                    Name     = prop.Name,
                    JsonType = prop.Value.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? "string" : "string",
                    IsRequired = required.Contains(prop.Name),
                };

                // Collect enum values if present
                if (prop.Value.TryGetProperty("enum", out JsonElement enumEl) && enumEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement ev in enumEl.EnumerateArray())
                    {
                        string? val = ev.ValueKind == JsonValueKind.String ? ev.GetString()
                                    : ev.ValueKind == JsonValueKind.Number ? ev.GetRawText()
                                    : null;
                        if (val != null)
                            param.EnumValues.Add(val);
                    }
                }

                parameters.Add(param);
            }
        }
        catch
        {
            // Malformed schema — return whatever was parsed so far
        }

        return parameters;
    }
}
