using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using FluentAvalonia.UI.Controls;
using MessagePack;
using MessagePack.Resolvers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Res = NamingTool.Properties.Resources;

namespace Naming
{
    /// <summary>
    /// Configuration window for the Naming plugin
    /// </summary>
    internal partial class NamingConfigWindow : Window, INotifyPropertyChanged
    {
        private NamingConfig _config = new();
        private List<IHostPerson> _availablePersons = new();
        private string _displayName = string.Empty;
        private string _functionName = "create_subtask";
        private string _functionDescription = "Create a task to ...";
        private string _returnToolName = "set_result";
        private string _returnToolDescription = "Report back with the result after completion or failure";
        private string _promptMessage = string.Empty;
        private int _maxRecursionLevel = 6;
        private ConfigPerson? _selectedExecutivePerson;
        private bool _isFunctionNameValid = true;
        private bool _isErrorReportingEnabled;
        private string _errorToolName = "report_error";
        private string _errorToolDescription = "Report an error or issue encountered during task execution";
        private ErrorReportingBehavior _selectedErrorReportingBehavior = ErrorReportingBehavior.Pause;
        private string _customErrorMessageToParent = string.Empty;
        private ErrorReportingBehaviorOptionView? _selectedErrorReportingBehaviorOption;

        // Regex pattern for valid function names: only lowercase letters and underscores
        private static readonly Regex FunctionNamePattern = new Regex(@"^[a-z_]+$", RegexOptions.Compiled);

        public string? Result { get; private set; }
        public string? DisplayNameResult { get; private set; }

        internal ObservableCollection<ParameterConfigViewModel> InputParameters { get; set; } = new();
        internal ObservableCollection<ParameterConfigViewModel> ReturnParameters { get; set; } = new();
        internal ObservableCollection<ParameterConfigViewModel> ErrorReportingParameters { get; set; } = new();
        internal ObservableCollection<ConfigPerson> AvailablePersons { get; set; } = new();
        internal ObservableCollection<ErrorReportingBehaviorOptionView> ErrorReportingBehaviorOptions { get; } = new();

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public string FunctionName
        {
            get => _functionName;
            set 
            { 
                if (SetProperty(ref _functionName, value))
                {
                    ValidateFunctionName();
                    UpdateFunctionNameDisplay();
                }
            }
        }

        public bool IsFunctionNameValid
        {
            get => _isFunctionNameValid;
            private set => SetProperty(ref _isFunctionNameValid, value);
        }

        public string FunctionDescription
        {
            get => _functionDescription;
            set => SetProperty(ref _functionDescription, value);
        }

        public string ReturnToolName
        {
            get => _returnToolName;
            set => SetProperty(ref _returnToolName, value);
        }

        public string ReturnToolDescription
        {
            get => _returnToolDescription;
            set => SetProperty(ref _returnToolDescription, value);
        }

        public bool IsErrorReportingEnabled
        {
            get => _isErrorReportingEnabled;
            set
            {
                if (SetProperty(ref _isErrorReportingEnabled, value))
                {
                    OnPropertyChanged(nameof(IsErrorReportingParametersEmpty));
                    OnPropertyChanged(nameof(IsCustomErrorMessageVisible));
                }
            }
        }

        public string ErrorToolName
        {
            get => _errorToolName;
            set => SetProperty(ref _errorToolName, value);
        }

        public string ErrorToolDescription
        {
            get => _errorToolDescription;
            set => SetProperty(ref _errorToolDescription, value);
        }

        public ErrorReportingBehaviorOptionView? SelectedErrorReportingBehaviorOption
        {
            get => _selectedErrorReportingBehaviorOption;
            set
            {
                if (SetProperty(ref _selectedErrorReportingBehaviorOption, value))
                {
                    if (value != null)
                    {
                        SelectedErrorReportingBehavior = value.Behavior;
                    }
                }
            }
        }

        public ErrorReportingBehavior SelectedErrorReportingBehavior
        {
            get => _selectedErrorReportingBehavior;
            set
            {
                if (SetProperty(ref _selectedErrorReportingBehavior, value))
                {
                    // sync selected option if needed
                    var matching = ErrorReportingBehaviorOptions.FirstOrDefault(opt => opt.Behavior == value);
                    if (!Equals(_selectedErrorReportingBehaviorOption, matching))
                    {
                        _selectedErrorReportingBehaviorOption = matching;
                        OnPropertyChanged(nameof(SelectedErrorReportingBehaviorOption));
                    }
                    OnPropertyChanged(nameof(IsCustomErrorMessageVisible));
                }
            }
        }

