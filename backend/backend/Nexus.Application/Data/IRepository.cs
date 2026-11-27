using System.Linq.Expressions;

namespace Nexus.Application.Data;

public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(long id);
    Task<List<TEntity>> GetAllAsync();
    Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);
    Task<TEntity?> GetSingleAsync(Expression<Func<TEntity, bool>> predicate);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate);
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null);
    Task AddAsync(TEntity entity);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}
