using FluentAssertions;
using NUnit.Framework;
using NeuroMod.Integration;

namespace NeuroMod.Tests.Integration;

/// <summary>
/// Regression tests for errand pickup status messaging.
/// </summary>
/// <pre>The pickup-status action derives its response from the shared errand completion tracker.</pre>
/// <post>The contained tests verify concise pickup-state messages for active and inactive errand assignments.</post>
[TestFixture]
public class ErrandPickupStatusTests
{
    private float _testTime;

    [SetUp]
    /// <summary>
    /// Resets tracker state before each pickup-status test.
    /// </summary>
    /// <pre>A prior test may have left tracker state or a custom time provider configured.</pre>
    /// <post>The shared errand tracker starts clean and deterministic for the next test.</post>
    public void SetUp()
    {
        _testTime = 0f;
        ErrandCompletionTracker.TestTimeProvider = () => _testTime;
        ErrandCompletionTracker.Instance.CancelTracking();
    }

    [TearDown]
    /// <summary>
    /// Clears tracker state after each pickup-status test.
    /// </summary>
    /// <pre>A pickup-status test has completed.</pre>
    /// <post>The tracker is reset and production time behavior is restored.</post>
    public void TearDown()
    {
        ErrandCompletionTracker.Instance.CancelTracking();
        ErrandCompletionTracker.TestTimeProvider = null;
    }

    [Test]
    /// <summary>
    /// Verifies that the pickup-status helper reports no assignment when nothing is being tracked.
    /// </summary>
    /// <pre>The tracker is idle and there is no last errand snapshot.</pre>
    /// <post>The test confirms the helper reports that no errand is currently assigned.</post>
    public void BuildPickupStatusMessage_WhenNoErrandIsAssigned_ReturnsIdleMessage()
    {
        string message = GetErrandPickupStatusAction.BuildPickupStatusMessage(null, null, false, false);

        message.Should().Be("No errand is currently assigned.");
    }

    [Test]
    /// <summary>
    /// Verifies that the pickup-status helper reports acquisition when the errand has not started yet.
    /// </summary>
    /// <pre>An errand is currently tracked in the acquiring state.</pre>
    /// <post>The test confirms the helper reports that the errand has not been picked up yet.</post>
    public void BuildPickupStatusMessage_WhenErrandIsAcquiring_ReturnsWaitingMessage()
    {
        ErrandCompletionTracker.Instance.BeginTracking("Dig", "Dig", 4, 9);

        string message = GetErrandPickupStatusAction.BuildPickupStatusMessage(
            ErrandCompletionTracker.Instance.CurrentProgress,
            ErrandCompletionTracker.Instance.LastCompletedProgress,
            true,
            true);

        message.Should().Contain("Errand not picked up yet");
        message.Should().Contain("Dig");
        message.Should().Contain("(4,9)");
    }

    [Test]
    /// <summary>
    /// Verifies that the pickup-status helper reports success after the duplicant starts the errand.
    /// </summary>
    /// <pre>An errand is currently tracked in the in-progress state.</pre>
    /// <post>The test confirms the helper reports that the errand was picked up successfully.</post>
    public void BuildPickupStatusMessage_WhenErrandIsInProgress_ReturnsPickedUpMessage()
    {
        ErrandCompletionTracker.Instance.BeginTracking("Build", "Build", 12, 15);
        ErrandCompletionTracker.Instance.OnChoreStarted("Build");

        string message = GetErrandPickupStatusAction.BuildPickupStatusMessage(
            ErrandCompletionTracker.Instance.CurrentProgress,
            ErrandCompletionTracker.Instance.LastCompletedProgress,
            true,
            false);

        message.Should().Contain("Errand picked up successfully");
        message.Should().Contain("Build");
        message.Should().Contain("(12,15)");
    }

    [Test]
    /// <summary>
    /// Verifies that the pickup-status helper reports interruption after a picked-up errand is paused.
    /// </summary>
    /// <pre>An errand was started and then interrupted.</pre>
    /// <post>The test confirms the helper reports that the errand had been picked up before interruption.</post>
    public void BuildPickupStatusMessage_WhenErrandIsInterrupted_ReturnsInterruptedMessage()
    {
        ErrandCompletionTracker.Instance.BeginTracking("Mop", "Tidy", 7, 3);
        ErrandCompletionTracker.Instance.OnChoreStarted("Mop");
        ErrandCompletionTracker.Instance.OnChoreInterrupted("Compulsory: Emote");

        string message = GetErrandPickupStatusAction.BuildPickupStatusMessage(
            ErrandCompletionTracker.Instance.CurrentProgress,
            ErrandCompletionTracker.Instance.LastCompletedProgress,
            true,
            false);

        message.Should().Contain("was picked up, but is currently interrupted");
        message.Should().Contain("Mop");
        message.Should().Contain("(7,3)");
    }
}
