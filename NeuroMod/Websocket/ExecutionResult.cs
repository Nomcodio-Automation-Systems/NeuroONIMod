#nullable enable

namespace NeuroSdk.Websocket;

public sealed class ExecutionResult
{
    public bool Successful { get; }
    public string? Message { get; }

    private ExecutionResult(bool success, string? message)
    {
        Successful = success;
        Message = message;
    }

    public static ExecutionResult Success(string? message = null)
    {
        return new(true, message);
    }

    public static ExecutionResult Failure(string reason)
    {
        return new(false, reason);
    }

    public static ExecutionResult VedalFailure(string reason)
    {
        return Failure(reason + Strings.VedalFaultSuffix);
    }

    public static ExecutionResult ModFailure(string reason)
    {
        return Failure(reason + Strings.ModFaultSuffix);
    }
}