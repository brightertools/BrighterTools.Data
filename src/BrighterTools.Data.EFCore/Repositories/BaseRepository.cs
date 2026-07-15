using BrighterTools.Data.Abstractions.Entities;
using BrighterTools.Data.Abstractions.Requests;
using BrighterTools.Data.EFCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace BrighterTools.Data.EFCore.Repositories;

public interface IBaseRepository<TEntity, TKey, TUserId>
    where TEntity : class, IEntity<TKey>
    where TKey : struct, IComparable
    where TUserId : struct
{
    Task<int> SaveAsync(TEntity entity, TUserId? userId = null, CancellationToken cancellationToken = default);
    Task<bool> ChangeEntityOrderAsync(TKey id, bool moveUp, Dictionary<string, int>? foreignKeyFilters = null, Dictionary<string, string>? enumFilters = null, CancellationToken cancellationToken = default);
    Task<bool> ChangeEntityOrderAsync(TKey id, bool moveUp, ListRequest? request = null, CancellationToken cancellationToken = default);
    Task<bool> MoveEntityToIndexAsync(TKey id, int newIndex, Dictionary<string, int>? foreignKeyFilters = null, Dictionary<string, string>? enumFilters = null, CancellationToken cancellationToken = default);
    Task<bool> MoveEntityToIndexAsync(TKey id, int newIndex, ListRequest? request = null, CancellationToken cancellationToken = default);
    Task<bool> ReorderAsync(Dictionary<string, int>? foreignKeyFilters = null, Dictionary<string, string>? enumFilters = null, CancellationToken cancellationToken = default);
    Task<bool> ReorderAsync(ListRequest? request = null, CancellationToken cancellationToken = default);
    Task<int> GetNextOrderAsync(Dictionary<string, int>? foreignKeyFilters = null, Dictionary<string, string>? enumFilters = null, CancellationToken cancellationToken = default);
    Task<int> GetNextOrderAsync(ListRequest? request = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAndReorderAsync(TKey id, Dictionary<string, int>? foreignKeyFilters = null, Dictionary<string, string>? enumFilters = null, bool hardDelete = false, CancellationToken cancellationToken = default);
    Task<bool> DeleteAndReorderAsync(TKey id, ListRequest? request = null, bool hardDelete = false, CancellationToken cancellationToken = default);
}

public abstract class BaseRepository<TEntity, TKey, TUserId> : IBaseRepository<TEntity, TKey, TUserId>
    where TEntity : class, IEntity<TKey>
    where TKey : struct, IComparable
    where TUserId : struct
{
    protected readonly DbContext DbContext;
    protected readonly ILogger Logger;
    protected readonly int TenantId;
    protected DbSet<TEntity> Set => DbContext.Set<TEntity>();

    private static readonly ConcurrentDictionary<string, PropertyInfo?> PropertyCache = new();

    protected BaseRepository(DbContext dbContext, ILogger logger, ITenantContextAccessor? tenantContextAccessor = null)
    {
        DbContext = dbContext;
        Logger = logger;
        TenantId = tenantContextAccessor?.CurrentTenantId ?? 0;
        OnInitialized();
    }

    public virtual async Task<int> SaveAsync(TEntity entity, TUserId? userId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await OnBeforeSave(entity);

        var entry = DbContext.Entry(entity);
        if (!entry.IsKeySet)
        {
            await Set.AddAsync(entity, cancellationToken);
        }
        else
        {
            if (entry.State == EntityState.Detached)
            {
                Set.Attach(entity);
            }

            entry.State = EntityState.Modified;
        }

        var affectedRows = await PersistEntityAsync(entity, userId, cancellationToken);
        await OnAfterSave(entity, affectedRows);
        return affectedRows;
    }

    public Task<bool> ChangeEntityOrderAsync(TKey id, bool moveUp, Dictionary<string, int>? foreignKeyFilters = null, Dictionary<string, string>? enumFilters = null, CancellationToken cancellationToken = default)
        => ChangeEntityOrderAsync(id, moveUp, CreateListRequest(foreignKeyFilters, enumFilters), cancellationToken);

    public async Task<bool> ChangeEntityOrderAsync(TKey id, bool moveUp, ListRequest? request = null, CancellationToken cancellationToken = default)
    {
        var items = await GetFilteredOrderedEntities(request, cancellationToken);
        if (items.Count == 0) return false;

        var currentIndex = items.FindIndex(x => EqualityComparer<TKey>.Default.Equals(x.Id, id));
        if (currentIndex < 0) return false;

        var targetIndex = moveUp ? currentIndex - 1 : currentIndex + 1;
        if (targetIndex < 0 || targetIndex >= items.Count) return false;

        SwapOrders(items, currentIndex, targetIndex);
        await DbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> MoveEntityToIndexAsync(TKey id, int newIndex, Dictionary<string, int>? foreignKeyFilters = null, Dictionary<string, string>? enumFilters = null, CancellationToken cancellationToken = default)
        => MoveEntityToIndexAsync(id, newIndex, CreateListRequest(foreignKeyFilters, enumFilters), cancellationToken);

    public async Task<bool> MoveEntityToIndexAsync(TKey id, int newIndex, ListRequest? request = null, CancellationToken cancellationToken = default)
    {
        var items = await GetFilteredOrderedEntities(request, cancellationToken);
        if (items.Count == 0) return false;

        var entity = items.FirstOrDefault(x => EqualityComparer<TKey>.Default.Equals(x.Id, id));
        if (entity is null) return false;

        items.Remove(entity);
        newIndex = Math.Clamp(newIndex, 0, items.Count);
        items.Insert(newIndex, entity);

        ReassignOrderValues(items);
        await DbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> ReorderAsync(Dictionary<string, int>? foreignKeyFilters = null, Dictionary<string, string>? enumFilters = null, CancellationToken cancellationToken = default)
        => ReorderAsync(CreateListRequest(foreignKeyFilters, enumFilters), cancellationToken);

    public async Task<bool> ReorderAsync(ListRequest? request = null, CancellationToken cancellationToken = default)
    {
        var items = await GetFilteredOrderedEntities(request, cancellationToken);
        if (items.Count == 0) return false;

        ReassignOrderValues(items);
        await DbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<int> GetNextOrderAsync(Dictionary<string, int>? foreignKeyFilters = null, Dictionary<string, string>? enumFilters = null, CancellationToken cancellationToken = default)
        => GetNextOrderAsync(CreateListRequest(foreignKeyFilters, enumFilters), cancellationToken);

    public async Task<int> GetNextOrderAsync(ListRequest? request = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> query = Set;
        if (request is not null)
        {
            query = ApplyStandardListFilters(query, request);
        }

        var maxOrder = await query.AsNoTracking().Select(x => (int?)EF.Property<int>(x, "Order")).MaxAsync(cancellationToken) ?? 0;
        return maxOrder + 1;
    }

    public Task<bool> DeleteAndReorderAsync(TKey id, Dictionary<string, int>? foreignKeyFilters = null, Dictionary<string, string>? enumFilters = null, bool hardDelete = false, CancellationToken cancellationToken = default)
        => DeleteAndReorderAsync(id, CreateListRequest(foreignKeyFilters, enumFilters), hardDelete, cancellationToken);

    public async Task<bool> DeleteAndReorderAsync(TKey id, ListRequest? request = null, bool hardDelete = false, CancellationToken cancellationToken = default)
    {
        var entity = await Set.FirstOrDefaultAsync(CreateIdEqualsPredicate(id), cancellationToken);
        if (entity is null) return false;

        var deletedProperty = GetCachedProperty(typeof(TEntity), "Deleted");
        if (!hardDelete && deletedProperty?.PropertyType == typeof(bool))
        {
            deletedProperty.SetValue(entity, true);
            DbContext.Update(entity);
        }
        else
        {
            DbContext.Remove(entity);
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return await ReorderAsync(request, cancellationToken);
    }

    protected IQueryable<TEntity> BuildQuery(bool tracking = false, params string[] includes)
    {
        IQueryable<TEntity> query = Set;
        if (includes?.Any() == true)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }

        return tracking ? query : query.AsNoTracking();
    }

    protected Task<TEntity?> GetByIdAsync(TKey id, bool tracking = false, params string[] includes)
        => BuildQuery(tracking, includes).FirstOrDefaultAsync(CreateIdEqualsPredicate(id));

    protected IQueryable<TEntity> ApplyStandardListFilters(IQueryable<TEntity> query, ListRequest request)
    {
        query = ApplyForeignKeyFilters(query, request.ForeignKeyFilters);
        query = ApplyEnumFilters(query, request.EnumFilters);
        return query;
    }

    protected virtual void OnInitialized() { }

    protected virtual async Task OnBeforeSave(TEntity entity)
    {
        EnforceTenant(entity);
        await OnBeforeSaveInternal(entity);
    }

    protected virtual Task OnBeforeSaveInternal(TEntity entity) => Task.CompletedTask;

    protected virtual Task OnAfterSave(TEntity entity, int affectedRows) => Task.CompletedTask;

    protected virtual Task<int> PersistEntityAsync(TEntity entity, TUserId? userId, CancellationToken cancellationToken)
        => DbContext.SaveChangesAsync(cancellationToken);

    protected virtual int? ToAuditUserId(TUserId? userId)
    {
        if (!userId.HasValue) return null;
        return Convert.ToInt32(userId.Value);
    }

    private void EnforceTenant(TEntity entity)
    {
        var tenantProperty = GetCachedProperty(typeof(TEntity), "TenantId");
        if (tenantProperty is null) return;

        var currentValue = (int?)tenantProperty.GetValue(entity);

        if (TenantId == 0)
        {
            if (!currentValue.HasValue || currentValue.Value == 0)
            {
                throw new InvalidOperationException($"Tenant context missing when saving {typeof(TEntity).Name}.");
            }
            return;
        }

        if (!currentValue.HasValue || currentValue.Value == 0)
        {
            tenantProperty.SetValue(entity, TenantId);
            return;
        }

        if (currentValue.Value != TenantId)
        {
            throw new InvalidOperationException($"Cross-tenant save detected for {typeof(TEntity).Name}.");
        }
    }

    private async Task<List<TEntity>> GetFilteredOrderedEntities(ListRequest? request, CancellationToken cancellationToken)
    {
        IQueryable<TEntity> query = Set;
        if (request is not null)
        {
            query = ApplyStandardListFilters(query, request);
        }

        query = query.OrderBy(x => EF.Property<int>(x, "Order"));
        return await query.ToListAsync(cancellationToken);
    }

    private static IQueryable<TEntity> ApplyForeignKeyFilters(IQueryable<TEntity> query, Dictionary<string, int>? foreignKeyFilters)
    {
        if (foreignKeyFilters is null) return query;

        foreach (var filter in foreignKeyFilters)
        {
            var propertyName = ResolvePropertyName(filter.Key);
            if (propertyName is null) continue;
            query = query.Where(x => EF.Property<int>(x, propertyName) == filter.Value);
        }

        return query;
    }

    private static IQueryable<TEntity> ApplyEnumFilters(IQueryable<TEntity> query, Dictionary<string, string>? enumFilters)
    {
        if (enumFilters is null) return query;

        foreach (var filter in enumFilters)
        {
            var propertyName = ResolvePropertyName(filter.Key);
            if (propertyName is null) continue;

            var propertyInfo = GetCachedProperty(typeof(TEntity), propertyName);
            if (propertyInfo is null) continue;

            var propertyType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
            if (!propertyType.IsEnum) continue;

            object? enumValue;
            try { enumValue = Enum.Parse(propertyType, filter.Value, true); }
            catch { continue; }

            query = query.Where(x => EF.Property<object>(x, propertyName).Equals(enumValue));
        }

        return query;
    }

    private static ListRequest CreateListRequest(Dictionary<string, int>? foreignKeyFilters, Dictionary<string, string>? enumFilters)
        => new() { ForeignKeyFilters = foreignKeyFilters, EnumFilters = enumFilters };

    private static string? ResolvePropertyName(string rawPropertyName)
    {
        if (string.IsNullOrWhiteSpace(rawPropertyName)) return null;
        return GetCachedProperty(typeof(TEntity), rawPropertyName)?.Name;
    }

    private static PropertyInfo? GetCachedProperty(Type type, string name)
    {
        var key = $"{type.FullName}:{name}";
        return PropertyCache.GetOrAdd(key, _ => type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase));
    }

    private static Expression<Func<TEntity, bool>> CreateIdEqualsPredicate(TKey id)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var left = Expression.Property(parameter, nameof(IEntity<TKey>.Id));
        var right = Expression.Constant(id, typeof(TKey));
        var body = Expression.Equal(left, right);
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static void SwapOrders(IReadOnlyList<TEntity> items, int firstIndex, int secondIndex)
    {
        var orderProperty = GetCachedProperty(typeof(TEntity), "Order");
        if (orderProperty is null) return;

        var firstValue = orderProperty.GetValue(items[firstIndex]);
        var secondValue = orderProperty.GetValue(items[secondIndex]);
        orderProperty.SetValue(items[firstIndex], secondValue);
        orderProperty.SetValue(items[secondIndex], firstValue);
    }

    private static void ReassignOrderValues(IReadOnlyList<TEntity> items)
    {
        var orderProperty = GetCachedProperty(typeof(TEntity), "Order");
        if (orderProperty is null) return;

        for (var index = 0; index < items.Count; index++)
        {
            orderProperty.SetValue(items[index], index + 1);
        }
    }
}
