#nullable enable

namespace NeuroSdk.Websocket;

/// <summary>
/// Represents the outcome of validating or executing a Neuro action or incoming command.
/// </summary>
/// <pre>
/// Callers use this type as the canonical success or failure payload for protocol-visible results.
/// </pre>
/// <post>
/// Instances communicate whether an operation succeeded and optionally carry a user-facing message.
/// </post>
public sealed class ExecutionResult
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Successful { get; }

    /// <summary>
    /// Gets the optional message associated with the result.
    /// </summary>
    public string? Message { get; }

    private ExecutionResult(bool success, string? message)
    {
        Successful = success;
        Message = message;
    }

    /// <summary>
    /// Creates a successful execution result.
    /// </summary>
    /// <param name="message">Optional success message for the caller.</param>
    /// <returns>A successful execution result.</returns>
    /// <pre>
    /// The operation completed successfully and may have a user-facing summary.
    /// </pre>
    /// <post>
    /// A successful result instance is returned.
    /// </post>
    public static ExecutionResult Success(string? message = null)
    {
        return new(true, message);
    }

    /// <summary>
    /// Creates a failed execution result.
    /// </summary>
    /// <param name="reason">The failure reason to expose to the caller.</param>
    /// <returns>A failed execution result.</returns>
    /// <pre>
    /// The operation could not complete successfully and <paramref name="reason"/> explains why.
    /// </pre>
    /// <post>
    /// A failed result instance is returned.
    /// </post>
    public static ExecutionResult Failure(string reason)
    {
        return new(false, reason);
    }

    /// <summary>
    /// Creates a failed execution result attributed to the remote Vedal-side fault domain.
    /// </summary>
    /// <param name="reason">The base failure reason.</param>
    /// <returns>A failed execution result with the Vedal fault suffix appended.</returns>
    /// <pre>
    /// <paramref name="reason"/> describes a failure that should be attributed to the Vedal-side runtime.
    /// </pre>
    /// <post>
    /// A failed result is returned with the Vedal-specific suffix appended to the message.
    /// </post>
    public static ExecutionResult VedalFailure(string reason)
    {
        return Failure(reason + Strings.VedalFaultSuffix);
    }

    /// <summary>
    /// Creates a failed execution result attributed to the local mod fault domain.
    /// </summary>
    /// <param name="reason">The base failure reason.</param>
    /// <returns>A failed execution result with the mod fault suffix appended.</returns>
    /// <pre>
    /// <paramref name="reason"/> describes a failure that should be attributed to the local mod runtime.
    /// </pre>
    /// <post>
    /// A failed result is returned with the mod-specific suffix appended to the message.
    /// </post>
    public static ExecutionResult ModFailure(string reason)
    {
        return Failure(reason + Strings.ModFaultSuffix);
    }
}