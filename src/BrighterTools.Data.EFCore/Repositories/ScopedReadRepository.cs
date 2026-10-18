using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BrighterTools.Data.EFCore.Repositories;

/// <summary>Mandatory host-owned authorization scope, applied before every repository query.</summary>
public interface IReadScope<TEntity> where TEntity : class
{
    /// <summary>Restricts the query to entities visible to the caller; absence of identity must fail closed.</summary>
    IQueryable<TEntity> Apply(IQueryable<TEntity> query);
}

/// <summary>Read persistence for arbitrary scalar/composite keys. Commands and commits belong to explicit workflows.</summary>
public abstract class ScopedReadRepository<TEntity, TKey>(DbContext db, IReadScope<TEntity> scope)
    where TEntity : class where TKey : notnull
{
    /// <summary>Shared unit of work for explicit custom command operations; never expose it from public repository contracts.</summary>
    protected DbContext Context => db;
    /// <summary>Untracked query with the mandatory host scope applied.</summary>
    protected IQueryable<TEntity> Query => scope.Apply(db.Set<TEntity>().AsNoTracking());
    /// <summary>Matches every component of the explicit entity key.</summary>
    protected abstract Expression<Func<TEntity, bool>> MatchKey(TKey key);
    /// <summary>Reads one authorized entity without consulting tracked entities or committing changes.</summary>
    public Task<TEntity?> GetByKeyAsync(TKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        // Do not use Find: the change tracker can contain entities outside the current scope.
        return Query.SingleOrDefaultAsync(MatchKey(key), cancellationToken);
    }
}


