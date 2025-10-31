using FluentAssertions;
using NeuroSdk.Websocket;
using NUnit.Framework;
using System;

namespace NeuroMod.Tests.Websocket;

/// <summary>
/// Tests for WebSocket components that don't require Unity dependencies
/// </summary>
[TestFixture]
public class WebsocketTests
{
    [Test]
    public void ExecutionResult_Success_WithMessage_ShouldHaveCorrectProperties()
    {
        // Arrange
        string message = "Operation completed successfully";

        // Act
        ExecutionResult result = ExecutionResult.Success(message);

        // Assert
        result.Successful.Should().BeTrue();
        result.Message.Should().Be(message);
    }

    [Test]
    public void ExecutionResult_Success_WithoutMessage_ShouldHaveNullMessage()
    {
        // Act
        ExecutionResult result = ExecutionResult.Success();

        // Assert
        result.Successful.Should().BeTrue();
        result.Message.Should().BeNull();
    }

    [Test]
    public void ExecutionResult_Failure_ShouldHaveCorrectProperties()
    {
        // Arrange
        string reason = "Invalid input parameter";

        // Act
        ExecutionResult result = ExecutionResult.Failure(reason);

        // Assert
        result.Successful.Should().BeFalse();
        result.Message.Should().Be(reason);
    }

    [Test]
    public void ExecutionResult_VedalFailure_ShouldAppendSuffix()
    {
        // Arrange
        string reason = "Network connection failed";

        // Act
        ExecutionResult result = ExecutionResult.VedalFailure(reason);

        // Assert
        result.Successful.Should().BeFalse();
        result.Message.Should().StartWith(reason);
        result.Message.Should().Contain("(This is probably not your fault, blame Vedal.)");
    }

    [Test]
    public void ExecutionResult_ModFailure_ShouldAppendSuffix()
    {
        // Arrange
        string reason = "Mod configuration error";

        // Act
        ExecutionResult result = ExecutionResult.ModFailure(reason);

        // Assert
        result.Successful.Should().BeFalse();
        result.Message.Should().StartWith(reason);
        result.Message.Should().Contain("(This is probably not your fault, blame the game integration.)");
    }

    [Test]
    public void WsMessage_ShouldHaveRequiredProperties()
    {
        // This test verifies the WsMessage type exists and has expected structure
        Type wsMessageType = typeof(WsMessage);

        wsMessageType.Should().NotBeNull();
        // WsMessage uses properties, not fields
        wsMessageType.GetProperty("Command").Should().NotBeNull();
        wsMessageType.GetProperty("Data").Should().NotBeNull();
    }

    [Test]
    [Ignore("Requires Unity runtime - MessageQueue depends on Unity types")]
    public void MessageQueue_ShouldHaveExpectedMethods()
    {
        // This test verifies the MessageQueue type has expected structure
        Type messageQueueType = typeof(MessageQueue);

        messageQueueType.Should().NotBeNull();
        messageQueueType.GetMethod("Enqueue").Should().NotBeNull();
        messageQueueType.GetMethod("Dequeue").Should().NotBeNull();
    }
}