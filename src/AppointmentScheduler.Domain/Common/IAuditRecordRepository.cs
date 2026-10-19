namespace AppointmentScheduler.Domain.Common;

/// <summary>Audit trail (ADR-0011). Append-only: records are staged here and committed by <see cref="IUnitOfWork"/>.</summary>
public interface IAuditRecordRepository
{
    void Add(AuditRecord record);
}
