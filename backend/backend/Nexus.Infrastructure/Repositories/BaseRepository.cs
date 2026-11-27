using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Nexus.Application.Data;

namespace Nexus.Infrastructure.Repositories;

public class BaseRepository<TEntity>(NexusDbContext ctx) : IRepository<TEntity> where TEntity : class
{
    protected readonly NexusDbContext Ctx = ctx;
    protected readonly DbSet<TEntity> Set = ctx.Set<TEntity>();

    public virtual async Task<TEntity?> GetByIdAsync(long id) => await Set.FindAsync(id);

    public virtual Task<List<TEntity>> GetAllAsync() => Set.AsNoTracking().ToListAsync();

    public virtual Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate) =>
        Set.AsNoTracking().Where(predicate).ToListAsync();

    public virtual Task<TEntity?> GetSingleAsync(Expression<Func<TEntity, bool>> predicate) =>
        Set.AsNoTracking().FirstOrDefaultAsync(predicate);

    public virtual Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate) => Set.AnyAsync(predicate);

    public virtual Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null) =>
        predicate is null ? Set.CountAsync() : Set.CountAsync(predicate);

    public virtual async Task AddAsync(TEntity entity) => await Set.AddAsync(entity);

    public virtual void Update(TEntity entity) => Set.Update(entity);

    public virtual void Remove(TEntity entity) => Set.Remove(entity);
}
