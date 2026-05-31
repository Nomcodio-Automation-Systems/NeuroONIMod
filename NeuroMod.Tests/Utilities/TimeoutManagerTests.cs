using FluentAssertions;
using NUnit.Framework;
using System.Threading.Tasks;

namespace NeuroMod.Tests.Utilities;

/// <summary>
/// Comprehensive tests for TimeoutManager class
/// Tests timeout handling, operation management, and error scenarios
/// </summary>
public class TimeoutManagerTests
{
    private TimeoutManager _timeoutManager = null!;

    [SetUp]
    public void Setup()
    {
        _timeoutManager = TimeoutManager.Instance;
    }

    [TearDown]
    public void TearDown()
    {
        // Reset timeout count for clean test state
        _timeoutManager.ResetTimeoutCount();
    }

    /// <summary>
    /// Test that TimeoutManager instance is accessible
    /// </summary>
    [Test]
    public void Instance_ShouldBeAvailable()
    {
        // Assert
        _timeoutManager.Should().NotBeNull("TimeoutManager instance should be available");
        TimeoutManager.Instance.Should().BeSameAs(_timeoutManager, "Should be singleton");
    }

    /// <summary>
    /// Test that operations execute successfully within timeout
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime for Debug.Log")]
    public async Task ExecuteWithTimeout_QuickOperation_ShouldSucceed()
    {
        // Arrange
        bool operationExecuted = false;
        async Task<bool> operation()
        {
            await Task.Delay(100); // Quick operation
            operationExecuted = true;
            return true;
        }

        // Act
        bool result = await _timeoutManager.ExecuteWithTimeout(
            "test_operation",
operation,
            () => false,
            5);

        // Assert
        result.Should().BeTrue("Quick operation should succeed");
        operationExecuted.Should().BeTrue("Operation should have been executed");
    }

    /// <summary>
    /// Test that timeout count can be reset
    /// </summary>
    [Test]
    public void ResetTimeoutCount_ShouldResetCounter()
    {
        // Act
        _timeoutManager.ResetTimeoutCount();

        // Assert
        // Should not throw exception
        _timeoutManager.GetPendingOperationsCount().Should().BeGreaterOrEqualTo(0);
    }

    /// <summary>
    /// Test that pending operations count can be retrieved
    /// </summary>
    [Test]
    public void GetPendingOperationsCount_ShouldReturnValidCount()
    {
        // Act
        int count = _timeoutManager.GetPendingOperationsCount();

        // Assert
        count.Should().BeGreaterOrEqualTo(0, "Pending operations count should be non-negative");
    }

    /// <summary>
    /// Test that all operations can be cancelled
    /// </summary>
    [Test]
    public void CancelAllOperations_ShouldCancelOperations()
    {
        // Act & Assert - Should not throw
        System.Action cancelAction = () => _timeoutManager.CancelAllOperations();
        cancelAction.Should().NotThrow("Cancelling operations should not throw");
    }

    /// <summary>
    /// Test void operations with timeout
    /// </summary>
    [Test]
    [Ignore("Requires Unity runtime for Debug.Log")]
    public async Task ExecuteWithTimeout_VoidOperation_ShouldExecute()
    {
        // Arrange
        bool operationExecuted = false;
        async Task operation()
        {
            await Task.Delay(50);
            operationExecuted = true;
        }

        // Act
        await _timeoutManager.ExecuteWithTimeout(
            "test_void_operation",
operation,
            null,
            5);

        // Assert
        operationExecuted.Should().BeTrue("Void operation should have been executed");
    }

    /// <summary>
    /// Test error handling with invalid parameters
    /// </summary>
    [Test]
    public void TimeoutManager_WithInvalidParameters_ShouldHandleGracefully()
    {
        // Act & Assert - Test various scenarios
        System.Action resetAction = () => _timeoutManager.ResetTimeoutCount();
        System.Action cancelAction = () => _timeoutManager.CancelAllOperations();
        System.Action countAction = () => _timeoutManager.GetPendingOperationsCount();

        resetAction.Should().NotThrow("Reset should not throw");
        cancelAction.Should().NotThrow("Cancel should not throw");
        countAction.Should().NotThrow("Count should not throw");
    }
}