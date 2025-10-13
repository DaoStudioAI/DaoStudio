using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NamingTool.Properties;
using Serilog;
using DaoStudio.Interfaces.Plugins;

namespace Naming
{
    /// <summary>
    /// Main plugin implementation that manages the plugin lifecycle
    /// </summary>
    public class NamingPluginFactory : IPluginFactory, IPluginConfigAvalonia
    {
        private IHost? _host;

        // Dictionary to track plugin instances by their InstanceId
        private readonly ConcurrentDictionary<long, List<WeakReference<NamingPluginInstance>>> _instanceRegistry
            = new ConcurrentDictionary<long, List<WeakReference<NamingPluginInstance>>>();

        /// <summary>
        /// Shows an error dialog with the specified title and message
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The error message</param>
        /// <param name="parent">Optional parent window</param>
        /// <returns>A task that completes when the dialog is closed</returns>
        private async Task ShowErrorAsync(string title, string message, Window? parent = null)
        {
            await ShowDialogAsync(title, message, Resources.OK, null, null, parent);
        }

        /// <summary>
        /// Shows an error dialog for an exception
        /// </summary>
        /// <param name="ex">The exception to display</param>
        /// <param name="title">Optional title (defaults to "Error")</param>
        /// <param name="parent">Optional parent window</param>
        /// <returns>A task that completes when the dialog is closed</returns>
        private async Task ShowExceptionAsync(Exception ex, string? title = null, Window? parent = null)
        {
            await ShowErrorAsync(title ?? Resources.Error, ex.Message, parent);
        }

