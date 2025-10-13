using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaoStudio.Plugins.KVStore.Models;
using LiteDB;
using Serilog;

namespace DaoStudio.Plugins.KVStore.Repositories;

/// <summary>
/// Repository class for managing key-value pairs using LiteDB
/// Provides CRUD operations with session isolation
/// </summary>
public class KeyValueRepository : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<Models.KeyValuePair> _collection;
    private readonly KeyValueStoreConfig _config;
    private bool _disposed;

    /// <summary>
    /// Constructor that creates or opens a LiteDB database
    /// </summary>
    /// <param name="databasePath">Path to the LiteDB database file</param>
    /// <param name="config">Configuration for case sensitivity</param>
    public KeyValueRepository(string databasePath, KeyValueStoreConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        try
        {
            // Ensure directory exists
            var directoryPath = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Create or open the LiteDB database
            _database = new LiteDatabase(databasePath);
            _collection = _database.GetCollection<Models.KeyValuePair>("keyvalues");

            // Create indexes for better query performance
            _collection.EnsureIndex(x => x.TopSessionId);
            _collection.EnsureIndex(x => x.Key);
            _collection.EnsureIndex(x => new { x.TopSessionId, x.Key }, unique: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing KeyValueRepository with database path: {DatabasePath}", databasePath);
            throw;
        }
    }

    /// <summary>
    /// Gets all keys for a specific top session ID
    /// </summary>
    /// <param name="topSessionId">The top session identifier</param>
    /// <returns>Array of keys</returns>
    public string[] GetKeys(string topSessionId)
    {
        if (string.IsNullOrEmpty(topSessionId))
            return Array.Empty<string>();

        try
        {
            return _collection.Find(x => x.TopSessionId == topSessionId)
                             .OrderBy(x => x.Key)  // Add consistent ordering
                             .Select(x => x.Key)
                             .ToArray();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting keys for session {TopSessionId}", topSessionId);
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Gets a page of keys for a specific top session ID with database-level paging
    /// </summary>
    /// <param name="topSessionId">The top session identifier</param>
    /// <param name="skip">The number of keys to skip (0-based indexing, optional)</param>
    /// <param name="take">The number of keys to take (optional, defaults to all remaining)</param>
    /// <returns>Array of keys for the specified page</returns>
    public string[] GetKeysWithPaging(string topSessionId, int skip = 0, int? take = null)
    {
        if (string.IsNullOrEmpty(topSessionId))
            return Array.Empty<string>();

        // Ensure skip is at least 0
        skip = Math.Max(0, skip);

        try
        {
            var results = _collection.Find(x => x.TopSessionId == topSessionId)
                                     .OrderBy(x => x.Key)  // Add consistent ordering
                                     .Skip(skip);

            // Apply take if specified and positive, or if zero (to return empty)
            if (take.HasValue)
            {
                if (take.Value <= 0)
                {
                    // Return empty array for zero or negative take
                    return Array.Empty<string>();
                }
                results = results.Take(take.Value);
            }

            return results.Select(x => x.Key)
                         .ToArray();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting keys with paging for session {TopSessionId}, skip {Skip}, take {Take}", topSessionId, skip, take);
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Attempts to get a value for the specified key in the specified session
    /// </summary>
    /// <param name="topSessionId">The top session identifier</param>
    /// <param name="key">The key to look up</param>
    /// <param name="value">The value if found</param>
    /// <returns>True if key exists, false otherwise</returns>
    public bool TryGetValue(string topSessionId, string key, out string? value)
    {
        value = null;
        
        if (string.IsNullOrEmpty(topSessionId) || string.IsNullOrEmpty(key))
            return false;

        try
        {
            var keyComparison = _config.IsCaseSensitive ? key : key.ToLowerInvariant();
            var sessionComparison = _config.IsCaseSensitive ? topSessionId : topSessionId.ToLowerInvariant();

            Models.KeyValuePair? kvp;
            if (_config.IsCaseSensitive)
            {
                kvp = _collection.FindOne(x => x.TopSessionId == sessionComparison && x.Key == keyComparison);
            }
            else
            {
                kvp = _collection.FindOne(x => x.TopSessionId.ToLower() == sessionComparison && x.Key.ToLower() == keyComparison);
            }

            if (kvp != null)
            {
                value = kvp.Value;
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting value for session {TopSessionId} and key {Key}", topSessionId, key);
        }

        return false;
    }

    /// <summary>
    /// Sets a value for the specified key in the specified session
    /// </summary>
    /// <param name="topSessionId">The top session identifier</param>
    /// <param name="key">The key to set</param>
    /// <param name="value">The value to store</param>
    /// <returns>True if successful</returns>
    public bool SetValue(string topSessionId, string key, string value)
    {
        if (string.IsNullOrEmpty(topSessionId) || string.IsNullOrEmpty(key))
            return false;

        try
        {
            var keyComparison = _config.IsCaseSensitive ? key : key.ToLowerInvariant();
            var sessionComparison = _config.IsCaseSensitive ? topSessionId : topSessionId.ToLowerInvariant();

            Models.KeyValuePair? existingKvp;
            if (_config.IsCaseSensitive)
            {
                existingKvp = _collection.FindOne(x => x.TopSessionId == sessionComparison && x.Key == keyComparison);
            }
            else
            {
                existingKvp = _collection.FindOne(x => x.TopSessionId.ToLower() == sessionComparison && x.Key.ToLower() == keyComparison);
            }

            if (existingKvp != null)
            {
                // Update existing record
                existingKvp.Value = value;
                existingKvp.UpdatedAt = DateTime.UtcNow;
                _collection.Update(existingKvp);
            }
            else
            {
                // Insert new record
                var newKvp = new Models.KeyValuePair(topSessionId, key, value);
                _collection.Insert(newKvp);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting value for session {TopSessionId} and key {Key}", topSessionId, key);
            return false;
        }
    }

    /// <summary>
    /// Deletes a key-value pair from the specified session
    /// </summary>
    /// <param name="topSessionId">The top session identifier</param>
    /// <param name="key">The key to delete</param>
    /// <returns>True if the key was found and deleted</returns>
    public bool DeleteKey(string topSessionId, string key)
    {
        if (string.IsNullOrEmpty(topSessionId) || string.IsNullOrEmpty(key))
            return false;

        try
        {
            var keyComparison = _config.IsCaseSensitive ? key : key.ToLowerInvariant();
            var sessionComparison = _config.IsCaseSensitive ? topSessionId : topSessionId.ToLowerInvariant();

            int deletedCount;
            if (_config.IsCaseSensitive)
            {
                deletedCount = _collection.DeleteMany(x => x.TopSessionId == sessionComparison && x.Key == keyComparison);
            }
            else
            {
                deletedCount = _collection.DeleteMany(x => x.TopSessionId.ToLower() == sessionComparison && x.Key.ToLower() == keyComparison);
            }

            return deletedCount > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting key for session {TopSessionId} and key {Key}", topSessionId, key);
            return false;
        }
    }

    /// <summary>
    /// Checks if a key exists in the specified session
    /// </summary>
    /// <param name="topSessionId">The top session identifier</param>
    /// <param name="key">The key to check</param>
    /// <returns>True if the key exists</returns>
    public bool KeyExists(string topSessionId, string key)
    {
        return TryGetValue(topSessionId, key, out _);
    }

    /// <summary>
    /// Gets the total count of key-value pairs for a specific session
    /// </summary>
    /// <param name="topSessionId">The top session identifier</param>
    /// <returns>Count of key-value pairs</returns>
    public int GetKeyCount(string topSessionId)
    {
        if (string.IsNullOrEmpty(topSessionId))
            return 0;

        try
        {
            return _collection.Count(x => x.TopSessionId == topSessionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting key count for session {TopSessionId}", topSessionId);
            return 0;
        }
    }

    /// <summary>
    /// Clears all key-value pairs for a specific session
    /// </summary>
    /// <param name="topSessionId">The top session identifier</param>
    /// <returns>Number of deleted records</returns>
    public int ClearSession(string topSessionId)
    {
        if (string.IsNullOrEmpty(topSessionId))
            return 0;

        try
        {
            return _collection.DeleteMany(x => x.TopSessionId == topSessionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error clearing session {TopSessionId}", topSessionId);
            return 0;
        }
    }

    /// <summary>
    /// Disposes the database connection
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
            try
            {
                _database?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disposing KeyValueRepository");
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Finalizer
    /// </summary>
    ~KeyValueRepository()
    {
        Dispose(false);
    }
}