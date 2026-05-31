using FluentAssertions;
using NUnit.Framework;
using NeuroMod.Integration;

namespace NeuroMod.Tests.Integration;

/// <summary>
/// Regression tests for isolated errand monitor decision logic.
/// </summary>
/// <pre>The monitor exposes narrow internal helpers for acquisition and priority-restore decisions.</pre>
/// <post>The contained tests verify the decision helpers preserve the intended errand-monitor invariants.</post>
[TestFixture]
public class ErrandMonitorDecisionTests
{
    [Test]
    /// <summary>
    /// Verifies that exact-target acquisition accepts only the reserved target chore.
    /// </summary>
    /// <pre>The decision helper is evaluating candidates for an assignment that selected one exact target.</pre>
    /// <post>The test confirms only the exact reserved target is allowed during acquisition.</post>
    public void ShouldAllowAcquiringCandidate_WhenExactTargetExists_OnlyAllowsThatExactTarget()
    {
        ErrandMonitor.ShouldAllowAcquiringCandidate(true, true, true, false, false, false).Should().BeTrue();
        ErrandMonitor.ShouldAllowAcquiringCandidate(true, true, false, false, false, false).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that exact-target acquisition also accepts an equivalent replacement chore for the same errand.
    /// </summary>
    /// <pre>The decision helper is evaluating a recreated chore instance that still represents the originally targeted errand.</pre>
    /// <post>The test confirms equivalent replacement chores are allowed during acquisition.</post>
    public void ShouldAllowAcquiringCandidate_WhenEquivalentTargetExists_AllowsEquivalentReplacement()
    {
        ErrandMonitor.ShouldAllowAcquiringCandidate(true, true, false, true, false, false).Should().BeTrue();
    }

    [Test]
    /// <summary>
    /// Verifies that type-based acquisition accepts only matching work-chore types.
    /// </summary>
    /// <pre>The decision helper is evaluating candidates for an assignment constrained by chore type.</pre>
    /// <post>The test confirms only matching type candidates are allowed during acquisition.</post>
    public void ShouldAllowAcquiringCandidate_WhenTargetTypeExists_AllowsOnlyMatchingType()
    {
        ErrandMonitor.ShouldAllowAcquiringCandidate(true, false, false, false, true, true).Should().BeTrue();
        ErrandMonitor.ShouldAllowAcquiringCandidate(true, false, false, false, true, false).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that non-work chores are rejected even if other acquisition flags would match.
    /// </summary>
    /// <pre>The decision helper is evaluating a candidate that is outside the normal work-chore range.</pre>
    /// <post>The test confirms non-work chores are rejected during acquisition.</post>
    public void ShouldAllowAcquiringCandidate_RejectsNonWorkChoresEvenWhenTheyOtherwiseMatch()
    {
        ErrandMonitor.ShouldAllowAcquiringCandidate(false, false, false, false, false, false).Should().BeFalse();
        ErrandMonitor.ShouldAllowAcquiringCandidate(false, true, true, false, false, false).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that the monitor restores priority when the current value still reflects the temporary boost.
    /// </summary>
    /// <pre>The decision helper is comparing the original priority against a current value of five.</pre>
    /// <post>The test confirms the restore decision returns true when the max-priority boost is still active.</post>
    public void ShouldRestoreBoostedPriority_WhenCurrentIsTemporaryBoost_ReturnsTrue()
    {
        ErrandMonitor.ShouldRestoreBoostedPriority(5, 3).Should().BeTrue();
    }

    [Test]
    /// <summary>
    /// Verifies that the monitor does not restore priority when another change already replaced the temporary boost.
    /// </summary>
    /// <pre>The decision helper is comparing the original priority against a current value that is no longer a temporary boost or was originally max.</pre>
    /// <post>The test confirms the restore decision returns false for non-restorable cases.</post>
    public void ShouldRestoreBoostedPriority_WhenPriorityAlreadyChangedOrWasOriginallyMax_ReturnsFalse()
    {
        ErrandMonitor.ShouldRestoreBoostedPriority(4, 3).Should().BeFalse();
        ErrandMonitor.ShouldRestoreBoostedPriority(5, 5).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that the monitor does not force-stop the current chore while the simulation is paused.
    /// </summary>
    /// <pre>The interruption decision helper is evaluating an assignment while the game is paused.</pre>
    /// <post>The test confirms immediate interruption is deferred during pause.</post>
    public void ShouldInterruptCurrentChoreImmediately_WhenPaused_ReturnsFalse()
    {
        ErrandMonitor.ShouldInterruptCurrentChoreImmediately(true, true).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that the monitor interrupts immediately when a driver exists and the simulation is running.
    /// </summary>
    /// <pre>The interruption decision helper is evaluating an assignment while the game is unpaused.</pre>
    /// <post>The test confirms immediate interruption is allowed only for active simulation with a driver.</post>
    public void ShouldInterruptCurrentChoreImmediately_WhenRunningWithDriver_ReturnsTrue()
    {
        ErrandMonitor.ShouldInterruptCurrentChoreImmediately(true, false).Should().BeTrue();
        ErrandMonitor.ShouldInterruptCurrentChoreImmediately(false, false).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that the monitor does not queue another forced interrupt while one is already pending.
    /// </summary>
    /// <pre>The retry helper is evaluating a still-pending forced interruption request.</pre>
    /// <post>The test confirms redundant retry requests are suppressed while a deferred interrupt is queued.</post>
    public void ShouldRetryForcedInterrupt_WhenPendingInterruptExists_ReturnsFalse()
    {
        ErrandMonitor.ShouldRetryForcedInterrupt(10f, 0f, true).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that the monitor retries a forced interrupt only after the retry cooldown has elapsed.
    /// </summary>
    /// <pre>The retry helper is comparing the current time against the timestamp of the last interrupt attempt.</pre>
    /// <post>The test confirms retry requests are throttled until the configured cooldown expires.</post>
    public void ShouldRetryForcedInterrupt_WhenCooldownElapsed_ReturnsExpectedResult()
    {
        ErrandMonitor.ShouldRetryForcedInterrupt(10f, 9.8f, false).Should().BeFalse();
        ErrandMonitor.ShouldRetryForcedInterrupt(10f, 9.5f, false).Should().BeTrue();
    }

    [Test]
    /// <summary>
    /// Verifies that the monitor availability helper rejects stale or unusable chore references.
    /// </summary>
    /// <pre>The availability helper may receive a null chore reference when the cached errand no longer exists.</pre>
    /// <post>The test confirms stale chore references are treated as unavailable.</post>
    public void IsChoreAvailable_WhenChoreIsNull_ReturnsFalse()
    {
        ErrandMonitor.IsChoreAvailable(null).Should().BeFalse();
    }
}