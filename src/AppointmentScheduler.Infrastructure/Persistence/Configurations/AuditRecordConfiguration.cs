using AppointmentScheduler.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointmentScheduler.Infrastructure.Persistence.Configurations;

/// <summary>ADR-0011: insert-only, no FKs, no indexes beyond the PK.</summary>
internal sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.Action).HasMaxLength(64).IsRequired();
        b.Property(a => a.EntityType).HasMaxLength(64).IsRequired();
        b.Property(a => a.EntityId).HasMaxLength(64);
        b.Property(a => a.TraceId).HasMaxLength(64);
    }
}
