using System;
using FluentAssertions;
using Jennifer.Wpf.Automation;
using Jennifer.Wpf.Parsing;
using Jennifer.Wpf.Session;
using NUnit.Framework;

namespace Jennifer.Wpf.Tests.Session;

/// <summary>
/// Tests Jennifer's extracted session coordination logic.
/// </summary>
public class JenniferSessionCoordinatorTests
{
    [Test]
    public void ProcessIncomingMessage_ShouldCreateFallbackActionValues()
    {
        JenniferIncomingMessageResult result = JenniferSessionCoordinator.ProcessIncomingMessage(
            "{\"command\":\"action\",\"data\":{}}",
            new DateTimeOffset(2026, 3, 12, 8, 30, 0, TimeSpan.Zero));

        result.Kind.Should().Be(JenniferWsMessageKind.Action);
        result.IncomingAction.Should().NotBeNull();
        result.IncomingAction!.Id.Should().NotBeNullOrWhiteSpace();
        result.IncomingAction.Name.Should().Be("unknown");
        result.IncomingAction.ReceivedAt.Should().Be(new DateTimeOffset(2026, 3, 12, 8, 30, 0, TimeSpan.Zero));
    }

    [Test]
    public void UpsertPendingAction_ShouldReplaceDuplicateIdAndKeepNewestFirst()
    {
        JenniferIncomingAction[] existing =
        [
            new JenniferIncomingAction { Id = "1", Name = "inspect" },
            new JenniferIncomingAction { Id = "2", Name = "pickup" },
        ];

        IReadOnlyList<JenniferIncomingAction> merged = JenniferSessionCoordinator.UpsertPendingAction(
            existing,
            new JenniferIncomingAction { Id = "1", Name = "inspect-updated" });

        merged.Should().HaveCount(2);
        merged[0].Name.Should().Be("inspect-updated");
        merged[1].Id.Should().Be("2");
    }

    [Test]
    public void RemovePendingAction_ShouldRemoveMatchingIdOnly()
    {
        JenniferIncomingAction[] existing =
        [
            new JenniferIncomingAction { Id = "1", Name = "inspect" },
            new JenniferIncomingAction { Id = "2", Name = "pickup" },
        ];

        IReadOnlyList<JenniferIncomingAction> remaining = JenniferSessionCoordinator.RemovePendingAction(existing, "1");

        remaining.Should().ContainSingle();
        remaining[0].Id.Should().Be("2");
    }

    [Test]
    public void FindAutomationReply_ShouldReturnReplyForMatchingStepWhenEnabled()
    {
        JenniferAutomationPlan plan = new()
        {
            Steps =
            [
                new JenniferAutomationStep { ActionName = "inspect", ResultSuccess = false, ResultMessage = "busy" },
            ],
        };

        JenniferAutomationReply? reply = JenniferSessionCoordinator.FindAutomationReply(
            plan,
            true,
            new JenniferIncomingAction { Id = "1", Name = "inspect" });

        reply.Should().NotBeNull();
        reply!.ResultSuccess.Should().BeFalse();
        reply.ResultMessage.Should().Be("busy");
    }

    [Test]
    public void FindAutomationReply_ShouldReturnNullWhenDisabledOrNotMatching()
    {
        JenniferAutomationPlan plan = new()
        {
            Steps = [new JenniferAutomationStep { ActionName = "inspect", ResultMessage = "ok" }],
        };

        JenniferSessionCoordinator.FindAutomationReply(plan, false, new JenniferIncomingAction { Name = "inspect" }).Should().BeNull();
        JenniferSessionCoordinator.FindAutomationReply(plan, true, new JenniferIncomingAction { Name = "pickup" }).Should().BeNull();
    }

    [Test]
    public void BuildForceRequestPlan_ShouldUseWebSocketWithNormalizedDistinctActions()
    {
        JenniferForceRequestPlan plan = JenniferSessionCoordinator.BuildForceRequestPlan(
            true,
            " ONI ",
            [" inspect ", "INSPECT", "pickup"],
            " state ",
            " query ",
            " high ",
            true);

        plan.Mode.Should().Be(JenniferForceRequestMode.WebSocket);
        plan.ActionNames.Should().Equal("inspect", "pickup");
        plan.Priority.Should().Be("high");
        plan.WebSocketPayload.Should().NotBeNull();
    }

    [Test]
    public void BuildForceRequestPlan_ShouldUseCompatibilityTcpForSingleActionWithoutWebSocket()
    {
        JenniferForceRequestPlan plan = JenniferSessionCoordinator.BuildForceRequestPlan(false, "ONI", ["inspect"], "", "", "medium", true);

        plan.Mode.Should().Be(JenniferForceRequestMode.CompatibilityTcp);
        plan.CompatibilityMessage.Should().Be("inspect");
    }

    [Test]
    public void BuildForceRequestPlan_ShouldRejectMultipleActionsWithoutWebSocket()
    {
        JenniferForceRequestPlan plan = JenniferSessionCoordinator.BuildForceRequestPlan(false, "ONI", ["inspect", "pickup"], "", "", "medium", true);

        plan.Mode.Should().Be(JenniferForceRequestMode.None);
        plan.LogMessage.Should().Be("[Force] Connect to WebSocket before sending multiple actions at once.");
    }
}