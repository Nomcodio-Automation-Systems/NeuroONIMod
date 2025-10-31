#nullable enable

using JetBrains.Annotations;
using NeuroSdk.Messages.API;

namespace NeuroSdk.Messages.Outgoing;

[PublicAPI]
public sealed class Startup : OutgoingMessageBuilder
{
    protected override string Command => "startup";
    protected override object? Data => null;
}