        public string CustomErrorMessageToParent
        {
            get => _customErrorMessageToParent;
            set => SetProperty(ref _customErrorMessageToParent, value);
        }

        public bool IsErrorReportingParametersEmpty => ErrorReportingParameters.Count == 0;

        public bool IsCustomErrorMessageVisible => IsErrorReportingEnabled && SelectedErrorReportingBehavior == ErrorReportingBehavior.ReportError;

        public string PromptMessage
        {
            get => _promptMessage;
            set => SetProperty(ref _promptMessage, value);
        }

        public int MaxRecursionLevel
        {
            get => _maxRecursionLevel;
            set => SetProperty(ref _maxRecursionLevel, value);
        }

        internal ConfigPerson? SelectedExecutivePerson
        {
            get => _selectedExecutivePerson;
            set => SetProperty(ref _selectedExecutivePerson, value);
        }

        public bool IsInputParametersEmpty => InputParameters.Count == 0;

        public bool IsReturnParametersEmpty => ReturnParameters.Count == 0;

        public NamingConfigWindow()
        {
            DataContext = this;
            InitializeComponent();
            InitializeDefaultParameters();
            ErrorReportingParameters.CollectionChanged += (s, e) => OnPropertyChanged(nameof(IsErrorReportingParametersEmpty));
            InitializeErrorReportingBehaviorOptions();
            
            // Validate the initial function name
            ValidateFunctionName();
        }

        public NamingConfigWindow(PlugToolInfo plugInstanceInfo, List<IHostPerson> availablePersons) : this()
        {
            Initialize(plugInstanceInfo, availablePersons);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }


        public void Initialize(PlugToolInfo plugToolInfo, List<IHostPerson> availablePersons)
        {
            _availablePersons = availablePersons ?? new List<IHostPerson>();
            
            // Convert IHostPerson to ConfigPerson for binding
            AvailablePersons.Clear();
            
            // Add "Use current session's Person" option as the first item
            AvailablePersons.Add(new ConfigPerson 
            { 
                Name = Res.Field_ExecutivePerson_UseCurrentSession, 
                Description = Res.Field_ExecutivePerson_UseCurrentSession_Description,
                UseCurrentSession = true
            });
            
            foreach (var person in _availablePersons)
            {
                AvailablePersons.Add(new ConfigPerson 
                { 
                    Name = person.Name, 
                    Description = person.Description
                });
            }
            
            DisplayName = plugToolInfo.DisplayName ?? string.Empty;
            LoadConfiguration(plugToolInfo.Config ?? string.Empty);
        }

        /// <summary>
        /// Load configuration from either JSON string or MessagePack byte array
        /// </summary>
        /// <param name="configJson">Optional JSON string configuration</param>
        /// <param name="configData">Optional MessagePack byte array configuration</param>
        private void LoadConfiguration(string? configJson = null, byte[]? configData = null)
        {
            if (!string.IsNullOrWhiteSpace(configJson))
            {
                // Load from JSON string
                _config = JsonSerializer.Deserialize<NamingConfig>(configJson) ?? new NamingConfig();
            }
            else if (configData != null && configData.Length > 0)
            {
                // Load from MessagePack byte array
                var options = MessagePack.Resolvers.ContractlessStandardResolver.Options;
                _config = MessagePack.MessagePackSerializer.Deserialize<NamingConfig>(configData, options) ?? new NamingConfig();
            }
            else
            {
                _config = new NamingConfig();
            }

            // Apply configuration to properties
            FunctionName = _config.FunctionName;
            FunctionDescription = _config.FunctionDescription;
            ReturnToolName = _config.ReturnToolName;
            ReturnToolDescription = _config.ReturnToolDescription;
            PromptMessage = _config.PromptMessage;
            MaxRecursionLevel = _config.MaxRecursionLevel;

            // Set selected executive person
            if (_config.ExecutivePerson != null)
            {
                SelectedExecutivePerson = AvailablePersons.FirstOrDefault(p =>
                    p.Name == _config.ExecutivePerson.Name);
            }

            // If no person is selected, default to "Use current session's Person"
            if (SelectedExecutivePerson == null && AvailablePersons.Any())
            {
                SelectedExecutivePerson = AvailablePersons.First(); // This is "Use current session's Person"
            }

            // Load parameters
            LoadParameters();

            if (_config.ErrorReportingConfig != null)
            {
                IsErrorReportingEnabled = true;
                ErrorToolName = _config.ErrorReportingToolName;
                ErrorToolDescription = _config.ErrorReportingConfig.ToolDescription;
                SelectedErrorReportingBehavior = _config.ErrorReportingConfig.Behavior;
                CustomErrorMessageToParent = _config.ErrorReportingConfig.CustomErrorMessageToParent ?? string.Empty;
            }
            else
            {
                IsErrorReportingEnabled = false;
                ErrorToolName = Res.ErrorReporting_DefaultToolName;
                ErrorToolDescription = Res.ErrorReporting_DefaultToolDescription;
                SelectedErrorReportingBehavior = ErrorReportingBehavior.Pause;
                CustomErrorMessageToParent = string.Empty;
            }

            // Validate after loading configuration
            ValidateFunctionName();
        }

