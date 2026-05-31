using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Jennifer.Wpf.Parsing;

/// <summary>
/// Describes a single schema parameter extracted from a Neuro action source file.
/// </summary>
public sealed class JenniferActionParameter
{
    /// <summary>Gets or sets the parameter field name (e.g. "schedule_type").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON type string (e.g. "string", "integer", "boolean").</summary>
    public string JsonType { get; set; } = "string";

    /// <summary>Gets or sets whether the field is listed in the schema's Required array.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Gets or sets allowed enum values when the field is an enumeration.</summary>
    public List<string> EnumValues { get; set; } = new();
}

/// <summary>
/// Represents a discovered Neuro action from source parsing.
/// </summary>
/// <post>The metadata is suitable for Jennifer registration and quick-action display.</post>
public sealed class JenniferDiscoveredAction
{
    /// <summary>Gets or sets the action name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the action description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the source action declares a schema.</summary>
    public bool HasSchema { get; set; }

    /// <summary>Gets or sets where this action was discovered (e.g. "source", "game", "manual").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets the schema parameters discovered for this action.</summary>
    public List<JenniferActionParameter> Parameters { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether at least one schema parameter is required.
    /// </summary>
    /// <pre>The action metadata has already been populated from source parsing or registration.</pre>
    /// <post>The property returns <see langword="true"/> only when the action cannot be dispatched without supplying parameter values.</post>
    public bool HasRequiredParameters => Parameters.Any(parameter => parameter.IsRequired);

    /// <summary>
    /// Gets a value indicating whether the action can be dispatched without providing parameter data.
    /// </summary>
    /// <pre>The action metadata has already been populated from source parsing or registration.</pre>
    /// <post>The property returns <see langword="true"/> when the action has no schema parameters or all declared parameters are optional.</post>
    public bool SupportsParameterlessDispatch => Parameters.Count == 0 || !HasRequiredParameters;
}

/// <summary>
/// Extracts Jennifer action metadata from NeuroMod source files.
/// Multiple actions per file are supported (e.g. ScheduleActions.cs, ErrandActions.cs).
/// </summary>
/// <post>Parsed action metadata can be registered directly with Jennifer.</post>
public static class JenniferActionCatalogParser
{
    // Matches: public override string Name => "action_name";
    private static readonly Regex NamePattern = new(
        @"public\s+override\s+string\s+Name\s*=>\s*""(?<name>[^""]+)""",
        RegexOptions.Compiled);

    // Matches: protected override string Description => "...";  (single-line, may use + concatenation)
    private static readonly Regex DescriptionPattern = new(
        @"Description\s*=>\s*""(?<desc>[^""]*)",
        RegexOptions.Compiled);

    // Marks start of a Schema property block
    private static readonly Regex SchemaStartPattern = new(
        @"protected\s+override\s+JsonSchema\???\s+Schema\s*=>",
        RegexOptions.Compiled);

    // Matches: ["field_name"] = new JsonSchema
    private static readonly Regex SchemaFieldPattern = new(
        @"\[""(?<field>[^""]+)""\]\s*=\s*new\s+JsonSchema",
        RegexOptions.Compiled);

    // Matches: Type = JsonSchemaType.String  (or Integer, Boolean, etc.)
    private static readonly Regex SchemaTypePattern = new(
        @"Type\s*=\s*JsonSchemaType\.(?<type>\w+)",
        RegexOptions.Compiled);

    // Matches: Required = new List<string>{ "a", "b" }
    private static readonly Regex RequiredPattern = new(
        @"Required\s*=\s*new\s+List<string>\s*\{(?<fields>[^}]+)\}",
        RegexOptions.Compiled);

    // Matches individual string literals inside Required or Enum lists
    private static readonly Regex StringLiteralPattern = new(
        @"""(?<val>[^""]+)""",
        RegexOptions.Compiled);

    // Matches individual numeric literals (int or float) inside Enum lists
    private static readonly Regex NumericLiteralPattern = new(
        @"(?<![""\w])(?<val>-?\d+(?:\.\d+)?)(?![""\w])",
        RegexOptions.Compiled);

