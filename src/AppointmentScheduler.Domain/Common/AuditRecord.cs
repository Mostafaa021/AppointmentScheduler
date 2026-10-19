namespace AppointmentScheduler.Domain.Common;

/// <summary>Append-only audit entry (ADR-0011). Holds ids only, never PII (NFR-9).</summary>
public sealed class AuditRecord
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; } = "";
    public string EntityType { get; private set; } = "";
    public string? EntityId { get; private set; }
    public DateTimeOffset TimestampUtc { get; private set; }
    public string? TraceId { get; private set; }

    private AuditRecord() { } // EF

    public AuditRecord(Guid? userId, string action, string entityType, string? entityId, DateTimeOffset timestampUtc, string? traceId)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        TimestampUtc = timestampUtc;
        TraceId = traceId;
    }
}
