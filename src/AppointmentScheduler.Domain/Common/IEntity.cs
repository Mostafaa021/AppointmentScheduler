namespace AppointmentScheduler.Domain.Common;

/// <summary>An entity with an identity, so <see cref="IRepository{TEntity, TKey}"/> can find it by key (ADR-0016).</summary>
public interface IEntity<TKey>
{
    TKey Id { get; }
}
