using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using FluentAvalonia.UI.Controls;
using MessagePack;
using MessagePack.Resolvers;
using Naming;
using Naming.AdvConfig.Events;
using Naming.AdvConfig.Tabs;
using Naming.ParallelExecution;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Naming.AdvConfig
{
    /// <summary>
    /// Advanced configuration window for the Naming plugin with tabbed interface
    /// </summary>
    public partial class AdvancedNamingConfigWindow : Window, INotifyPropertyChanged
    {
        private readonly IConfigurationEventHub _eventHub;
        private List<IHostPerson> _availablePersons = new();
        private NamingConfig _originalConfig = new();
        private string _displayName = string.Empty;

        public string? Result { get; private set; }
        public string? DisplayNameResult { get; private set; }

        public AdvancedNamingConfigWindow()
        {
            _eventHub = new ConfigurationEventHub();
            InitializeComponent();
            InitializeEventHandlers();
            InitializeTabs();
            DataContext = this;
        }

        public AdvancedNamingConfigWindow(string configJson, List<IHostPerson> availablePersons) : this()
        {
            Initialize(configJson, availablePersons);
        }

        public AdvancedNamingConfigWindow(PlugToolInfo plugToolInfo, List<IHostPerson> availablePersons) : this()
        {
            Initialize(plugToolInfo, availablePersons);
        }

        #region Properties

        public string DisplayName
        {
            get => _displayName;
            set
            {
                _displayName = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Initialization

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeEventHandlers()
        {
            // Subscribe to configuration events
            _eventHub.ValidationRequested += OnValidationRequested;
            _eventHub.ExportRequested += OnExportRequested;
            _eventHub.ConfigurationRequested += OnConfigurationRequested;

            // Wire up button events
            var saveAndCloseButton = this.FindControl<Button>("SaveAndCloseButton");
            var cancelButton = this.FindControl<Button>("CancelButton");
            var importButton = this.FindControl<Button>("ImportButton");
            var exportButton = this.FindControl<Button>("ExportButton");
            var simpleModeButton = this.FindControl<Button>("SimpleModeButton");

            if (saveAndCloseButton != null)
                saveAndCloseButton.Click += OnSaveAndCloseClick;
            if (cancelButton != null)
                cancelButton.Click += OnCancelClick;
            if (importButton != null)
                importButton.Click += OnImportClick;
            if (exportButton != null)
                exportButton.Click += OnExportClick;
            if (simpleModeButton != null)
                simpleModeButton.Click += OnSimpleModeClick;

            // Get TabControl and add selection changed event handler
            var tabControl = this.FindControl<TabControl>("MainTabView");
            if (tabControl != null)
            {
                // Sync Base Information values whenever the user switches tabs
                tabControl.SelectionChanged += OnTabSelectionChanged;
            }
        }

        private void InitializeTabs()
        {
            // Base Information Tab
            var baseInfoTab = this.FindControl<BaseInfoTab>("BaseInfoTab");
            baseInfoTab?.Initialize(_eventHub);

            // Other tabs get at least an empty configuration so that their DataContext is ready
            var inputParametersTab = this.FindControl<InputParametersTab>("InputParametersTab");
            inputParametersTab?.Initialize(_eventHub, _originalConfig);

            var promptTemplateTab = this.FindControl<PromptTemplateTab>("PromptTemplateTab");
            promptTemplateTab?.Initialize(_eventHub, _originalConfig);

            var outputParametersTab = this.FindControl<OutputParametersTab>("OutputParametersTab");
            outputParametersTab?.Initialize(_eventHub, _originalConfig);

            var parallelConfigTab = this.FindControl<ParallelConfigTab>("ParallelConfigTab");
            parallelConfigTab?.Initialize(_eventHub, _originalConfig);

            var errorHandlingTab = this.FindControl<ErrorHandlingTab>("ErrorHandlingTab");
            errorHandlingTab?.Initialize(_eventHub, _originalConfig);
        }

        public void Initialize(string configJson, List<IHostPerson> availablePersons)
        {
            Initialize(new PlugToolInfo { Config = configJson }, availablePersons);
        }

        public void Initialize(PlugToolInfo plugToolInfo, List<IHostPerson> availablePersons)
        {
            _availablePersons = availablePersons ?? new List<IHostPerson>();
            DisplayName = plugToolInfo.DisplayName ?? "New Configuration";

            // Load configuration
            LoadConfiguration(plugToolInfo.Config ?? string.Empty);
            
            // Initialize tabs with configuration
            InitializeTabsWithConfig();
        }

        private void LoadConfiguration(string configJson)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(configJson))
                {
                    _originalConfig = JsonSerializer.Deserialize<NamingConfig>(configJson) ?? new NamingConfig();
                }
                else
                {
                    _originalConfig = new NamingConfig();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load configuration");
                _originalConfig = new NamingConfig();
            }
        }

        private void InitializeTabsWithConfig()
        {
            // Initialize BaseInfoTab with configuration
            var baseInfoTab = this.FindControl<BaseInfoTab>("BaseInfoTab");
            if (baseInfoTab?.ViewModel != null)
            {
                baseInfoTab.ViewModel.Initialize(_originalConfig, _availablePersons, DisplayName);
            }

            // Initialize Input Parameters Tab
            var inputParametersTab = this.FindControl<InputParametersTab>("InputParametersTab");
            inputParametersTab?.Initialize(_eventHub, _originalConfig);

            // Initialize Prompt Template Tab
            var promptTemplateTab = this.FindControl<PromptTemplateTab>("PromptTemplateTab");
            promptTemplateTab?.Initialize(_eventHub, _originalConfig);

            // Initialize Output Parameters Tab
            var outputParametersTab = this.FindControl<OutputParametersTab>("OutputParametersTab");
            outputParametersTab?.Initialize(_eventHub, _originalConfig);

            // Initialize Parallel Config Tab
            var parallelConfigTab = this.FindControl<ParallelConfigTab>("ParallelConfigTab");
            parallelConfigTab?.Initialize(_eventHub, _originalConfig);

            // Initialize Error Handling Tab
            var errorHandlingTab = this.FindControl<ErrorHandlingTab>("ErrorHandlingTab");
            errorHandlingTab?.Initialize(_eventHub, _originalConfig);
        }

        #endregion

        #region Event Handlers


        private void OnValidationRequested(object? sender, ValidationRequestEventArgs e)
        {
            // Implement validation logic here
            // For now, just log the validation request
            Log.Debug("Validation requested from {TabName}", e.TabName);
        }

        private void OnExportRequested(object? sender, ConfigurationExportEventArgs e)
        {
            // Collect configuration data from all tabs
            var exportData = CollectAllConfiguration();
            e.ExportData.Add(e.TabName, exportData);
        }

        private void OnConfigurationRequested(object? sender, ConfigurationRequestEventArgs e)
        {
            // Handle cross-tab configuration requests
            // For example, if one tab needs data from another
            if (e.RequestedType == typeof(List<ParameterConfig>))
            {
                // Return input parameters for other tabs to use
                var baseInfoTab = this.FindControl<BaseInfoTab>("BaseInfoTab");
                // e.ResponseData = baseInfoTab?.ViewModel?.GetConfiguration()?.InputParameters;
            }
        }

        private async void OnSaveAndCloseClick(object? sender, RoutedEventArgs e)
        {
            if (await SaveConfiguration(true))
            {
                Close(true);
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Result = null;
            Close();
        }

        private async void OnImportClick(object? sender, RoutedEventArgs e)
        {
            await ImportConfiguration();
        }

        private async void OnExportClick(object? sender, RoutedEventArgs e)
        {
            await ExportConfiguration();
        }

        private void OnSimpleModeClick(object? sender, RoutedEventArgs e)
        {
            // Create a config from current settings to pass to the simple window
            var currentConfig = CollectAllConfiguration();
            currentConfig.UseSimpleConfigMode = true; // Set mode to simple
            Result = JsonSerializer.Serialize(currentConfig);
            DisplayNameResult = DisplayName;

            // Close this window and signal to the factory to open the simple window
            Close("SIMPLE_MODE");
        }

        #endregion

        #region Configuration Management

        private async Task<bool> SaveConfiguration(bool closeAfterSave)
        {
            try
            {
                var config = CollectAllConfiguration();
                
                // Validate ParallelExecutionConfig according to section 7 of README.md
                var validationResult = ValidateParallelExecutionConfig(config);
                if (!validationResult.IsValid)
                {
                    await ShowErrorDialog("Configuration Validation Error", validationResult.ErrorMessage);
                    return false;
                }
                
                Result = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                
                // Get DisplayName from BaseInfo configuration
                var baseInfoTab = this.FindControl<BaseInfoTab>("BaseInfoTab");
                if (baseInfoTab?.ViewModel != null)
                {
                    var baseInfo = baseInfoTab.ViewModel.GetConfiguration();
                    DisplayNameResult = baseInfo.DisplayName;
                    DisplayName = baseInfo.DisplayName; // Update window display name too
                }
                else
                {
                    DisplayNameResult = DisplayName;
                }
                
                // Remove the asterisk from DisplayName to indicate saved state
                if (DisplayName.EndsWith("*"))
                {
                    DisplayName = DisplayName.TrimEnd('*');
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save configuration");
                await ShowErrorDialog("Save Error", $"Failed to save configuration: {ex.Message}");
                return false;
            }
        }

        private NamingConfig CollectAllConfiguration()
        {
            // Tabs work on the shared instance that was passed in during their initialization.
            // By re-using that same object we guarantee that all changes are already reflected.
            var config = _originalConfig ?? new NamingConfig();

            // The Base-Info tab keeps its own ViewModel copy, so we need to sync its fields back.
            var baseInfoTab = this.FindControl<BaseInfoTab>("BaseInfoTab");
            if (baseInfoTab?.ViewModel != null)
            {
                var baseInfo = baseInfoTab.ViewModel.GetConfiguration();
                config.FunctionName       = baseInfo.FunctionName;
                config.FunctionDescription = baseInfo.FunctionDescription;
                config.MaxRecursionLevel  = baseInfo.MaxRecursionLevel;
                config.ExecutivePerson    = baseInfo.ExecutivePerson;
            }

            // Collect from Input Parameters Tab
            var inputParametersTab = this.FindControl<InputParametersTab>("InputParametersTab");
            if (inputParametersTab != null)
            {
                var inputConfig = inputParametersTab.GetConfiguration();
                config.InputParameters = inputConfig.InputParameters;
            }

            // Collect from Prompt Template Tab
            var promptTemplateTab = this.FindControl<PromptTemplateTab>("PromptTemplateTab");
            if (promptTemplateTab != null)
            {
                var promptConfig = promptTemplateTab.GetConfiguration();
                config.PromptMessage = promptConfig.PromptMessage;
            }

            // Collect from Output Parameters Tab
            var outputParametersTab = this.FindControl<OutputParametersTab>("OutputParametersTab");
            if (outputParametersTab != null)
            {
                var outputConfig = outputParametersTab.GetConfiguration();
                config.ReturnParameters = outputConfig.ReturnParameters;
                config.ReturnToolName = outputConfig.ReturnToolName;
                config.ReturnToolDescription = outputConfig.ReturnToolDescription;
            }

            // Collect from Parallel Config Tab
            var parallelConfigTab = this.FindControl<ParallelConfigTab>("ParallelConfigTab");
            if (parallelConfigTab != null)
            {
                var parallelConfig = parallelConfigTab.GetConfiguration();
                config.ParallelConfig = parallelConfig.ParallelConfig;
            }

            // Collect from Error Handling Tab
            var errorHandlingTab = this.FindControl<ErrorHandlingTab>("ErrorHandlingTab");
            if (errorHandlingTab != null)
            {
                var errorHandlingConfig = errorHandlingTab.GetConfiguration();
                config.DanglingBehavior = errorHandlingConfig.DanglingBehavior;
                config.ErrorMessage = errorHandlingConfig.ErrorMessage;
                config.UrgingMessage = errorHandlingConfig.UrgingMessage;
                config.ErrorReportingToolName = errorHandlingConfig.ErrorReportingToolName;
                config.ErrorReportingConfig = errorHandlingConfig.ErrorReportingConfig;
            }

            return config;
        }

        private (bool IsValid, string ErrorMessage) ValidateParallelExecutionConfig(NamingConfig config)
        {
            // If parallel execution is disabled, no validation needed
            if (config.ParallelConfig == null || config.ParallelConfig.ExecutionType == ParallelExecutionType.None)
            {
                return (true, string.Empty);
            }

            var parallelConfig = config.ParallelConfig;

            // General validation
            if (parallelConfig.MaxConcurrency <= 0)
            {
                return (false, "MaxConcurrency must be greater than 0 when parallel execution is enabled.");
            }

            // Validate based on execution type according to README.md section 7
            switch (parallelConfig.ExecutionType)
            {
                case ParallelExecutionType.ParameterBased:
                    // For ParameterBased execution, we need at least one input parameter that's not excluded
                    var availableParameters = config.InputParameters?.Where(p => 
                        parallelConfig.ExcludedParameters == null || 
                        !parallelConfig.ExcludedParameters.Contains(p.Name)).ToList() ?? new List<ParameterConfig>();
                    
                    if (availableParameters.Count == 0)
                    {
                        return (false, "ParameterBased execution requires at least one input parameter that is not excluded. " +
                                     "Either add input parameters or adjust the excluded parameters list.");
                    }
                    break;

                case ParallelExecutionType.ListBased:
                    // For ListBased execution, ListParameterName must be specified and must refer to an array/list parameter
                    if (string.IsNullOrWhiteSpace(parallelConfig.ListParameterName))
                    {
                        return (false, "ListBased execution requires a ListParameterName to be specified.");
                    }
                    
                    var listParameter = config.InputParameters?.FirstOrDefault(p => p.Name == parallelConfig.ListParameterName);
                    if (listParameter == null)
                    {
                        return (false, $"ListBased execution requires the ListParameterName '{parallelConfig.ListParameterName}' " +
                                     "to match an existing input parameter.");
                    }
                    
                    // Check if the parameter type suggests it's a list/array
                    if (listParameter.Type != ParameterType.Array && !listParameter.Name.ToLowerInvariant().Contains("list"))
                    {
                        return (false, $"Parameter '{parallelConfig.ListParameterName}' should be of Array type or have 'list' in its name " +
                                     "for ListBased execution. Each item in this parameter will become a separate parallel session.");
                    }
                    break;

                case ParallelExecutionType.ExternalList:
                    // For ExternalList execution, ExternalList must contain at least one item
                    if (parallelConfig.ExternalList == null || parallelConfig.ExternalList.Count == 0)
                    {
                        return (false, "ExternalList execution requires at least one item in the ExternalList. " +
                                     "Each string in the external list will become a separate parallel session.");
                    }
                    break;

                default:
                    return (false, $"Unknown parallel execution type: {parallelConfig.ExecutionType}");
            }

            return (true, string.Empty);
        }

        #endregion

        #region Import/Export

        private async Task ImportConfiguration()
        {
            try
            {
                var storageProvider = this.StorageProvider;
                var filePickerOptions = new FilePickerOpenOptions
                {
                    Title = "Import Advanced Configuration",
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Advanced Naming Config")
                        {
                            Patterns = new[] { "*.dsnaming" },
                            MimeTypes = new[] { "application/octet-stream" }
                        }
                    },
                    AllowMultiple = false
                };

                var files = await storageProvider.OpenFilePickerAsync(filePickerOptions);
                if (files?.Count > 0)
                {
                    await ImportFromFile(files[0]);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to import configuration");
                await ShowErrorDialog("Import Error", $"Failed to import configuration: {ex.Message}");
            }
        }

        private async Task ExportConfiguration()
        {
            try
            {
                var storageProvider = this.StorageProvider;
                var fileName = $"advanced_naming_config_{DateTime.Now:yyyyMMdd}.dsnaming";
                
                var filePickerOptions = new FilePickerSaveOptions
                {
                    Title = "Export Advanced Configuration",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Advanced Naming Config")
                        {
                            Patterns = new[] { "*.dsnaming" },
                            MimeTypes = new[] { "application/octet-stream" }
                        }
                    },
                    DefaultExtension = "dsnaming",
                    SuggestedFileName = fileName
                };

                var file = await storageProvider.SaveFilePickerAsync(filePickerOptions);
                if (file != null)
                {
                    await ExportToFile(file);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to export configuration");
                await ShowErrorDialog("Export Error", $"Failed to export configuration: {ex.Message}");
            }
        }

        private async Task ImportFromFile(IStorageFile file)
        {
            using var stream = await file.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            
            var data = memoryStream.ToArray();
            var options = ContractlessStandardResolver.Options;
            
            var importData = MessagePackSerializer.Deserialize<Dictionary<string, object>>(data, options);
            if (importData.TryGetValue("AdvancedNamingTool", out var configObject))
            {
                var configBytes = MessagePackSerializer.Serialize(configObject, options);
                var configJson = JsonSerializer.Serialize(
                    MessagePackSerializer.Deserialize<NamingConfig>(configBytes, options),
                    new JsonSerializerOptions { WriteIndented = true }
                );
                
                LoadConfiguration(configJson);
                InitializeTabsWithConfig();
            }
        }

        private async Task ExportToFile(IStorageFile file)
        {
            var config = CollectAllConfiguration();
            var exportData = new Dictionary<string, object>
            {
                {
                    "metadata", new Dictionary<string, object>
                    {
                        { "exportDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                        { "exportedBy", "Advanced Naming Tool" },
                        { "version", "2.0" }
                    }
                },
                { "AdvancedNamingTool", config }
            };
            
            var options = ContractlessStandardResolver.Options;
            var data = MessagePackSerializer.Serialize(exportData, options);
            
            using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(data, 0, data.Length);
        }

        #endregion

        #region Dialog Helpers

        private async Task ShowErrorDialog(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync(this);
        }

        private async Task<bool> ShowConfirmDialog(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "Cancel",
                PrimaryButtonText = "Yes"
            };
            var result = await dialog.ShowAsync(this);
            return result == ContentDialogResult.Primary;
        }

        #endregion

        #region INotifyPropertyChanged

        public new event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Handle tab selection changes to ensure edits on the Basic Information tab are not lost
        /// when the user navigates to a different tab.
        /// </summary>
        private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            SyncBaseInfoTabToConfig();
        }

        /// <summary>
        /// Copies the current values from the BaseInfoTab ViewModel back into the shared
        /// <see cref="_originalConfig"/> instance so that other tabs and the save routine
        /// have the latest data.
        /// </summary>
        private void SyncBaseInfoTabToConfig()
        {
            var baseInfoTab = this.FindControl<BaseInfoTab>("BaseInfoTab");
            if (baseInfoTab?.ViewModel == null)
                return;

            var baseInfo = baseInfoTab.ViewModel.GetConfiguration();
            _originalConfig.FunctionName = baseInfo.FunctionName;
            _originalConfig.FunctionDescription = baseInfo.FunctionDescription;
            _originalConfig.MaxRecursionLevel = baseInfo.MaxRecursionLevel;
            _originalConfig.ExecutivePerson = baseInfo.ExecutivePerson;
        }

        #endregion
    }
}