        private void InitializeDefaultParameters()
        {
            // Default input context values - empty list as requested
            // InputParameters will remain empty by default

            // Default return parameters - empty list as requested
            // ReturnParameters will remain empty by default
        }

        private void InitializeErrorReportingBehaviorOptions()
        {
            ErrorReportingBehaviorOptions.Clear();
            ErrorReportingBehaviorOptions.Add(new ErrorReportingBehaviorOptionView(
                ErrorReportingBehavior.Pause,
                Res.ErrorReporting_Behavior_Pause,
                Res.ErrorReporting_Behavior_PauseDescription));

            ErrorReportingBehaviorOptions.Add(new ErrorReportingBehaviorOptionView(
                ErrorReportingBehavior.ReportError,
                Res.ErrorReporting_Behavior_ReportError,
                Res.ErrorReporting_Behavior_ReportErrorDescription));

            // Ensure selected option aligns with current behavior value
            SelectedErrorReportingBehavior = ErrorReportingBehavior.Pause;
        }

        private void LoadParameters()
        {
            // Load function context values if they exist
            if (_config.InputParameters?.Any() == true)
            {
                InputParameters.Clear();
                foreach (var param in _config.InputParameters)
                {
                    InputParameters.Add(new ParameterConfigViewModel(param));
                }
            }

            // Load return parameters if they exist
            if (_config.ReturnParameters?.Any() == true)
            {
                ReturnParameters.Clear();
                foreach (var param in _config.ReturnParameters)
                {
                    ReturnParameters.Add(new ParameterConfigViewModel(param));
                }
            }

            // Load error reporting parameters if they exist
            ErrorReportingParameters.Clear();
            if (_config.ErrorReportingConfig?.Parameters?.Any() == true)
            {
                foreach (var param in _config.ErrorReportingConfig.Parameters)
                {
                    ErrorReportingParameters.Add(new ParameterConfigViewModel(param));
                }
            }
            
            // Notify about empty state changes
            OnPropertyChanged(nameof(IsInputParametersEmpty));
            OnPropertyChanged(nameof(IsReturnParametersEmpty));
            OnPropertyChanged(nameof(IsErrorReportingParametersEmpty));
        }

        private void OnAddFunctionParameterClick(object? sender, RoutedEventArgs e)
        {
            var newParam = new ParameterConfig
            {
                Name = "newContextValue",
                Description = "Context value description",
                IsRequired = true,
                Type = ParameterType.String
            };
            InputParameters.Add(new ParameterConfigViewModel(newParam));
            OnPropertyChanged(nameof(IsInputParametersEmpty));
        }

        private void OnRemoveFunctionParameterClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ParameterConfigViewModel param)
            {
                InputParameters.Remove(param);
                OnPropertyChanged(nameof(IsInputParametersEmpty));
            }
        }

        private void OnAddReturnParameterClick(object? sender, RoutedEventArgs e)
        {
            var newParam = new ParameterConfig
            {
                Name = "isSuccess",
                Description = "The result of the task. 0 for failed, 1 for success.",
                IsRequired = true,
                Type = ParameterType.Bool
            };
            ReturnParameters.Add(new ParameterConfigViewModel(newParam));
            OnPropertyChanged(nameof(IsReturnParametersEmpty));
        }

        private void OnRemoveReturnParameterClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ParameterConfigViewModel param)
            {
                ReturnParameters.Remove(param);
                OnPropertyChanged(nameof(IsReturnParametersEmpty));
            }
        }

        private void OnAddErrorReportingParameterClick(object? sender, RoutedEventArgs e)
        {
            var newParam = new ParameterConfig
            {
                Name = "error_message",
                Description = Res.ErrorReporting_DefaultParameter_ErrorMessage,
                IsRequired = true,
                Type = ParameterType.String
            };
            ErrorReportingParameters.Add(new ParameterConfigViewModel(newParam));
            OnPropertyChanged(nameof(IsErrorReportingParametersEmpty));
        }

        private void OnRemoveErrorReportingParameterClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ParameterConfigViewModel param)
            {
                ErrorReportingParameters.Remove(param);
                OnPropertyChanged(nameof(IsErrorReportingParametersEmpty));
            }
        }

        private void OnLoadDefaultErrorReportingParametersClick(object? sender, RoutedEventArgs e)
        {
            ErrorReportingParameters.Clear();

            ErrorReportingParameters.Add(new ParameterConfigViewModel(new ParameterConfig
            {
                Name = "error_message",
                Description = Res.ErrorReporting_DefaultParameter_ErrorMessage,
                IsRequired = true,
                Type = ParameterType.String
            }));

            ErrorReportingParameters.Add(new ParameterConfigViewModel(new ParameterConfig
            {
                Name = "error_type",
                Description = Res.ErrorReporting_DefaultParameter_ErrorType,
                IsRequired = false,
                Type = ParameterType.String
            }));

            OnPropertyChanged(nameof(IsErrorReportingParametersEmpty));
        }

        private async void OnSaveAndCloseClick(object? sender, RoutedEventArgs e)
        {
            // Validate the form before saving
            if (!await ValidateFormAsync())
            {
                return; // Don't save if validation fails
            }

            try
            {
                var config = new NamingConfig
                {
                    Version = 1,
                    FunctionName = FunctionName,
                    FunctionDescription = FunctionDescription,
                    ReturnToolName = ReturnToolName,
                    ReturnToolDescription = ReturnToolDescription,
                    PromptMessage = PromptMessage,
                    MaxRecursionLevel = MaxRecursionLevel,
                    ExecutivePerson = SelectedExecutivePerson?.UseCurrentSession == true ? null : SelectedExecutivePerson,
                    InputParameters = InputParameters.Select(p => p.ToParameterConfig()).ToList(),
                    ReturnParameters = ReturnParameters.Select(p => p.ToParameterConfig()).ToList(),
                    ErrorReportingToolName = ErrorToolName,
                    ErrorReportingConfig = IsErrorReportingEnabled
                        ? new ErrorReportingConfig
                        {
                            ToolDescription = ErrorToolDescription,
                            Behavior = SelectedErrorReportingBehavior,
                            CustomErrorMessageToParent = CustomErrorMessageToParent,
                            Parameters = ErrorReportingParameters.Select(p => p.ToParameterConfig()).ToList()
                        }
                        : null,
                    UseSimpleConfigMode = _config.UseSimpleConfigMode // Preserve mode setting
                };

                Result = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                DisplayNameResult = DisplayName;
                // Return TRUE to the caller so that ConfigInstance knows we actually saved
                Close(true);
            }
            catch (Exception ex)
            {
                // Show error message - simplified for now
                Log.Error(ex, "Failed to save configuration");
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Result = null;
            Close();
        }

        private void OnAdvancedModeClick(object? sender, RoutedEventArgs e)
        {
            // Create a config from current settings to pass to the advanced window
            var currentConfig = CreateConfigFromCurrentSettings();
            currentConfig.UseSimpleConfigMode = false; // Set mode to advanced
            Result = JsonSerializer.Serialize(currentConfig);
            DisplayNameResult = DisplayName;

            // Close this window and signal to the factory to open the advanced window
            Close("ADVANCED_MODE");
        }
        
        /// <summary>
        /// Creates a NamingConfig object from the current UI settings
        /// </summary>
        private NamingConfig CreateConfigFromCurrentSettings()
        {
            return new NamingConfig
            {
                Version = 1,
                FunctionName = FunctionName,
                FunctionDescription = FunctionDescription,
                ReturnToolName = ReturnToolName,
                ReturnToolDescription = ReturnToolDescription,
                PromptMessage = PromptMessage,
                MaxRecursionLevel = MaxRecursionLevel,
                ExecutivePerson = SelectedExecutivePerson?.UseCurrentSession == true ? null : SelectedExecutivePerson,
                InputParameters = InputParameters.Select(p => p.ToParameterConfig()).ToList(),
                ReturnParameters = ReturnParameters.Select(p => p.ToParameterConfig()).ToList(),
                ErrorReportingToolName = ErrorToolName,
                ErrorReportingConfig = IsErrorReportingEnabled
                    ? new ErrorReportingConfig
                    {
                        ToolDescription = ErrorToolDescription,
                        Behavior = SelectedErrorReportingBehavior,
                        CustomErrorMessageToParent = CustomErrorMessageToParent,
                        Parameters = ErrorReportingParameters.Select(p => p.ToParameterConfig()).ToList()
                    }
                    : null,
                UseSimpleConfigMode = _config.UseSimpleConfigMode // Preserve the current mode setting
            };
        }
        

        /// <summary>
        /// Shows a result dialog that automatically closes after 2-3 seconds
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="parent">Optional parent window</param>
        /// <returns>A task that completes when the dialog is closed</returns>
        public async Task ShowResultDialog(string message, Window? parent = null)
        {
            var dialog = new ContentDialog
            {
                Title = "Result",
                Content = message,
                CloseButtonText = "OK"
            };
            
            // Show the dialog
            parent ??= this;
            
            // Create a cancellation token that will cancel after 2-3 seconds
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(1)); // 1 seconds
            
            try
            {
                // Start the dialog
                var dialogTask = dialog.ShowAsync(parent);
                
                // Wait for either the dialog to complete or the timeout to occur
                await Task.WhenAny(dialogTask, Task.Delay(Timeout.Infinite, cts.Token).ContinueWith(_ => { }));
                
                // If the dialog is still open (task not completed), close it
                if (!dialogTask.IsCompleted)
                {
                    dialog.Hide();
                }
            }
            catch (TaskCanceledException)
            {
                // This is expected when the timeout occurs
                dialog.Hide();
            }
            catch (Exception ex)
            {
                // Log any unexpected errors
                Log.Error(ex, "Error showing result dialog");
            }
        }

        private void OnFunctionNameTextChanged(object? sender, TextChangedEventArgs e)
        {
            // Trigger validation when text changes
            ValidateFunctionName();
            UpdateFunctionNameDisplay();
        }

        private void OnPromptMessageGotFocus(object? sender, RoutedEventArgs e)
        {
            // Auto-populate with input parameter placeholders when text box is empty
            if (sender is TextBox textBox && string.IsNullOrWhiteSpace(textBox.Text))
            {
                if (InputParameters.Count > 0)
                {
                    var parameterPlaceholders = InputParameters
                        .Select(p => $"{{{{{p.Name}}}}}")
                        .ToList();
                    
                    var placeholderText = string.Join("\n", parameterPlaceholders);
                    textBox.Text = placeholderText;
                    PromptMessage = placeholderText;
                    
                    // Set cursor position at the end
                    textBox.CaretIndex = placeholderText.Length;
                }
            }
        }

        private void ValidateFunctionName()
        {
            IsFunctionNameValid = !string.IsNullOrWhiteSpace(FunctionName) && 
                                 FunctionNamePattern.IsMatch(FunctionName);
        }

        private void UpdateFunctionNameDisplay()
        {
            if (this.FindControl<TextBox>("FunctionNameTextBox") is TextBox textBox)
            {
                if (IsFunctionNameValid)
                {
                    textBox.Classes.Remove("error");
                }
                else
                {
                    textBox.Classes.Add("error");
                }
            }
        }

        private async Task<bool> ValidateFormAsync()
        {
            ValidateFunctionName();

            if (!IsFunctionNameValid)
            {
                this.FindControl<TextBox>("FunctionNameTextBox")?.Focus();
                return false;
            }

            if (IsErrorReportingEnabled)
            {
                if (string.IsNullOrWhiteSpace(ErrorToolName))
                {
                    await ShowErrorAsync(Res.ErrorReporting_Validation_ToolNameRequired);
                    this.FindControl<TextBox>("ErrorToolNameTextBox")?.Focus();
                    return false;
                }

                if (string.Equals(ErrorToolName, ReturnToolName, StringComparison.OrdinalIgnoreCase))
                {
                    await ShowErrorAsync(Res.ErrorReporting_Validation_ToolNameConflict);
                    this.FindControl<TextBox>("ErrorToolNameTextBox")?.Focus();
                    return false;
                }

                if (ErrorReportingParameters.Any(p => string.IsNullOrWhiteSpace(p.Name)))
                {
                    await ShowErrorAsync(Res.ErrorReporting_Validation_InvalidParameterName);
                    return false;
                }

                var duplicateNames = ErrorReportingParameters
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                    .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateNames.Count > 0)
                {
                    await ShowErrorAsync(string.Format(Res.ErrorReporting_Validation_DuplicateParameters, string.Join(", ", duplicateNames)));
                    return false;
                }
            }

            return true;
        }

        private async Task ShowErrorAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = Res.Error,
                Content = message,
                CloseButtonText = Res.OK
            };

            await dialog.ShowAsync(this);
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    internal class ErrorReportingBehaviorOptionView
    {
        public ErrorReportingBehaviorOptionView(ErrorReportingBehavior behavior, string displayName, string description)
        {
            Behavior = behavior;
            DisplayName = displayName;
            Description = description;
        }

        public ErrorReportingBehavior Behavior { get; }

        public string DisplayName { get; }

        public string Description { get; }
    }
}