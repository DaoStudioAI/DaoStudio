using Avalonia.Controls;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using System.Linq;
using System.Text.Json;

namespace FileTool;

public class FileToolPluginFactory : IPluginFactory, IPluginConfigAvalonia
{
    private IHost? _host; // Stores the IHost instance

    public async Task SetHost(IHost host)
    {
        _host = host;
        // Optional: Log host assignment for debugging
        // Console.WriteLine($"FileToolPlugin: Host set. Type: {host?.GetType().FullName}");
        await Task.CompletedTask;
    }

    public PluginInfo GetPluginInfo()
    {
        return new PluginInfo
        {
            StaticId = "Com.DaoStudio.FileTool",
            Version = "1.0",
            DisplayName = "File Tool"
        };
    }

    public async Task<PlugToolInfo> CreateToolConfigAsync(long instanceid)
    {
        var config = new FileToolConfig
        {
            RootDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        // Get function names by creating a temporary plugin instance
        var tempInstanceInfo = new PlugToolInfo
        {
            InstanceId = instanceid,
            Config = JsonSerializer.Serialize(config),
            DisplayName = GetPluginInfo().DisplayName,
            SupportConfigWindow = true
        };
        
        var functionNames = await GetPluginFunctionNamesAsync(tempInstanceInfo);
        var description = "File system operations tool";
        if (functionNames.Any())
        {
            description += ": " + string.Join(", ", functionNames);
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
    public async Task<PlugToolInfo> ConfigInstance(Window win, PlugToolInfo plugInstanceInfo)
    {
        // Show the configuration dialog
        var configDialog = new FileToolConfigWindow(plugInstanceInfo);

        if (win != null)
        {
            // Show as modal dialog
            var result = await configDialog.ShowDialog<bool>(win);

            if (result && !string.IsNullOrEmpty(configDialog.Result))
            {
                // User clicked Save, return the updated configuration
                plugInstanceInfo.Config = configDialog.Result;
                plugInstanceInfo.DisplayName = configDialog.DisplayNameResult;
                
                // Update description with function names
                var functionNames = await GetPluginFunctionNamesAsync(plugInstanceInfo);
                var description = "File system operations tool";
                if (functionNames.Any())
                {
                    description += ": " + string.Join(", ", functionNames);
                }
                plugInstanceInfo.Description = description;
                
                return plugInstanceInfo;
            }
        }

        // User cancelled or something went wrong, return the original instance info
        return plugInstanceInfo;
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
            Console.WriteLine($"Error getting plugin function names: {ex.Message}");
            return new List<string>();
        }
    }
    
    public async Task DeleteToolConfigAsync(PlugToolInfo plugInstanceInfo)
    {
        // Plugin cleanup is now handled by individual plugin instances
        // No global cleanup needed at the factory level
        await Task.CompletedTask;
    }
    
    public async Task<IPluginTool> CreatePluginToolAsync(PlugToolInfo plugInstanceInfo)
    {
        // Create a new instance of the consolidated FileOperations plugin
        var plugin = new FileOperations(plugInstanceInfo);
        await Task.CompletedTask;
        return plugin;
    }
}