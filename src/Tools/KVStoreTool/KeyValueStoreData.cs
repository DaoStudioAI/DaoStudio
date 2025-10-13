using System;
using DaoStudio.Plugins.KVStore.Repositories;

namespace DaoStudio.Plugins.KVStore;

/// <summary>
/// Data access layer for key-value store using LiteDB for persistence
/// No longer uses MessagePack serialization as data is persisted directly in LiteDB
/// </summary>
public class KeyValueStoreData : IDisposable
{
    /// <summary>
    /// Configuration for the key-value store
    /// </summary>
    private KeyValueStoreConfig Config { get; set; }

    /// <summary>
    /// Repository for accessing LiteDB data
    /// </summary>
    private KeyValueRepository? _repository;
    
    /// <summary>
    /// The database path for lazy initialization
    /// </summary>
    private string? _databasePath;
    
    /// <summary>
    /// The current top session ID being used
    /// </summary>
    private string _currentTopSessionId = string.Empty;

    /// <summary>
    /// Whether this instance has been disposed
    /// </summary>
    private bool _disposed;
    
    /// <summary>
    /// Constructor with default configuration (case sensitive)
    /// </summary>
    public KeyValueStoreData() : this(new KeyValueStoreConfig())
    {
    }
    
    /// <summary>
    /// Constructor with specific configuration
    /// </summary>
    /// <param name="config">The configuration to use</param>
    public KeyValueStoreData(KeyValueStoreConfig config)
    {
        Config = config ?? new KeyValueStoreConfig();
    }

    /// <summary>
    /// Initializes the repository for a specific session (lazy initialization)
    /// </summary>
    /// <param name="databasePath">Path to the LiteDB database file</param>
    /// <param name="topSessionId">The top session ID for this instance</param>
    public void Initialize(string databasePath, string topSessionId)
    {
        if (_repository != null)
        {
            _repository.Dispose();
            _repository = null;
        }

        // Store the database path and session ID for lazy initialization
        _databasePath = databasePath;
        _currentTopSessionId = topSessionId;
    }

    /// <summary>
    /// Ensures the repository is initialized (lazy initialization)
    /// </summary>
    private void EnsureRepositoryInitialized()
    {
        if (_repository == null && !string.IsNullOrEmpty(_databasePath))
        {
            _repository = new KeyValueRepository(_databasePath, Config);
        }
    }

    /// <summary>
    /// Gets all keys available in the specified namespace (top session ID)
    /// </summary>
    /// <param name="topSessionId">The top session ID to look up</param>
    /// <returns>Array of string keys or an empty array if namespace not found</returns>
    public string[] GetKeys(string topSessionId)
    {
        if (string.IsNullOrEmpty(topSessionId))
            return Array.Empty<string>();

        EnsureRepositoryInitialized();
        
        if (_repository == null)
            return Array.Empty<string>();

        return _repository.GetKeys(topSessionId);
    }

    /// <summary>
    /// Gets a page of keys available in the specified namespace (top session ID) with database-level paging
    /// </summary>
    /// <param name="topSessionId">The top session ID to look up</param>
    /// <param name="skip">The number of keys to skip (0-based indexing, optional)</param>
    /// <param name="take">The number of keys to take (optional, defaults to all remaining)</param>
    /// <returns>Array of string keys for the specified page or an empty array if namespace not found</returns>
    public string[] GetKeysWithPaging(string topSessionId, int skip = 0, int? take = null)
    {
        if (string.IsNullOrEmpty(topSessionId))
            return Array.Empty<string>();

        EnsureRepositoryInitialized();
        
        if (_repository == null)
            return Array.Empty<string>();

        return _repository.GetKeysWithPaging(topSessionId, skip, take);
    }

    /// <summary>
    /// Attempts to get a value for the specified key in the specified namespace (top session ID)
    /// </summary>
    /// <param name="topSessionId">The top session ID to look up</param>
    /// <param name="key">The key to look up</param>
    /// <param name="value">The value if found</param>
    /// <returns>True if key exists in the namespace, false otherwise</returns>
    public bool TryGetValue(string topSessionId, string key, out string? value)
    {
        value = null;
        
        if (string.IsNullOrEmpty(topSessionId) || string.IsNullOrEmpty(key))
            return false;

        EnsureRepositoryInitialized();
        
        if (_repository == null)
            return false;
            
        return _repository.TryGetValue(topSessionId, key, out value);
    }

    /// <summary>
    /// Sets a value for the specified key in the specified namespace (top session ID)
    /// </summary>
    /// <param name="topSessionId">The top session ID to use</param>
    /// <param name="key">The key to set</param>
    /// <param name="value">The value to store</param>
    /// <returns>True if the operation succeeded</returns>
    public bool SetValue(string topSessionId, string key, string value)
    {
        if (string.IsNullOrEmpty(topSessionId) || string.IsNullOrEmpty(key))
            return false;

        EnsureRepositoryInitialized();
        
        if (_repository == null)
            return false;
            
        return _repository.SetValue(topSessionId, key, value);
    }

    /// <summary>
    /// Deletes a key-value pair from the specified namespace (top session ID)
    /// </summary>
    /// <param name="topSessionId">The top session ID to use</param>
    /// <param name="key">The key to delete</param>
    /// <returns>True if the key was found and deleted, false otherwise</returns>
    public bool DeleteKey(string topSessionId, string key)
    {
        if (string.IsNullOrEmpty(topSessionId) || string.IsNullOrEmpty(key))
            return false;

        EnsureRepositoryInitialized();
        
        if (_repository == null)
            return false;
            
        return _repository.DeleteKey(topSessionId, key);
    }

    /// <summary>
    /// Disposes the data store and releases database resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose method
    /// </summary>
    /// <param name="disposing">True if disposing</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _repository?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer
    /// </summary>
    ~KeyValueStoreData()
    {
        Dispose(false);
    }
}
