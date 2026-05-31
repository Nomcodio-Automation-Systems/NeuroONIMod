#nullable enable

using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace NeuroSdk.Json;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
/// <summary>
/// Represents a mutable JSON schema fragment used to describe Neuro SDK message payloads.
/// </summary>
/// <pre>Callers populate only the keywords relevant to the schema node they want to emit.</pre>
/// <post>The instance serializes to JSON schema-compatible keyword fields understood by Neuro clients.</post>
public sealed class JsonSchema
{
    /// <summary>
    /// Gets or sets the child property schemas for object-shaped payloads.
    /// </summary>
    /// <pre>The schema represents or may represent an object payload.</pre>
    /// <post>The returned dictionary is never null and contains the configured property schemas.</post>
    [JsonIgnore]
    public Dictionary<string, JsonSchema> Properties
    {
        get => _properties ??= [];
        set => _properties = value;
    }

    /// <summary>
    /// Gets or sets the logical schema type using the SDK enum abstraction.
    /// </summary>
    /// <pre>The backing string keyword, when present, maps to a supported <see cref="JsonSchemaType"/> value.</pre>
    /// <post>Reading and writing through this property keeps the enum abstraction synchronized with the serialized type keyword.</post>
    [JsonIgnore]
    public JsonSchemaType Type
    {
        get => _type switch
        {
            "string" => JsonSchemaType.String,
            "number" => JsonSchemaType.Float,
            "integer" => JsonSchemaType.Integer,
            "boolean" => JsonSchemaType.Boolean,
            "object" => JsonSchemaType.Object,
            "array" => JsonSchemaType.Array,
            "null" => JsonSchemaType.Null,
            _ => JsonSchemaType.None
        }; set => _type = value switch
        {
            JsonSchemaType.String => "string",
            JsonSchemaType.Float => "number",
            JsonSchemaType.Integer => "integer",
            JsonSchemaType.Boolean => "boolean",
            JsonSchemaType.Object => "object",
            JsonSchemaType.Array => "array",
            JsonSchemaType.Null => "null",
            _ => null
        };
    }

    /// <summary>
    /// Gets or sets the allowed enum values for the schema.
    /// </summary>
    /// <pre>The schema should restrict payloads to a discrete set of literal values.</pre>
    /// <post>The returned list is never null and contains the configured enum values.</post>
    [JsonIgnore]
    public List<object> Enum
    {
        get => _enum ??= [];
        set => _enum = value;
    }

    /// <summary>
    /// Gets or sets the required property names for object schemas.
    /// </summary>
    /// <pre>The schema represents or may represent an object payload.</pre>
    /// <post>The returned list is never null and contains the configured required-property names.</post>
    [JsonIgnore]
    public List<string> Required
    {
        get => _required ??= [];
        set => _required = value;
    }

    #region Keywords

    [JsonProperty("properties")]
    private Dictionary<string, JsonSchema>? _properties;

    [JsonProperty("items")]
    public JsonSchema? Items { get; set; }

    [JsonProperty("type")]
    private string? _type;

    [JsonProperty("enum")]
    private List<object>? _enum;

    [JsonProperty("const")]
    public object? Const { get; set; }

    [JsonProperty("minLength")]
    public int? MinLength { get; set; }

    [JsonProperty("pattern")]
    public string? Pattern { get; set; }

    [JsonProperty("maxLength")]
    public int? MaxLength { get; set; }

    [JsonProperty("maximum")]
    public float? Maximum { get; set; }

    [JsonProperty("exclusiveMinimum")]
    public float? ExclusiveMinimum { get; set; }

    [JsonProperty("exclusiveMaximum")]
    public float? ExclusiveMaximum { get; set; }

    [JsonProperty("minimum")]
    public float? Minimum { get; set; }

    [JsonProperty("required")]
    private List<string>? _required;

    [JsonProperty("minItems")]
    public int? MinItems { get; set; }

    [JsonProperty("maxItems")]
    public int? MaxItems { get; set; }

    [JsonProperty("uniqueItems")]
    public bool? UniqueItems { get; set; }

    [JsonProperty("format")]
    public string? Format { get; set; }

    #endregion Keywords
}