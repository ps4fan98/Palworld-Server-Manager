using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PalworldServerManager.Core.Models;
using PalworldServerManager.Core.Options;
using PalworldServerManager.Core.Services;

namespace PalworldServerManager.Infrastructure.Services;

internal sealed class WindowsPalworldProcessService(
    IOptionsMonitor<PalworldServerOptions> optionsMonitor,
    ILogger<WindowsPalworldProcessService> logger)
    : IPalworldProcessService, IDisposable
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly ConcurrentQueue<ServerLogLine> _logBuffer = new();
    private Process? _managedProcess;
    private bool _disposed;

    public Task<ServerRuntimeStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = optionsMonitor.CurrentValue;
        var executablePath = GetExecutablePath(options);

        using var process = TryGetRunningProcess(options);

        if (process is null)
        {
            return Task.FromResult(new ServerRuntimeStatus(
                IsRunning: false,
                ExecutableExists: File.Exists(executablePath),
                ProcessId: null,
                StartedAtUtc: null,
                Uptime: null,
                InstallDirectory: options.InstallDirectory,
                ExecutablePath: executablePath,
                ObservedAtUtc: DateTimeOffset.UtcNow));
        }

        DateTimeOffset? startedAtUtc = null;
        TimeSpan? uptime = null;

        try
        {
            startedAtUtc = process.StartTime.ToUniversalTime();
            uptime = DateTimeOffset.UtcNow - startedAtUtc;
        }
        catch (InvalidOperationException)
        {
            // The process exited between discovery and inspection.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The service account may not be permitted to inspect StartTime.
        }

        return Task.FromResult(new ServerRuntimeStatus(
            IsRunning: SafeIsRunning(process),
            ExecutableExists: File.Exists(executablePath),
            ProcessId: process.Id,
            StartedAtUtc: startedAtUtc,
            Uptime: uptime,
            InstallDirectory: options.InstallDirectory,
            ExecutablePath: executablePath,
            ObservedAtUtc: DateTimeOffset.UtcNow));
    }

    public Task<IReadOnlyList<ServerLogLine>> GetRecentLogsAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var boundedTake = Math.Clamp(take, 1, 2_000);
        IReadOnlyList<ServerLogLine> result =
            _logBuffer.Reverse().Take(boundedTake).Reverse().ToArray();

        return Task.FromResult(result);
    }

    public async Task<OperationResult> StartAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();

            var options = optionsMonitor.CurrentValue;
            using var existingProcess = TryGetRunningProcess(options);

            if (existingProcess is not null && !existingProcess.HasExited)
            {
                return OperationResult.Fail(
                    $"Palworld is already running with PID {existingProcess.Id}.");
            }

            var executablePath = GetExecutablePath(options);

            if (!File.Exists(executablePath))
            {
                return OperationResult.Fail(
                    $"PalServer.exe was not found at '{executablePath}'.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = options.InstallDirectory,
                Arguments = options.StartupArguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, eventArgs) =>
                BufferLogLine("stdout", eventArgs.Data);

            process.ErrorDataReceived += (_, eventArgs) =>
                BufferLogLine("stderr", eventArgs.Data);

            process.Exited += (_, _) =>
            {
                BufferLogLine(
                    "manager",
                    $"Managed launcher exited with code {SafeExitCode(process)}.");

                logger.LogWarning(
                    "Managed Palworld launcher exited with code {ExitCode}.",
                    SafeExitCode(process));
            };

            if (!process.Start())
            {
                process.Dispose();
                return OperationResult.Fail("Windows did not start PalServer.exe.");
            }

            _managedProcess?.Dispose();
            _managedProcess = process;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            BufferLogLine(
                "manager",
                $"Started PalServer.exe with launcher PID {process.Id}.");

            logger.LogInformation(
                "Started Palworld using {ExecutablePath}; launcher PID {ProcessId}.",
                executablePath,
                process.Id);

            return OperationResult.Ok(
                $"Palworld launch requested successfully. Launcher PID: {process.Id}.");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            UnauthorizedAccessException)
        {
            logger.LogError(exception, "Failed to start Palworld.");
            BufferLogLine("manager", $"Start failed: {exception.Message}");
            return OperationResult.Fail($"Start failed: {exception.Message}");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<OperationResult> ForceStopAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();

            var options = optionsMonitor.CurrentValue;
            var processes = GetRunningProcesses(options);

            if (processes.Count == 0)
            {
                return OperationResult.Fail("No Palworld process is currently running.");
            }

            var stoppedProcessIds = new List<int>();
            var failures = new List<string>();

            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        var processId = process.Id;
                        process.Kill(entireProcessTree: true);

                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(15));

                        await process.WaitForExitAsync(timeout.Token);
                        stoppedProcessIds.Add(processId);
                    }
                    catch (Exception exception) when (
                        exception is InvalidOperationException or
                        System.ComponentModel.Win32Exception or
                        UnauthorizedAccessException or
                        OperationCanceledException)
                    {
                        failures.Add($"PID {SafeProcessId(process)}: {exception.Message}");
                    }
                }
            }

            _managedProcess?.Dispose();
            _managedProcess = null;

            if (failures.Count > 0)
            {
                var detail = string.Join("; ", failures);
                logger.LogError("One or more Palworld processes failed to stop: {Detail}", detail);
                BufferLogLine("manager", $"Force-stop incomplete: {detail}");

                return OperationResult.Fail(
                    $"Stopped {stoppedProcessIds.Count} process(es), but failures occurred: {detail}");
            }

            var processList = string.Join(", ", stoppedProcessIds);
            BufferLogLine("manager", $"Force-stopped Palworld PID(s): {processList}.");

            logger.LogWarning(
                "Force-stopped Palworld process IDs: {ProcessIds}.",
                processList);

            return OperationResult.Ok(
                $"Force-stopped Palworld process ID(s): {processList}.");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private static string GetExecutablePath(PalworldServerOptions options) =>
        Path.GetFullPath(
            Path.Combine(options.InstallDirectory, options.ExecutableRelativePath));

    private static Process? TryGetRunningProcess(PalworldServerOptions options)
    {
        var processes = GetRunningProcesses(options);
        var selected = processes.FirstOrDefault();

        foreach (var process in processes.Skip(1))
        {
            process.Dispose();
        }

        return selected;
    }

    private static IReadOnlyList<Process> GetRunningProcesses(
        PalworldServerOptions options)
    {
        var processesById = new Dictionary<int, Process>();

        foreach (var processName in options.ProcessNames.Where(
                     name => !string.IsNullOrWhiteSpace(name)))
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                if (!processesById.TryAdd(process.Id, process))
                {
                    process.Dispose();
                }
            }
        }

        return processesById.Values.ToArray();
    }

    private void BufferLogLine(string stream, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _logBuffer.Enqueue(
            new ServerLogLine(DateTimeOffset.UtcNow, stream, message));

        var maximumLines = Math.Clamp(
            optionsMonitor.CurrentValue.MaximumBufferedLogLines,
            100,
            20_000);

        while (_logBuffer.Count > maximumLines &&
               _logBuffer.TryDequeue(out _))
        {
        }
    }

    private static bool SafeIsRunning(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static int SafeExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static int SafeProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return -1;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _managedProcess?.Dispose();
        _operationGate.Dispose();
    }
}
