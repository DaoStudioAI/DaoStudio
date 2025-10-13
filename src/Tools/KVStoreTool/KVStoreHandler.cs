using System.ComponentModel;
using System.Linq;

namespace DaoStudio.Plugins.KVStore;

/// <summary>
/// Handler class containing the tool methods exposed to the LLM
/// </summary>
public class KVStoreHandler
{
    public readonly KeyValueStorePluginInstance _parent;

    public KVStoreHandler(KeyValueStorePluginInstance parent)
    {
        _parent = parent;
    }

    /// <summary>
    /// Lists keys available in the current session's top namespace with paging support
    /// </summary>
    /// <param name="page">The page number to retrieve (0-based indexing, defaults to 0)</param>
    /// <param name="pageSize">The number of keys per page (defaults to 50)</param>
    /// <returns>Array of string keys for the specified page</returns>
    [DisplayName("kv_list_keys")]
    [Description("Lists keys available in the Key Value Store for the current session's top namespace with paging support")]
    public async Task<string[]> ListKeys([Description("The page number to retrieve (0-based indexing)")] int page = 0, [Description("The number of keys per page")] int pageSize = 10)
    {
        var topSessionId = _parent.GetTopSessionId(_parent._currentSession);

        // Ensure page is at least 0
        page = Math.Max(0, page);
        pageSize = Math.Max(1, pageSize);

        // Calculate skip count (page is already 0-based)
        var skip = page * pageSize;

        // Use database-level paging instead of in-memory paging
        var pagedKeys = _parent._storeData.GetKeysWithPaging(topSessionId, skip, pageSize);

        await Task.CompletedTask;
        return pagedKeys;
    }

    /// <summary>
    /// Gets the value for a specific key
    /// </summary>
    /// <param name="key">The key to look up</param>
    /// <returns>The value as a string, or null if not found</returns>
    [DisplayName("kv_get_value")]
    [Description("Retrieves the value associated with the specified key")]
    public async Task<string?> GetValue([Description("The key to look up")] string key)
    {
        var topSessionId = _parent.GetTopSessionId(_parent._currentSession);

        if (_parent._storeData.TryGetValue(topSessionId, key, out var value))
        {
            return value;
        }

        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Sets a value for a specific key
    /// </summary>
    /// <param name="key">The key to set</param>
    /// <param name="value">The value to store</param>
    /// <returns>True if successful, false otherwise</returns>
    [DisplayName("kv_set_value")]
    [Description("Stores a string value with the specified key")]
    public async Task<bool> SetValue([Description("The key to set")] string key, [Description("The value to store")] string value)
    {
        var topSessionId = _parent.GetTopSessionId(_parent._currentSession);

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        bool success = _parent._storeData.SetValue(topSessionId, key, value);

        if (success)
        {
            // Save the updated store data
            await _parent.SaveStoreDataAsync();
        }

        return success;
    }

    /// <summary>
    /// Deletes a key-value pair
    /// </summary>
    /// <param name="key">The key to delete</param>
    /// <returns>True if the key was found and deleted, false otherwise</returns>
    [DisplayName("kv_delete_key")]
    [Description("Removes the key-value pair with the specified key")]
    public async Task<bool> DeleteKey([Description("The key to delete")] string key)
    {
        var topSessionId = _parent.GetTopSessionId(_parent._currentSession);

        bool success = _parent._storeData.DeleteKey(topSessionId, key);

        if (success)
        {
            // Save the updated store data
            await _parent.SaveStoreDataAsync();
        }

        return success;
    }

    /// <summary>
    /// Checks if a key exists in the store
    /// </summary>
    /// <param name="key">The key to check</param>
    /// <returns>True if the key exists, false otherwise</returns>
    [DisplayName("kv_is_key_exist")]
    [Description("Checks if the specified key exists in the Key Value Store")]
    public async Task<bool> IsKeyExist([Description("The key to check for existence")] string key)
    {
        var topSessionId = _parent.GetTopSessionId(_parent._currentSession);

        bool exists = _parent._storeData.TryGetValue(topSessionId, key, out _);

        await Task.CompletedTask;
        return exists;
    }
}


