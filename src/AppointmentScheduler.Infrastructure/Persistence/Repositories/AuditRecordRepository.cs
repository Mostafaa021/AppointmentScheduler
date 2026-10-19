using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

// Append-only: everything it needs (Add) comes from the base (ADR-0011).
internal sealed class AuditRecordRepository(AppDbContext db) : Repository<AuditRecord, Guid>(db), IAuditRecordRepository;
