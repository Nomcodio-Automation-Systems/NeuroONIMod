using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Jennifer.Wpf.Parsing;

/// <summary>
/// The recognized Jennifer WebSocket message categories.
/// </summary>
public enum JenniferWsMessageKind
{
    /// <summary>
    /// The payload could not be parsed into a known JSON message.
    /// </summary>
    Unknown,

    /// <summary>
    /// The payload is an incoming action request.
    /// </summary>
    Action,

    /// <summary>
    /// The payload requests a full action re-registration.
    /// </summary>
    ReRegisterAll,

    /// <summary>
    /// The payload is an actions/force request asking Jennifer to pick and dispatch one of the listed actions.
    /// </summary>
    ActionsForce,

    /// <summary>
    /// The game is registering (or re-registering) its known action names with Randy.
    /// </summary>
    ActionsRegister,

    /// <summary>
    /// The game is unregistering action names from Randy.
    /// </summary>
    ActionsUnregister,

    /// <summary>
    /// The payload is the game's response to a dispatched action.
    /// </summary>
    ActionResult,

    /// <summary>
    /// The payload was valid JSON but did not match a dedicated Jennifer handler.
    /// </summary>
    Generic,
}

/// <summary>
/// Represents a parsed Jennifer WebSocket message.
/// </summary>
/// <post>The parsed message contains the raw payload and any extracted action metadata.</post>
public sealed class JenniferWsMessage
{
    /// <summary>
    /// Gets or sets the parsed message kind.
    /// </summary>
    public JenniferWsMessageKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the command field value.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the extracted action identifier.
    /// </summary>
    public string? ActionId { get; set; }

    /// <summary>
    /// Gets or sets the extracted action name.
    /// </summary>
    public string? ActionName { get; set; }

    /// <summary>
    /// Gets or sets the action names listed in an actions/force request.
    /// </summary>
    public IReadOnlyList<string> ForceActionNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the action names extracted from an <c>actions/register</c> message.
    /// </summary>
    public IReadOnlyList<string> RegisteredActionNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the action names extracted from an <c>actions/unregister</c> message.
    /// </summary>
    public IReadOnlyList<string> UnregisteredActionNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the query text from an actions/force request.
    /// </summary>
    public string? ForceQuery { get; set; }

    /// <summary>
    /// Gets or sets the state text from an actions/force request.
    /// </summary>
    public string? ForceState { get; set; }

    /// <summary>
    /// Gets or sets the extracted action data.
    /// </summary>
    public string? ActionData { get; set; }

    /// <summary>
    /// Gets or sets the action identifier from an action/result response.
    /// </summary>
    public string? ActionResultId { get; set; }

    /// <summary>
    /// Gets or sets whether the action/result response reports success.
    /// </summary>
    public bool ActionResultSuccess { get; set; }

    /// <summary>
    /// Gets or sets the message from an action/result response.
    /// </summary>
    public string? ActionResultMessage { get; set; }

    /// <summary>
    /// Gets or sets the original raw payload.
    /// </summary>
    public string Raw { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the game name announced by the sender, if present.
    /// </summary>
    public string? GameName { get; set; }
}

/// <summary>
/// Parses Neuro-compatible WebSocket messages for Jennifer.
/// </summary>
/// <post>Incoming JSON payloads are categorized into actionable Jennifer message types.</post>
public static class JenniferWsMessageParser
{
    /// <summary>
    /// Parses a raw WebSocket payload.
    /// </summary>
    /// <param name="rawMessage">The raw message text.</param>
    /// <returns>The parsed Jennifer message.</returns>
    /// <post>The returned message always preserves the original raw payload.</post>
    public static JenniferWsMessage Parse(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return new JenniferWsMessage
            {
                Kind = JenniferWsMessageKind.Unknown,
                Raw = rawMessage ?? string.Empty,
            };
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(rawMessage);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return CreateGeneric(rawMessage, string.Empty);
            }

            string command = document.RootElement.TryGetProperty("command", out JsonElement commandElement)
                ? commandElement.GetString() ?? string.Empty
                : string.Empty;

            if (string.Equals(command, "actions/register", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "actions/unregister", StringComparison.OrdinalIgnoreCase))
            {
                JsonElement dataElement = document.RootElement.TryGetProperty("data", out JsonElement parsedRegData)
                    ? parsedRegData
                    : default;

                List<string> actionNames = [];

                // actions/register wraps names inside data.actions[].name
                if (string.Equals(command, "actions/register", StringComparison.OrdinalIgnoreCase)
                    && dataElement.ValueKind == JsonValueKind.Object
                    && dataElement.TryGetProperty("actions", out JsonElement actionsArr)
                    && actionsArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in actionsArr.EnumerateArray())
                    {
                        string? name = item.TryGetProperty("name", out JsonElement ne) ? ne.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            actionNames.Add(name!);
                        }
                    }
                }

