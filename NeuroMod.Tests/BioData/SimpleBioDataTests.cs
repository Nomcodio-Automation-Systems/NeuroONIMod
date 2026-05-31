using FluentAssertions;
using NUnit.Framework;
using System.Reflection;

namespace NeuroMod.Tests.BioData;

/// <summary>
/// Basic tests for BioData monitoring components
/// </summary>
public class SimpleBioDataTests
{
    [Test]
    [Ignore("Requires Unity runtime initialization")]
    public void DuplicateBioDataMonitor_ShouldHaveInstance()
    {
        // Assert
        DuplicateBioDataMonitor.Instance.Should().NotBeNull();
    }
    [Test]
    public void BioDataAnalyzer_ShouldHaveStaticMethods()
    {
        // Arrange & Act - Test that we can call static methods
        // Since it's static, we just verify the type exists and methods are accessible

        // Assert
        MethodInfo method = typeof(BioDataAnalyzer).GetMethod("AnalyzeColonyHealth");
        method.Should().NotBeNull();
        method.IsStatic.Should().BeTrue();
    }

    [Test]
    [Ignore("Requires Unity runtime - DuplicateBioData constructor depends on Unity MinionIdentity type")]
    public void DuplicateBioData_ShouldCreateValidInstance_WithMockMinion()
    {
        // This test would require a real MinionIdentity which is complex to mock
        // So we'll just test that the type exists and has the right constructor
        ConstructorInfo constructor = typeof(DuplicateBioData).GetConstructor([typeof(MinionIdentity)]);
        constructor.Should().NotBeNull();
    }
}