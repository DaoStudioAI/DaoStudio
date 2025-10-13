using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Plugins.KVStore.Resources;
using Serilog;

namespace DaoStudio.Plugins.KVStore;

/// <summary>
/// Plugin instance that implements the KeyValueStore functionality
/// and provides tool methods for working with session-scoped key-value storage
/// </summary>
public partial class KeyValueStorePluginInstance : BasePlugin<KeyValueStoreConfig>
{
    private readonly IHost _host;
    public KeyValueStoreData _storeData;
    public IHostSession? _currentSession;
    private readonly Dictionary<string, KeyValueStoreData> _sessionDataCache;
    
    public KeyValueStorePluginInstance(IHost host, PlugToolInfo plugInstanceInfo) : base(plugInstanceInfo)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _sessionDataCache = new Dictionary<string, KeyValueStoreData>();
        _storeData = new KeyValueStoreData(Config); // Default instance
    }

    /// <summary>
    /// Gets or creates the store data for a specific top session ID
    /// </summary>
    private KeyValueStoreData GetStoreDataForSession(string topSessionId)
    {
        if (string.IsNullOrEmpty(topSessionId))
        {
            return _storeData; // Return default instance for empty session
        }

        // Check if we already have this session data cached
        if (_sessionDataCache.TryGetValue(topSessionId, out var existingData))
        {
            return existingData;
        }

        // Create new store data for this session
        var sessionData = new KeyValueStoreData(Config);
        
        // Get the database path using the plugin config folder and session ID
        var configFolderPath = _host.GetPluginConfigureFolderPath("KVStore");
        var databasePath = Path.Combine(configFolderPath, $"{topSessionId}.db");
        
        // Initialize the store data with the database path
        sessionData.Initialize(databasePath, topSessionId);
        
        // Cache the session data
        _sessionDataCache[topSessionId] = sessionData;
        
        return sessionData;
    }

    /// <summary>
    /// No longer needed - LiteDB handles persistence automatically
    /// This method is kept for compatibility but does nothing
    /// </summary>
    public async Task SaveStoreDataAsync()
    {
        // LiteDB handles persistence automatically, so this method is now a no-op
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the top-most parent session ID for the given session
    /// </summary>
    /// <param name="session">The session to get the top parent for</param>
    /// <returns>The top parent session ID or an empty string if not found</returns>
    public string GetTopSessionId(IHostSession? session)
    {
        if (session == null)
            return string.Empty;
            
        // Traverse up the parent chain to find the top-most parent
        var current = session;
        while (current.ParentSessionId != null)
        {
            // Get the parent session using ParentSessionId
            // Since we don't have a direct way to get the parent session object,
            // we'll just use the ParentSessionId for the namespace
            break;
        }
        
        return current.Id.ToString();
    }

    /// <summary>
    /// Registers the tool functions for a specific session
    /// </summary>
    protected override async Task RegisterToolFunctionsAsync(
        List<FunctionWithDescription> toolcallFunctions,
        IHostPerson? person, 
        IHostSession? hostSession)
    {
        // Store the current session for use in tool methods (null when called from GUI tool tab)
        _currentSession = hostSession;
        
        // Get the session-specific store data
        var topSessionId = GetTopSessionId(hostSession);
        _storeData = GetStoreDataForSession(topSessionId);

        // Add all the KeyValueStore tool functions
        var kvHandler = new KVStoreHandler(this);
        var toolFunctions = IPluginExtensions.CreateFunctionsFromToolMethods(kvHandler, "KeyValueStore");
        
        // Apply instance name prefix if configured
        if (!string.IsNullOrEmpty(Config.InstanceName))
        {
            var prefix = $"{Config.InstanceName}_";
            
            foreach (var function in toolFunctions)
            {
                // Add prefix to function name
                function.Description.Name = prefix + function.Description.Name;
                
                // Add instance name to function description
                function.Description.Description = $"[{Config.InstanceName}] {function.Description.Description}";
            }
        }
        
        toolcallFunctions.AddRange(toolFunctions);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Handle the session close event - dispose session-specific resources
    /// </summary>
    protected override async Task<byte[]?> OnSessionCloseAsync(IHostSession hostSession)
    {
        // Clear the current session reference if it matches
        if (_currentSession == hostSession)
        {
            _currentSession = null;
        }
        
        // Clean up session-specific data from cache
        var topSessionId = GetTopSessionId(hostSession);
        if (_sessionDataCache.TryGetValue(topSessionId, out var sessionData))
        {
            sessionData.Dispose();
            _sessionDataCache.Remove(topSessionId);
        }
        
        // Return null to keep the existing status (no longer used for persistence)
        return await Task.FromResult<byte[]?>(null);
    }

    /// <summary>
    /// Dispose all cached session data when the plugin instance is disposed
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var sessionData in _sessionDataCache.Values)
            {
                sessionData?.Dispose();
            }
            _sessionDataCache.Clear();
            
            _storeData?.Dispose();
        }
        
        base.Dispose(disposing);
    }
}
