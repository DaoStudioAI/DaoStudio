using Avalonia.Controls;
using DaoStudio.Common.Plugins;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Models;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using McMaster.NETCore.Plugins;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace DaoStudio.Services
{
    /// <summary>
    /// Service implementation for plugin management operations
    /// </summary>
    public class PluginService : IPluginService
    {
        private readonly ILogger<PluginService> logger;
        private readonly IToolService toolService;
        private readonly IHost DaoStudio;

        // Keep strong references to plugin loaders to prevent GC and AssemblyLoadContext unloading
        private readonly List<PluginLoader> _plugins = new();

        public PluginService(ILogger<PluginService> logger, IToolService toolService,
            IHost DaoStudio)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.toolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
            this.DaoStudio = DaoStudio ?? throw new ArgumentNullException(nameof(DaoStudio));
        }

        #region Properties

        /// <summary>
        /// Collection of loaded plugin factories
        /// </summary>
        public List<IPluginFactory>? PluginFactories { get; private set; }

        /// <summary>
        /// Dictionary of active plugin tool instances keyed by tool ID
        /// </summary>
        public Dictionary<long, IPluginTool>? PluginTools { get; private set; }

        #endregion

        #region Plugin Management

        /// <summary>
        /// Load plugins dynamically from subfolders within a specified directory
        /// </summary>
        /// <param name="pluginFolderPath">Path to the plugins directory</param>
        /// <returns>List of loaded plugin factory objects</returns>
        public async Task<List<IPluginFactory>> LoadPluginsAsync(string pluginFolderPath)
        {
            var plugins = new List<IPluginFactory>();
            var logger = this.logger;

            // Validate arguments
            if (pluginFolderPath is null)
            {
                throw new ArgumentNullException(nameof(pluginFolderPath));
            }

            if (string.IsNullOrWhiteSpace(pluginFolderPath))
            {
                throw new ArgumentException("Plugin folder path cannot be empty or whitespace.", nameof(pluginFolderPath));
            }

            // If directory does not exist, log and return empty result instead of throwing
            if (!Directory.Exists(pluginFolderPath))
            {
                logger.LogWarning($"Plugin folder not found: {pluginFolderPath}");
                PluginFactories = new List<IPluginFactory>();
                return PluginFactories;
            }

            // Get shared assemblies that plugins should use from the host
            var sharedAssemblies = GetSharedAssemblies();

            logger.LogInformation("Scanning plugin folder {PluginFolderPath}", pluginFolderPath);

            // Get all subdirectories (each represents a plugin)
            var pluginDirectories = Directory.GetDirectories(pluginFolderPath);
            logger.LogInformation("Found {PluginDirectoryCount} plugin directories", pluginDirectories.Length);

            foreach (var pluginDir in pluginDirectories)
            {
                var pluginName = Path.GetFileName(pluginDir);
                var pluginDllPath = Path.Combine(pluginDir, $"{pluginName}.dll");

                logger.LogInformation("Processing plugin directory {PluginDirectory}", pluginDir);

                if (!File.Exists(pluginDllPath))
                {
                    logger.LogWarning($"Plugin DLL not found: {pluginDllPath}");
                    continue;
                }

                try
                {
                    // Create plugin loader with shared assemblies
                    var loader = PluginLoader.CreateFromAssemblyFile(
                        pluginDllPath,
                        config =>
                        {
                            config.PreferSharedTypes = true;
                            config.IsUnloadable = true;

                            // Add shared assemblies
                            foreach (var assembly in sharedAssemblies)
                            {
                                config.SharedAssemblies.Add(assembly.GetName());
                            }
                        });

                    // Store loader reference to prevent GC and AssemblyLoadContext unloading
                    _plugins.Add(loader);

                    // Load the plugin assembly
                    var pluginAssembly = loader.LoadDefaultAssembly();
                    logger.LogInformation("Loaded plugin assembly {AssemblyName} from {AssemblyLocation}", pluginAssembly.FullName, pluginAssembly.Location);

                    // Find types implementing IPluginFactory
                    List<Type> pluginTypes;
                    try
                    {
                        pluginTypes = pluginAssembly.GetTypes()
                            .Where(t => typeof(IPluginFactory).IsAssignableFrom(t) && !t.IsAbstract)
                            .ToList();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        var loaderExceptions = ex.LoaderExceptions?.Select(le => le?.Message ?? string.Empty)?.ToArray() ?? Array.Empty<string>();
                        logger.LogError(ex, "Failed to resolve plugin factory types in assembly {AssemblyName}. Loader exceptions: {LoaderExceptions}", pluginAssembly.FullName, string.Join(" | ", loaderExceptions));
                        continue;
                    }

                    logger.LogInformation("Discovered {PluginFactoryCount} plugin factory types in assembly {AssemblyName}", pluginTypes.Count, pluginAssembly.FullName);

                    foreach (var pluginType in pluginTypes)
                    {
                        logger.LogDebug("Creating plugin factory instance from type {PluginType}", pluginType.FullName);

                        IPluginFactory? instance;
                        try
                        {
                            instance = Activator.CreateInstance(pluginType) as IPluginFactory;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to instantiate plugin factory type {PluginType}", pluginType.FullName);
                            continue;
                        }

                        if (instance != null)
                        {
                            try
                            {
                                logger.LogDebug("Setting host for plugin factory {PluginType}", pluginType.FullName);
                                await instance.SetHost(DaoStudio);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to set host for plugin factory type {PluginType}", pluginType.FullName);
                                continue;
                            }

                            plugins.Add(instance);

                            try
                            {
                                var plugInfo = instance.GetPluginInfo();
                                logger.LogInformation("Loaded plugin: {DisplayName} v{Version} (StaticId: {StaticId}, Type: {PluginType})",
                                    plugInfo.DisplayName,
                                    plugInfo.Version,
                                    plugInfo.StaticId,
                                    pluginType.FullName);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to retrieve plugin info for type {PluginType}", pluginType.FullName);
                            }
                        }
                        else
                        {
                            logger.LogWarning("Activator returned null for plugin factory type {PluginType}", pluginType.FullName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Failed to load plugin from {pluginDir}");
                }
            }

            logger.LogInformation($"Loaded {plugins.Count} plugins from {pluginFolderPath}");
            PluginFactories = plugins;
            return plugins;
        }

        /// <summary>
        /// Initialize plugin instances from PluginFactories using LlmTool configurations
        /// ConfigInstance id is LlmTool.Id
        /// </summary>
        public async Task InitializeAsync()
        {
            if (PluginFactories == null || PluginFactories.Count == 0)
            {
                PluginTools = new Dictionary<long, IPluginTool>();
                return;
            }

            var plugins = new Dictionary<long, IPluginTool>();

            try
            {
                // Get all LlmTool configurations using the Tools API
                var allTools = await toolService.GetAllToolsAsync();

                // Group tools by StaticId to determine multiple instances
                var toolsByStaticId = allTools.GroupBy(t => t.StaticId).ToLookup(g => g.Key, g => g.ToList());

                foreach (var iTool in allTools.Where(t => t.IsEnabled))
                {
                    // Find matching plugin factory by StaticId
                    var pluginFactory = PluginFactories.FirstOrDefault(pf =>
                        pf.GetPluginInfo().StaticId == iTool.StaticId);

                    if (pluginFactory != null)
                    {
                        try
                        {
                            // Determine if there are multiple instances for this plugin
                            var toolsWithSameStaticId = toolsByStaticId[iTool.StaticId].FirstOrDefault() ?? new List<ITool>();
                            var hasMultipleInstances = toolsWithSameStaticId.Count > 1;

                            // Create PlugInstanceInfo from ITool data
                            var plugInstanceInfo = new PlugToolInfo
                            {
                                InstanceId = iTool.Id, // ConfigInstance id is ITool.Id
                                Description = iTool.Description,
                                Config = iTool.ToolConfig,
                                DisplayName = iTool.Name, // Include display name from database
                                Status = iTool.StateData, // ITool.StateData is byte[]? which matches PlugToolInfo.Status
                                HasMultipleInstances = hasMultipleInstances
                            };

                            // Create plugin instance
                            var pluginInstance = await pluginFactory.CreatePluginToolAsync(plugInstanceInfo);
                            plugins[iTool.Id] = pluginInstance; // Use iTool.Id as key

                            logger.LogInformation($"Initialized plugin instance: {iTool.Name} (StaticId: {iTool.StaticId}, Id: {iTool.Id}, HasMultipleInstances: {hasMultipleInstances})");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"Failed to initialize plugin instance for tool: {iTool.Name} (StaticId: {iTool.StaticId})");
                        }
                    }
                    else
                    {
                        logger.LogWarning($"No plugin factory found for tool: {iTool.Name} (StaticId: {iTool.StaticId})");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize plugins from storage");
            }

            PluginTools = plugins;
            logger.LogInformation($"Initialized {plugins.Count} plugin instances");

            InitializeToolEventHandlers();
        }

        /// <summary>
        /// Subscribe to tool events to keep PluginTools synchronized
        /// </summary>
        public void InitializeToolEventHandlers()
        {
            // Subscribe to events to keep PluginTools synchronized
            toolService.ToolChanged += OnToolChanged;
            toolService.ToolListUpdated += OnToolListUpdated;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle tool changes (create, update, delete)
        /// </summary>
        private async void OnToolChanged(object? sender, ToolOperationEventArgs e)
        {
            if (PluginTools == null)
                return;

            var logger = this.logger;

            try
            {
                switch (e.OperationType)
                {
                    case ToolOperationType.Created:
                        if (e.Tool != null)
                            await AddPluginToolAsync(e.Tool, logger);
                        break;

                    case ToolOperationType.Updated:
                        if (e.Tool != null)
                            await UpdatePluginToolAsync(e.Tool, logger);
                        break;

                    case ToolOperationType.Deleted:
                        if (e.ToolId.HasValue)
                            await RemovePluginToolAsync(e.ToolId.Value, logger);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling tool change event for tool ID: {ToolId}, Operation: {Operation}",
                    e.ToolId, e.OperationType);
            }
        }

        /// <summary>
        /// Handle tool list updates (add, remove)
        /// </summary>
        private async void OnToolListUpdated(object? sender, ToolListUpdateEventArgs e)
        {
            if (PluginTools == null)
                return;

            var logger = this.logger;

            try
            {
                switch (e.UpdateType)
                {
                    case ToolListUpdateType.Added:
                        if (e.Tool != null)
                            await AddPluginToolAsync(e.Tool, logger);
                        break;

                    case ToolListUpdateType.Removed:
                        if (e.ToolId.HasValue)
                            await RemovePluginToolAsync(e.ToolId.Value, logger);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling tool list update event for tool ID: {ToolId}, Update: {UpdateType}",
                    e.ToolId, e.UpdateType);
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Get assemblies that should be shared between host and plugins
        /// </summary>
        /// <returns>Array of shared assemblies</returns>
        private Assembly[] GetSharedAssemblies()
        {
            // Return assemblies that should be shared between host and plugins
            return new[]
            {
                typeof(IPluginFactory).Assembly,                    // DaoStudio.Common
                typeof(Window).Assembly,                     // Avalonia.Controls
                typeof(JsonSerializer).Assembly,             // System.Text.Json
                typeof(Microsoft.Extensions.Logging.ILogger).Assembly,                    // Microsoft.Extensions.Logging.Abstractions
                typeof(ConcurrentDictionary<,>).Assembly,    // System.Collections.Concurrent
                // Add other shared framework assemblies as needed
            };
        }

        /// <summary>
        /// Add a plugin tool instance to PluginTools dictionary
        /// </summary>
        private async Task AddPluginToolAsync(ITool tool, ILogger logger)
        {
            if (!tool.IsEnabled || PluginFactories == null)
                return;

            // Find matching plugin factory by StaticId
            var pluginFactory = PluginFactories.FirstOrDefault(pf =>
                pf.GetPluginInfo().StaticId == tool.StaticId);

            if (pluginFactory != null)
            {
                try
                {
                    // Get all tools to determine if there are multiple instances
                    var allTools = await toolService.GetAllToolsAsync();
                    var toolsWithSameStaticId = allTools.Where(t => t.StaticId == tool.StaticId).ToList();
                    var hasMultipleInstances = toolsWithSameStaticId.Count > 1;

                    // Create PlugInstanceInfo from LlmTool data
                    var plugInstanceInfo = new PlugToolInfo
                    {
                        InstanceId = tool.Id,
                        Description = tool.Description,
                        Config = tool.ToolConfig,
                        DisplayName = tool.Name, // Include display name from database
                        Status = tool.StateData,
                        HasMultipleInstances = hasMultipleInstances
                    };

                    // Create plugin instance
                    var pluginInstance = await pluginFactory.CreatePluginToolAsync(plugInstanceInfo);
                    PluginTools![tool.Id] = pluginInstance;

                    logger.LogInformation("Added plugin instance: {ToolName} (StaticId: {StaticId}, Id: {ToolId}, HasMultipleInstances: {HasMultipleInstances})",
                        tool.Name, tool.StaticId, tool.Id, hasMultipleInstances);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to add plugin instance for tool: {ToolName} (StaticId: {StaticId})",
                        tool.Name, tool.StaticId);
                }
            }
            else
            {
                logger.LogWarning("No plugin factory found for tool: {ToolName} (StaticId: {StaticId})",
                    tool.Name, tool.StaticId);
            }
        }

        /// <summary>
        /// Update a plugin tool instance in PluginTools dictionary
        /// </summary>
        private async Task UpdatePluginToolAsync(ITool tool, ILogger logger)
        {
            if (PluginTools!.ContainsKey(tool.Id))
            {
                // If tool is disabled, remove it
                if (!tool.IsEnabled)
                {
                    await RemovePluginToolAsync(tool.Id, logger);
                    return;
                }

                // For updates, we need to dispose the old instance and create a new one
                // since the configuration might have changed
                try
                {
                    // Dispose old instance if it implements IDisposable
                    if (PluginTools[tool.Id] is IDisposable disposableInstance)
                    {
                        disposableInstance.Dispose();
                    }

                    // Remove old instance
                    PluginTools.Remove(tool.Id);

                    // Add updated instance
                    await AddPluginToolAsync(tool, logger);

                    logger.LogInformation("Updated plugin instance: {ToolName} (Id: {ToolId})", tool.Name, tool.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to update plugin instance for tool: {ToolName} (Id: {ToolId})",
                        tool.Name, tool.Id);
                }
            }
            else if (tool.IsEnabled)
            {
                // If tool wasn't in PluginTools but is now enabled, add it
                await AddPluginToolAsync(tool, logger);
            }
        }

        /// <summary>
        /// Remove a plugin tool instance from PluginTools dictionary
        /// </summary>
        private async Task RemovePluginToolAsync(long toolId, ILogger logger)
        {
            if (PluginTools!.ContainsKey(toolId))
            {
                try
                {
                    // Dispose the instance if it implements IDisposable
                    if (PluginTools[toolId] is IDisposable disposableInstance)
                    {
                        disposableInstance.Dispose();
                    }

                    PluginTools.Remove(toolId);
                    logger.LogInformation("Removed plugin instance for tool ID: {ToolId}", toolId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to remove plugin instance for tool ID: {ToolId}", toolId);
                }
            }

            await Task.CompletedTask; // Make method async for consistency
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            // Dispose plugin loaders to properly unload assemblies
            foreach (var loader in _plugins)
            {
                try
                {
                    loader?.Dispose();
                }
                catch (Exception ex)
                {
                    var logger = this.logger;
                    logger.LogError(ex, "Error disposing plugin loader");
                }
            }
            _plugins.Clear();

            // Dispose and clear plugin instances
            if (PluginTools != null)
            {
                foreach (var plugin in PluginTools.Values)
                {
                    try
                    {
                        plugin?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        var logger = this.logger;
                        logger.LogError(ex, "Error disposing plugin instance");
                    }
                }
                PluginTools.Clear();
            }

            // Clear plugin factories list
            PluginFactories?.Clear();

            // Unsubscribe from events
            if (toolService != null)
            {
                toolService.ToolChanged -= OnToolChanged;
                toolService.ToolListUpdated -= OnToolListUpdated;
            }
        }

        #endregion
    }
}