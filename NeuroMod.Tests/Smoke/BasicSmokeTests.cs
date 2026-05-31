using FluentAssertions;
using NUnit.Framework;
using NeuroSdk.Websocket;

namespace NeuroMod.Tests.Smoke;

public class BasicSmokeTests
{
    [Test]
    public void ExecutionResult_Success_IsSuccessful()
    {
        var r = ExecutionResult.Success();
        r.Should().NotBeNull();
        r.Successful.Should().BeTrue();
    }

    [Test]
    public void ExecutionResult_Failure_IsFailure()
    {
        var r = ExecutionResult.Failure("err");
        r.Should().NotBeNull();
        r.Successful.Should().BeFalse();
        r.Message.Should().Contain("err");
    }
}
