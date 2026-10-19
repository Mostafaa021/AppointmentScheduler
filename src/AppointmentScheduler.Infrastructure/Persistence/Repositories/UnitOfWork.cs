using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Doctors;
using AppointmentScheduler.Domain.Patients;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

/// <summary>
/// The request-scoped <see cref="AppDbContext"/> is the unit of work. Repositories are created here, on first use, over that
/// same context (they are not registered in DI), so whatever they stage is committed by <see cref="SaveChangesAsync"/>, which
/// translates exclusion and concurrency violations into domain conflicts (ADR-0002, ADR-0004, ADR-0016).
/// </summary>
internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public IAppointmentRepository Appointments => field ??= new AppointmentRepository(db);

    public IDoctorRepository Doctors => field ??= new DoctorRepository(db);

    public IPatientRepository Patients => field ??= new PatientRepository(db);

    public IAuditRecordRepository AuditRecords => field ??= new AuditRecordRepository(db);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await work();
        await transaction.CommitAsync(cancellationToken); // an exception above disposes the transaction → rollback
    }
}
