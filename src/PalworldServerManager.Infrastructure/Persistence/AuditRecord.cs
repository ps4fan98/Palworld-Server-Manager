namespace PalworldServerManager.Infrastructure.Persistence;

public sealed class AuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string Actor { get; set; } = "local-system";

    public string Action { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string? Detail { get; set; }
}