    // Matches: Enum = new List<object> { "a", "b" }
    private static readonly Regex EnumListPattern = new(
        @"Enum\s*=\s*new\s+List<object>\s*\{(?<vals>[^}]+)\}",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses all Neuro actions declared inside a single source text.
    /// </summary>
    /// <param name="sourceText">The full content of one C# source file.</param>
    /// <returns>All discovered actions in declaration order.</returns>
    /// <post>Returns an empty list when no recognisable action names are found.</post>
    public static List<JenniferDiscoveredAction> ParseAllFromSource(string sourceText)
    {
        var results = new List<JenniferDiscoveredAction>();
        if (string.IsNullOrWhiteSpace(sourceText))
            return results;

        // Find every Name => "…" position; each marks one action class
        MatchCollection nameMatches = NamePattern.Matches(sourceText);
        if (nameMatches.Count == 0)
            return results;

        for (int i = 0; i < nameMatches.Count; i++)
        {
            Match nameMt = nameMatches[i];
            // The slice belonging to this action starts at its own Name declaration
            // and ends at the next Name declaration (or end of file).
            int sliceStart = nameMt.Index;
            int sliceEnd   = (i + 1 < nameMatches.Count) ? nameMatches[i + 1].Index : sourceText.Length;
            string slice   = sourceText.Substring(sliceStart, sliceEnd - sliceStart);

            string actionName = nameMt.Groups["name"].Value.Trim();

            // Description – pick the last match in the slice (closer to the Name declaration)
            string description = string.Empty;
            foreach (Match dm in DescriptionPattern.Matches(slice))
                description = dm.Groups["desc"].Value.Trim();

            // Collect Required field names for this slice
            var requiredFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match rm in RequiredPattern.Matches(slice))
                foreach (Match lm in StringLiteralPattern.Matches(rm.Groups["fields"].Value))
                    requiredFields.Add(lm.Groups["val"].Value);

            // Detect schema presence — treat "Schema => null" as no schema so parameterless
            // actions (which return null) still get quick-action buttons in the UI.
            bool hasSchema = SchemaStartPattern.IsMatch(slice)
                && !Regex.IsMatch(slice, @"Schema\s*=>\s*null\s*;");

            // Extract per-field metadata from the schema block
            var parameters = new List<JenniferActionParameter>();
            if (hasSchema)
            {
                MatchCollection fieldMatches = SchemaFieldPattern.Matches(slice);
                for (int f = 0; f < fieldMatches.Count; f++)
                {
                    string fieldName = fieldMatches[f].Groups["field"].Value;

                    // The mini-block for this field runs to the next field match or end of slice
                    int fieldStart = fieldMatches[f].Index;
                    int fieldEnd   = (f + 1 < fieldMatches.Count)
                        ? fieldMatches[f + 1].Index
                        : slice.Length;
                    string fieldBlock = slice.Substring(fieldStart, fieldEnd - fieldStart);

                    // Determine JSON type
                    string jsonType = "string";
                    Match typeMt = SchemaTypePattern.Match(fieldBlock);
                    if (typeMt.Success)
                    {
                        jsonType = typeMt.Groups["type"].Value switch
                        {
                            "Integer" => "integer",
                            "Boolean" => "boolean",
                            "Number"  => "number",
                            "Array"   => "array",
                            "Object"  => "object",
                            _         => "string"
                        };
                    }

                    // Collect enum values if present (handles both string and numeric literals)
                    var enumVals = new List<string>();
                    Match enumMt = EnumListPattern.Match(fieldBlock);
                    if (enumMt.Success)
                    {
                        string valsBlock = enumMt.Groups["vals"].Value;
                        // Prefer string literals; fall back to numeric literals if none found.
                        MatchCollection strMatches = StringLiteralPattern.Matches(valsBlock);
                        if (strMatches.Count > 0)
                        {
                            foreach (Match em in strMatches)
                                enumVals.Add(em.Groups["val"].Value);
                        }
                        else
                        {
                            foreach (Match em in NumericLiteralPattern.Matches(valsBlock))
                                enumVals.Add(em.Groups["val"].Value);
                        }
                    }

                    parameters.Add(new JenniferActionParameter
                    {
                        Name       = fieldName,
                        JsonType   = jsonType,
                        IsRequired = requiredFields.Contains(fieldName),
                        EnumValues = enumVals
                    });
                }
            }

            results.Add(new JenniferDiscoveredAction
            {
                Name        = actionName,
                Description = description,
                HasSchema   = hasSchema,
                Parameters  = parameters
            });
        }

        return results;
    }

    // File names that contain test/debug/demo actions not intended for the production catalog,
    // or whose actions have been superseded by a newer action (e.g. get_biodata → get_status).
    private static readonly HashSet<string> _excludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "DebugActions.cs",
        "DemoToggleAction.cs",
        "ErrandTestActions.cs",
        "BioDataActions.cs",  // get_biodata replaced by get_status (StatusActions.cs)
    };

    /// <summary>
    /// Parses every C# action file in a directory, returning all actions across all files.
    /// </summary>
    /// <param name="directoryPath">The directory to inspect.</param>
    /// <param name="cancellationToken">The optional cancellation token.</param>
    /// <returns>De-duplicated actions sorted alphabetically by name.</returns>
    /// <post>The returned actions are de-duplicated by name and sorted alphabetically. Test, debug, and demo files are excluded.</post>
    public static async Task<IReadOnlyList<JenniferDiscoveredAction>> ParseDirectoryAsync(
        string directoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            return Array.Empty<JenniferDiscoveredAction>();

        var actions = new Dictionary<string, JenniferDiscoveredAction>(StringComparer.OrdinalIgnoreCase);
        foreach (string filePath in Directory.GetFiles(directoryPath, "*.cs", SearchOption.TopDirectoryOnly))
        {
            // Skip test, debug, and demo files — they are not intended for the production catalog.
            if (_excludedFileNames.Contains(Path.GetFileName(filePath)))
                continue;

            cancellationToken.ThrowIfCancellationRequested();
            string sourceText = await File.ReadAllTextAsync(filePath, cancellationToken);
            foreach (JenniferDiscoveredAction action in ParseAllFromSource(sourceText))
                actions[action.Name] = action;
        }

        return actions.Values
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}