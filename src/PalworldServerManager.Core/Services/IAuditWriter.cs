namespace PalworldServerManager.Core.Services;

public interface IAuditWriter
{
    Task WriteAsync(
        string action,
        string outcome,
        string? detail = null,
        string actor = "local-system",
        CancellationToken cancellationToken = default);
}