                // actions/unregister wraps names inside data.action_names[]
                if (string.Equals(command, "actions/unregister", StringComparison.OrdinalIgnoreCase)
                    && dataElement.ValueKind == JsonValueKind.Object
                    && dataElement.TryGetProperty("action_names", out JsonElement unregArr)
                    && unregArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in unregArr.EnumerateArray())
                    {
                        string? name = item.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            actionNames.Add(name!);
                        }
                    }
                }

                return new JenniferWsMessage
                {
                    Kind = string.Equals(command, "actions/register", StringComparison.OrdinalIgnoreCase)
                        ? JenniferWsMessageKind.ActionsRegister
                        : JenniferWsMessageKind.ActionsUnregister,
                    Command = command,
                    RegisteredActionNames = string.Equals(command, "actions/register", StringComparison.OrdinalIgnoreCase) ? actionNames : [],
                    UnregisteredActionNames = string.Equals(command, "actions/unregister", StringComparison.OrdinalIgnoreCase) ? actionNames : [],
                    Raw = rawMessage,
                    GameName = document.RootElement.TryGetProperty("game", out JsonElement gameEl) ? gameEl.GetString() : null,
                };
            }

            if (string.Equals(command, "actions/force", StringComparison.OrdinalIgnoreCase))
            {
                JsonElement dataElement = document.RootElement.TryGetProperty("data", out JsonElement parsedForceData)
                    ? parsedForceData
                    : default;

                List<string> actionNames = [];
                if (dataElement.ValueKind == JsonValueKind.Object
                    && dataElement.TryGetProperty("action_names", out JsonElement actionNamesElement)
                    && actionNamesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in actionNamesElement.EnumerateArray())
                    {
                        string? name = item.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            actionNames.Add(name!);
                        }
                    }
                }

                return new JenniferWsMessage
                {
                    Kind = JenniferWsMessageKind.ActionsForce,
                    Command = command,
                    ForceActionNames = actionNames,
                    ForceQuery = TryGetString(dataElement, "query"),
                    ForceState = TryGetString(dataElement, "state"),
                    Raw = rawMessage,
                };
            }

            if (string.Equals(command, "actions/reregister_all", StringComparison.OrdinalIgnoreCase))
            {
                return new JenniferWsMessage
                {
                    Kind = JenniferWsMessageKind.ReRegisterAll,
                    Command = command,
                    Raw = rawMessage,
                };
            }

            if (string.Equals(command, "action", StringComparison.OrdinalIgnoreCase))
            {
                JsonElement dataElement = document.RootElement.TryGetProperty("data", out JsonElement parsedData)
                    ? parsedData
                    : default;

                return new JenniferWsMessage
                {
                    Kind = JenniferWsMessageKind.Action,
                    Command = command,
                    ActionId = TryGetString(dataElement, "id"),
                    ActionName = TryGetString(dataElement, "name"),
                    ActionData = TryGetFlexibleString(dataElement, "data"),
                    Raw = rawMessage,
                };
            }

            if (string.Equals(command, "action/result", StringComparison.OrdinalIgnoreCase))
            {
                JsonElement dataElement = document.RootElement.TryGetProperty("data", out JsonElement parsedResultData)
                    ? parsedResultData
                    : default;

                bool success = dataElement.ValueKind == JsonValueKind.Object
                    && dataElement.TryGetProperty("success", out JsonElement successElement)
                    && successElement.ValueKind == JsonValueKind.True;

                return new JenniferWsMessage
                {
                    Kind = JenniferWsMessageKind.ActionResult,
                    Command = command,
                    ActionResultId = TryGetString(dataElement, "id"),
                    ActionResultSuccess = success,
                    ActionResultMessage = TryGetString(dataElement, "message"),
                    Raw = rawMessage,
                };
            }

            return CreateGeneric(rawMessage, command);
        }
        catch (JsonException)
        {
            return new JenniferWsMessage
            {
                Kind = JenniferWsMessageKind.Unknown,
                Raw = rawMessage,
            };
        }
    }

    /// <summary>
    /// Creates a generic parsed Jennifer message.
    /// </summary>
    /// <param name="rawMessage">The raw payload.</param>
    /// <param name="command">The parsed command.</param>
    /// <returns>The generic Jennifer message.</returns>
    /// <post>The generic message preserves the command when it was available.</post>
    private static JenniferWsMessage CreateGeneric(string rawMessage, string command)
    {
        return new JenniferWsMessage
        {
            Kind = JenniferWsMessageKind.Generic,
            Command = command,
            Raw = rawMessage,
        };
    }

    /// <summary>
    /// Extracts a string property from a JSON object element.
    /// </summary>
    /// <param name="element">The object element.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The extracted string, or <c>null</c> when it is unavailable.</returns>
    /// <post>The returned value is safe for direct Jennifer display.</post>
    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement propertyElement))
        {
            return null;
        }

        return propertyElement.ValueKind switch
        {
            JsonValueKind.String => propertyElement.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => propertyElement.ToString(),
        };
    }

    /// <summary>
    /// Extracts a property as either a string value or raw JSON text.
    /// </summary>
    /// <param name="element">The object element.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The string or raw JSON payload for the property.</returns>
    /// <post>Object and array payloads remain intact as JSON text.</post>
    private static string? TryGetFlexibleString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement propertyElement))
        {
            return null;
        }

        return propertyElement.ValueKind switch
        {
            JsonValueKind.String => propertyElement.GetString(),
            JsonValueKind.Object or JsonValueKind.Array => propertyElement.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => propertyElement.ToString(),
        };
    }
}