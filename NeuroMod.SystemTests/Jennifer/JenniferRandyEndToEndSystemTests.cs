using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NeuroMod.SystemTests.Support;
using NUnit.Framework;

namespace NeuroMod.SystemTests.Jennifer;

/// <summary>
/// End-to-end system tests that run Jennifer and Randy together and drive a complete action roundtrip.
/// </summary>
/// <invariant>Tests in this fixture are not parallelized because they need exclusive access to ports 8000, 1337, and 8081.</invariant>
[NonParallelizable]
public class JenniferRandyEndToEndSystemTests
{
    /// <summary>
    /// Starts Randy and Jennifer together, waits for Jennifer to register actions with Randy,
    /// forces the registered test action via Randy's HTTP API and verifies Jennifer auto-replies successfully.
    /// </summary>
    /// <post>Randy's HTTP response reports success and carries the action name returned by Jennifer's auto-reply.</post>
    [Test]
    [Category("Integration")]
    [Category("Jennifer")]
    [Category("Randy")]
    [Timeout(120000)]
    public async Task JenniferAndRandy_ShouldCompleteEndToEndActionRoundtrip()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));

        if (await RandyProcessScope.IsPortOpenAsync(8000, timeout.Token) ||
            await RandyProcessScope.IsPortOpenAsync(1337, timeout.Token))
        {
            Assert.Ignore("Ports 8000 or 1337 are already in use � E2E test cannot safely start Randy.");
        }

        if (await RandyProcessScope.IsPortOpenAsync(8081, timeout.Token))
        {
            Assert.Ignore("Port 8081 is already in use � E2E test cannot safely start Jennifer.");
        }

        string workspaceRoot = RandyProcessScope.FindWorkspaceRoot();
        string e2ePlanPath = Path.Combine(workspaceRoot, "tests", "jennifer", "e2e-roundtrip-plan.json");
        if (!File.Exists(e2ePlanPath))
        {
            Assert.Fail($"E2E test plan not found at '{e2ePlanPath}'.");
        }

        using HttpClient httpClient = new() { BaseAddress = new Uri("http://127.0.0.1:1337/") };

        await using RandyProcessScope randy = await RandyProcessScope.StartAsync(timeout.Token);
        await using JenniferProcessScope jennifer = await JenniferProcessScope.StartAsync(timeout.Token, $"--plan=\"{e2ePlanPath}\"");

        // Wait for Jennifer's ready marker (Jennifer TCP listener is up)
        await JenniferProcessSystemTests.WaitForFileAsync(jennifer.ReadyMarkerPath, timeout.Token);

        // Poll Randy's GET /actions until Jennifer has connected and registered e2e_ping.
        // This is more reliable than a fixed delay because it reflects actual Randy state.
        await WaitForActionRegisteredAsync(httpClient, "e2e_ping", timeout.Token);

        // Force the action via Randy's HTTP API � Randy forwards it to Jennifer's WebSocket.
        // Jennifer's auto-reply will respond immediately because autoRespond=true in the plan.
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(string.Empty, new
        {
            command = "action",
            data = new
            {
                id = "e2e-001",
                name = "e2e_ping",
            },
        }, timeout.Token);

        response.EnsureSuccessStatusCode();

        using JsonDocument responseBody = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        responseBody.RootElement.GetProperty("success").GetBoolean().Should().BeTrue(
            "Jennifer should auto-reply to e2e_ping with resultSuccess=true per the E2E roundtrip plan.");
        responseBody.RootElement.GetProperty("action").GetString().Should().Be("e2e_ping");

        // Graceful shutdown: close Jennifer and confirm the ready marker is removed
        await jennifer.CloseAsync(timeout.Token);
        await JenniferProcessSystemTests.WaitForFileDeletionAsync(jennifer.ReadyMarkerPath, timeout.Token);
    }

    /// <summary>
    /// Polls Randy's GET /actions endpoint until the specified action name is registered,
    /// which confirms Jennifer has connected to Randy and sent its actions/register payload.
    /// </summary>
    /// <param name="httpClient">HTTP client targeting Randy's HTTP API (port 1337).</param>
    /// <param name="expectedActionName">The action name Jennifer should have registered.</param>
    /// <param name="cancellationToken">Token used to abort the wait.</param>
    /// <pre>Randy's HTTP server is running on port 1337 and exposes GET /actions.</pre>
    /// <post>Randy's action registry contains the expected action name.</post>
    private static async Task WaitForActionRegisteredAsync(
        HttpClient httpClient,
        string expectedActionName,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using HttpResponseMessage resp = await httpClient.GetAsync("actions", cancellationToken);
                if (resp.IsSuccessStatusCode)
                {
                    string body = await resp.Content.ReadAsStringAsync(cancellationToken);
                    using JsonDocument doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("actions", out JsonElement actionsEl) &&
                        actionsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement nameEl in actionsEl.EnumerateArray())
                        {
                            if (nameEl.ValueKind == JsonValueKind.String &&
                                string.Equals(nameEl.GetString(), expectedActionName, StringComparison.OrdinalIgnoreCase))
                            {
                                TestContext.Progress.WriteLine($"[E2E] Randy has registered action '{expectedActionName}'. Jennifer is connected.");
                                return;
                            }
                        }
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Randy HTTP may not be ready yet; retry
            }
            catch (JsonException)
            {
                // Malformed response; retry
            }

            await Task.Delay(300, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for Randy to report '{expectedActionName}' in registered actions.");
    }
}
