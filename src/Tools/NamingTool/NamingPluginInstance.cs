using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NamingTool.Properties;
using Serilog;
using DaoStudio.Interfaces.Plugins;
using Naming.Types;

namespace Naming
{
    /// <summary>
    /// Plugin instance that implements IPlugin interface for handling individual plugin sessions
    /// and contains the actual tool methods that can be called by the LLM
    /// </summary>
    internal class NamingPluginInstance : BasePlugin<NamingConfig>
    {
        private readonly IHost _host;
        private readonly Dictionary<IHostSession, NamingHandler> _sessionHandlers;

        public NamingPluginInstance(IHost host, PlugToolInfo plugInstanceInfo) : base(plugInstanceInfo)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _sessionHandlers = new Dictionary<IHostSession, NamingHandler>();
        }

        protected override NamingConfig ValidateConfiguration(NamingConfig config)
        {
            // Normalize legacy configs: if sentinel flag was persisted, treat it as null (use current session)
            try
            {
                if (config?.ExecutivePerson?.UseCurrentSession == true)
                {
                    config.ExecutivePerson = null;
                }
            }
            catch
            {
                // Ignore normalization errors and return config as-is
            }
            return config ?? CreateDefaultConfiguration();
        } 
        /// <summary>
        /// Update the plugin configuration at runtime
        /// </summary>
        /// <param name="newConfig">The new configuration object</param>
        public override void UpdateConfig(NamingConfig newConfig)
        {
            base.UpdateConfig(newConfig);
        }

        protected override async Task RegisterToolFunctionsAsync(List<FunctionWithDescription> toolcallFunctions,
            IHostPerson? person, IHostSession? hostSession)
        {
            if (_config == null)
            {
                // Should not happen; base ensures config exists
                Log.Error("Naming plugin configuration is null; cannot register functions.");
                throw new InvalidOperationException("Naming plugin configuration is null.");
            }

            // Check recursion level before adding tools
            bool shouldAddTools = true;
            try
            {
                if (hostSession != null && _config.MaxRecursionLevel >= 0)
                {
                    var currentLevel = await NamingLevelCalculator.CalculateCurrentLevelAsync(hostSession, _host);
                    if (currentLevel >= _config.MaxRecursionLevel)
                    {
                        shouldAddTools = false; // Don't add delegation tools at or above max level when limit is non-negative
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail - fall back to allowing delegation
                // This ensures robustness if level calculation fails
                Log.Error(ex, "Error calculating recursion level");
            }

            if (shouldAddTools)
            {
                // Create a new handler for this session
                var handler = new NamingHandler(_host, _config, hostSession);
                if (hostSession!=null)
                {
                    _sessionHandlers[hostSession] = handler;
                }

                // Create tool functions from the handler instance and add them to the list
                var toolFunctions = IPluginExtensions.CreateFunctionsFromToolMethods(handler, "Naming");
                
                // Update function metadata from configuration (name, description, params)
                await UpdateFunctionDescriptions(toolFunctions, _config);
                
                toolcallFunctions.AddRange(toolFunctions);
            }
            // If shouldAddTools is false, we simply don't add any tools - the LLM won't have access to create_subtask
        }
        
        
        private async Task UpdateFunctionDescriptions(List<FunctionWithDescription> toolFunctions, NamingConfig config)
        {
            try
            {
                // Find the Naming function dynamically – first by underlying method name, then by configured name
                var NamingFunction = toolFunctions.FirstOrDefault(f => f.Function.Method.Name == nameof(NamingHandler.Naming))
                                   ?? toolFunctions.FirstOrDefault(f => f.Description.Name.Equals(config.FunctionName, StringComparison.OrdinalIgnoreCase));
                if (NamingFunction != null)
                {
                    // Update function name and description from config
                    NamingFunction.Description.Name = config.FunctionName;
                    
                    NamingFunction.Description.Description = config.FunctionDescription;

                    // Clear default parameter (requestData) – we only want parameters defined in configuration
                    NamingFunction.Description.Parameters.Clear();

                    // Add parameters from configuration if any are defined
                    if (config.InputParameters != null && config.InputParameters.Count > 0)
                    {
                        foreach (var paramConfig in config.InputParameters)
                        {
                            NamingFunction.Description.Parameters.Add(ParameterConfigConverter.ConvertToMetadata(paramConfig));
                        }
                    }

                    // Fill ReturnParameter with return type information from configuration
                    if (config.ReturnParameters != null && config.ReturnParameters.Count > 0)
                    {
                        // For multiple return parameters, create a composite object type
                        if (config.ReturnParameters.Count == 1)
                        {
                            NamingFunction.Description.ReturnParameter = ParameterConfigConverter.ConvertToMetadata(config.ReturnParameters[0]);
                        }
                        else
                        {
                            // Multiple return parameters - create an object type with properties
                            var objectProperties = new Dictionary<string, FunctionTypeMetadata>();
                            foreach (var returnParam in config.ReturnParameters)
                            {
                                objectProperties[returnParam.Name] = ParameterConfigConverter.ConvertToMetadata(returnParam);
                            }
                            
                            NamingFunction.Description.ReturnParameter = new FunctionTypeMetadata
                            {
                                Name = "result",
                                Description = "Result object containing multiple return values",
                                ParameterType = typeof(object),
                                IsRequired = true,
                                DefaultValue = null,
                                ObjectProperties = objectProperties
                            };
                        }
                    }
                    else
                    {
                        // Default return type if no specific return parameters are configured
                        NamingFunction.Description.ReturnParameter = new FunctionTypeMetadata
                        {
                            Name = "result",
                            Description = "Task completion result",
                            ParameterType = typeof(string),
                            IsRequired = true,
                            DefaultValue = null
                        };
                    }
                }
            }
            catch
            {
                // If we can't get persons info, just use the default description
            }
        }

        /// <summary>
        /// Converts a <see cref="ParameterConfig"/> into the runtime <see cref="Type"/> representation.
        /// Exposed for unit testing via reflection to verify complex parameter shapes.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="paramConfig"/> is null.</exception>
        private static Type ConvertParameterConfigToType(ParameterConfig paramConfig)
        {
            if (paramConfig == null)
            {
                throw new ArgumentNullException(nameof(paramConfig));
            }

            var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);
            return metadata.ParameterType;
        }

        protected override async Task<byte[]?> OnSessionCloseAsync(IHostSession hostSession)
        {
            // Remove the handler for this session if it exists
            if (_sessionHandlers.ContainsKey(hostSession))
            {
                _sessionHandlers.Remove(hostSession);
            }
            return await Task.FromResult<byte[]?>(null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sessionHandlers.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
