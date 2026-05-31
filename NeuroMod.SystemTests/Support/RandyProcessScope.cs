using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NeuroMod.SystemTests.Support;

/// <summary>
/// Manages the lifecycle of a Randy mock server process for system tests.
/// </summary>
/// <invariant>_process is non-null after construction.</invariant>
internal sealed class RandyProcessScope : IAsyncDisposable
{
    private readonly Process _process;

    private RandyProcessScope(Process process)
    {
        _process = process;
    }

    /// <summary>
    /// Starts Randy from the workspace Randy/ directory and waits for ports 8000 and 1337 to be ready.
    /// </summary>
    /// <param name="cancellationToken">Token used to abort the start sequence.</param>
    /// <returns>A scope that keeps the Randy process alive until disposed.</returns>
    /// <pre>Ports 8000 and 1337 are free.</pre>
    /// <post>Randy is listening on ws://127.0.0.1:8000 and http://127.0.0.1:1337.</post>
    public static async Task<RandyProcessScope> StartAsync(CancellationToken cancellationToken)
    {
        string workspaceRoot = FindWorkspaceRoot();
        string randyDirectory = Path.Combine(workspaceRoot, "Randy");
        if (!Directory.Exists(randyDirectory))
        {
            Assert.Ignore($"Randy directory not found at '{randyDirectory}'.");
        }

        if (await IsPortOpenAsync(8000, cancellationToken) || await IsPortOpenAsync(1337, cancellationToken))
        {
            Assert.Ignore("Ports 8000 or 1337 are already in use, so the live Randy system test cannot safely start its own server instance.");
        }

        await EnsureDependenciesInstalledAsync(randyDirectory, cancellationToken);

        Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetTsxExecutable(randyDirectory),
                Arguments = "index.ts",
                WorkingDirectory = randyDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        StringBuilder output = new();
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                output.AppendLine(args.Data);
                TestContext.Progress.WriteLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                output.AppendLine(args.Data);
                TestContext.Progress.WriteLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await WaitForPortAsync(8000, cancellationToken);
            await WaitForPortAsync(1337, cancellationToken);
            return new RandyProcessScope(process);
        }
        catch
        {
            TryKill(process);
            throw new AssertionException($"Randy failed to start successfully. Output:{Environment.NewLine}{output}");
        }
    }

    /// <summary>
    /// Disposes the Randy process scope, killing the process if still running.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        TryKill(_process);
        _process.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task EnsureDependenciesInstalledAsync(string randyDirectory, CancellationToken cancellationToken)
    {
        if (Directory.Exists(Path.Combine(randyDirectory, "node_modules")))
        {
            return;
        }

        using Process installProcess = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetNpmExecutable(),
                Arguments = "install --no-fund --no-audit",
                WorkingDirectory = randyDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        installProcess.Start();
        await installProcess.WaitForExitAsync(cancellationToken);
        if (installProcess.ExitCode != 0)
        {
            throw new AssertionException($"npm install failed for Randy with exit code {installProcess.ExitCode}.");
        }
    }

    internal static string FindWorkspaceRoot()
    {
        DirectoryInfo? current = new(TestContext.CurrentContext.TestDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Put Neuro Into a Dupe.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Workspace root containing 'Put Neuro Into a Dupe.sln' could not be found.");
    }

    private static string GetNpmExecutable()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "npm.cmd" : "npm";
    }

    private static string GetTsxExecutable(string randyDirectory)
    {
        string fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "tsx.cmd" : "tsx";
        string fullPath = Path.Combine(randyDirectory, "node_modules", ".bin", fileName);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Randy local tsx executable was not found at '{fullPath}'.");
        }

        return fullPath;
    }

    internal static async Task WaitForPortAsync(int port, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsPortOpenAsync(port, cancellationToken))
            {
                return;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for Randy to open port {port}.");
    }

    internal static async Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using TcpClient client = new();
            await client.ConnectAsync("127.0.0.1", port, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
        }
    }
}
