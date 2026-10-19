using AppointmentScheduler.Infrastructure.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointmentScheduler.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public const string Table = "idempotency_records";

    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable(Table);
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Key).HasMaxLength(100).IsRequired();
        b.Property(r => r.Method).HasMaxLength(10).IsRequired();
        b.Property(r => r.Path).HasMaxLength(2048).IsRequired();
        b.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.ResponseHeaders).HasColumnType("jsonb");

        // The lock (ADR-0006): a second INSERT for the same user + key does nothing, so exactly one request wins.
        b.HasIndex(r => new { r.UserId, r.Key }).IsUnique();
        b.HasIndex(r => r.ExpiresAtUtc); // purge job
    }
}
