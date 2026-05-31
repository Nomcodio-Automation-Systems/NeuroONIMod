#nullable enable

using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeuroSdk.Json;

/// <summary>
/// Provides concise helpers for building simple JSON schema fragments used by the SDK.
/// </summary>
/// <pre>Callers supply values and schema kinds that can be represented directly by <see cref="JsonSchema"/>.</pre>
/// <post>Each helper returns a new schema fragment and does not mutate shared global schema state.</post>
[PublicAPI]
public static class QJS
{
    /// <summary>
    /// Creates a constant-value schema from an arbitrary CLR value.
    /// </summary>
    /// <typeparam name="T">The CLR type of the constant value.</typeparam>
    /// <param name="value">The value that the schema should require.</param>
    /// <returns>A schema whose <c>const</c> constraint matches <paramref name="value"/>.</returns>
    /// <pre><paramref name="value"/> is serializable by the schema serializer used by the SDK.</pre>
    /// <post>The returned schema constrains valid JSON to exactly <paramref name="value"/>.</post>
    private static JsonSchema Const<T>(T value)
    {
        return new JsonSchema
        {
            Const = value
        };
    }

    /// <summary>
    /// Creates a constant-value schema for a string literal.
    /// </summary>
    /// <param name="value">The string value that the schema should require.</param>
    /// <returns>A schema whose <c>const</c> constraint matches <paramref name="value"/>.</returns>
    /// <pre><paramref name="value"/> represents the only accepted string value.</pre>
    /// <post>The returned schema constrains valid JSON to exactly <paramref name="value"/>.</post>
    public static JsonSchema Const(string value)
    {
        return Const<string>(value);
    }

    /// <summary>
    /// Creates a constant-value schema for an integer literal.
    /// </summary>
    /// <param name="value">The integer value that the schema should require.</param>
    /// <returns>A schema whose <c>const</c> constraint matches <paramref name="value"/>.</returns>
    /// <pre><paramref name="value"/> represents the only accepted integer value.</pre>
    /// <post>The returned schema constrains valid JSON to exactly <paramref name="value"/>.</post>
    public static JsonSchema Const(int value)
    {
        return Const<int>(value);
    }

    /// <summary>
    /// Creates a constant-value schema for a fixed string sequence.
    /// </summary>
    /// <param name="values">The exact string sequence that the schema should require.</param>
    /// <returns>A schema whose <c>const</c> constraint matches <paramref name="values"/>.</returns>
    /// <pre><paramref name="values"/> enumerates the full constant sequence in the expected order.</pre>
    /// <post>The returned schema constrains valid JSON to exactly the provided string sequence.</post>
    public static JsonSchema Const(IEnumerable<string> values)
    {
        return Const<IEnumerable<string>>(values);
    }

    /// <summary>
    /// Creates a constant-value schema for a fixed integer sequence.
    /// </summary>
    /// <param name="values">The exact integer sequence that the schema should require.</param>
    /// <returns>A schema whose <c>const</c> constraint matches <paramref name="values"/>.</returns>
    /// <pre><paramref name="values"/> enumerates the full constant sequence in the expected order.</pre>
    /// <post>The returned schema constrains valid JSON to exactly the provided integer sequence.</post>
    public static JsonSchema Const(IEnumerable<int> values)
    {
        return Const<IEnumerable<int>>(values);
    }

    /// <summary>
    /// Gets a schema that accepts only an empty JSON array.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned schema constrains valid JSON to an empty array literal.</post>
    public static JsonSchema ConstEmptyArray => Const(Array.Empty<object>());

    /// <summary>
    /// Gets a schema that accepts only the JSON null literal.
    /// </summary>
    /// <pre>No input is required.</pre>
    /// <post>The returned schema constrains valid JSON to the null literal.</post>
    public static JsonSchema ConstNull => Enum(new object?[] { null });

    /// <summary>
    /// Creates an enumeration schema from a sequence of CLR values.
    /// </summary>
    /// <typeparam name="T">The CLR type of the enumerated values.</typeparam>
    /// <param name="values">The set of accepted values.</param>
    /// <returns>A schema whose <c>enum</c> constraint contains all supplied values.</returns>
    /// <pre><paramref name="values"/> can be enumerated successfully and each value is serializable by the schema serializer.</pre>
    /// <post>The returned schema constrains valid JSON to one of the supplied values.</post>
    private static JsonSchema Enum<T>(IEnumerable<T> values)
    {
        return new JsonSchema
        {
            Enum = [.. values.Cast<object>()]
        };
    }

    /// <summary>
    /// Creates an enumeration schema from a sequence of strings.
    /// </summary>
    /// <param name="values">The accepted string values.</param>
    /// <returns>A schema whose <c>enum</c> constraint contains all supplied values.</returns>
    /// <pre><paramref name="values"/> enumerates the full accepted string set.</pre>
    /// <post>The returned schema constrains valid JSON to one of the supplied string values.</post>
    public static JsonSchema Enum(IEnumerable<string> values)
    {
        return Enum<string>(values);
    }

    /// <summary>
    /// Creates an enumeration schema from a sequence of integers.
    /// </summary>
    /// <param name="values">The accepted integer values.</param>
    /// <returns>A schema whose <c>enum</c> constraint contains all supplied values.</returns>
    /// <pre><paramref name="values"/> enumerates the full accepted integer set.</pre>
    /// <post>The returned schema constrains valid JSON to one of the supplied integer values.</post>
    public static JsonSchema Enum(IEnumerable<int> values)
    {
        return Enum<int>(values);
    }

    /// <summary>
    /// Creates a schema fragment constrained only by JSON value kind.
    /// </summary>
    /// <param name="type">The JSON schema type flag to require.</param>
    /// <returns>A schema whose <c>type</c> constraint matches <paramref name="type"/>.</returns>
    /// <pre><paramref name="type"/> identifies at least one supported JSON schema primitive or composite kind.</pre>
    /// <post>The returned schema constrains valid JSON to the requested kind.</post>
    public static JsonSchema Type(JsonSchemaType type)
    {
        return new JsonSchema
        {
            Type = type
        };
    }
}