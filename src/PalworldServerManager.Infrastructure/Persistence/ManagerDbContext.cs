using Microsoft.EntityFrameworkCore;

namespace PalworldServerManager.Infrastructure.Persistence;

public sealed class ManagerDbContext(DbContextOptions<ManagerDbContext> options)
    : DbContext(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var audit = modelBuilder.Entity<AuditRecord>();

        audit.HasKey(record => record.Id);
        audit.Property(record => record.Actor).HasMaxLength(200).IsRequired();
        audit.Property(record => record.Action).HasMaxLength(200).IsRequired();
        audit.Property(record => record.Outcome).HasMaxLength(100).IsRequired();
        audit.Property(record => record.Detail).HasMaxLength(4_000);
        audit.HasIndex(record => record.OccurredAtUtc);
    }
}
