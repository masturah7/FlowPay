using Microsoft.EntityFrameworkCore;

namespace FlowPay.BuildingBlocks;

/// <summary>
/// Generic EF Core repository base. Entity-specific repositories derive from
/// this and add their own query methods using the protected `Set` — a
/// service should never reach into a DbContext directly; add a repository
/// method instead, even for a one-off query.
/// </summary>
public abstract class EfRepository<TEntity, TKey>(DbContext dbContext) : IRepository<TEntity, TKey>
    where TEntity : class
{
    protected DbContext DbContext { get; } = dbContext;

    protected DbSet<TEntity> Set { get; } = dbContext.Set<TEntity>();

    public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken) =>
        await Set.FindAsync([id], cancellationToken);

    public virtual Task<List<TEntity>> ListAsync(CancellationToken cancellationToken) =>
        Set.ToListAsync(cancellationToken);

    public virtual void Add(TEntity entity) => Set.Add(entity);

    public virtual void Remove(TEntity entity) => Set.Remove(entity);

    public Task ReloadAsync(TEntity entity, CancellationToken cancellationToken) =>
        DbContext.Entry(entity).ReloadAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        DbContext.SaveChangesAsync(cancellationToken);
}
