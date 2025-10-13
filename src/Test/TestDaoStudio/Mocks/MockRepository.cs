using DaoStudio.DBStorage.Interfaces;
using System.Linq.Expressions;

namespace TestDaoStudio.Mocks;

/// <summary>
/// Generic mock repository implementation for testing data layer operations.
/// Provides in-memory storage and tracking of repository operations.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class MockRepository<T> where T : class
{
    private readonly List<T> _entities = new();
    private readonly List<string> _operationLog = new();
    private long _nextId = 1;

    public IReadOnlyList<T> Entities => _entities.AsReadOnly();
    public IReadOnlyList<string> OperationLog => _operationLog.AsReadOnly();

    public Task<T> AddAsync(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        LogOperation($"AddAsync: {entity.GetType().Name}");
        
        // Set ID if the entity has an Id property
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(long))
        {
            idProperty.SetValue(entity, _nextId++);
        }

        _entities.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<T> UpdateAsync(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        LogOperation($"UpdateAsync: {entity.GetType().Name}");
        
        // Find and replace existing entity by Id
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null)
        {
            var id = idProperty.GetValue(entity);
            var existingIndex = _entities.FindIndex(e => idProperty.GetValue(e)?.Equals(id) == true);
            if (existingIndex >= 0)
            {
                _entities[existingIndex] = entity;
            }
        }
        
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        LogOperation($"DeleteAsync: {entity.GetType().Name}");
        _entities.Remove(entity);
        return Task.CompletedTask;
    }

    public Task<T?> GetByIdAsync(long id)
    {
        LogOperation($"GetByIdAsync: {id}");
        
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null)
        {
            var entity = _entities.FirstOrDefault(e => idProperty.GetValue(e)?.Equals(id) == true);
            return Task.FromResult(entity);
        }
        
        return Task.FromResult<T?>(null);
    }

    public Task<IEnumerable<T>> GetAllAsync()
    {
        LogOperation("GetAllAsync");
        return Task.FromResult<IEnumerable<T>>(_entities.ToList());
    }

    public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        LogOperation($"FindAsync: {predicate}");
        var compiled = predicate.Compile();
        var results = _entities.Where(compiled);
        return Task.FromResult(results);
    }

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        LogOperation($"FirstOrDefaultAsync: {predicate}");
        var compiled = predicate.Compile();
        var result = _entities.FirstOrDefault(compiled);
        return Task.FromResult(result);
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        LogOperation($"AnyAsync: {predicate}");
        var compiled = predicate.Compile();
        var result = _entities.Any(compiled);
        return Task.FromResult(result);
    }

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        LogOperation($"CountAsync: {predicate}");
        
        if (predicate == null)
        {
            return Task.FromResult(_entities.Count);
        }
        
        var compiled = predicate.Compile();
        var result = _entities.Count(compiled);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Clears all entities and operation logs.
    /// </summary>
    public void Clear()
    {
        _entities.Clear();
        _operationLog.Clear();
        _nextId = 1;
    }

    /// <summary>
    /// Seeds the repository with initial data.
    /// </summary>
    public void Seed(params T[] entities)
    {
        _entities.AddRange(entities);
    }

    /// <summary>
    /// Verifies that a specific operation was logged.
    /// </summary>
    public bool WasOperationCalled(string operation)
    {
        return _operationLog.Any(log => log.Contains(operation));
    }

    private void LogOperation(string operation)
    {
        _operationLog.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {operation}");
    }
}

/// <summary>
/// Specific mock implementations for commonly used repositories.
/// </summary>
public static class MockRepositories
{
    public static MockRepository<T> Create<T>() where T : class
    {
        return new MockRepository<T>();
    }

    public static MockRepository<T> CreateWithData<T>(params T[] entities) where T : class
    {
        var repo = new MockRepository<T>();
        repo.Seed(entities);
        return repo;
    }
}
