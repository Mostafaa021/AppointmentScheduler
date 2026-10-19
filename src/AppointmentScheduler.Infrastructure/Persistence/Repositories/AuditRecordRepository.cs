using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

internal sealed class AuditRecordRepository(AppDbContext db) : IAuditRecordRepository
{
    public void Add(AuditRecord record) => db.AuditRecords.Add(record);
}
