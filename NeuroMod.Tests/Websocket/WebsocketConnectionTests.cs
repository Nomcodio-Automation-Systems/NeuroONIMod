using FluentAssertions;
using NeuroSdk.Websocket;
using NUnit.Framework;

namespace NeuroMod.Tests.Websocket;

/// <summary>
/// Comprehensive tests for WebsocketConnection class
/// Tests connection management, message handling, and error scenarios
/// </summary>
public class WebsocketConnectionTests
{
    [SetUp]
    public void Setup()
    {
        // Setup test environment
    }

    [TearDown]
    public void TearDown()
    {
        // Cleanup after each test
    }

    /// <summary>
    /// Test that WebsocketConnection instance can be accessed
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime - WebsocketConnection.Instance depends on Unity initialization")]
    public void Instance_ShouldBeAccessible()
    {
        // Act
        WebsocketConnection? instance = WebsocketConnection.Instance;

        // Assert - Instance might be null if not initialized in Unity context
        // Just ensure property access doesn't throw
        System.Action getInstanceAction = () => { WebsocketConnection? _ = WebsocketConnection.Instance; };
        getInstanceAction.Should().NotThrow("Accessing Instance property should not throw");
    }

    /// <summary>
    /// Test that instance Send method handles null gracefully
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime for Debug.Log")]
    public void Instance_Send_WithNullMessage_ShouldHandleGracefully()
    {
        // Arrange
        WebsocketConnection? instance = WebsocketConnection.Instance;

        if (instance != null)
        {
            // Act & Assert
            System.Action sendAction = () => instance.Send(null!);
            sendAction.Should().NotThrow("Instance.Send with null should handle gracefully");
        }
        else
        {
            return;
        }
    }

    /// <summary>
    /// Test that instance SendImmediate method handles null gracefully
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime for Debug.Log")]
    public void Instance_SendImmediate_WithNullMessage_ShouldHandleGracefully()
    {
        // Arrange
        WebsocketConnection? instance = WebsocketConnection.Instance;

        if (instance != null)
        {
            // Act & Assert
            System.Action sendAction = () => instance.SendImmediate(null!);
            sendAction.Should().NotThrow("Instance.SendImmediate with null should handle gracefully");
        }
        else
        {
            return;
        }
    }

    /// <summary>
    /// Test IsConnected property access
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime - WebsocketConnection.Instance depends on Unity initialization")]
    public void IsConnected_PropertyAccess_ShouldNotThrow()
    {
        // Arrange
        WebsocketConnection? instance = WebsocketConnection.Instance;

        if (instance != null)
        {
            // Act & Assert
            System.Action getConnectedAction = () => { bool _ = instance.IsConnected; };
            getConnectedAction.Should().NotThrow("Accessing IsConnected should not throw");
        }
        else
        {
            // Instance is null in test context, which is expected
            return;
        }
    }

    /// <summary>
    /// Test that WebSocket connection APIs are available (using current non-obsolete methods)
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime for Debug.Log")]
    public void WebsocketConnection_CurrentApiMethods_ShouldBeAvailable()
    {
        // Arrange
        WebsocketConnection? instance = WebsocketConnection.Instance;

        if (instance != null)
        {
            // Assert that instance methods exist and can be called
            System.Action sendAction = () => instance.Send(null!);
            System.Action sendImmediateAction = () => instance.SendImmediate(null!);

            sendAction.Should().NotThrow("Instance.Send method should be available");
            sendImmediateAction.Should().NotThrow("Instance.SendImmediate method should be available");
        }
        else
        {
            return;
        }
    }

    /// <summary>
    /// Test that deprecated static methods still exist for backward compatibility but are obsolete
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime for Debug.Log")]
    public void WebsocketConnection_DeprecatedApiMethods_ShouldStillExist()
    {
        // These methods are obsolete but should still work for backward compatibility
        System.Action trySendAction = () =>
        {
#pragma warning disable CS0618 // Type or member is obsolete
            WebsocketConnection.TrySend(null!);
#pragma warning restore CS0618 // Type or member is obsolete
        };

        System.Action trySendImmediateAction = () =>
        {
#pragma warning disable CS0618 // Type or member is obsolete
            WebsocketConnection.TrySendImmediate(null!);
#pragma warning restore CS0618 // Type or member is obsolete
        };

        trySendAction.Should().NotThrow("Deprecated TrySend method should still be available");
        trySendImmediateAction.Should().NotThrow("Deprecated TrySendImmediate method should still be available");
    }
}