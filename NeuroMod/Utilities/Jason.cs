#nullable enable

using Newtonsoft.Json;

namespace NeuroSdk.Utilities;

/// <summary>
/// Centralizes JSON serialization settings used by the NeuroMod runtime.
/// </summary>
/// <pre>Callers provide values that are safe for Newtonsoft.Json serialization.</pre>
/// <post>Serialized output omits null-valued properties to keep websocket payloads compact.</post>
internal static class Jason
{
    /// <summary>
    /// Serializes a value using the runtime JSON settings.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The JSON representation of <paramref name="value"/>.</returns>
    /// <pre><paramref name="value"/> is serializable by Newtonsoft.Json.</pre>
    /// <post>The returned JSON string excludes null properties according to the shared serializer settings.</post>
    public static string Serialize(object? value)
    {
        return JsonConvert.SerializeObject(value, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        });
    }
}