using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jennifer.Wpf.Contracts;
using Jennifer.Wpf.Parsing;
using NeuroMod.SystemTests.Support;
using NUnit.Framework;

namespace NeuroMod.SystemTests.Randy;

/// <summary>
/// Live Randy integration tests that verify a full websocket and HTTP contract roundtrip.
/// </summary>
[NonParallelizable]
public class RandyLiveContractSystemTests
{
    [Test]
    [Category("Integration")]
    [Category("Randy")]
    [Timeout(120000)]
    /// <summary>
    /// Verifies that a Jennifer-compatible client can register actions with Randy and complete an action/result roundtrip.
    /// </summary>
    /// <post>A running Randy instance has accepted the websocket client, delivered an action request, and returned the correlated HTTP success response.</post>
    public async Task JenniferCompatibleClient_ShouldCompleteRandyRoundtrip()
    {
        using ClientWebSocket socket = new();
        using HttpClient httpClient = new() { BaseAddress = new Uri("http://127.0.0.1:1337/") };
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await using RandyProcessScope scope = await RandyProcessScope.StartAsync(timeout.Token);

        await socket.ConnectAsync(new Uri("ws://127.0.0.1:8000"), timeout.Token);

        string initialMessage = await ReceiveMessageAsync(socket, timeout.Token);
        JenniferWsMessage initialParsed = JenniferWsMessageParser.Parse(initialMessage);
        initialParsed.Kind.Should().Be(JenniferWsMessageKind.ReRegisterAll);

        await SendAsync(socket, JenniferRandyContractPayloadFactory.CreateStartupPayload("ONI"), timeout.Token);
        await SendAsync(socket, JenniferRandyContractPayloadFactory.CreateActionsRegisterPayload(
            "ONI",
            [new JenniferDiscoveredAction { Name = "inspect", Description = "Inspect target", HasSchema = true }]), timeout.Token);

        Task<HttpResponseMessage> actionRequest = httpClient.PostAsJsonAsync(string.Empty, new
        {
            command = "action",
            data = new
            {
                id = "integration-1",
                name = "inspect",
            },
        }, timeout.Token);

        string actionMessage = await ReceiveMessageAsync(socket, timeout.Token);
        JenniferWsMessage actionParsed = JenniferWsMessageParser.Parse(actionMessage);
        actionParsed.Kind.Should().Be(JenniferWsMessageKind.Action);
        actionParsed.ActionId.Should().Be("integration-1");
        actionParsed.ActionName.Should().Be("inspect");

        await SendAsync(socket, JenniferRandyContractPayloadFactory.CreateActionResultPayload("integration-1", true, "Completed by system test"), timeout.Token);

        using HttpResponseMessage response = await actionRequest;
        response.EnsureSuccessStatusCode();

        using JsonDocument responseBody = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        responseBody.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        responseBody.RootElement.GetProperty("action").GetString().Should().Be("inspect");
        responseBody.RootElement.GetProperty("message").GetString().Should().Be("Action executed successfully");
    }

    private static async Task SendAsync(ClientWebSocket socket, string payload, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task<string> ReceiveMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        StringBuilder builder = new();

        while (true)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("Randy closed the websocket before the expected message arrived.");
            }

            builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (result.EndOfMessage)
            {
                return builder.ToString();
            }
        }
    }
}
