using DaoStudioUI.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DesktopUI.Resources;
using FluentAvalonia.UI.Controls;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DaoStudioUI.ViewModels
{
    public partial class ToolsViewModel : ObservableObject
    {        // Collections
        [ObservableProperty]
        private ObservableCollection<Models.ToolItem> _tools = new();

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private ObservableCollection<ModelItem> _models = new();


        [ObservableProperty]
        private ModelItem? _modelSelectorSelectedItem;

        private readonly IToolService _toolService;
        private readonly IPeopleService _peopleService;
        private readonly IPluginService _pluginService;

        public ToolsViewModel(IToolService toolService, IPeopleService peopleService, IPluginService pluginService)
        {
            _toolService = toolService;
            _peopleService = peopleService;
            _pluginService = pluginService;
            
            // Load data
            Task.Run(() => LoadDataAsync());
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            if (IsLoading) return;

            IsLoading = true;

            try
            {
                // Load tools using DaoStudio method
                var tools = await _toolService.GetAllToolsAsync();

                // Load all people for the model selector
                var people = await _peopleService.GetAllPeopleAsync();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Models.Clear();
                    foreach (var person in people)
                    {
                        Models.Add(new ModelItem
                        {
                            Id = person.Id,
                            Name = person.Name,
                        });
                    }
                    Tools.Clear();
                    foreach (var tool in tools)
                    {
                        Tools.Add(DaoStudioUI.Models.ToolItem.FromTool(tool));
                    }

                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                // Handle errors
                Log.Error(ex, "Error loading tools");
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void AddTool(Visual? anchor = null)
        {
            // Create a menu with plugin options
            var menu = new MenuFlyout();

            if (_pluginService.PluginFactories != null)
            {
                foreach (var plugin in _pluginService.PluginFactories)
                {
                    var plugInfo = plugin.GetPluginInfo();
                    var item = new MenuItem { Header = plugInfo.DisplayName ?? Strings.Tools_DefaultPluginName };
                    item.Click += async (s, e) => await AddToolFromPlugin(plugin);
                    menu.Items.Add(item);
                }
            }


            // Show the menu below the button
            if (anchor is Control control)
            {
                menu.ShowAt(control);
            }
        }
        [RelayCommand]
        private void SaveTool(DaoStudioUI.Models.ToolItem tool)
        {
            if (tool != null)
            {
                Task.Run(async () =>
                {
                    await SaveToolAsync(tool);
                    tool.HasChanges = false;
                });
            }
        }
        private async Task SaveToolAsync(DaoStudioUI.Models.ToolItem tool)
        {
            if (tool == null)
            {
                Log.Warning("SaveToolAsync called with null tool");
                return;
            }

            try
            {
                if (tool.Id == 0)
                {
                    // Create new tool using parameter-based method
                    Log.Debug("Creating new tool: {ToolName}", tool.Name);
                    
                    var createdTool = await _toolService.CreateToolAsync(
                        name: tool.Name ?? string.Empty,
                        description: tool.Description ?? string.Empty,
                        staticId: tool.StaticId ?? string.Empty,
                        toolConfig: tool.ToolConfig ?? string.Empty,
                        parameters: tool.Parameters ?? new Dictionary<string, string>(),
                        isEnabled: tool.IsEnabled,
                        appId: tool.AppId);
                    
                    tool.Id = createdTool.Id;
                    Log.Information("Successfully created tool {ToolName} with ID {ToolId}", tool.Name, tool.Id);
                }
                else
                {
                    // Update existing tool - get the existing tool first and then update it
                    Log.Debug("Updating existing tool: {ToolName} (ID: {ToolId})", tool.Name, tool.Id);
                    
                    var existingTool = await _toolService.GetToolAsync(tool.Id);
                    if (existingTool == null)
                    {
                        Log.Warning("Tool with ID {ToolId} not found for update", tool.Id);
                        throw new InvalidOperationException($"Tool with ID {tool.Id} not found");
                    }
                    
                    // Update the existing tool properties
                    existingTool.Name = tool.Name ?? string.Empty;
                    existingTool.Description = tool.Description ?? string.Empty;
                    existingTool.ToolConfig = tool.ToolConfig ?? string.Empty;
                    existingTool.Parameters = tool.Parameters ?? new Dictionary<string, string>();
                    existingTool.IsEnabled = tool.IsEnabled;
                    
                    var success = await _toolService.UpdateToolAsync(existingTool);
                    if (!success)
                    {
                        Log.Warning("Failed to update tool {ToolName} (ID: {ToolId})", tool.Name, tool.Id);
                    }
                    else
                    {
                        Log.Information("Successfully updated tool {ToolName} (ID: {ToolId})", tool.Name, tool.Id);
                    }
                }
            }
            catch (ArgumentException argEx)
            {
                Log.Error(argEx, "Invalid arguments when saving tool {ToolName}: {Error}", tool.Name, argEx.Message);
                throw;
            }
            catch (InvalidOperationException opEx)
            {
                Log.Error(opEx, "Invalid operation when saving tool {ToolName}: {Error}", tool.Name, opEx.Message);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error saving tool {ToolName} (ID: {ToolId}): {Error}", tool.Name, tool.Id, ex.Message);
                throw;
            }
        }
        [RelayCommand]
        private async Task DeleteTool(DaoStudioUI.Models.ToolItem tool)
        {
            if (tool == null || IsLoading) return;

            // Show confirmation dialog
            var dialog = new ContentDialog
            {
                Title = Strings.Tools_ConfirmDeleteTool,
                Content = string.Format(System.Globalization.CultureInfo.CurrentCulture, 
                    Strings.Tools_ConfirmDeleteToolMessage, tool.Name),
                PrimaryButtonText = Strings.Settings_DeleteButton,
                CloseButtonText = Strings.Common_Cancel,
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await DeleteToolAsync(tool.Id);
                await LoadDataAsync();
            }
        }

        private async Task DeleteToolAsync(long id)
        {
            try
            {
                // Use DaoStudio method for delete
                await _toolService.DeleteToolAsync(id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting tool");
            }
        }
        [RelayCommand]
        private async Task ShowConfigDlg(DaoStudioUI.Models.ToolItem? tool)
        {
            if (tool == null)
            {
                Log.Debug("ShowConfigDlg called with null tool.");
                return;
            }

            IPluginFactory? targetPlugin = _pluginService.PluginFactories?.FirstOrDefault(p => p.GetPluginInfo().StaticId == tool.StaticId);

            if (targetPlugin == null)
            {
                Log.Warning("Plugin with StaticId {StaticId} not found for tool {ToolName}", tool.StaticId, tool.Name);
                await DialogService.ShowErrorAsync(Strings.Common_Error,
                                                   string.Format(Strings.Tools_PluginNotFound_Format, tool.Name, tool.StaticId),
                                                   null); // Pass null for parent window as owner is not yet determined here
                return;
            }

            try
            {
                Window? owner = null; // Owner is now declared outside all try blocks in this method section.

                try
                {
                    // Attempt to get the owner window
                    if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktopApp)
                    {
                        owner = desktopApp.MainWindow;
                    }

                    if (owner == null)
                    {
                        Log.Error("Cannot show config dialog: Main window not found.");
                        await DialogService.ShowErrorAsync(Strings.Common_Error, "Cannot display configuration: main window not found.", null);
                        return;
                    }

                    // This nested try-catch handles the plugin-specific interaction
                    try
                    {
                        var originalConfig = tool.ToolConfig;
                        var originalDescription = tool.Description;
                        var uiplug = targetPlugin as IPluginConfigAvalonia;

                        if (uiplug == null)
                        {
                            throw new NotImplementedException("Plugin does not implement IPluginConfigAvalonia");
                        }

                        // Determine if there are multiple instances for this plugin
                        var toolsWithSameStaticId = Tools.Where(t => t.StaticId == tool.StaticId).Count();
                        var hasMultipleInstances = toolsWithSameStaticId > 1;

                        var plugInstanceInfo = new PlugToolInfo
                        {
                            InstanceId = tool.Id,
                            Description = originalDescription,
                            Config = originalConfig,
                            DisplayName = tool.Name, // Include current display name
                            Status = null, // Initialize as null, can be set by the plugin
                            HasMultipleInstances = hasMultipleInstances
                        };

                        PlugToolInfo? resultInfo = await uiplug.ConfigInstance(owner, plugInstanceInfo);

                        if (resultInfo != null)
                        {
                            bool updateNeeded = false;
                            if (resultInfo.Config != null && originalConfig != resultInfo.Config)
                            {
                                tool.ToolConfig = resultInfo.Config;
                                updateNeeded = true;
                            }
                            if (resultInfo.Description != null && originalDescription != resultInfo.Description)
                            {
                                tool.Description = resultInfo.Description;
                                updateNeeded = true;
                            }
                            if (resultInfo.DisplayName != null && tool.Name != resultInfo.DisplayName)
                            {
                                tool.Name = resultInfo.DisplayName;
                                updateNeeded = true;
                            }
                            if (updateNeeded)
                            {
                                await SaveToolAsync(tool);
                            }
                        }
                    }
                    catch (NotImplementedException nie)
                    {
                        Log.Warning(nie, "Plugin {PluginStaticId} ({ToolName}) has not implemented ConfigInstance.", targetPlugin.GetPluginInfo().StaticId, tool.Name);
                        await DialogService.ShowErrorAsync(Strings.Tools_FeatureNotAvailable_Title,
                                                           string.Format(Strings.Tools_ConfigNotImplemented_Format, tool.Name),
                                                           owner); // owner is in scope
                    }
                    catch (Exception ex) // Catches exceptions from ConfigInstance or subsequent logic within plugin interaction
                    {
                        Log.Error(ex, "Error during plugin configuration for tool {ToolName} (Plugin {PluginStaticId})", tool.Name, targetPlugin.GetPluginInfo().StaticId);
                        await DialogService.ShowErrorAsync(Strings.Common_Error,
                                                           string.Format(Strings.Tools_ConfigError_Format, tool.Name, ex.Message),
                                                           owner); // owner is in scope
                    }
                }
                catch (Exception ex) // Catches general errors in ShowConfigDlg, including owner initialization issues or other unexpected errors
                {
                    Log.Error(ex, "Overall error in ShowConfigDlg for tool {ToolName} (Plugin {PluginStaticId})", tool.Name, targetPlugin.GetPluginInfo().StaticId);
                    string errorMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                                                       Strings.Tools_ConfigGeneralError_Format ?? "A general error occurred while showing tool configuration for '{0}': {1}",
                                                       tool.Name, ex.Message);
                    // owner might be null if the error occurred during its initialization or before plugin interaction try-block
                    await DialogService.ShowErrorAsync(Strings.Common_Error, errorMessage, owner);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in ShowConfigDlg for tool {ToolName} (Plugin {PluginStaticId})", tool.Name, targetPlugin?.GetPluginInfo().StaticId);
            }
        }

        [RelayCommand]
        private async Task AddToolFromPlugin(IPluginFactory plugin)
        {
            if (plugin == null)
            {
                Log.Warning("AddToolFromPlugin called with null plugin");
                return;
            }

            ITool? createdTool = null;
            
            try
            {
                var plugInfo = plugin.GetPluginInfo();
                if (string.IsNullOrEmpty(plugInfo.StaticId))
                {
                    throw new InvalidOperationException("Plugin StaticId cannot be null or empty");
                }

                var displayName = plugInfo.DisplayName ?? $"Tool_{plugInfo.StaticId}";
                Log.Information("Adding tool from plugin: {PluginName} (StaticId: {StaticId})", displayName, plugInfo.StaticId);

                // Create tool with plugin configuration
                var instanceInfo = await plugin.CreateToolConfigAsync(0);
                if (instanceInfo == null)
                {
                    throw new InvalidOperationException($"Plugin {plugInfo.StaticId} returned null configuration");
                }

                var parameters = new Dictionary<string, string>();
                if (instanceInfo.SupportConfigWindow)
                {
                    parameters[DaoStudio.Constants.LlmToolParameterNames.ShowConfigWin] = bool.TrueString;
                }

                createdTool = await _toolService.CreateToolAsync(
                    name: instanceInfo.DisplayName ?? displayName,
                    description: instanceInfo.Description ?? Strings.Tools_Initializing,
                    staticId: plugInfo.StaticId,
                    toolConfig: instanceInfo.Config ?? string.Empty,
                    parameters: parameters,
                    isEnabled: true,
                    appId: 0);

                Log.Information("Successfully created tool {ToolName} (ID: {ToolId}) from plugin {PluginStaticId}", 
                    createdTool.Name, createdTool.Id, plugInfo.StaticId);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var toolItem = DaoStudioUI.Models.ToolItem.FromTool(createdTool);
                    Tools.Add(toolItem);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding tool from plugin {PluginStaticId}: {Error}", 
                    plugin.GetPluginInfo()?.StaticId ?? "Unknown", ex.Message);
                
                var errorMessage = string.Format(System.Globalization.CultureInfo.CurrentCulture, 
                    Strings.Tools_AddToolError, ex.Message);
                
                try
                {
                    var owner = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                        ? desktop.MainWindow : null;
                    await DialogService.ShowErrorAsync(Strings.Common_Error, errorMessage, owner);
                }
                catch (Exception dialogEx)
                {
                    Log.Error(dialogEx, "Failed to show error dialog");
                }
                
                // Cleanup if tool was partially created
                if (createdTool?.Id > 0)
                {
                    try
                    {
                        await _toolService.DeleteToolAsync(createdTool.Id);
                    }
                    catch (Exception cleanupEx)
                    {
                        Log.Warning(cleanupEx, "Failed to cleanup tool {ToolId}", createdTool.Id);
                    }
                }
            }
        }

        private async Task ShowPluginErrorDialog(string errorMessage)
        {
            try
            {
                var owner = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow : null;
                await DialogService.ShowErrorAsync(Strings.Common_Error, errorMessage, owner);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to show error dialog: {Error}", ex.Message);
            }
        }

        private async Task CleanupFailedTool(long toolId)
        {
            try
            {
                Log.Information("Cleaning up failed tool creation for ID: {ToolId}", toolId);
                await _toolService.DeleteToolAsync(toolId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to cleanup tool {ToolId} after creation failure: {Error}", toolId, ex.Message);
            }
        }
        [RelayCommand]
        private async Task EditToolName(DaoStudioUI.Models.ToolItem? toolItem)
        {
            if (toolItem == null || IsLoading) return;

            var dialog = new ContentDialog
            {
                Title = Strings.Tools_EditNameDialogTitle,
                PrimaryButtonText = Strings.Common_Save,
                CloseButtonText = Strings.Common_Cancel,
                DefaultButton = ContentDialogButton.Primary
            };

            var textBox = new TextBox
            {
                Text = toolItem.Name,
                Watermark = Strings.Tools_ToolNameWatermark,
                MinWidth = 300
            };

            dialog.Content = textBox;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                toolItem.Name = textBox.Text; // Triggers OnNameChanged for HasChanges and LastModified
                await SaveToolAsync(toolItem);
            }
        }

        // Get a model name from its ID for display        // Get a model name from its ID for display
        public string GetModelName(long modelId)
        {
            var model = Models.FirstOrDefault(m => m.Id == modelId);
            return model != null ? model.Name : string.Format(Strings.Tools_UnknownModel, modelId);
        }

        public partial class ModelItem : ObservableObject
        {
            [ObservableProperty]
            private long _id;

            [ObservableProperty]
            private string _name = string.Empty;
        }
    }
}