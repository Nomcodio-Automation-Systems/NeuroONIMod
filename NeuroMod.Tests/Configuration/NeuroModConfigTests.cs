using FluentAssertions;
using NUnit.Framework;
using System;

namespace NeuroMod.Tests.Configuration;

/// <summary>
/// Tests for NeuroModConfig static configuration class
/// Tests configuration properties and environment variable overrides
/// </summary>
public class NeuroModConfigTests
{
    private const string ConsoleOutputEnvVar = "NEURO_MOD_CONSOLE_OUTPUT";
    private const string DebugLoggingEnvVar = "NEURO_MOD_DEBUG_LOGGING";

    [SetUp]
    public void Setup()
    {
        // Clean up environment variables before each test
        Environment.SetEnvironmentVariable(ConsoleOutputEnvVar, null);
        Environment.SetEnvironmentVariable(DebugLoggingEnvVar, null);

        // Reset config to defaults
        NeuroMod.NeuroModConfig.EnableConsoleOutput = false;
        NeuroMod.NeuroModConfig.EnableDebugLogging = false;
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up environment variables after each test
        Environment.SetEnvironmentVariable(ConsoleOutputEnvVar, null);
        Environment.SetEnvironmentVariable(DebugLoggingEnvVar, null);

        // Reset config to defaults
        NeuroMod.NeuroModConfig.EnableConsoleOutput = false;
        NeuroMod.NeuroModConfig.EnableDebugLogging = false;
    }

    /// <summary>
    /// Test that EnableConsoleOutput has default value of false
    /// </summary>
    [Test]
    public void EnableConsoleOutput_Default_ShouldBeFalse()
    {
        // Assert
        NeuroMod.NeuroModConfig.EnableConsoleOutput.Should().BeFalse();
    }

    /// <summary>
    /// Test that EnableDebugLogging has default value of false
    /// </summary>
    [Test]
    public void EnableDebugLogging_Default_ShouldBeFalse()
    {
        // Assert
        NeuroMod.NeuroModConfig.EnableDebugLogging.Should().BeFalse();
    }

    /// <summary>
    /// Test that EnableConsoleOutput can be set to true
    /// </summary>
    [Test]
    public void EnableConsoleOutput_SetToTrue_ShouldReturnTrue()
    {
        // Act
        NeuroMod.NeuroModConfig.EnableConsoleOutput = true;

        // Assert
        NeuroMod.NeuroModConfig.EnableConsoleOutput.Should().BeTrue();
    }

    /// <summary>
    /// Test that EnableDebugLogging can be set to true
    /// </summary>
    [Test]
    public void EnableDebugLogging_SetToTrue_ShouldReturnTrue()
    {
        // Act
        NeuroMod.NeuroModConfig.EnableDebugLogging = true;

        // Assert
        NeuroMod.NeuroModConfig.EnableDebugLogging.Should().BeTrue();
    }

    /// <summary>
    /// Test that EnableConsoleOutput can be overridden by environment variable
    /// </summary>
    [Test]
    public void EnableConsoleOutput_WithEnvVarTrue_ShouldReturnTrue()
    {
        // Arrange
        Environment.SetEnvironmentVariable(ConsoleOutputEnvVar, "true");

        // Act & Assert
        NeuroMod.NeuroModConfig.EnableConsoleOutput.Should().BeTrue();
    }

    /// <summary>
    /// Test that EnableDebugLogging can be overridden by environment variable
    /// </summary>
    [Test]
    public void EnableDebugLogging_WithEnvVarTrue_ShouldReturnTrue()
    {
        // Arrange
        Environment.SetEnvironmentVariable(DebugLoggingEnvVar, "true");

        // Act & Assert
        NeuroMod.NeuroModConfig.EnableDebugLogging.Should().BeTrue();
    }

    /// <summary>
    /// Test that environment variable takes precedence over set value
    /// </summary>
    [Test]
    public void EnableConsoleOutput_EnvVarOverridesSetValue()
    {
        // Arrange
        NeuroMod.NeuroModConfig.EnableConsoleOutput = false;
        Environment.SetEnvironmentVariable(ConsoleOutputEnvVar, "true");

        // Act & Assert
        NeuroMod.NeuroModConfig.EnableConsoleOutput.Should().BeTrue();
    }

