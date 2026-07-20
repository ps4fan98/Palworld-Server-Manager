using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PalworldServerManager.Infrastructure.Identity;

namespace PalworldServerManager.Infrastructure.Persistence;

public sealed class ManagerDbContext(DbContextOptions<ManagerDbContext> options)
    : IdentityDbContext<OwnerUser>(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var owner = builder.Entity<OwnerUser>();
        owner.Property(user => user.CreatedAtUtc).IsRequired();
        owner.Property(user => user.IsEnabled).IsRequired();
        owner.HasIndex(user => user.NormalizedUserName).IsUnique();

        var audit = builder.Entity<AuditRecord>();

        audit.HasKey(record => record.Id);
        audit.Property(record => record.Actor).HasMaxLength(200).IsRequired();
        audit.Property(record => record.Action).HasMaxLength(200).IsRequired();
        audit.Property(record => record.Outcome).HasMaxLength(100).IsRequired();
        audit.Property(record => record.Detail).HasMaxLength(4_000);
        audit.HasIndex(record => record.OccurredAtUtc);
    }
}
