using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Plugins.KVStore.Resources;
using Serilog;


namespace DaoStudio.Plugins.KVStore;

/// <summary>
/// Factory class for creating and managing KeyValueStore plugin instances
/// </summary>
public class KeyValueStorePluginFactory : IPluginFactory, IPluginConfigAvalonia
{
    private IHost? _host;

    // Dictionary to track plugin instances by their InstanceId
    private readonly ConcurrentDictionary<long, List<WeakReference<KeyValueStorePluginInstance>>> _instanceRegistry
        = new ConcurrentDictionary<long, List<WeakReference<KeyValueStorePluginInstance>>>();

    /// <summary>
    /// Set the host instance for accessing sessions
    /// </summary>
    /// <param name="host">The host instance</param>
    public async Task SetHost(IHost host)
    {
        _host = host;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Get plugin metadata
    /// </summary>
    /// <returns>Plugin information</returns>
    public PluginInfo GetPluginInfo()
    {
        return new PluginInfo
        {
                StaticId = "Com.DaoStudio.KVStore",
            Version = "1.0",
            DisplayName = Strings.DisplayName
        };
    }

    /// <summary>
    /// Create a new plugin instance with default configuration
    /// </summary>
    /// <param name="instanceid">The instance ID</param>
    /// <returns>Plugin instance information</returns>
    public async Task<PlugToolInfo> CreateToolConfigAsync(long instanceid)
    {
        var config = new KeyValueStoreConfig();

        // Get function names by creating a temporary plugin instance
        var tempInstanceInfo = new PlugToolInfo
        {
            InstanceId = instanceid,
            Config = JsonSerializer.Serialize(config),
            DisplayName = GetPluginInfo().DisplayName,
            SupportConfigWindow = true
        };

        var functionNames = await GetPluginFunctionNamesAsync(tempInstanceInfo);
        var description = Strings.Description;
        if (functionNames.Any())
        {
            description += ":" + string.Join(", ", functionNames);
        }

        var instanceInfo = new PlugToolInfo
        {
            Description = description,
            Config = JsonSerializer.Serialize(config),
            DisplayName = GetPluginInfo().DisplayName, // Set from plugin info
            SupportConfigWindow = true
        };

        return instanceInfo;
    }

    /// <summary>
    /// Helper method to get function names from a plugin instance
    /// </summary>
    /// <param name="plugInstanceInfo">The plugin instance information</param>
    /// <returns>List of function names</returns>
    private async Task<List<string>> GetPluginFunctionNamesAsync(PlugToolInfo plugInstanceInfo)
    {
        try
        {
            if (_host == null)
                return new List<string>();

            // Create a temporary plugin tool instance
            var pluginTool = await CreatePluginToolAsync(plugInstanceInfo);

            // Get functions with null hostSession
            var functions = new List<FunctionWithDescription>();
            await pluginTool.GetSessionFunctionsAsync(functions, null, null);

            // Extract function names
            var functionNames = functions.Select(f => f.Description.Name).ToList();

            // Clean up the temporary instance
            pluginTool.Dispose();

            return functionNames;
        }
        catch (Exception ex)
        {
            // Log error but don't fail - return empty list
            Log.Error(ex, "Error getting plugin function names");
            return new List<string>();
        }
    }

    /// <summary>
    /// Delete a plugin instance and clean up all associated data
    /// </summary>
    /// <param name="plugInstanceInfo">The plugin instance information</param>
    public async Task DeleteToolConfigAsync(PlugToolInfo plugInstanceInfo)
    {
        // Clean up the instance registry for this instance ID
        _instanceRegistry.TryRemove(plugInstanceInfo.InstanceId, out _);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Show configuration window and update the plugin configuration
    /// </summary>
    /// <param name="win">The parent window</param>
    /// <param name="plugInstanceInfo">The plugin instance information</param>
    /// <returns>Updated plugin instance information</returns>
    public async Task<PlugToolInfo> ConfigInstance(Window win, PlugToolInfo plugInstanceInfo)
    {
        // Show the configuration dialog
        var configDialog = new KeyValueStoreConfigWindow(plugInstanceInfo);

        // Show as modal dialog
        var result = await configDialog.ShowDialog<bool>(win);

        if (result && !string.IsNullOrEmpty(configDialog.Result))
        {
            // User clicked Save, return the updated configuration
            plugInstanceInfo.Config = configDialog.Result;
            plugInstanceInfo.DisplayName = configDialog.DisplayNameResult;

            // Update description with function names
            var functionNames = await GetPluginFunctionNamesAsync(plugInstanceInfo);
            var description = Strings.Description;
            if (functionNames.Any())
            {
                description += ": " + string.Join(", ", functionNames);
            }
            plugInstanceInfo.Description = description;

            // Find all active plugin instances for this ID and update their configuration
            if (_instanceRegistry.TryGetValue(plugInstanceInfo.InstanceId, out var instances))
            {
                var aliveInstances = instances.ToList();

                foreach (var weakRef in aliveInstances)
                {
                    if (weakRef.TryGetTarget(out var instance))
                    {
                        try
                        {
                            // Parse the new config
                            var updatedConfig = JsonSerializer.Deserialize<KeyValueStoreConfig>(configDialog.Result);
                            if (updatedConfig != null)
                            {
                                // Update the instance configuration
                                // Note: With LiteDB storage, we can't easily migrate existing data
                                // between different case sensitivity settings without knowing all sessions.
                                // The configuration change will apply to new operations.
                                instance.UpdateConfig(updatedConfig);

                                // For LiteDB-based storage, configuration changes are applied per-session
                                // when each session's database is initialized with the new config.
                                // Existing data remains in the database files unchanged.
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error updating plugin configuration: {Message}", ex.Message);
                        }
                    }
                }
            }
        }

        return plugInstanceInfo;
    }

    /// <summary>
    /// Create a new plugin instance
    /// </summary>
    /// <returns>A new plugin instance</returns>
    public async Task<IPluginTool> CreatePluginToolAsync(PlugToolInfo plugInstanceInfo)
    {
        if (_host == null)
            throw new InvalidOperationException(Strings.Error_HostMustBeSet);

        var plugin = new KeyValueStorePluginInstance(_host, plugInstanceInfo);

        // Register the instance for synchronization
        RegisterInstance(plugInstanceInfo.InstanceId, plugin);

        await Task.CompletedTask;
        return plugin;
    }

    /// <summary>
    /// Register a plugin instance for synchronization
    /// </summary>
    /// <param name="instanceId">The instance ID</param>
    /// <param name="instance">The plugin instance</param>
    private void RegisterInstance(long instanceId, KeyValueStorePluginInstance instance)
    {
        _instanceRegistry.AddOrUpdate(
            instanceId,
            new List<WeakReference<KeyValueStorePluginInstance>> { new WeakReference<KeyValueStorePluginInstance>(instance) },
            (key, existing) =>
            {
                // Clean up dead references before adding new one
                var aliveRefs = existing.Where(wr => wr.TryGetTarget(out _)).ToList();
                aliveRefs.Add(new WeakReference<KeyValueStorePluginInstance>(instance));
                return aliveRefs;
            });
    }
}
