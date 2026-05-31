using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NeuroMod.SystemTests.Support;
using NUnit.Framework;

namespace NeuroMod.SystemTests.Jennifer;

/// <summary>
/// Live Jennifer process tests that verify the testing tool starts and exposes its local compatibility seams.
/// </summary>
[NonParallelizable]
public class JenniferProcessSystemTests
{
    [Test]
    [Category("Integration")]
    [Category("Jennifer")]
    [Timeout(120000)]
    public async Task JenniferProcess_ShouldCreateReadyMarker_AndRespondOnCompatibilityTcp()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));

        if (await RandyProcessScope.IsPortOpenAsync(8081, timeout.Token))
        {
            Assert.Ignore("Port 8081 is already in use, so the Jennifer process system test cannot safely start its own instance.");
        }

        await using JenniferProcessScope scope = await JenniferProcessScope.StartAsync(timeout.Token);

        await WaitForFileAsync(scope.ReadyMarkerPath, timeout.Token);
        File.Exists(scope.ReadyMarkerPath).Should().BeTrue();

        string response = await SendCompatibilityMessageAsync("hello from system test", timeout.Token);
        response.Should().Contain("Jennifer: hello from system test");

        await scope.CloseAsync(timeout.Token);
        await WaitForFileDeletionAsync(scope.ReadyMarkerPath, timeout.Token);
    }

    private static async Task<string> SendCompatibilityMessageAsync(string message, CancellationToken cancellationToken)
    {
        using TcpClient client = new();
        await client.ConnectAsync("127.0.0.1", 8081, cancellationToken);
        using NetworkStream stream = client.GetStream();

        byte[] payload = Encoding.UTF8.GetBytes(message);
        await stream.WriteAsync(payload.AsMemory(0, payload.Length), cancellationToken);

        byte[] responseBuffer = new byte[1024];
        int read = await stream.ReadAsync(responseBuffer.AsMemory(0, responseBuffer.Length), cancellationToken);
        return Encoding.UTF8.GetString(responseBuffer, 0, read).Trim();
    }

    internal static async Task WaitForFileAsync(string path, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for file '{path}'.");
    }

    internal static async Task WaitForFileDeletionAsync(string path, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
            {
                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for file '{path}' to be deleted.");
    }
}
