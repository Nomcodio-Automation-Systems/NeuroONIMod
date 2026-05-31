using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NeuroMod.SystemTests.Support;

/// <summary>
/// Manages the lifecycle of a Jennifer.Wpf process for system tests.
/// </summary>
/// <invariant>_process is non-null after construction.</invariant>
internal sealed class JenniferProcessScope : IAsyncDisposable
{
    private readonly Process _process;

    private JenniferProcessScope(Process process, string executablePath)
    {
        _process = process;
        ReadyMarkerPath = Path.Combine(Path.GetDirectoryName(executablePath)!, "jennifer_ready.txt");
    }

    /// <summary>
    /// Path to the ready-marker file that Jennifer writes when it has fully started.
    /// </summary>
    public string ReadyMarkerPath { get; }

    /// <summary>
    /// Starts Jennifer.Wpf.exe and returns a scope that keeps the process alive until disposed.
    /// </summary>
    /// <param name="cancellationToken">Token used to abort the start sequence.</param>
    /// <param name="extraArgs">Optional additional command-line arguments passed to Jennifer.</param>
    /// <returns>A scope wrapping the live Jennifer process.</returns>
    /// <pre>Jennifer.Wpf.exe exists in its expected bin/Debug path.</pre>
    /// <post>The Jennifer process is running. Callers must wait for ReadyMarkerPath to appear before interacting.</post>
    public static Task<JenniferProcessScope> StartAsync(CancellationToken cancellationToken, string? extraArgs = null)
    {
        string executablePath = GetJenniferExecutablePath();
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"Jennifer executable was not found at '{executablePath}'.");
        }

        string? workingDirectory = Path.GetDirectoryName(executablePath);
        if (workingDirectory is null)
        {
            throw new DirectoryNotFoundException($"The Jennifer executable directory could not be resolved from '{executablePath}'.");
        }

        string readyMarkerPath = Path.Combine(workingDirectory, "jennifer_ready.txt");
        if (File.Exists(readyMarkerPath))
        {
            File.Delete(readyMarkerPath);
        }

        Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = extraArgs ?? string.Empty,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
            },
        };

        process.Start();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new JenniferProcessScope(process, executablePath));
    }

    /// <summary>
    /// Gracefully closes the Jennifer window and waits for the process to exit.
    /// </summary>
    /// <param name="cancellationToken">Token used to abort the close wait.</param>
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        if (_process.HasExited)
        {
            return;
        }

        try
        {
            _process.CloseMainWindow();
            await _process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            RandyProcessScope.TryKill(_process);
        }
    }

    /// <summary>
    /// Disposes the Jennifer process scope, killing the process if still running.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        RandyProcessScope.TryKill(_process);
        _process.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string GetJenniferExecutablePath()
    {
        string workspaceRoot = RandyProcessScope.FindWorkspaceRoot();
        return Path.Combine(workspaceRoot, "Jennifer.Wpf", "bin", "Debug", "net10.0-windows", "Jennifer.Wpf.exe");
    }
}
