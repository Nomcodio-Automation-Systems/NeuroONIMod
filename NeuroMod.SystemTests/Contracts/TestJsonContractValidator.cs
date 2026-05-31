using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NeuroMod.SystemTests.Contracts;

/// <summary>
/// Validates JSON payloads against the limited schema subset used by the Randy test contract files.
/// </summary>
internal static class TestJsonContractValidator
{
    public static void Validate(string schemaPath, string json)
    {
        using JsonDocument schemaDocument = JsonDocument.Parse(File.ReadAllText(schemaPath));
        using JsonDocument payloadDocument = JsonDocument.Parse(json);
        ValidateElement(schemaDocument.RootElement, payloadDocument.RootElement, "$", schemaPath);
    }

    private static void ValidateElement(JsonElement schema, JsonElement value, string path, string schemaPath)
    {
        if (schema.TryGetProperty("type", out JsonElement typeElement))
        {
            string expectedType = typeElement.GetString() ?? string.Empty;
            ValidateType(expectedType, value, path, schemaPath);
        }

        if (schema.TryGetProperty("const", out JsonElement constElement))
        {
            if (value.ValueKind != constElement.ValueKind || value.ToString() != constElement.ToString())
            {
                throw new InvalidOperationException($"Schema '{schemaPath}' expected {path} to equal '{constElement}', but found '{value}'.");
            }
        }

        if (schema.TryGetProperty("enum", out JsonElement enumElement))
        {
            bool match = enumElement.EnumerateArray().Any(candidate => candidate.ToString() == value.ToString());
            if (!match)
            {
                throw new InvalidOperationException($"Schema '{schemaPath}' expected {path} to be one of [{string.Join(", ", enumElement.EnumerateArray().Select(candidate => candidate.ToString()))}], but found '{value}'.");
            }
        }

        if (schema.TryGetProperty("required", out JsonElement requiredElement) && value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonElement requiredProperty in requiredElement.EnumerateArray())
            {
                string propertyName = requiredProperty.GetString() ?? string.Empty;
                if (!value.TryGetProperty(propertyName, out _))
                {
                    throw new InvalidOperationException($"Schema '{schemaPath}' requires property '{path}.{propertyName}', but it was missing.");
                }
            }
        }

        if (schema.TryGetProperty("properties", out JsonElement propertiesElement) && value.ValueKind == JsonValueKind.Object)
        {
            Dictionary<string, JsonElement> propertySchemas = propertiesElement
                .EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);

            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (propertySchemas.TryGetValue(property.Name, out JsonElement propertySchema))
                {
                    ValidateElement(propertySchema, property.Value, $"{path}.{property.Name}", schemaPath);
                }
                else if (schema.TryGetProperty("additionalProperties", out JsonElement additionalPropertiesElement)
                    && additionalPropertiesElement.ValueKind == JsonValueKind.False)
                {
                    throw new InvalidOperationException($"Schema '{schemaPath}' does not allow extra property '{path}.{property.Name}'.");
                }
            }
        }

        if (schema.TryGetProperty("items", out JsonElement itemsElement) && value.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
            {
                ValidateElement(itemsElement, item, $"{path}[{index}]", schemaPath);
                index++;
            }
        }
    }

    private static void ValidateType(string expectedType, JsonElement value, string path, string schemaPath)
    {
        bool matches = expectedType switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => true,
        };

        if (!matches)
        {
            throw new InvalidOperationException($"Schema '{schemaPath}' expected {path} to be '{expectedType}', but found '{value.ValueKind}'.");
        }
    }
}