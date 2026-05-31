using FluentAssertions;
using NUnit.Framework;
using NeuroMod.Integration;

namespace NeuroMod.Tests.Integration;

/// <summary>
/// Tests for the ErrandCompletionTracker lifecycle management.
/// Validates state transitions, timeout checking, and notification behavior.
/// </summary>
/// <pre>The tracker can be reset and driven through errand lifecycle transitions in an isolated test context.</pre>
/// <post>The contained tests verify lifecycle state, timeout handling, and archived progress behavior.</post>
public class ErrandCompletionTrackerTests
{
    private float _testTime;

    [SetUp]
    /// <summary>
    /// Resets the tracker and installs a deterministic test time provider before each test.
    /// </summary>
    /// <pre>A prior test may have left tracker state or a custom time provider configured.</pre>
    /// <post>The tracker starts from a clean state and reads time from <see cref="_testTime"/>.</post>
    public void SetUp()
    {
        _testTime = 0f;
        ErrandCompletionTracker.TestTimeProvider = () => _testTime;
        // Cancel any existing tracking to start clean
        ErrandCompletionTracker.Instance.CancelTracking();
    }

    [TearDown]
    /// <summary>
    /// Clears tracker state and removes the deterministic time provider after each test.
    /// </summary>
    /// <pre>A test has completed and may have left tracker state or a custom time provider configured.</pre>
    /// <post>The tracker is cleared and production time behavior is restored for subsequent tests.</post>
    public void TearDown()
    {
        ErrandCompletionTracker.Instance.CancelTracking();
        ErrandCompletionTracker.TestTimeProvider = null;
    }

