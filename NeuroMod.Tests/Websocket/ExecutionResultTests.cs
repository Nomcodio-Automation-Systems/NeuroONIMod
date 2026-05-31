using FluentAssertions;
using NeuroSdk.Websocket;
using NUnit.Framework;

namespace NeuroMod.Tests.Websocket;

/// <summary>
/// Tests for the ExecutionResult class
/// Tests result creation, properties, and factory methods
/// </summary>
public class ExecutionResultTests
{
    /// <summary>
    /// Test that Success creates a successful result
    /// </summary>
    [Test]
    public void Success_WithoutMessage_ShouldCreateSuccessfulResult()
    {
        // Act
        ExecutionResult result = ExecutionResult.Success();

        // Assert
        result.Should().NotBeNull();
        result.Successful.Should().BeTrue();
        result.Message.Should().BeNull();
    }

    /// <summary>
    /// Test that Success with message creates a successful result with message
    /// </summary>
    [Test]
    public void Success_WithMessage_ShouldCreateSuccessfulResultWithMessage()
    {
        // Arrange
        string message = "Operation completed successfully";

        // Act
        ExecutionResult result = ExecutionResult.Success(message);

        // Assert
        result.Should().NotBeNull();
        result.Successful.Should().BeTrue();
        result.Message.Should().Be(message);
    }

    /// <summary>
    /// Test that Failure creates a failed result
    /// </summary>
    [Test]
    public void Failure_WithReason_ShouldCreateFailedResult()
    {
        // Arrange
        string reason = "Operation failed";

        // Act
        ExecutionResult result = ExecutionResult.Failure(reason);

        // Assert
        result.Should().NotBeNull();
        result.Successful.Should().BeFalse();
        result.Message.Should().Be(reason);
    }

    /// <summary>
    /// Test that VedalFailure creates a failed result with Vedal fault suffix
    /// </summary>
    [Test]
    public void VedalFailure_WithReason_ShouldAppendVedalSuffix()
    {
        // Arrange
        string reason = "Connection lost";

        // Act
        ExecutionResult result = ExecutionResult.VedalFailure(reason);

        // Assert
        result.Should().NotBeNull();
        result.Successful.Should().BeFalse();
        result.Message.Should().NotBeNull();
        result.Message.Should().StartWith(reason);
        result.Message.Should().Contain("Vedal", "should contain Vedal fault indicator");
    }

    /// <summary>
    /// Test that ModFailure creates a failed result with Mod fault suffix
    /// </summary>
    [Test]
    public void ModFailure_WithReason_ShouldAppendModSuffix()
    {
        // Arrange
        string reason = "Invalid configuration";

        // Act
        ExecutionResult result = ExecutionResult.ModFailure(reason);

        // Assert
        result.Should().NotBeNull();
        result.Successful.Should().BeFalse();
        result.Message.Should().NotBeNull();
        result.Message.Should().StartWith(reason);
        result.Message.Should().Contain("game integration", "should contain mod/game integration fault indicator");
    }

    /// <summary>
    /// Test that multiple Success calls create independent results
    /// </summary>
    [Test]
    public void Success_MultipleCalls_ShouldCreateIndependentResults()
    {
        // Act
        ExecutionResult result1 = ExecutionResult.Success("Message 1");
        ExecutionResult result2 = ExecutionResult.Success("Message 2");

        // Assert
        result1.Should().NotBeSameAs(result2);
        result1.Message.Should().Be("Message 1");
        result2.Message.Should().Be("Message 2");
    }

    /// <summary>
    /// Test that multiple Failure calls create independent results
    /// </summary>
    [Test]
    public void Failure_MultipleCalls_ShouldCreateIndependentResults()
    {
        // Act
        ExecutionResult result1 = ExecutionResult.Failure("Error 1");
        ExecutionResult result2 = ExecutionResult.Failure("Error 2");

        // Assert
        result1.Should().NotBeSameAs(result2);
        result1.Message.Should().Be("Error 1");
        result2.Message.Should().Be("Error 2");
    }

    /// <summary>
    /// Test that results with empty messages work correctly
    /// </summary>
    [Test]
    public void Failure_WithEmptyReason_ShouldCreateResultWithEmptyMessage()
    {
        // Arrange
        string reason = "";

        // Act
        ExecutionResult result = ExecutionResult.Failure(reason);

        // Assert
        result.Should().NotBeNull();
        result.Successful.Should().BeFalse();
        result.Message.Should().BeEmpty();
    }

    /// <summary>
    /// Test that Success and Failure have opposite success states
    /// </summary>
    [Test]
    public void SuccessAndFailure_ShouldHaveOppositeStates()
    {
        // Act
        ExecutionResult success = ExecutionResult.Success();
        ExecutionResult failure = ExecutionResult.Failure("Error");

        // Assert
        success.Successful.Should().BeTrue();
        failure.Successful.Should().BeFalse();
        success.Successful.Should().NotBe(failure.Successful);
    }

    /// <summary>
    /// Test that VedalFailure and ModFailure both return failures
    /// </summary>
    [Test]
    public void VedalFailureAndModFailure_ShouldBothBeFailures()
    {
        // Act
        ExecutionResult vedalFailure = ExecutionResult.VedalFailure("Vedal error");
        ExecutionResult modFailure = ExecutionResult.ModFailure("Mod error");

        // Assert
        vedalFailure.Successful.Should().BeFalse();
        modFailure.Successful.Should().BeFalse();
    }

    /// <summary>
    /// Test that VedalFailure and ModFailure have different suffixes
    /// </summary>
    [Test]
    public void VedalFailureAndModFailure_ShouldHaveDifferentSuffixes()
    {
        // Arrange
        string baseReason = "Test error";

        // Act
        ExecutionResult vedalFailure = ExecutionResult.VedalFailure(baseReason);
        ExecutionResult modFailure = ExecutionResult.ModFailure(baseReason);

        // Assert
        vedalFailure.Message.Should().NotBe(modFailure.Message, "different fault types should have different messages");
        vedalFailure.Message.Should().StartWith(baseReason);
        modFailure.Message.Should().StartWith(baseReason);
    }

    /// <summary>
    /// Test that Success with null message is handled correctly
    /// </summary>
    [Test]
    public void Success_WithNullMessage_ShouldStoreNull()
    {
        // Act
        ExecutionResult result = ExecutionResult.Success(null);

        // Assert
        result.Successful.Should().BeTrue();
        result.Message.Should().BeNull();
    }

    /// <summary>
    /// Test that long messages are preserved correctly
    /// </summary>
    [Test]
    public void Failure_WithLongMessage_ShouldPreserveFullMessage()
    {
        // Arrange
        string longMessage = new('A', 1000);

        // Act
        ExecutionResult result = ExecutionResult.Failure(longMessage);

        // Assert
        result.Message.Should().HaveLength(1000);
        result.Message.Should().Be(longMessage);
    }

    /// <summary>
    /// Test that special characters in messages are preserved
    /// </summary>
    [Test]
    public void Failure_WithSpecialCharacters_ShouldPreserveCharacters()
    {
        // Arrange
        string specialMessage = "Error: \"Test\" with\nnewlines and\ttabs";

        // Act
        ExecutionResult result = ExecutionResult.Failure(specialMessage);

        // Assert
        result.Message.Should().Be(specialMessage);
        result.Message.Should().Contain("\"");
        result.Message.Should().Contain("\n");
        result.Message.Should().Contain("\t");
    }
}