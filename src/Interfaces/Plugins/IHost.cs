using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaoStudio.Interfaces.Plugins
{
    public interface IHost
    {
        Task<List<IPerson>> GetPersonsAsync(string? name);//null for all persons, or specific person name
        Task<ISession> StartNewSession(long? parentSessId, string? personName = null);
        Task<ISession> OpenHostSession(long sessionId);

        /// <summary>
        /// Creates a new session and returns it as IHostSession for use by plugins
        /// </summary>
        /// <param name="parent">The parent session (can be IHostSession)</param>
        /// <param name="personName">Optional person name for the session</param>
        /// <returns>The created session wrapped as IHostSession</returns>
        Task<IHostSession> StartNewHostSessionAsync(IHostSession? parent, string? personName = null);

        /// <summary>
        /// Gets persons as IHostPerson for use by plugins
        /// </summary>
        /// <param name="name">The name filter (null for all persons)</param>
        /// <returns>List of persons wrapped as IHostPerson</returns>
        Task<List<IHostPerson>> GetHostPersonsAsync(string? name);

    /// <summary>
    /// Gets the plugin configuration folder path for the specified plugin id
    /// </summary>
    /// <param name="pluginId">The plugin identifier used to create a subfolder under the config folder</param>
    /// <returns>The path to the plugin configuration folder</returns>
    string GetPluginConfigureFolderPath(string pluginId);
    }
}
