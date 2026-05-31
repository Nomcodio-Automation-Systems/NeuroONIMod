using System.Text.Json;
using FluentAssertions;
using Jennifer.Wpf.Contracts;
using Jennifer.Wpf.Parsing;
using NUnit.Framework;

namespace Jennifer.Wpf.Tests.Contracts;

/// <summary>
/// Tests Jennifer's outbound websocket contract against the message shapes Randy consumes.
/// </summary>
public class JenniferRandyContractPayloadFactoryTests
{
    [Test]
    /// <summary>
    /// Verifies that Jennifer emits the Randy-compatible startup envelope.
    /// </summary>
    /// <post>The serialized payload contains the expected command and game fields without an unexpected data wrapper.</post>
    public void CreateStartupPayload_ShouldMatchRandyEnvelope()
    {
        string json = JenniferRandyContractPayloadFactory.CreateStartupPayload("ONI");

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("command").GetString().Should().Be("startup");
        document.RootElement.GetProperty("game").GetString().Should().Be("ONI");
        document.RootElement.TryGetProperty("data", out _).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that Jennifer emits the Randy-compatible actions/register envelope.
    /// </summary>
    /// <post>The serialized payload contains the required actions array and only emits schema for schema-bearing actions.</post>
    public void CreateActionsRegisterPayload_ShouldMatchRandyEnvelope()
    {
        JenniferDiscoveredAction[] actions =
        [
            new JenniferDiscoveredAction { Name = "inspect", Description = "Inspect target", HasSchema = true },
            new JenniferDiscoveredAction { Name = "wave", Description = "", HasSchema = false },
        ];

        string json = JenniferRandyContractPayloadFactory.CreateActionsRegisterPayload("ONI", actions);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        root.GetProperty("command").GetString().Should().Be("actions/register");
        root.GetProperty("game").GetString().Should().Be("ONI");

        JsonElement registeredActions = root.GetProperty("data").GetProperty("actions");
        registeredActions.GetArrayLength().Should().Be(2);
        registeredActions[0].GetProperty("name").GetString().Should().Be("inspect");
        registeredActions[0].GetProperty("description").GetString().Should().Be("Inspect target");
        registeredActions[0].GetProperty("schema").GetProperty("type").GetString().Should().Be("object");
        registeredActions[1].GetProperty("name").GetString().Should().Be("wave");
        registeredActions[1].GetProperty("description").GetString().Should().Be("wave");
        registeredActions[1].TryGetProperty("schema", out _).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that Jennifer emits the Randy-compatible actions/force envelope.
    /// </summary>
    /// <post>The serialized payload contains the exact field names Randy reads, with normalized and de-duplicated action names.</post>
    public void CreateActionsForcePayload_ShouldMatchRandyEnvelope()
    {
        string json = JenniferRandyContractPayloadFactory.CreateActionsForcePayload(
            "ONI",
            [" inspect ", "INSPECT", "wave"],
            " current state ",
            " do something ",
            " high ",
            true);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement data = document.RootElement.GetProperty("data");

        document.RootElement.GetProperty("command").GetString().Should().Be("actions/force");
        document.RootElement.GetProperty("game").GetString().Should().Be("ONI");
        data.GetProperty("state").GetString().Should().Be("current state");
        data.GetProperty("query").GetString().Should().Be("do something");
        data.GetProperty("priority").GetString().Should().Be("high");
        data.GetProperty("ephemeral_context").GetBoolean().Should().BeTrue();
        data.GetProperty("action_names").EnumerateArray().Select(element => element.GetString()).Should().Equal("inspect", "wave");
    }

    [Test]
    /// <summary>
    /// Verifies that Jennifer emits the Randy-compatible action/result envelope.
    /// </summary>
    /// <post>The serialized payload preserves the action id and omits the message field when no text is supplied.</post>
    public void CreateActionResultPayload_ShouldMatchRandyEnvelope()
    {
        string json = JenniferRandyContractPayloadFactory.CreateActionResultPayload("abc123", true, " ");

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement data = document.RootElement.GetProperty("data");

        document.RootElement.GetProperty("command").GetString().Should().Be("action/result");
        data.GetProperty("id").GetString().Should().Be("abc123");
        data.GetProperty("success").GetBoolean().Should().BeTrue();
        data.TryGetProperty("message", out _).Should().BeFalse();
    }
}