    /// <summary>
    /// Test that environment variable with false works correctly
    /// </summary>
    [Test]
    public void EnableDebugLogging_WithEnvVarFalse_ShouldReturnFalse()
    {
        // Arrange
        NeuroMod.NeuroModConfig.EnableDebugLogging = true;
        Environment.SetEnvironmentVariable(DebugLoggingEnvVar, "false");

        // Act & Assert
        NeuroMod.NeuroModConfig.EnableDebugLogging.Should().BeFalse();
    }

    /// <summary>
    /// Test that invalid environment variable is ignored
    /// </summary>
    [Test]
    public void EnableConsoleOutput_WithInvalidEnvVar_ShouldUseSetValue()
    {
        // Arrange
        NeuroMod.NeuroModConfig.EnableConsoleOutput = true;
        Environment.SetEnvironmentVariable(ConsoleOutputEnvVar, "invalid");

        // Act & Assert
        NeuroMod.NeuroModConfig.EnableConsoleOutput.Should().BeTrue();
    }

    /// <summary>
    /// Test that empty environment variable is ignored
    /// </summary>
    [Test]
    public void EnableDebugLogging_WithEmptyEnvVar_ShouldUseSetValue()
    {
        // Arrange
        NeuroMod.NeuroModConfig.EnableDebugLogging = true;
        Environment.SetEnvironmentVariable(DebugLoggingEnvVar, "");

        // Act & Assert
        NeuroMod.NeuroModConfig.EnableDebugLogging.Should().BeTrue();
    }

    /// <summary>
    /// Test that config values can be toggled multiple times
    /// </summary>
    [Test]
    public void ConfigValues_CanBeToggledMultipleTimes()
    {
        // Act & Assert
        NeuroMod.NeuroModConfig.EnableConsoleOutput = true;
        NeuroMod.NeuroModConfig.EnableConsoleOutput.Should().BeTrue();

        NeuroMod.NeuroModConfig.EnableConsoleOutput = false;
        NeuroMod.NeuroModConfig.EnableConsoleOutput.Should().BeFalse();

        NeuroMod.NeuroModConfig.EnableConsoleOutput = true;
        NeuroMod.NeuroModConfig.EnableConsoleOutput.Should().BeTrue();
    }

    /// <summary>
    /// Test that both config values are independent
    /// </summary>
    [Test]
    public void ConfigValues_AreIndependent()
    {
        // Act
        NeuroMod.NeuroModConfig.EnableConsoleOutput = true;
        NeuroMod.NeuroModConfig.EnableDebugLogging = false;

        // Assert
        NeuroMod.NeuroModConfig.EnableConsoleOutput.Should().BeTrue();
        NeuroMod.NeuroModConfig.EnableDebugLogging.Should().BeFalse();
    }

    /// <summary>
    /// Test that environment variables with different cases are handled
    /// </summary>
    [Test]
    public void EnableConsoleOutput_WithMixedCaseEnvVar_ShouldWorkCorrectly()
    {
        // Arrange
        Environment.SetEnvironmentVariable(ConsoleOutputEnvVar, "True");

        // Act & Assert
        NeuroMod.NeuroModConfig.EnableConsoleOutput.Should().BeTrue();
    }

    /// <summary>
    /// Test that config is static and maintains state across accesses
    /// </summary>
    [Test]
    public void ConfigValues_ShouldMaintainStateAcrossAccesses()
    {
        // Arrange
        NeuroMod.NeuroModConfig.EnableConsoleOutput = true;

        // Act - Access multiple times
        bool value1 = NeuroMod.NeuroModConfig.EnableConsoleOutput;
        bool value2 = NeuroMod.NeuroModConfig.EnableConsoleOutput;
        bool value3 = NeuroMod.NeuroModConfig.EnableConsoleOutput;

        // Assert
        value1.Should().BeTrue();
        value2.Should().BeTrue();
        value3.Should().BeTrue();
    }
}