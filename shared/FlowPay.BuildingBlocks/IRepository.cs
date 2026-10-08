namespace FlowPay.BuildingBlocks;

/// <summary>
/// Generic data-access contract every repository implements. Keep this
/// interface thin — it's plumbing, not behavior. Entity-specific lookups
/// (e.g. `GetByEmailAsync`) belong on a derived interface
/// (`IAccountRepository : IRepository&lt;Account, Guid&gt;`), not bolted on here.
/// Business rules (e.g. "what does a duplicate email mean") belong in the
/// service layer that consumes the repository, not in the repository itself.
/// </summary>
public interface IRepository<TEntity, in TKey>
    where TEntity : class
{
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken);

    Task<List<TEntity>> ListAsync(CancellationToken cancellationToken);

    void Add(TEntity entity);

    void Remove(TEntity entity);

    /// <summary>
    /// Refreshes a tracked entity's values from the data store, discarding
    /// any in-memory edits that were never persisted. Needed after a failed
    /// SaveChangesAsync (e.g. a unique-constraint conflict on an idempotent
    /// write) — without this, the entity's in-memory state stays the
    /// mutated-but-never-saved version, and because `GetByIdAsync` serves
    /// already-tracked instances from the change tracker rather than
    /// re-querying, calling it again would return that same stale state.
    /// </summary>
    Task ReloadAsync(TEntity entity, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
