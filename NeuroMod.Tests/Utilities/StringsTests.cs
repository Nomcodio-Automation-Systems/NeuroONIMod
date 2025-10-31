using FluentAssertions;
using NUnit.Framework;

namespace NeuroMod.Tests.Utilities;

/// <summary>
/// Tests for the Strings class
/// Tests string constants and format strings used throughout the SDK
/// </summary>
[TestFixture]
public class StringsTests
{
    /// <summary>
    /// Test that VedalFaultSuffix is not empty
    /// </summary>
    [Test]
    public void VedalFaultSuffix_ShouldNotBeEmpty()
    {
        // Assert
        NeuroSdk.Strings.VedalFaultSuffix.Should().NotBeNullOrWhiteSpace();
        NeuroSdk.Strings.VedalFaultSuffix.Should().Contain("Vedal");
    }

    /// <summary>
    /// Test that ModFaultSuffix is not empty
    /// </summary>
    [Test]
    public void ModFaultSuffix_ShouldNotBeEmpty()
    {
        // Assert
        NeuroSdk.Strings.ModFaultSuffix.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Test that action failure messages are not empty
    /// </summary>
    [Test]
    public void ActionFailureMessages_ShouldNotBeEmpty()
    {
        // Assert
        NeuroSdk.Strings.ActionFailedNoData.Should().NotBeNullOrWhiteSpace();
        NeuroSdk.Strings.ActionFailedNoId.Should().NotBeNullOrWhiteSpace();
        NeuroSdk.Strings.ActionFailedNoName.Should().NotBeNullOrWhiteSpace();
        NeuroSdk.Strings.ActionFailedInvalidJson.Should().NotBeNullOrWhiteSpace();
        NeuroSdk.Strings.ActionFailedUnregistered.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Test that ActionFailedUnknownAction format string works correctly
    /// </summary>
    [Test]
    public void ActionFailedUnknownAction_FormatString_ShouldWorkCorrectly()
    {
        // Act
        string result = NeuroSdk.Strings.ActionFailedUnknownAction.Format("test_action");

        // Assert
        result.Should().Contain("test_action");
        result.Should().Contain("Action failed");
        result.Should().Contain("Unknown action");
    }

    /// <summary>
    /// Test that ActionFailedCaughtException format string works correctly
    /// </summary>
    [Test]
    public void ActionFailedCaughtException_FormatString_ShouldWorkCorrectly()
    {
        // Act
        string result = NeuroSdk.Strings.ActionFailedCaughtException.Format("TestException");

        // Assert
        result.Should().Contain("TestException");
        result.Should().Contain("Action failed");
        result.Should().Contain("exception");
    }

    /// <summary>
    /// Test that ActionFailedMissingRequiredParameter format string works correctly
    /// </summary>
    [Test]
    public void ActionFailedMissingRequiredParameter_FormatString_ShouldWorkCorrectly()
    {
        // Act
        string result = NeuroSdk.Strings.ActionFailedMissingRequiredParameter.Format("paramName");

        // Assert
        result.Should().Contain("paramName");
        result.Should().Contain("Missing required");
        result.Should().Contain("parameter");
    }

    /// <summary>
    /// Test that ActionFailedInvalidParameter format string works correctly
    /// </summary>
    [Test]
    public void ActionFailedInvalidParameter_FormatString_ShouldWorkCorrectly()
    {
        // Act
        string result = NeuroSdk.Strings.ActionFailedInvalidParameter.Format("paramName");

        // Assert
        result.Should().Contain("paramName");
        result.Should().Contain("Invalid");
        result.Should().Contain("parameter");
    }

    /// <summary>
    /// Test that MessageHandlerFailedCaughtException format string works correctly
    /// </summary>
    [Test]
    public void MessageHandlerFailedCaughtException_FormatString_ShouldWorkCorrectly()
    {
        // Act
        string result = NeuroSdk.Strings.MessageHandlerFailedCaughtException.Format("TestException");

        // Assert
        result.Should().Contain("TestException");
        result.Should().Contain("Message handler failed");
        result.Should().Contain("exception");
    }

    /// <summary>
    /// Test that all string constants start with "Action failed" where appropriate
    /// </summary>
    [Test]
    public void ActionFailureMessages_ShouldStartWithActionFailed()
    {
        // Assert
        NeuroSdk.Strings.ActionFailedNoData.Should().StartWith("Action failed");
        NeuroSdk.Strings.ActionFailedNoId.Should().StartWith("Action failed");
        NeuroSdk.Strings.ActionFailedNoName.Should().StartWith("Action failed");
        NeuroSdk.Strings.ActionFailedInvalidJson.Should().StartWith("Action failed");
        NeuroSdk.Strings.ActionFailedUnregistered.Should().NotStartWith("Action failed",
            "this message has a different structure");
    }

    /// <summary>
    /// Test that fault suffixes are different
    /// </summary>
    [Test]
    public void FaultSuffixes_ShouldBeDifferent()
    {
        // Assert
        NeuroSdk.Strings.VedalFaultSuffix.Should().NotBe(NeuroSdk.Strings.ModFaultSuffix);
    }

    /// <summary>
    /// Test that format strings can handle special characters
    /// </summary>
    [Test]
    public void FormatStrings_ShouldHandleSpecialCharacters()
    {
        // Act
        string result1 = NeuroSdk.Strings.ActionFailedUnknownAction.Format("action\"with\"quotes");
        string result2 = NeuroSdk.Strings.ActionFailedCaughtException.Format("Exception\nwith\nnewlines");

        // Assert
        result1.Should().Contain("action\"with\"quotes");
        result2.Should().Contain("Exception\nwith\nnewlines");
    }

    /// <summary>
    /// Test that format strings can handle empty strings
    /// </summary>
    [Test]
    public void FormatStrings_ShouldHandleEmptyStrings()
    {
        // Act
        string result = NeuroSdk.Strings.ActionFailedUnknownAction.Format("");

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("Action failed");
    }

    /// <summary>
    /// Test that format strings can handle long strings
    /// </summary>
    [Test]
    public void FormatStrings_ShouldHandleLongStrings()
    {
        // Arrange
        string longString = new('A', 1000);

        // Act
        string result = NeuroSdk.Strings.ActionFailedUnknownAction.Format(longString);

        // Assert
        result.Should().Contain(longString);
    }

    /// <summary>
    /// Test that all action failure constants contain meaningful error descriptions
    /// </summary>
    [Test]
    public void ActionFailureMessages_ShouldContainMeaningfulDescriptions()
    {
        // Assert
        NeuroSdk.Strings.ActionFailedNoData.Should().Contain("data");
        NeuroSdk.Strings.ActionFailedNoId.Should().Contain("id");
        NeuroSdk.Strings.ActionFailedNoName.Should().Contain("name");
        NeuroSdk.Strings.ActionFailedInvalidJson.Should().Contain("JSON");
        NeuroSdk.Strings.ActionFailedUnregistered.Should().Contain("unregistered");
    }

    /// <summary>
    /// Test that FormatString instances can be reused
    /// </summary>
    [Test]
    public void FormatStrings_CanBeReusedMultipleTimes()
    {
        // Act
        string result1 = NeuroSdk.Strings.ActionFailedUnknownAction.Format("action1");
        string result2 = NeuroSdk.Strings.ActionFailedUnknownAction.Format("action2");
        string result3 = NeuroSdk.Strings.ActionFailedUnknownAction.Format("action3");

        // Assert
        result1.Should().Contain("action1");
        result2.Should().Contain("action2");
        result3.Should().Contain("action3");

        result1.Should().NotContain("action2");
        result2.Should().NotContain("action3");
    }

    /// <summary>
    /// Test that fault suffixes have appropriate tone
    /// </summary>
    [Test]
    public void FaultSuffixes_ShouldHaveAppropriateTone()
    {
        // Assert
        NeuroSdk.Strings.VedalFaultSuffix.Should().Contain("not your fault");
        NeuroSdk.Strings.ModFaultSuffix.Should().Contain("not your fault");
    }
}