using Avalonia.Controls;
using Avalonia.Threading;
using DaoStudio.Common.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using BrowserTool.Properties;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;


namespace BrowserTool;

public enum BrowserType
{
    Unknown = 0,
    Chrome = 1,
    Firefox = 2,
    Edge = 3,
    Safari = 4,
    Opera = 5,
    Brave = 6
}
public enum BrowserToolType
{
    Embeded = 0,
    PlaywrightLocal = 1,
    PlaywrightOs = 2,
}

public partial class BrowserToolPluginFactory : IPluginFactory, IPluginConfigAvalonia
{
    public PluginInfo GetPluginInfo()
    {
        return new PluginInfo
        {
            StaticId = "Com.DaoStudio.BrowserTool",
            Version = "1.0",
            DisplayName = Properties.Resources.BrowserToolDisplayName,
        };
    }

    public async Task<PlugToolInfo> CreateToolConfigAsync(long instanceid)
    {
        //var browserInfo = GetDefaultBrowserPath();

        var config = new BrowserToolConfig()
        {
            BrowserToolType = BrowserToolType.Embeded,
            BrowserPath = string.Empty,
            BrowserType = BrowserType.Unknown,
            MinElementSize = 3 // Default minimum element size in pixels
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
        var description = Properties.Resources.BrowserToolDescription;
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
        // Show the configuration window
        var result = await BrowserConfigWindow.ShowDialog(win, plugInstanceInfo);
        
        // Get function names by creating a temporary plugin instance
        var functionNames = await GetPluginFunctionNamesAsync(result);
        var description = Properties.Resources.BrowserToolDescription;
        if (functionNames.Any())
        {
            description += ": " + string.Join(", ", functionNames);
        }
        result.Description = description;
        
        return result;
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
        // Create a new instance of the plugin
        var plugin = new BrowserToolPluginInstance(this, plugInstanceInfo);
        await Task.CompletedTask;
        return plugin;
    }
}