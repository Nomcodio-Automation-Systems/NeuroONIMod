using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;

namespace NeuroMod.Tests.Configuration;

/// <summary>
/// Comprehensive tests for ConfigManager class
/// Tests configuration loading, validation, fallbacks, and error handling
/// </summary>
[TestFixture]
public class ConfigManagerTests
{
    private ConfigManager _configManager = null!;
    private string _testConfigPath = null!;

    [SetUp]
    public void Setup()
    {
        // Create a test directory for config files
        _testConfigPath = Path.Combine(Path.GetTempPath(), "NeuroModTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testConfigPath);

        // Reset singleton for testing
        ResetConfigManagerInstance();
        _configManager = ConfigManager.Instance;
    }

    [TearDown]
    public void Cleanup()
    {
        // Clean up test files
        if (Directory.Exists(_testConfigPath))
        {
            Directory.Delete(_testConfigPath, true);
        }

        ResetConfigManagerInstance();
    }

    /// <summary>
    /// Test that ConfigManager singleton pattern works correctly
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime and Application.persistentDataPath")]
    public void ConfigManager_Singleton_ShouldReturnSameInstance()
    {
        // Act
        ConfigManager instance1 = ConfigManager.Instance;
        ConfigManager instance2 = ConfigManager.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2, "ConfigManager should return the same instance");
        instance1.Should().NotBeNull("ConfigManager instance should not be null");
    }

    /// <summary>
    /// Test that default configuration is loaded when no config file exists
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime and Application.persistentDataPath")]
    public void LoadConfig_WhenNoFileExists_ShouldCreateDefaultConfig()
    {
        // Arrange
        // No config file exists

        // Act
        bool result = _configManager.LoadConfig();

        // Assert
        result.Should().BeFalse("LoadConfig should return false when creating default config");
        _configManager.IsLoaded.Should().BeTrue("ConfigManager should be marked as loaded");
        _configManager.Config.Should().NotBeNull("Config should not be null");
        _configManager.Config.Neuro.Should().NotBeNull("Neuro config should not be null");
        _configManager.Config.Duplicant.Should().NotBeNull("Duplicant config should not be null");
        _configManager.Config.Game.Should().NotBeNull("Game config should not be null");
        _configManager.Config.Timeout.Should().NotBeNull("Timeout config should not be null");
    }

    /// <summary>
    /// Test that GetConfigValue method works correctly with valid selector
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime and Application.persistentDataPath")]
    public void GetConfigValue_WithValidSelector_ShouldReturnValue()
    {
        // Arrange
        _configManager.LoadConfig(); // Load default config

        // Act
        string? endpointUrl = _configManager.GetConfigValue(c => c.Neuro.EndpointUrl, "fallback");
        int responseTimeout = _configManager.GetConfigValue(c => c.Neuro.ResponseTimeout, 999);

        // Assert
        endpointUrl.Should().Be("ws://localhost:8000");
        responseTimeout.Should().Be(10);
    }

    /// <summary>
    /// Test that GetConfigValue returns fallback when config not loaded
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime and Application.persistentDataPath")]
    public void GetConfigValue_WhenNotLoaded_ShouldReturnFallback()
    {
        // Arrange
        // Don't load config

        // Act
        string? endpointUrl = _configManager.GetConfigValue(c => c.Neuro.EndpointUrl, "fallback");
        int responseTimeout = _configManager.GetConfigValue(c => c.Neuro.ResponseTimeout, 999);

        // Assert
        endpointUrl.Should().Be("fallback");
        responseTimeout.Should().Be(999);
    }

    /// <summary>
    /// Test that GetConfigValue handles exceptions gracefully
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime and Application.persistentDataPath")]
    public void GetConfigValue_WithExceptionInSelector_ShouldReturnFallback()
    {
        // Arrange
        _configManager.LoadConfig();

        // Act
        string? result = _configManager.GetConfigValue<string>(c => throw new InvalidOperationException(), "fallback");

        // Assert
        result.Should().Be("fallback");
    }

    /// <summary>
    /// Test that configuration classes have proper default values
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime and Application.persistentDataPath")]
    public void DefaultConfiguration_ShouldHaveValidValues()
    {
        // Arrange & Act
        _configManager.LoadConfig();
        ModConfig config = _configManager.Config;

        // Assert - Neuro defaults
        config.Neuro.EndpointUrl.Should().Be("ws://localhost:8000");
        config.Neuro.ConnectionTimeout.Should().Be(30);
        config.Neuro.ResponseTimeout.Should().Be(10);
        config.Neuro.MaxRetryAttempts.Should().Be(3);
        config.Neuro.RetryDelay.Should().Be(5);
        config.Neuro.AutoReconnect.Should().BeTrue();

        // Assert - Duplicant defaults
        config.Duplicant.DefaultName.Should().Be("NeuroBot");
        config.Duplicant.AllowRename.Should().BeTrue();
        config.Duplicant.FallbackBehavior.Should().Be("idle");
        config.Duplicant.BioMonitoringEnabled.Should().BeTrue();
        config.Duplicant.BioUpdateFrequency.Should().Be(5);

        // Assert - Game defaults
        config.Game.ScheduleControlEnabled.Should().BeTrue();
        config.Game.RealtimeDecisions.Should().BeTrue();
        config.Game.CommandPriority.Should().Be("high");
        config.Game.DebugLogging.Should().BeTrue();
        config.Game.LogLevel.Should().Be("info");
        config.Game.PerformanceMonitoring.Should().BeTrue();

        // Assert - Timeout defaults
        config.Timeout.GlobalTimeout.Should().Be(15);
        config.Timeout.DecisionTimeout.Should().Be(8);
        config.Timeout.ActionTimeout.Should().Be(12);
        config.Timeout.QueryTimeout.Should().Be(5);
        config.Timeout.FallbackStrategies.Should().NotBeNull();
        config.Timeout.ShowTimeoutWarnings.Should().BeTrue();
        config.Timeout.EscalationThreshold.Should().Be(5);
        config.Timeout.EscalationAction.Should().Be("switch_to_manual_mode");
    }

    /// <summary>
    /// Helper method to reset ConfigManager singleton for testing
    /// Note: In a real implementation, this would require making the _instance field accessible for testing
    /// </summary>
    private void ResetConfigManagerInstance()
    {
        // This would require reflection or a test-specific reset method in the actual ConfigManager
        // For now, this demonstrates the intended test behavior
    }
}