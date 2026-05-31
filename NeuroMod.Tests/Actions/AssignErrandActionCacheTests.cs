using FluentAssertions;
using NUnit.Framework;
using NeuroMod;

namespace NeuroMod.Tests.Actions;

/// <summary>
/// Regression tests for assign-errand cache recovery helpers.
/// </summary>
/// <pre>The list-errands cache may retain stale errand ids after the underlying chore instance disappears.</pre>
/// <post>The contained tests verify stale errand ids are cleared consistently.</post>
[TestFixture]
public class AssignErrandActionCacheTests
{
    [SetUp]
    /// <summary>
    /// Clears the errand scan caches before each test.
    /// </summary>
    /// <pre>A prior test may have left errand id cache entries behind.</pre>
    /// <post>Both errand-id caches start empty for the next test.</post>
    public void SetUp()
    {
        ListErrandsAction.LastScanCache.Clear();
        ListErrandsAction.LastScanReferenceCache.Clear();
    }

    [Test]
    /// <summary>
    /// Verifies that clearing a stale errand id removes both cached chore and scan metadata entries.
    /// </summary>
    /// <pre>The errand id exists in one or both caches before the helper runs.</pre>
    /// <post>The test confirms no cache retains the stale errand id afterwards.</post>
    public void ClearCachedErrandId_RemovesEntriesFromAllCaches()
    {
        ListErrandsAction.LastScanReferenceCache[7] = new ListErrandsAction.ErrandScanReference("Dig", 12, 34);

        AssignErrandAction.ClearCachedErrandId(7);

        ListErrandsAction.LastScanCache.ContainsKey(7).Should().BeFalse();
        ListErrandsAction.LastScanReferenceCache.ContainsKey(7).Should().BeFalse();
    }

    [Test]
    /// <summary>
    /// Verifies that an unmatched errand token produces a non-matching fallback resolution result.
    /// </summary>
    /// <pre>The caller supplied an errand_type that matches neither a chore type nor a chore group.</pre>
    /// <post>The test confirms assign_errand reports the token as unresolved.</post>
    public void ResolveChoreTypeMatch_WhenNothingMatches_ReturnsNoMatch()
    {
        AssignErrandAction.ChoreTypeMatch match = AssignErrandAction.ResolveChoreTypeMatch("definitely_not_a_real_errand_type");

        match.IsMatch.Should().BeFalse();
        match.DisplayName.Should().Be("definitely_not_a_real_errand_type");
    }

    [TestCase("relax")]
    [TestCase("toilet")]
    [TestCase("use_toilet")]
    [TestCase("bathroom")]
    /// <summary>
    /// Verifies that known schedule-gated errand tokens are recognized explicitly.
    /// </summary>
    /// <param name="requestedType">The caller-supplied errand token.</param>
    /// <pre>The caller asked assign_errand for an activity that only appears during a matching schedule block.</pre>
    /// <post>The test confirms assign_errand can return a schedule-specific failure message for the token.</post>
    public void IsScheduleGatedErrandRequest_WhenKnownScheduleToken_ReturnsTrue(string requestedType)
    {
        AssignErrandAction.IsScheduleGatedErrandRequest(requestedType).Should().BeTrue();
    }

    [TestCase("dig")]
    [TestCase("build")]
    [TestCase("harvest")]
    /// <summary>
    /// Verifies that ordinary work errands are not treated as schedule-gated requests.
    /// </summary>
    /// <param name="requestedType">The caller-supplied errand token.</param>
    /// <pre>The caller asked assign_errand for a normal work errand.</pre>
    /// <post>The test confirms assign_errand keeps its standard resolution path for non-schedule errands.</post>
    public void IsScheduleGatedErrandRequest_WhenNormalWorkToken_ReturnsFalse(string requestedType)
    {
        AssignErrandAction.IsScheduleGatedErrandRequest(requestedType).Should().BeFalse();
    }
}
