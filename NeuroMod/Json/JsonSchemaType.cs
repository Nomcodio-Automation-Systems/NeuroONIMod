#nullable enable

namespace NeuroSdk.Json;

/// <summary>
/// Defines the JSON value kinds that can be expressed by the SDK schema helpers.
/// </summary>
/// <pre>Values are used as schema type flags when constructing <see cref="JsonSchema"/> fragments.</pre>
/// <post>Consumers can combine these flags to describe the permitted JSON kinds for a schema node.</post>
public enum JsonSchemaType
{
    /// <summary>
    /// No type specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// String type.
    /// </summary>
    String = 1,

    /// <summary>
    /// Float type.
    /// </summary>
    Float = 2,

    /// <summary>
    /// Integer type.
    /// </summary>
    Integer = 4,

    /// <summary>
    /// Boolean type.
    /// </summary>
    Boolean = 8,

    /// <summary>
    /// Object type.
    /// </summary>
    Object = 16,

    /// <summary>
    /// Array type.
    /// </summary>
    Array = 32,

    /// <summary>
    /// Null type.
    /// </summary>
    Null = 64,
}