    [Test]
    /// <summary>
    /// Verifies that the tracker reports no active work when idle.
    /// </summary>
    /// <pre>The tracker has been reset and no errand has been started.</pre>
    /// <post>The test confirms <see cref="ErrandCompletionTracker.IsTracking"/> returns false while idle.</post>
    public void IsTracking_WhenIdle_ReturnsFalse()
    {
        // Assert
        ErrandCompletionTracker.Instance.IsTracking().Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that beginning tracking transitions the tracker into the acquiring state.
    /// </summary>
    /// <pre>The tracker is idle before the test starts a new errand.</pre>
    /// <post>The test confirms assignment metadata is stored and the tracker enters acquiring state.</post>
    public void BeginTracking_SetsStateToAcquiring()
    {
        // Act
        ErrandCompletionTracker.Instance.BeginTracking("Mop", "Tidy", 10, 20);

        // Assert
        ErrandCompletionTracker.Instance.IsTracking().Should().BeTrue();
        ErrandCompletionTracker.Instance.CurrentProgress.Should().NotBeNull();
        ErrandCompletionTracker.Instance.CurrentProgress!.State
            .Should().Be(ErrandCompletionTracker.ErrandState.Acquiring);
        ErrandCompletionTracker.Instance.CurrentProgress.ChoreTypeName
            .Should().Be("Mop");
        ErrandCompletionTracker.Instance.CurrentProgress.ChoreGroupName
            .Should().Be("Tidy");
        ErrandCompletionTracker.Instance.CurrentProgress.TargetX.Should().Be(10);
        ErrandCompletionTracker.Instance.CurrentProgress.TargetY.Should().Be(20);
    }

    [Test]
    /// <summary>
    /// Verifies that starting a chore transitions the tracker into the in-progress state.
    /// </summary>
    /// <pre>An errand is already being tracked in the acquiring state.</pre>
    /// <post>The test confirms the tracker records an in-progress state and start time.</post>
    public void OnChoreStarted_TransitionsToInProgress()
    {
        // Arrange
        ErrandCompletionTracker.Instance.BeginTracking("Build", "Build", 5, 15);

        // Act
        ErrandCompletionTracker.Instance.OnChoreStarted("Build");

        // Assert
        ErrandCompletionTracker.Instance.CurrentProgress!.State
            .Should().Be(ErrandCompletionTracker.ErrandState.InProgress);
        ErrandCompletionTracker.Instance.CurrentProgress.StartTime.Should().NotBeNull();
    }

    [Test]
    /// <summary>
    /// Verifies that interruptions move the tracker into the interrupted state and increment the count.
    /// </summary>
    /// <pre>An errand is already in progress when the interruption is reported.</pre>
    /// <post>The test confirms interruption state and count are updated consistently.</post>
    public void OnChoreInterrupted_TransitionsToInterrupted_AndIncrementsCount()
    {
        // Arrange
        ErrandCompletionTracker.Instance.BeginTracking("Dig", "Dig", 3, 7);
        ErrandCompletionTracker.Instance.OnChoreStarted("Dig");

        // Act
        ErrandCompletionTracker.Instance.OnChoreInterrupted("Emote: Dance");

        // Assert
        ErrandCompletionTracker.Instance.CurrentProgress!.State
            .Should().Be(ErrandCompletionTracker.ErrandState.Interrupted);
        ErrandCompletionTracker.Instance.CurrentProgress.InterruptionCount.Should().Be(1);
    }

    [Test]
    /// <summary>
    /// Verifies that resuming an interrupted chore returns the tracker to in-progress state.
    /// </summary>
    /// <pre>The current errand has already entered the interrupted state.</pre>
    /// <post>The test confirms the tracker transitions back to in-progress state.</post>
    public void OnChoreResumed_TransitionsBackToInProgress()
    {
        // Arrange
        ErrandCompletionTracker.Instance.BeginTracking("Dig", "Dig", 3, 7);
        ErrandCompletionTracker.Instance.OnChoreStarted("Dig");
        ErrandCompletionTracker.Instance.OnChoreInterrupted("Emote");

        // Act
        ErrandCompletionTracker.Instance.OnChoreResumed();

        // Assert
        ErrandCompletionTracker.Instance.CurrentProgress!.State
            .Should().Be(ErrandCompletionTracker.ErrandState.InProgress);
    }

    [Test]
    /// <summary>
    /// Verifies that successful completion clears current progress and archives the finished errand.
    /// </summary>
    /// <pre>An errand is actively tracked when completion is reported.</pre>
    /// <post>The test confirms active progress is cleared and archived as completed.</post>
    public void OnChoreCompleted_ClearsCurrentProgress_ArchivesToLast()
    {
        // Arrange
        ErrandCompletionTracker.Instance.BeginTracking("Mop", "Tidy", 10, 20);
        ErrandCompletionTracker.Instance.OnChoreStarted("Mop");

        // Act
        ErrandCompletionTracker.Instance.OnChoreCompleted();

        // Assert
        ErrandCompletionTracker.Instance.CurrentProgress.Should().BeNull();
        ErrandCompletionTracker.Instance.IsTracking().Should().BeFalse();
        ErrandCompletionTracker.Instance.LastCompletedProgress.Should().NotBeNull();
        ErrandCompletionTracker.Instance.LastCompletedProgress!.State
            .Should().Be(ErrandCompletionTracker.ErrandState.Completed);
        ErrandCompletionTracker.Instance.LastCompletedProgress.ChoreTypeName
            .Should().Be("Mop");
    }

    [Test]
    /// <summary>
    /// Verifies that failures clear current progress and archive the failed errand.
    /// </summary>
    /// <pre>An errand is actively tracked when failure is reported.</pre>
    /// <post>The test confirms active progress is cleared and archived with failed state information.</post>
    public void OnErrandFailed_ClearsCurrentProgress_ArchivesToLast()
    {
        // Arrange
        ErrandCompletionTracker.Instance.BeginTracking("Cook", "Cook", 0, 0);
        ErrandCompletionTracker.Instance.OnChoreStarted("Cook");

        // Act
        ErrandCompletionTracker.Instance.OnErrandFailed("No ingredients available");

        // Assert
        ErrandCompletionTracker.Instance.CurrentProgress.Should().BeNull();
        ErrandCompletionTracker.Instance.IsTracking().Should().BeFalse();
        ErrandCompletionTracker.Instance.LastCompletedProgress.Should().NotBeNull();
        ErrandCompletionTracker.Instance.LastCompletedProgress!.State
            .Should().Be(ErrandCompletionTracker.ErrandState.Failed);
        ErrandCompletionTracker.Instance.LastCompletedProgress.StatusMessage
            .Should().Contain("No ingredients");
    }

    [Test]
    /// <summary>
    /// Verifies that explicit timeout completion archives the errand as timed out.
    /// </summary>
    /// <pre>An errand is actively tracked when timeout completion is reported.</pre>
    /// <post>The test confirms the active errand is archived with timed-out state.</post>
    public void OnErrandTimedOut_ClearsCurrentProgress_SetsTimedOutState()
    {
        // Arrange
        ErrandCompletionTracker.Instance.BeginTracking("Harvest", "Farming", 20, 30);
        ErrandCompletionTracker.Instance.OnChoreStarted("Harvest");

        // Act
        ErrandCompletionTracker.Instance.OnErrandTimedOut();

        // Assert
        ErrandCompletionTracker.Instance.IsTracking().Should().BeFalse();
        ErrandCompletionTracker.Instance.LastCompletedProgress!.State
            .Should().Be(ErrandCompletionTracker.ErrandState.TimedOut);
    }

    [Test]
    /// <summary>
    /// Verifies that acquire-phase timeouts fail tracking when pickup takes too long.
    /// </summary>
    /// <pre>An errand remains in acquiring state while deterministic test time advances past the acquire timeout.</pre>
    /// <post>The test confirms the tracker fails and archives the errand after the acquire timeout elapses.</post>
    public void CheckTimeouts_WhenAcquireTakesTooLong_FailsTracking()
    {
        ErrandCompletionTracker.Instance.BeginTracking("Build", "Build", 5, 15);

        _testTime = 61f;
        ErrandCompletionTracker.Instance.CheckTimeouts();

        ErrandCompletionTracker.Instance.CurrentProgress.Should().BeNull();
        ErrandCompletionTracker.Instance.IsTracking().Should().BeFalse();
        ErrandCompletionTracker.Instance.LastCompletedProgress.Should().NotBeNull();
        ErrandCompletionTracker.Instance.LastCompletedProgress!.State
            .Should().Be(ErrandCompletionTracker.ErrandState.Failed);
        ErrandCompletionTracker.Instance.LastCompletedProgress.StatusMessage
            .Should().Contain("pick up the errand in time");
    }

    [Test]
    /// <summary>
    /// Verifies that long-running errands time out after the configured in-progress threshold.
    /// </summary>
    /// <pre>An errand is already in progress while deterministic test time advances past the errand timeout.</pre>
    /// <post>The test confirms the tracker archives the errand as timed out.</post>
    public void CheckTimeouts_WhenErrandRunsTooLong_TimesOutTracking()
    {
        ErrandCompletionTracker.Instance.BeginTracking("Harvest", "Farming", 20, 30);
        ErrandCompletionTracker.Instance.OnChoreStarted("Harvest");

        _testTime = 601f;
        ErrandCompletionTracker.Instance.CheckTimeouts();

        ErrandCompletionTracker.Instance.CurrentProgress.Should().BeNull();
        ErrandCompletionTracker.Instance.IsTracking().Should().BeFalse();
        ErrandCompletionTracker.Instance.LastCompletedProgress.Should().NotBeNull();
        ErrandCompletionTracker.Instance.LastCompletedProgress!.State
            .Should().Be(ErrandCompletionTracker.ErrandState.TimedOut);
    }

    [Test]
    /// <summary>
    /// Verifies that interrupted errands fail when they do not resume within the grace period.
    /// </summary>
    /// <pre>An errand is interrupted and deterministic test time advances beyond the interruption grace threshold.</pre>
    /// <post>The test confirms the tracker archives the errand as failed due to missing resumption.</post>
    public void CheckTimeouts_WhenInterruptedBeyondGracePeriod_FailsTracking()
    {
        ErrandCompletionTracker.Instance.BeginTracking("Dig", "Dig", 3, 7);
        ErrandCompletionTracker.Instance.OnChoreStarted("Dig");

        _testTime = 10f;
        ErrandCompletionTracker.Instance.OnChoreInterrupted("Emote");

        _testTime = 26f;
        ErrandCompletionTracker.Instance.CheckTimeouts();

        ErrandCompletionTracker.Instance.CurrentProgress.Should().BeNull();
        ErrandCompletionTracker.Instance.LastCompletedProgress.Should().NotBeNull();
        ErrandCompletionTracker.Instance.LastCompletedProgress!.State
            .Should().Be(ErrandCompletionTracker.ErrandState.Failed);
        ErrandCompletionTracker.Instance.LastCompletedProgress.StatusMessage
            .Should().Contain("not resumed");
    }

    [Test]
    /// <summary>
    /// Verifies that too many interruptions fail the errand immediately during timeout evaluation.
    /// </summary>
    /// <pre>An errand has accumulated the maximum supported number of interruptions.</pre>
    /// <post>The test confirms the tracker archives the errand as failed because interruption limits were exceeded.</post>
    public void CheckTimeouts_WhenInterruptedTooManyTimes_FailsTracking()
    {
        ErrandCompletionTracker.Instance.BeginTracking("Build", "Build", 5, 5);
        ErrandCompletionTracker.Instance.OnChoreStarted("Build");

        for (int i = 0; i < 4; i++)
        {
            ErrandCompletionTracker.Instance.OnChoreInterrupted($"Emote {i}");
            ErrandCompletionTracker.Instance.OnChoreResumed();
        }

        ErrandCompletionTracker.Instance.OnChoreInterrupted("Emote final");
        ErrandCompletionTracker.Instance.CheckTimeouts();

        ErrandCompletionTracker.Instance.CurrentProgress.Should().BeNull();
        ErrandCompletionTracker.Instance.LastCompletedProgress.Should().NotBeNull();
        ErrandCompletionTracker.Instance.LastCompletedProgress!.State
            .Should().Be(ErrandCompletionTracker.ErrandState.Failed);
        ErrandCompletionTracker.Instance.LastCompletedProgress.StatusMessage
            .Should().Contain("Too many interruptions");
    }

    [Test]
    /// <summary>
    /// Verifies that cancelling tracking clears active progress.
    /// </summary>
    /// <pre>An errand is currently being tracked.</pre>
    /// <post>The test confirms no active errand remains after cancellation.</post>
    public void CancelTracking_ClearsCurrentProgress()
    {
        // Arrange
        ErrandCompletionTracker.Instance.BeginTracking("Build", "Build", 5, 5);

        // Act
        ErrandCompletionTracker.Instance.CancelTracking();

        // Assert
        ErrandCompletionTracker.Instance.CurrentProgress.Should().BeNull();
        ErrandCompletionTracker.Instance.IsTracking().Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that starting a new errand archives the previously tracked one.
    /// </summary>
    /// <pre>An errand is already being tracked when a second errand begins.</pre>
    /// <post>The test confirms the old errand is archived and the new one becomes current.</post>
    public void BeginTracking_WhenAlreadyTracking_ArchivesPriorAndStartsNew()
    {
        // Arrange
        ErrandCompletionTracker.Instance.BeginTracking("Mop", "Tidy", 10, 20);
        ErrandCompletionTracker.Instance.OnChoreStarted("Mop");

        // Act — Start tracking a new errand over the old one
        ErrandCompletionTracker.Instance.BeginTracking("Dig", "Dig", 30, 40);

        // Assert — Old one archived, new one is current
        ErrandCompletionTracker.Instance.CurrentProgress!.ChoreTypeName.Should().Be("Dig");
        ErrandCompletionTracker.Instance.CurrentProgress.State
            .Should().Be(ErrandCompletionTracker.ErrandState.Acquiring);
        ErrandCompletionTracker.Instance.LastCompletedProgress!.ChoreTypeName
            .Should().Be("Mop");
    }

    [Test]
    /// <summary>
    /// Verifies that repeated interruptions accumulate on the current errand.
    /// </summary>
    /// <pre>An errand can be interrupted and resumed multiple times within one lifecycle.</pre>
    /// <post>The test confirms the interruption counter reflects every interruption observed.</post>
    public void MultipleInterruptions_TrackCount()
    {
        // Arrange
        ErrandCompletionTracker.Instance.BeginTracking("Build", "Build", 5, 5);
        ErrandCompletionTracker.Instance.OnChoreStarted("Build");

        // Act
        ErrandCompletionTracker.Instance.OnChoreInterrupted("Emote 1");
        ErrandCompletionTracker.Instance.OnChoreResumed();
        ErrandCompletionTracker.Instance.OnChoreInterrupted("Emote 2");
        ErrandCompletionTracker.Instance.OnChoreResumed();
        ErrandCompletionTracker.Instance.OnChoreInterrupted("Schedule change");

        // Assert
        ErrandCompletionTracker.Instance.CurrentProgress!.InterruptionCount.Should().Be(3);
    }

    [Test]
    /// <summary>
    /// Verifies that tracker state changes raise the public lifecycle event.
    /// </summary>
    /// <pre>A handler is subscribed before the tracker moves through several lifecycle transitions.</pre>
    /// <post>The test confirms the event fires for each expected state transition.</post>
    public void OnErrandStateChanged_FiresEvent()
    {
        // Arrange
        int eventCount = 0;
        System.Action<ErrandCompletionTracker.ErrandProgress> handler = _ => eventCount++;
        ErrandCompletionTracker.Instance.OnErrandStateChanged += handler;

        // Act
        ErrandCompletionTracker.Instance.BeginTracking("Mop", "Tidy", 1, 1);
        ErrandCompletionTracker.Instance.OnChoreStarted("Mop");
        ErrandCompletionTracker.Instance.OnChoreInterrupted("Test");
        ErrandCompletionTracker.Instance.OnChoreResumed();

        // Assert — 4 state changes: Acquiring, InProgress, Interrupted, InProgress
        eventCount.Should().Be(4);

        // Cleanup
        ErrandCompletionTracker.Instance.OnErrandStateChanged -= handler;
    }

    [Test]
    /// <summary>
    /// Verifies that the textual summary includes the main errand fields and interruption state.
    /// </summary>
    /// <pre>An interrupted errand is currently being tracked.</pre>
    /// <post>The test confirms the summary contains chore identity, location, and interruption details.</post>
    public void GetSummary_ContainsRelevantInfo()
    {
        // Arrange
        ErrandCompletionTracker.Instance.BeginTracking("Mop", "Tidy", 10, 20);
        ErrandCompletionTracker.Instance.OnChoreStarted("Mop");
        ErrandCompletionTracker.Instance.OnChoreInterrupted("Test reason");

        // Act
        string summary = ErrandCompletionTracker.Instance.CurrentProgress!.GetSummary();

        // Assert
        summary.Should().Contain("Mop");
        summary.Should().Contain("Tidy");
        summary.Should().Contain("10");
        summary.Should().Contain("20");
        summary.Should().Contain("Interrupted");
        summary.Should().Contain("1x");
    }
}

/// <summary>
/// Tests for ErrandReservationHelper.
/// </summary>
/// <pre>The reservation helper can be reset between tests.</pre>
/// <post>The contained tests verify reservation helper behavior for null and clear-all scenarios.</post>
public class ErrandReservationHelperTests
{
    /// <summary>
    /// Initializes the reservation helper test fixture with an empty reservation set.
    /// </summary>
    /// <pre>Prior tests may have left reserved chores in the helper.</pre>
    /// <post>The reservation set is cleared before each fixture instance is used.</post>
    public ErrandReservationHelperTests()
    {
        ErrandReservationHelper.ClearAll();
    }

    [Test]
    /// <summary>
    /// Verifies that reserving a null chore is rejected.
    /// </summary>
    /// <pre>No valid chore instance is supplied to the helper.</pre>
    /// <post>The test confirms the helper returns false for null chore reservations.</post>
    public void ReserveChore_NullChore_ReturnsFalse()
    {
        // Act & Assert
        ErrandReservationHelper.ReserveChore(null!).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that querying null does not report a reservation.
    /// </summary>
    /// <pre>No valid chore instance is supplied to the helper.</pre>
    /// <post>The test confirms the helper reports null chores as unreserved.</post>
    public void IsReserved_NullChore_ReturnsFalse()
    {
        // Act & Assert
        ErrandReservationHelper.IsReserved(null!).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that clearing reservations succeeds even when nothing is reserved.
    /// </summary>
    /// <pre>The helper may already be empty before the clear operation runs.</pre>
    /// <post>The test confirms clearing leaves the reservation helper in an empty, usable state.</post>
    public void ClearAll_EmptiesReservations()
    {
        // Arrange — nothing reserved

        // Act
        ErrandReservationHelper.ClearAll();

        // Assert — should not throw
        ErrandReservationHelper.IsReserved(null!).Should().BeFalse();
    }
}
