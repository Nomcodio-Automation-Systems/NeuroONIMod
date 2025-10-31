using FluentAssertions;
using NUnit.Framework;

namespace NeuroMod.Tests.Integration;

/// <summary>
/// Basic tests for Integration components
/// </summary>
[TestFixture]
public class SimpleIntegrationTests
{
    [Test]
    [Ignore("Requires Unity runtime initialization")]
    public void NeuroIntegrationManager_ShouldHaveInstance()
    {
        // Assert
        NeuroIntegrationManager.Instance.Should().NotBeNull();
    }

    [Test]
    [Ignore("Requires Unity runtime initialization")]
    public void NeuroIntegrationBridge_ShouldHaveInstance()
    {
        // Assert
        NeuroIntegrationBridge.Instance.Should().NotBeNull();
    }

    [Test]
    [Ignore("EmergencyAction is not accessible from test context")]
    public void EmergencyAction_ShouldCreateValidInstance()
    {
        // Arrange & Act
        // Note: EmergencyAction is a nested class that requires special handling

        // Assert
        // This test is skipped due to accessibility issues
    }
}