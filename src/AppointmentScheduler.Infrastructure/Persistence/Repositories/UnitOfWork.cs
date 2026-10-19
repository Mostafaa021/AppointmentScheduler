using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

/// <summary>
/// The request-scoped <see cref="AppDbContext"/> is the unit of work; its <c>SaveChangesAsync</c> translates exclusion and
/// concurrency violations into domain conflicts (ADR-0002, ADR-0004, ADR-0016).
/// </summary>
internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
