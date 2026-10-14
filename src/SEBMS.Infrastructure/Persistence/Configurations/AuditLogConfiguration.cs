using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEBMS.Domain.Entities;

namespace SEBMS.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");

        // No foreign keys on purpose: the log must outlive the users, transactions and categories it describes.
        b.Property(x => x.AdminUserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.AdminEmail).IsRequired().HasMaxLength(256);
        b.Property(x => x.Action).IsRequired().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.EntityType).IsRequired().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.EntityId).IsRequired().HasMaxLength(450);
        b.Property(x => x.TargetUserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.TargetEmail).IsRequired().HasMaxLength(256);
        b.Property(x => x.Summary).IsRequired().HasMaxLength(300);
        b.Property(x => x.Details).HasMaxLength(2000);
        b.Property(x => x.OccurredAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(x => x.OccurredAtUtc).IsDescending().HasDatabaseName("IX_AuditLogs_OccurredAt");
        b.HasIndex(x => new { x.TargetUserId, x.OccurredAtUtc }).HasDatabaseName("IX_AuditLogs_Target");
    }
}
