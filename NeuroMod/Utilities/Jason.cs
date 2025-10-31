#nullable enable

using Newtonsoft.Json;

namespace NeuroSdk.Utilities;

internal static class Jason
{
    public static string Serialize(object? value)
    {
        return JsonConvert.SerializeObject(value, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        });
    }
}