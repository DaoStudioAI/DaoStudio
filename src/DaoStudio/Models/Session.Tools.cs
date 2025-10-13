using DaoStudio.Common.Plugins;
using DaoStudio;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Plugins;
using Microsoft.Extensions.Logging;

namespace DaoStudio;


internal partial class Session : ISession
{
    /// <summary>
    /// Gets the list of enabled tools for the current person based on their configuration
    /// </summary>
    public async Task<List<ITool>> GetEnabledPersonToolsAsync()
    {
        var allTools = await toolService.GetAllToolsAsync();

        // Filter tools based on person configuration
        if (person.IsAllToolsEnabled())
        {
            // Use all enabled tools
            var enabledTools = allTools.Where(tool => tool.IsEnabled)
                .ToList();
            logger.LogDebug("Loading all {ToolCount} enabled tools for person {PersonName} (all tools enabled)",
                enabledTools.Count, person.Name);
            return enabledTools;
        }
        else
        {
            // Use only specific tools from person's tool names
            if (person.ToolNames == null || person.ToolNames.Length == 0)
            {
                logger.LogDebug("No specific tools configured for person {PersonName}", person.Name);
                return new List<ITool>();
            }

            var allowedNames = new HashSet<string>(person.ToolNames, StringComparer.OrdinalIgnoreCase);
            var filteredTools = allTools
                .Where(tool => tool.IsEnabled && allowedNames.Contains(tool.Name))
                .ToList();
            logger.LogDebug("Loading {FilteredCount} of {TotalCount} tools for person {PersonName} (specific tools: {ToolNames})",
                filteredTools.Count, allTools.Count(), person.Name, string.Join(", ", person.ToolNames));
            return filteredTools;
        }
    }

    /// <summary>
    /// Gets the list of enabled tools for the current person as Tool instances (internal use)
    /// </summary>
    private async Task<List<ITool>> GetEnabledPersonToolsInternalAsync()
    {
        var allTools = await toolService.GetAllToolsAsync();

        // Filter tools based on person configuration
        if (person.IsAllToolsEnabled())
        {
            // Use all enabled tools
            var enabledTools = allTools.Where(tool => tool.IsEnabled)
                .ToList();
            return enabledTools;
        }
        else
        {
            // Use only specific tools from person's tool names
            if (person.ToolNames == null || person.ToolNames.Length == 0)
            {
                return new List<ITool>();
            }

            var allowedNames = new HashSet<string>(person.ToolNames, StringComparer.OrdinalIgnoreCase);
            var filteredTools = allTools
                .Where(tool => tool.IsEnabled && allowedNames.Contains(tool.Name))
                .ToList();
            return filteredTools;
        }
    }
    /// <summary>
    /// Updates the available plugins based on the current person's tool configuration
    /// </summary>
    private async Task UpdateAvailablePluginsAsync()
    {
        var enabledPersonTools = await GetEnabledPersonToolsInternalAsync();

        // Handle case where no tools are enabled
        if (enabledPersonTools.Count == 0)
        {
            SetTools(new Dictionary<string, List<FunctionWithDescription>>());
            logger.LogDebug("No tools enabled for person {PersonName}, clearing plugins", person.Name);
            return;
        }
        var moduleFunctions = new Dictionary<string, List<FunctionWithDescription>>();

        foreach (var tool in enabledPersonTools)
        {
            var plugin = pluginService.PluginTools?.TryGetValue(tool.Id, out var foundPlugin) == true ? foundPlugin : null;

            if (plugin != null)
            {
                try
                {
                    // Extract functions from this plugin
                    var functions = await ExtractFunctionsFromPluginAsync(plugin, tool);
                    if (functions.Count > 0)
                    {
                        var moduleName = tool.Name;
                        if (!moduleFunctions.ContainsKey(moduleName))
                        {
                            moduleFunctions[moduleName] = new List<FunctionWithDescription>();
                        }
                        moduleFunctions[moduleName].AddRange(functions);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to extract functions from plugin for tool {ToolName}", tool.Name);
                }
            }
            else
            {
                logger.LogWarning("No plugin instance found for enabled tool {ToolName} (StaticId: {StaticId})",
                    tool.Name, tool.StaticId);
            }
        }

        // Set the available plugins
        SetTools(moduleFunctions);

        logger.LogInformation("Updated available plugins for session {SessionId}, loaded {PluginCount} plugins with {FunctionCount} functions",
            Id, moduleFunctions.Keys.Count, moduleFunctions.Values.Sum(f => f.Count));
    }

    /// <summary>
    /// Extract functions from a plugin instance by simulating session start
    /// </summary>
    private async Task<List<FunctionWithDescription>> ExtractFunctionsFromPluginAsync(IPluginTool plugin, ITool tool)
    {
        var functions = new List<FunctionWithDescription>();

        try
        {         
            // Call StartSessionAsync to let the plugin register its functions
            // Pass this session as the host session since Session implements IHostSession
            await plugin.GetSessionFunctionsAsync(functions, new HostPersonAdapter(person), new HostSessionAdapter(this, messageService, Microsoft.Extensions.Logging.Abstractions.NullLogger<HostSessionAdapter>.Instance));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract functions from plugin for tool {ToolName}", tool.Name);
        }

        return functions;
    }    /// <summary>
         /// Closes the session on all plugins associated with the current person.
         /// </summary>
    private async Task ClosePluginSessionsAsync()
    {
        if (person == null)
        {
            return;
        }

        var enabledPersonTools = await GetEnabledPersonToolsInternalAsync(); foreach (var tool in enabledPersonTools)
        {
            var plugin = pluginService.PluginTools?.TryGetValue(tool.Id, out var foundPlugin) == true ? foundPlugin : null;

            if (plugin != null)
            {
                try
                {
                    // The plugin's CloseSessionAsync is called to perform any cleanup.
                    await plugin.CloseSessionAsync(new HostSessionAdapter(this, messageService, Microsoft.Extensions.Logging.Abstractions.NullLogger<HostSessionAdapter>.Instance));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to close session on plugin for tool {ToolName}", tool.Name);
                }
            }
        }
    }

    /// <summary>
    /// Sets the available functions for tool calls
    /// </summary>
    /// <param name="moduleFunctions">Dictionary of module names to lists of functions with their descriptions</param>
    public void SetTools(Dictionary<string, List<FunctionWithDescription>> moduleFunctions)
    {
        // Debug: Check if CustomReturnResultTool is missing and session has parent
        bool hasCustomReturnResultTool = _availablePlugin?.Any(kvp => kvp.Key == "CustomReturnResultTool") ?? false;

        if (!hasCustomReturnResultTool && dbsess.ParentSessId != null)
        {
            logger.LogWarning("Debug break: CustomReturnResultTool missing in session with parent {ParentId}", dbsess.ParentSessId);
        }
        // Store the plugin collections directly as Dictionary
        _availablePlugin = new Dictionary<string, List<FunctionWithDescription>>(moduleFunctions);
    }

    /// <summary>
    /// Gets the available functions for tool calls
    /// </summary>
    /// <returns>Dictionary of module names to lists of functions with their descriptions, or null if no tools are set</returns>
    public Dictionary<string, List<FunctionWithDescription>>? GetTools()
    {
        // For GetTools, we can't use async, so we need to check if initialized without calling InitializeAsync
        if (!_isInitialized)
        {
            // Return empty tools rather than throwing, as this method needs to be synchronous
            // The consumer should call a method that triggers initialization first
            return new Dictionary<string, List<FunctionWithDescription>>();
        }
        return _availablePlugin;
    }

}
