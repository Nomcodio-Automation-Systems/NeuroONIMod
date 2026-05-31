using System;

namespace Jennifer.Wpf;

/// <summary>
/// Lightweight in-process event bridge used by the TestServerService to notify the UI about incoming messages.
/// </summary>
internal static class JenniferServer
{
    public static event Action<string>? MessageReceived;

    // Optional callbacks used by the in-process test server to request payloads from the UI
    public static Func<string?>? BuildStartupPayload;
    public static Func<string?>? BuildActionsRegisterPayload;

    /// <summary>
    /// Event to notify about connected websocket client count changes. Payload is the textual status.
    /// </summary>
    public static event Action<string>? StatusChanged;

    public static void RaiseMessageReceived(string message)
    {
        MessageReceived?.Invoke(message);
    }

    public static void RaiseStatusChanged(string status)
    {
        StatusChanged?.Invoke(status);
    }
}
