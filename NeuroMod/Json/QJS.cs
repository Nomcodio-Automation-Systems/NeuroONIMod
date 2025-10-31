#nullable enable

using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeuroSdk.Json;

/// <summary>
/// Utility class for generating quick JSON schemas
/// </summary>
[PublicAPI]
public static class QJS
{
    private static JsonSchema Const<T>(T value)
    {
        return new JsonSchema
        {
            Const = value
        };
    }

    public static JsonSchema Const(string value)
    {
        return Const<string>(value);
    }

    public static JsonSchema Const(int value)
    {
        return Const<int>(value);
    }

    public static JsonSchema Const(IEnumerable<string> values)
    {
        return Const<IEnumerable<string>>(values);
    }

    public static JsonSchema Const(IEnumerable<int> values)
    {
        return Const<IEnumerable<int>>(values);
    }

    public static JsonSchema ConstEmptyArray => Const(Array.Empty<object>());
    public static JsonSchema ConstNull => Enum(new object?[] { null });

    private static JsonSchema Enum<T>(IEnumerable<T> values)
    {
        return new JsonSchema
        {
            Enum = [.. values.Cast<object>()]
        };
    }

    public static JsonSchema Enum(IEnumerable<string> values)
    {
        return Enum<string>(values);
    }

    public static JsonSchema Enum(IEnumerable<int> values)
    {
        return Enum<int>(values);
    }

    public static JsonSchema Type(JsonSchemaType type)
    {
        return new JsonSchema
        {
            Type = type
        };
    }
}