using PalworldServerManager.Core.Models;

namespace PalworldServerManager.Core.Services;

public interface IPalworldProcessService
{
    Task<ServerRuntimeStatus> GetStatusAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServerLogLine>> GetRecentLogsAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<OperationResult> StartAsync(
        CancellationToken cancellationToken = default);

    Task<OperationResult> ForceStopAsync(
        CancellationToken cancellationToken = default);
}
