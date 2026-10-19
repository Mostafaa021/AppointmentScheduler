using System.Linq.Expressions;
using AppointmentScheduler.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence.Repositories;

/// <summary>
/// The common half of every repository (ADR-0016, plan 013): add, get by id (tracked or read-only) and exists, over the
/// unit of work's <see cref="AppDbContext"/>. Each repository inherits it and adds its named queries; override
/// <see cref="WithDetails"/> when a get-by-id must load navigations.
/// </summary>
internal abstract class Repository<TEntity, TKey>(AppDbContext db) : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TKey : notnull
{
    protected AppDbContext Db { get; } = db;

    protected DbSet<TEntity> Set => Db.Set<TEntity>();

    public void Add(TEntity entity) => Set.Add(entity);

    public Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct) =>
        WithDetails(Set).FirstOrDefaultAsync(ById(id), ct);

    public Task<TEntity?> GetByIdReadOnlyAsync(TKey id, CancellationToken ct) =>
        WithDetails(Set.AsNoTracking()).FirstOrDefaultAsync(ById(id), ct);

    public Task<bool> ExistsAsync(TKey id, CancellationToken ct) => Set.AnyAsync(ById(id), ct);

    /// <summary>What a get-by-id loads besides the entity itself (e.g. navigations to show).</summary>
    protected virtual IQueryable<TEntity> WithDetails(IQueryable<TEntity> query) => query;

    private static Expression<Func<TEntity, bool>> ById(TKey id) => e => e.Id.Equals(id);
}
