namespace AppointmentScheduler.Domain.Common;

/// <summary>
/// What every repository offers (ADR-0016, plan 013); each aggregate's contract extends it with named queries. Changes are
/// staged; <see cref="IUnitOfWork"/> commits them. Deliberately no Remove (the audit trail is append-only) and no
/// list-everything (appointments and audit rows are unbounded), and never <c>IQueryable</c>.
/// </summary>
public interface IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull
{
    /// <summary>Stages a new entity.</summary>
    void Add(TEntity entity);

    /// <summary>Tracked (for changes); null when missing.</summary>
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct);

    /// <summary>Read-only; null when missing.</summary>
    Task<TEntity?> GetByIdReadOnlyAsync(TKey id, CancellationToken ct);

    Task<bool> ExistsAsync(TKey id, CancellationToken ct);
}
