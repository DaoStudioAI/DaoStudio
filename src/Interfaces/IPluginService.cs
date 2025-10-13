using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.Interfaces.Plugins;

namespace DaoStudio.Interfaces
{

    /// <summary>
    /// Service interface for plugin management operations
    /// </summary>
    public interface IPluginService : IDisposable
    {
        #region Properties

        /// <summary>
        /// Collection of loaded plugin factories
        /// </summary>
        List<IPluginFactory>? PluginFactories { get; }

        /// <summary>
        /// Dictionary of active plugin tool instances keyed by tool ID
        /// </summary>
        Dictionary<long, IPluginTool>? PluginTools { get; }

        #endregion

        #region Plugin Management

        /// <summary>
        /// Load plugins dynamically from subfolders within a specified directory
        /// </summary>
        /// <param name="pluginFolderPath">Path to the plugins directory</param>
        /// <returns>List of loaded plugin factory objects</returns>
        Task<List<IPluginFactory>> LoadPluginsAsync(string pluginFolderPath);

        /// <summary>
        /// Initialize plugin instances from PluginFactories using LlmTool configurations
        /// ConfigInstance id is LlmTool.Id
        /// </summary>
        Task InitializeAsync();

        #endregion
    }
}