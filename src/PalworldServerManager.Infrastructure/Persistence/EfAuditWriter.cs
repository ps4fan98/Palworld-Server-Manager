using Microsoft.EntityFrameworkCore;
using PalworldServerManager.Core.Services;

namespace PalworldServerManager.Infrastructure.Persistence;

internal sealed class EfAuditWriter(
    IDbContextFactory<ManagerDbContext> dbContextFactory) : IAuditWriter
{
    public async Task WriteAsync(
        string action,
        string outcome,
        string? detail = null,
        string actor = "local-system",
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.AuditRecords.Add(new AuditRecord
        {
            Actor = actor,
            Action = action,
            Outcome = outcome,
            Detail = detail
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
