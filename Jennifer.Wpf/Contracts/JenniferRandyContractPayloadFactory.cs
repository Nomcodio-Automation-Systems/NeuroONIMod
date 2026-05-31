using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jennifer.Wpf.Parsing;

namespace Jennifer.Wpf.Contracts;

/// <summary>
/// Builds Jennifer payloads that must remain compatible with Randy's expected websocket contract.
/// </summary>
/// <pre>The caller supplies already-resolved Jennifer action metadata and websocket request values.</pre>
/// <post>Each method returns the exact JSON text Jennifer should send over the websocket transport.</post>
public static class JenniferRandyContractPayloadFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Builds the startup message Jennifer sends before registering actions.
    /// </summary>
    /// <param name="gameName">The optional game name. Omitted from the payload when null or empty.</param>
    /// <returns>The serialized startup payload.</returns>
    /// <post>The returned payload contains the Randy-compatible <c>startup</c> envelope. The <c>game</c> field is omitted when no name is provided.</post>
    public static string CreateStartupPayload(string? gameName)
    {
        string? normalized = Normalize(gameName);
        return JsonSerializer.Serialize(new
        {
            command = "startup",
            game = string.IsNullOrWhiteSpace(normalized) ? null : normalized,
        }, SerializerOptions);
    }

    /// <summary>
    /// Builds an actions/register message for the supplied Jennifer action catalog.
    /// </summary>
    /// <param name="gameName">The optional game name. Omitted from the payload when null or empty.</param>
    /// <param name="actions">The actions Jennifer can register.</param>
    /// <returns>The serialized actions/register payload.</returns>
    /// <pre><paramref name="actions"/> contains the Jennifer action metadata that should be exposed to Randy.</pre>
    /// <post>The returned payload contains the Randy-compatible <c>actions/register</c> envelope. The <c>game</c> field is omitted when no name is provided.</post>
    public static string CreateActionsRegisterPayload(string? gameName, IEnumerable<JenniferDiscoveredAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        string? normalizedGame = Normalize(gameName);
        var payload = new
        {
            command = "actions/register",
            game = string.IsNullOrWhiteSpace(normalizedGame) ? null : normalizedGame,
            data = new
            {
                actions = actions
                    .Where(action => !string.IsNullOrWhiteSpace(action.Name))
                    .Select(action => CreateRegisterActionPayload(action))
                    .ToArray(),
            },
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    /// <summary>
    /// Builds an actions/force message for one or more Jennifer actions.
    /// </summary>
    /// <param name="gameName">The optional game name. Omitted when null or empty.</param>
    /// <param name="actionNames">The action names Jennifer wants Randy to execute.</param>
    /// <param name="state">The optional action state text.</param>
    /// <param name="query">The optional force query.</param>
    /// <param name="priority">The requested Randy priority.</param>
    /// <param name="ephemeral">Whether the force request is ephemeral.</param>
    /// <returns>The serialized actions/force payload.</returns>
    /// <pre><paramref name="actionNames"/> contains at least one action name after normalization.</pre>
    /// <post>The returned payload contains the Randy-compatible <c>actions/force</c> envelope.</post>
    public static string CreateActionsForcePayload(string? gameName, IEnumerable<string> actionNames, string state, string query, string priority, bool ephemeral)
    {
        ArgumentNullException.ThrowIfNull(actionNames);

        string[] normalizedActions = actionNames
            .Select(Normalize)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var payload = new
        {
            command = "actions/force",
            game = string.IsNullOrWhiteSpace(Normalize(gameName)) ? null : Normalize(gameName),
            data = new
            {
                state = Normalize(state),
                query = Normalize(query),
                ephemeral_context = ephemeral,
                priority = Normalize(priority),
                action_names = normalizedActions,
            },
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    /// <summary>
    /// Builds an <c>action</c> command that Jennifer sends to the game to trigger execution of a chosen action.
    /// This is the response to an <c>actions/force</c> request — Jennifer picks an action and dispatches it.
    /// </summary>
    /// <param name="id">A unique correlation id for this action invocation.</param>
    /// <param name="actionName">The name of the action to execute.</param>
    /// <param name="data">Optional JSON data to pass to the action handler.</param>
    /// <returns>The serialized <c>action</c> payload.</returns>
    public static string CreateActionPayload(string id, string actionName, string? data = null)
    {
        return JsonSerializer.Serialize(new
        {
            command = "action",
            data = new
            {
                id = Normalize(id),
                name = Normalize(actionName),
                data = string.IsNullOrWhiteSpace(data) ? null : data!.Trim(),
            },
        }, SerializerOptions);
    }

    /// <summary>
    /// Builds an action/result message for an action Randy previously sent.
    /// </summary>
    /// <param name="gameName">The game name to include in the C2S envelope.</param>
    /// <param name="id">The Randy correlation id.</param>
    /// <param name="success">Whether Jennifer reports the action as successful.</param>
    /// <param name="message">The optional human-readable result message.</param>
    /// <returns>The serialized action/result payload.</returns>
    /// <pre><paramref name="id"/> matches the action id Jennifer received from Randy.</pre>
    /// <post>The returned payload contains the Randy-compatible <c>action/result</c> envelope with the <c>game</c> field.</post>
    /// <summary>
    /// Creates an action/result payload without a game name (tests and callers that do not need to set the game field).
    /// </summary>
    /// <param name="id">The Randy correlation id.</param>
    /// <param name="success">Whether Jennifer reports the action as successful.</param>
    /// <param name="message">The optional human-readable result message.</param>
    /// <returns>The serialized action/result payload.</returns>
    /// <pre><paramref name="id"/> matches the action id Jennifer received from Randy.</pre>
    /// <post>The returned payload contains the Randy-compatible <c>action/result</c> envelope without a <c>game</c> field.</post>
    public static string CreateActionResultPayload(string id, bool success, string? message)
        => CreateActionResultPayload(null, id, success, message);

    /// <inheritdoc cref="CreateActionResultPayload(string,bool,string?)"/>
    /// <param name="gameName">The game name to include in the C2S envelope.</param>
    public static string CreateActionResultPayload(string? gameName, string id, bool success, string? message)
    {
        string? normalizedGame = Normalize(gameName);
        return JsonSerializer.Serialize(new
        {
            command = "action/result",
            game = string.IsNullOrWhiteSpace(normalizedGame) ? null : normalizedGame,
            data = new
            {
                id = Normalize(id),
                success,
                message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            },
        }, SerializerOptions);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static Dictionary<string, object?> CreateRegisterActionPayload(JenniferDiscoveredAction action)
    {
        Dictionary<string, object?> payload = new()
        {
            ["name"] = Normalize(action.Name),
            ["description"] = string.IsNullOrWhiteSpace(action.Description) ? Normalize(action.Name) : action.Description.Trim(),
        };

        if (action.HasSchema)
        {
            var properties = new Dictionary<string, object?>();

            foreach (JenniferActionParameter param in action.Parameters)
            {
                var propSchema = new Dictionary<string, object?>
                {
                    ["type"] = param.JsonType,
                };

                if (param.EnumValues.Count > 0)
                {
                    // Preserve the original type: deserialize numeric enum values back to their native type.
                    if (param.JsonType == "integer")
                    {
                        var enumInts = new List<object?>();
                        foreach (string val in param.EnumValues)
                        {
                            if (int.TryParse(val, out int iv))
                                enumInts.Add(iv);
                            else
                                enumInts.Add(val);
                        }
                        propSchema["enum"] = enumInts;
                    }
                    else if (param.JsonType == "number")
                    {
                        var enumNums = new List<object?>();
                        foreach (string val in param.EnumValues)
                        {
                            if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dv))
                                enumNums.Add(dv);
                            else
                                enumNums.Add(val);
                        }
                        propSchema["enum"] = enumNums;
                    }
                    else
                    {
                        propSchema["enum"] = param.EnumValues.ToArray();
                    }
                }

                properties[param.Name] = propSchema;
            }

            var required = action.Parameters
                .Where(p => p.IsRequired)
                .Select(p => p.Name)
                .ToArray();

            var schema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = properties,
            };

            if (required.Length > 0)
                schema["required"] = required;

            payload["schema"] = schema;
        }

        return payload;
    }
}