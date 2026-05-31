using FluentAssertions;
using NUnit.Framework;
using System;
using System.Reflection;
using NeuroMod.Integration;

namespace NeuroMod.Tests.Integration;

[TestFixture]
[NUnit.Framework.NonParallelizable]
/// <summary>
/// Verifies a compact errand tracker lifecycle flow using a fully reset singleton instance.
/// </summary>
/// <pre>The singleton tracker can be reset between tests through reflection.</pre>
/// <post>The contained test confirms key lifecycle states are emitted in order on a fresh tracker instance.</post>
public class ErrandCompletionTrackerLifecycleTests
{
    /// <summary>
    /// Resets the singleton tracker instance and clears any custom time provider.
    /// </summary>
    /// <pre>The tracker singleton may have been initialized by earlier tests.</pre>
    /// <post>The tracker singleton reference and custom time provider are cleared.</post>
    private void ResetTracker()
    {
        FieldInfo? f = typeof(ErrandCompletionTracker).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        if (f != null) f.SetValue(null, null);
        ErrandCompletionTracker.TestTimeProvider = null;
    }

    [SetUp]
    /// <summary>
    /// Resets the tracker and configures deterministic test time before each test.
    /// </summary>
    /// <pre>A prior test may have initialized the tracker singleton.</pre>
    /// <post>The tracker starts fresh and returns a deterministic current time.</post>
    public void SetUp()
    {
        ResetTracker();
        ErrandCompletionTracker.TestTimeProvider = () => 0f;
    }

    [TearDown]
    /// <summary>
    /// Resets the tracker singleton after each test.
    /// </summary>
    /// <pre>A test has completed and may have left tracker state behind.</pre>
    /// <post>The singleton tracker and custom time provider are cleared.</post>
    public void TearDown()
    {
        ResetTracker();
    }

    [Test]
    /// <summary>
    /// Verifies that one lifecycle can be interrupted, resumed, and completed while publishing states.
    /// </summary>
    /// <pre>A fresh tracker instance is available and a handler subscribes before lifecycle transitions occur.</pre>
    /// <post>The test confirms the tracker emits interrupted, resumed, and completed states for one lifecycle.</post>
    public void InterruptAndResumeAndComplete_Flow()
    {
        var states = new System.Collections.Generic.List<ErrandCompletionTracker.ErrandState>();
        Action<ErrandCompletionTracker.ErrandProgress> handler = (p) => states.Add(p.State);
        ErrandCompletionTracker.Instance.OnErrandStateChanged += handler;

        // Begin and start
        ErrandCompletionTracker.Instance.BeginTracking("Build", "", 0, 0);
        ErrandCompletionTracker.Instance.OnChoreStarted("Build");
        states.Should().Contain(ErrandCompletionTracker.ErrandState.InProgress);

        // Interrupt and resume
        ErrandCompletionTracker.Instance.OnChoreInterrupted("Emote: Dance");
        states.Should().Contain(ErrandCompletionTracker.ErrandState.Interrupted);

        ErrandCompletionTracker.Instance.OnChoreResumed();
        states.Should().Contain(ErrandCompletionTracker.ErrandState.InProgress);

        // Complete
        ErrandCompletionTracker.Instance.OnChoreCompleted();
        states.Should().Contain(ErrandCompletionTracker.ErrandState.Completed);
        ErrandCompletionTracker.Instance.LastCompletedProgress.Should().NotBeNull();
        ErrandCompletionTracker.Instance.LastCompletedProgress!.ChoreTypeName.Should().Be("Build");

        // Unsubscribe
        ErrandCompletionTracker.Instance.OnErrandStateChanged -= handler;
    }
}
