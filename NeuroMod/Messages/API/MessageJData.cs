#nullable enable

using Newtonsoft.Json.Linq;

namespace NeuroSdk.Messages.API;

public readonly struct MessageJData(JToken? data)
{
    public readonly JToken? Data = data;
};