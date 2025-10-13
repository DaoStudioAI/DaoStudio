using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace DaoStudio.Common.Plugins
{
    /// <summary>
    /// Abstract base class that provides common functionality for IPlugin implementations.
    /// This helper class handles standard plugin patterns like configuration parsing,
    /// session management, and proper disposal.
    /// </summary>
    /// <typeparam name="TConfig">The type of configuration object this plugin uses</typeparam>
    public abstract class BasePlugin<TConfig> : IPluginTool, IDisposable
        where TConfig : class, new()
    {
        protected readonly PlugToolInfo _plugInstanceInfo;
        protected TConfig _config;
        protected bool _disposed;

        /// <summary>
        /// Initializes a new instance of the BasePlugin class.
        /// </summary>
        /// <param name="plugInstanceInfo">The plugin instance information containing configuration and metadata</param>
        protected BasePlugin(PlugToolInfo plugInstanceInfo)
        {
            _plugInstanceInfo = plugInstanceInfo ?? throw new ArgumentNullException(nameof(plugInstanceInfo));
            _config = ParseConfiguration();
        }

        /// <summary>
        /// Parses the configuration from the plugin instance info JSON.
        /// Override this method to provide custom parsing logic or validation.
        /// </summary>
        /// <returns>The parsed configuration object, or a new default instance if parsing fails</returns>
        protected virtual TConfig ParseConfiguration()
        {
            if (!string.IsNullOrEmpty(_plugInstanceInfo.Config))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<TConfig>(_plugInstanceInfo.Config);
                    if (parsed != null)
                    {
                        return ValidateConfiguration(parsed);
                    }
                }
                catch (JsonException)
                {
                    // Ignore JSON parsing errors and fall back to default
                }
            }

            return CreateDefaultConfiguration();
        }

        /// <summary>
        /// Creates a default configuration instance.
        /// Override this method to provide custom default values.
        /// </summary>
        /// <returns>A new default configuration instance</returns>
        protected virtual TConfig CreateDefaultConfiguration()
        {
            return new TConfig();
        }

        /// <summary>
        /// Validates and potentially modifies the parsed configuration.
        /// Override this method to handle backward compatibility or configuration validation.
        /// </summary>
        /// <param name="config">The parsed configuration to validate</param>
        /// <returns>The validated (and potentially modified) configuration</returns>
        protected virtual TConfig ValidateConfiguration(TConfig config)
        {
            return config;
        }

        /// <summary>
        /// Updates the plugin configuration at runtime.
        /// </summary>
        /// <param name="newConfig">The new configuration object</param>
        public virtual void UpdateConfig(TConfig newConfig)
        {
            if (newConfig != null)
            {
                _config = ValidateConfiguration(newConfig);
                // Update the stored config in the plugin instance info
                _plugInstanceInfo.Config = JsonSerializer.Serialize(_config);
            }
        }

        /// <summary>
        /// Gets the current configuration object.
        /// </summary>
        public TConfig Config => _config;

        /// <summary>
        /// Gets the plugin instance information.
        /// </summary>
        public PlugToolInfo PlugInstanceInfo => _plugInstanceInfo;

        /// <summary>
        /// Abstract method for registering tool functions specific to this plugin.
        /// Derived classes must implement this to define their tool capabilities.
        /// </summary>
        /// <param name="toolcallFunctions">The collection to add function descriptions to</param>
        /// <param name="person">The person context for the session</param>
        /// <param name="hostSession">The host session (null when called from GUI tool tab)</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected abstract Task RegisterToolFunctionsAsync(List<FunctionWithDescription> toolcallFunctions, IHostPerson? person, IHostSession? hostSession);

        /// <summary>
        /// Called when a session starts. Override this method to perform session-specific initialization.
        /// </summary>
        /// <param name="person">The person context for the session</param>
        /// <param name="hostSession">The host session (null when called from GUI tool tab)</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected virtual Task OnSessionStartAsync(IHostPerson? person, IHostSession? hostSession)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a session closes. Override this method to perform session-specific cleanup.
        /// </summary>
        /// <param name="hostSession">The host session that is closing</param>
        /// <returns>A task representing the asynchronous operation, optionally returning session data</returns>
        protected virtual Task<byte[]?> OnSessionCloseAsync(IHostSession hostSession)
        {
            return Task.FromResult<byte[]?>(null);
        }

        #region IPlugin Implementation

        /// <summary>
        /// Implements the IPlugin.GetSessionFunctionsAsync method.
        /// This method handles configuration parsing, session initialization, and function registration.
        /// </summary>
        public async Task GetSessionFunctionsAsync(List<FunctionWithDescription> toolcallFunctions, IHostPerson? person, IHostSession? hostSession)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);

            // Call session start hook (hostSession is null when called from GUI tool tab)
            await OnSessionStartAsync(person, hostSession);

            // Register tool functions
            await RegisterToolFunctionsAsync(toolcallFunctions, person, hostSession);
        }

        /// <summary>
        /// Implements the IPlugin.CloseSessionAsync method.
        /// This method calls the session close hook for cleanup.
        /// </summary>
        public async Task<byte[]?> CloseSessionAsync(IHostSession hostSession)
        {
            if (_disposed)
                return null;

            return await OnSessionCloseAsync(hostSession);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases the resources used by the plugin.
        /// Override this method to dispose of custom resources, but make sure to call base.Dispose(disposing).
        /// </summary>
        /// <param name="disposing">true if disposing managed resources; otherwise, false</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources in derived classes
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    /// <summary>
    /// Base plugin class for plugins that don't require a specific configuration type.
    /// Uses a generic object as the configuration.
    /// </summary>
    public abstract class BasePlugin : BasePlugin<object>
    {
        /// <summary>
        /// Initializes a new instance of the BasePlugin class.
        /// </summary>
        /// <param name="plugInstanceInfo">The plugin instance information</param>
        protected BasePlugin(PlugToolInfo plugInstanceInfo) : base(plugInstanceInfo)
        {
        }
    }
}