        /// <summary>
        /// Shows a dialog with customizable buttons and content
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="content">The dialog content</param>
        /// <param name="closeButtonText">Text for the close/cancel button</param>
        /// <param name="primaryButtonText">Optional text for the primary button</param>
        /// <param name="secondaryButtonText">Optional text for the secondary button</param>
        /// <param name="parent">Optional parent window</param>
        /// <returns>The dialog result</returns>
        private async Task<ContentDialogResult> ShowDialogAsync(
            string title,
            object content,
            string closeButtonText,
            string? primaryButtonText = null,
            string? secondaryButtonText = null,
            Window? parent = null)
        {
            ContentDialogResult result = ContentDialogResult.None;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    var dialog = new ContentDialog
                    {
                        Title = title,
                        Content = content,
                        CloseButtonText = closeButtonText
                    };

                    if (!string.IsNullOrWhiteSpace(primaryButtonText))
                    {
                        dialog.PrimaryButtonText = primaryButtonText;
                        dialog.DefaultButton = ContentDialogButton.Primary;
                    }

                    if (!string.IsNullOrWhiteSpace(secondaryButtonText))
                    {
                        dialog.SecondaryButtonText = secondaryButtonText;
                    }

                    // Set parent window if provided to properly position dialog
                    if (parent != null)
                    {
                        // In Avalonia, we need to use the ShowAsync overload that takes a parent window
                        result = await dialog.ShowAsync(parent);
                    }
                    else
                    {
                        // Use the parameterless ShowAsync when no parent is provided
                        result = await dialog.ShowAsync();
                    }
                }
                catch (Exception ex)
                {
                    // Log any errors that occur while showing dialog
                    Log.Error(ex, "Error showing dialog");
                }
            });

            return result;
        }

        /// <summary>
        /// Set the host instance for accessing personas and starting new sessions
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
                StaticId = "Com.DaoStudio.Naming",
                Version = "1.0",
                DisplayName = Resources.NamingDisplayName
            };
        }

        /// <summary>
        /// Create a new plugin instance with default configuration
        /// </summary>
        /// <param name="instanceid">The instance ID</param>
        /// <returns>Plugin instance information</returns>
        public Task<PlugToolInfo> CreateToolConfigAsync(long instanceid)
        {
            var config = new NamingConfig();

            config.UrgingMessage = string.Format(Resources.Message_FinalizeTaskReminder, config.ReturnToolName);

            var instanceInfo = new PlugToolInfo
            {
                Description = Resources.DelegateSubtasks,
                Config = JsonSerializer.Serialize(config),
                DisplayName = GetPluginInfo().DisplayName, // Set from plugin info
                SupportConfigWindow = true
            };

            return Task.FromResult(instanceInfo);
        }

        /// <summary>
        /// Fix the ConfigInstance method signature to match IPluginConfigAvalonia interface
        /// </summary>
        /// <param name="win">Parent window</param>
        /// <param name="plugInstanceInfo">Plugin instance information</param>
        /// <returns>Updated plugin instance information</returns>
        public async Task<PlugToolInfo> ConfigInstance(Window win, PlugToolInfo plugInstanceInfo)
        {
            while (true)
            {
                try
                {
                    // Get all available persons from the host
                    var availablePersons = new List<IHostPerson>();
                    if (_host != null)
                    {
                        try
                        {
                            availablePersons = await _host.GetHostPersonsAsync(null);
                        }
                        catch (Exception ex)
                        {
                            // Show exception to user with custom title
                            await ShowExceptionAsync(ex, Resources.Error, win);
                            return plugInstanceInfo; // Return original config
                        }
                    }

                    // Determine which configuration dialog to show
                    bool useSimpleMode = true;
                    NamingConfig? currentConfig = null;
                    if (!string.IsNullOrEmpty(plugInstanceInfo.Config))
                    {
                        try
                        {
                            currentConfig = JsonSerializer.Deserialize<NamingConfig>(plugInstanceInfo.Config);
                            if (currentConfig != null)
                            {
                                useSimpleMode = currentConfig.UseSimpleConfigMode;
                            }
                        }
                        catch { /* Use default simple mode if deserialization fails */ }
                    }

                    // Show the appropriate dialog
                    Window dialog;
                    if (useSimpleMode)
                    {
                        dialog = new NamingConfigWindow(plugInstanceInfo, availablePersons);
                    }
                    else
                    {
                        dialog = new Naming.AdvConfig.AdvancedNamingConfigWindow(plugInstanceInfo, availablePersons);
                    }

                    var dialogResult = await dialog.ShowDialog<object>(win);

                    // Handle mode switching
                    if (dialogResult is string signal)
                    {
                        if (signal == "ADVANCED_MODE" || signal == "SIMPLE_MODE")
                        {
                            // The dialog closed to signal a mode switch.
                            // Update the config and loop again to show the correct dialog.
                            if (dialog is NamingConfigWindow simple)
                            {
                                plugInstanceInfo.Config = simple.Result;
                                plugInstanceInfo.DisplayName = simple.DisplayNameResult;
                            }
                            else if (dialog is Naming.AdvConfig.AdvancedNamingConfigWindow advanced)
                            {
                                plugInstanceInfo.Config = advanced.Result;
                                plugInstanceInfo.DisplayName = advanced.DisplayNameResult;
                            }
                            continue; // Re-run the loop to show the other dialog
                        }
                    }

                    // Handle normal save/close
                    if (dialogResult is bool and true)
                    {
                        // User saved, update the configuration
                        if (dialog is NamingConfigWindow simple)
                        {
                            plugInstanceInfo.Config = simple.Result;
                            plugInstanceInfo.DisplayName = simple.DisplayNameResult;
                        }
                        else if (dialog is Naming.AdvConfig.AdvancedNamingConfigWindow advanced)
                        {
                            plugInstanceInfo.Config = advanced.Result;
                            plugInstanceInfo.DisplayName = advanced.DisplayNameResult;
                        }

                        // Sync config to all instances
                        if (!string.IsNullOrEmpty(plugInstanceInfo.Config))
                        {
                            var newConfig = JsonSerializer.Deserialize<NamingConfig>(plugInstanceInfo.Config);
                            if (newConfig != null)
                            {
                                SyncConfigToInstances(plugInstanceInfo.InstanceId, newConfig);
                                plugInstanceInfo.Description = Resources.DelegateSubtasks + ": " + newConfig.FunctionName;
                            }
                        }
                    }

                    // Return the (potentially updated) config
                    return plugInstanceInfo;
                }
                catch (Exception ex)
                {
                    await ShowExceptionAsync(ex, Resources.ConfigurationError, win);
                    return plugInstanceInfo; // Return original config on error
                }
            }
        }

        /// <summary>
        /// Register a plugin instance for configuration synchronization
        /// </summary>
        /// <param name="instanceId">The instance ID</param>
        /// <param name="instance">The plugin instance</param>
        private void RegisterInstance(long instanceId, NamingPluginInstance instance)
        {
            _instanceRegistry.AddOrUpdate(
                instanceId,
                new List<WeakReference<NamingPluginInstance>> { new WeakReference<NamingPluginInstance>(instance) },
                (key, existing) =>
                {
                    // Clean up dead references before adding new one
                    var aliveRefs = existing.Where(wr => wr.TryGetTarget(out _)).ToList();
                    aliveRefs.Add(new WeakReference<NamingPluginInstance>(instance));
                    return aliveRefs;
                });
        }

        /// <summary>
        /// Sync configuration changes to all existing instances with the specified instance ID
        /// </summary>
        /// <param name="instanceId">The instance ID</param>
        /// <param name="newConfig">The new configuration object</param>
        private void SyncConfigToInstances(long instanceId, NamingConfig newConfig)
        {
            if (_instanceRegistry.TryGetValue(instanceId, out var instanceRefs))
            {
                var aliveRefs = new List<WeakReference<NamingPluginInstance>>();

                foreach (var weakRef in instanceRefs)
                {
                    if (weakRef.TryGetTarget(out var instance))
                    {
                        // Update the configuration
                        instance.UpdateConfig(newConfig);
                        aliveRefs.Add(weakRef);
                    }
                    // Dead references will be automatically excluded
                }

                // Update the registry with only alive references
                if (aliveRefs.Count > 0)
                {
                    _instanceRegistry.TryUpdate(instanceId, aliveRefs, instanceRefs);
                }
                else
                {
                    // Remove the entry if no instances are alive
                    _instanceRegistry.TryRemove(instanceId, out _);
                }
            }
        }

        /// <summary>
        /// Create a new plugin instance
        /// </summary>
        /// <returns>A new plugin instance</returns>
        public async Task<IPluginTool> CreatePluginToolAsync(PlugToolInfo plugInstanceInfo)
        {
            if (_host == null)
                throw new InvalidOperationException(Resources.Error_HostMustBeSet);

            var plugin = new NamingPluginInstance(_host, plugInstanceInfo);

            // Register the instance for configuration synchronization
            RegisterInstance(plugInstanceInfo.InstanceId, plugin);

            await Task.CompletedTask;
            return plugin;
        }
        /// <summary>
        /// Delete a plugin instance and clean up all associated sessions
        /// </summary>
        /// <param name="plugInstanceInfo">The plugin instance information</param>
        public async Task DeleteToolConfigAsync(PlugToolInfo plugInstanceInfo)
        {
            // Clean up the instance registry for this instance ID
            _instanceRegistry.TryRemove(plugInstanceInfo.InstanceId, out _);

            await Task.CompletedTask;
        }
    }
}