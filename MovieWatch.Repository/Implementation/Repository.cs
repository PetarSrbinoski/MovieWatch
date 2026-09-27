using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using MovieWatch.Domain.Common;
using MovieWatch.Repository.Interface;

namespace MovieWatch.Repository.Implementation;

public class Repository<TEntity>(ApplicationDbContext context) : IRepository<TEntity> where TEntity : BaseEntity
{
    public Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => context.Set<TEntity>().AsNoTracking().SingleOrDefaultAsync(predicate, cancellationToken);

    public Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => context.Set<TEntity>().SingleOrDefaultAsync(predicate, cancellationToken);

    public Task<List<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
        => (predicate is null ? context.Set<TEntity>() : context.Set<TEntity>().Where(predicate))
            .AsNoTracking().ToListAsync(cancellationToken);

    public Task<List<TEntity>> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
        => context.Set<TEntity>().AsNoTracking().OrderBy(entity => entity.Id).Skip(skip).Take(take)
            .ToListAsync(cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => context.Set<TEntity>().AnyAsync(predicate, cancellationToken);

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        context.Set<TEntity>().Add(entity);
        await SaveAsync(cancellationToken);
        return entity;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException
            { SqliteExtendedErrorCode: 2067 or 1555 or 787 or 275 or 1299 })
        {
            throw new OperationException(FailureKind.Conflict, "The record conflicts with an existing record or dependency.");
        }
    }

    public async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        context.Set<TEntity>().Remove(entity);
        await SaveAsync(cancellationToken);
    }
}
