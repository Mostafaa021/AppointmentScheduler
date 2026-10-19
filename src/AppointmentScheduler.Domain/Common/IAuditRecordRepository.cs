namespace AppointmentScheduler.Domain.Common;

/// <summary>Audit trail (ADR-0011). Append-only: records are staged with <c>Add</c> and committed by <see cref="IUnitOfWork"/>.</summary>
public interface IAuditRecordRepository : IRepository<AuditRecord, Guid>;
