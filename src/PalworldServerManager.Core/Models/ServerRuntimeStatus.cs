namespace PalworldServerManager.Core.Models;

public sealed record ServerRuntimeStatus(
    bool IsRunning,
    bool ExecutableExists,
    int? ProcessId,
    DateTimeOffset? StartedAtUtc,
    TimeSpan? Uptime,
    string InstallDirectory,
    string ExecutablePath,
    DateTimeOffset ObservedAtUtc);
