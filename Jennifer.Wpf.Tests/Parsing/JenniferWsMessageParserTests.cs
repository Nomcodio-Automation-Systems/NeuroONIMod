using System.Text.Json;
using FluentAssertions;
using Jennifer.Wpf.Parsing;
using NUnit.Framework;

namespace Jennifer.Wpf.Tests.Parsing;

/// <summary>
/// Tests Jennifer WebSocket message parsing.
/// </summary>
public class JenniferWsMessageParserTests
{
    [Test]
    /// <summary>
    /// Verifies that incoming action payloads are parsed into Jennifer action messages.
    /// </summary>
    /// <post>The parser returns the action id, name, and raw JSON data for object payloads.</post>
    public void Parse_ShouldRecognizeActionPayload()
    {
        const string json = """
        {
          "command": "action",
          "data": {
            "id": "abc123",
            "name": "inspect",
            "data": { "room": "lab" }
          }
        }
        """;

        JenniferWsMessage message = JenniferWsMessageParser.Parse(json);

        message.Kind.Should().Be(JenniferWsMessageKind.Action);
        message.Command.Should().Be("action");
        message.ActionId.Should().Be("abc123");
        message.ActionName.Should().Be("inspect");

        using JsonDocument document = JsonDocument.Parse(message.ActionData!);
        document.RootElement.GetProperty("room").GetString().Should().Be("lab");
    }

    [Test]
    /// <summary>
    /// Verifies that a re-register command is recognized explicitly.
    /// </summary>
    /// <post>The parser returns the dedicated re-register message kind.</post>
    public void Parse_ShouldRecognizeReregisterCommand()
    {
        JenniferWsMessage message = JenniferWsMessageParser.Parse("{\"command\":\"actions/reregister_all\"}");

        message.Kind.Should().Be(JenniferWsMessageKind.ReRegisterAll);
        message.Command.Should().Be("actions/reregister_all");
    }

    [Test]
    /// <summary>
    /// Verifies that Randy's stringified JSON action payload is preserved for Jennifer automation.
    /// </summary>
    /// <post>The parser returns the original stringified JSON payload instead of forcing it into an object.</post>
    public void Parse_ShouldPreserveStringifiedActionDataFromRandy()
    {
        const string json = """
        {
          "command": "action",
          "data": {
            "id": "abc123",
            "name": "inspect",
            "data": "{\"room\":\"lab\"}"
          }
        }
        """;

        JenniferWsMessage message = JenniferWsMessageParser.Parse(json);

        message.Kind.Should().Be(JenniferWsMessageKind.Action);
        message.ActionId.Should().Be("abc123");
        message.ActionName.Should().Be("inspect");
        message.ActionData.Should().Be("{\"room\":\"lab\"}");
    }

    [Test]
    /// <summary>
    /// Verifies that invalid JSON becomes an unknown Jennifer message.
    /// </summary>
    /// <post>The parser preserves the raw payload while reporting the message as unknown.</post>
    public void Parse_ShouldReturnUnknownForInvalidJson()
    {
        JenniferWsMessage message = JenniferWsMessageParser.Parse("not-json");

        message.Kind.Should().Be(JenniferWsMessageKind.Unknown);
        message.Raw.Should().Be("not-json");
    }

    [Test]
    public void Parse_ShouldReturnUnknownForEmptyInput()
    {
      JenniferWsMessage message = JenniferWsMessageParser.Parse("   ");

      message.Kind.Should().Be(JenniferWsMessageKind.Unknown);
    }

    [Test]
    public void Parse_ShouldReturnGenericForNonObjectJson()
    {
      JenniferWsMessage message = JenniferWsMessageParser.Parse("[]");

      message.Kind.Should().Be(JenniferWsMessageKind.Generic);
      message.Command.Should().BeEmpty();
    }

    [Test]
    public void Parse_ShouldReturnGenericForUnknownCommandObject()
    {
      JenniferWsMessage message = JenniferWsMessageParser.Parse("{\"command\":\"ping\"}");

      message.Kind.Should().Be(JenniferWsMessageKind.Generic);
      message.Command.Should().Be("ping");
    }

    [Test]
    public void Parse_ShouldAllowMissingActionMetadata()
    {
      JenniferWsMessage message = JenniferWsMessageParser.Parse("{\"command\":\"action\",\"data\":{\"data\":null}}");

      message.Kind.Should().Be(JenniferWsMessageKind.Action);
      message.ActionId.Should().BeNull();
      message.ActionName.Should().BeNull();
      message.ActionData.Should().BeNull();
    }

    [Test]
    public void Parse_ShouldPreservePrimitiveActionData()
    {
      JenniferWsMessage message = JenniferWsMessageParser.Parse("{\"command\":\"action\",\"data\":{\"id\":\"1\",\"name\":\"inspect\",\"data\":42}}");

      message.ActionData.Should().Be("42");
    }
}