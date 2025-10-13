using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Naming.AdvConfig.Events;
using NamingTool.Properties;

namespace Naming.AdvConfig.ViewModels
{
    /// <summary>
    /// ViewModel for the Error Handling tab
    /// </summary>
    public class ErrorHandlingTabViewModel : INotifyPropertyChanged
    {
        private readonly IConfigurationEventHub _eventHub;
        private DanglingBehavior _selectedDanglingBehavior = DanglingBehavior.Urge;
        private DanglingBehaviorOption? _selectedBehaviorOption;
        private string _errorMessage = string.Empty;
        private string _urgingMessage = string.Empty;

        private bool _isErrorReportingEnabled;
        private string _errorToolName = "report_error";
        private string _errorToolDescription = string.Empty;
        private ErrorReportingBehavior _selectedErrorReportingBehavior = ErrorReportingBehavior.Pause;
        private ErrorReportingBehaviorOption? _selectedErrorReportingBehaviorOption;
        private string _customErrorMessageToParent = string.Empty;

        private readonly ObservableCollection<ParameterConfig> _errorReportingParameters;

        private readonly RelayCommand _addErrorParameterCommand;
        private readonly RelayCommand<ParameterConfig> _removeErrorParameterCommand;
        private readonly RelayCommand _clearErrorParametersCommand;

        private bool _isInitializing;

        /// <summary>
        /// Available dangling behaviors for selection
        /// </summary>
        public ObservableCollection<DanglingBehaviorOption> AvailableBehaviors { get; set; } = new();

        /// <summary>
        /// Available error reporting behaviors for selection
        /// </summary>
        public ObservableCollection<ErrorReportingBehaviorOption> AvailableErrorReportingBehaviors { get; } = new();

        public ErrorHandlingTabViewModel(IConfigurationEventHub eventHub)
        {
            _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
            _errorReportingParameters = new ObservableCollection<ParameterConfig>();
            _errorReportingParameters.CollectionChanged += OnErrorParametersChanged;

            _addErrorParameterCommand = new RelayCommand(AddErrorReportingParameter, () => IsErrorReportingEnabled);
            _removeErrorParameterCommand = new RelayCommand<ParameterConfig>(RemoveErrorReportingParameter);
            _clearErrorParametersCommand = new RelayCommand(ClearErrorReportingParameters, () => HasErrorReportingParameters);

            InitializeAvailableBehaviors();
            InitializeErrorReportingBehaviors();

            // Initialize selected options to match default behavior values
            _selectedBehaviorOption = AvailableBehaviors.FirstOrDefault(b => b.Behavior == _selectedDanglingBehavior);
            // _selectedErrorReportingBehaviorOption is already set in InitializeErrorReportingBehaviors()

            // Set default description using resources when available
            _errorToolDescription = Resources.ErrorReporting_DefaultToolDescription;
        }

        #region Properties

        /// <summary>
        /// Selected dangling behavior option (for UI binding)
        /// </summary>
        public DanglingBehaviorOption? SelectedBehaviorOption
        {
            get => _selectedBehaviorOption;
            set
            {
                if (SetProperty(ref _selectedBehaviorOption, value))
                {
                    if (value != null)
                    {
                        SelectedDanglingBehavior = value.Behavior;
                    }
                }
            }
        }

        /// <summary>
        /// Selected dangling behavior
        /// </summary>
        public DanglingBehavior SelectedDanglingBehavior
        {
            get => _selectedDanglingBehavior;
            set
            {
                if (SetProperty(ref _selectedDanglingBehavior, value))
                {
                    // Update the selected option to match
                    var newSelectedOption = AvailableBehaviors.FirstOrDefault(b => b.Behavior == value);
                    if (_selectedBehaviorOption != newSelectedOption)
                    {
                        _selectedBehaviorOption = newSelectedOption;
                        OnPropertyChanged(nameof(SelectedBehaviorOption));
                    }
                    RaiseConfigurationChanged(nameof(SelectedDanglingBehavior));
                }
            }
        }

        /// <summary>
        /// Custom error message for ReportError behavior
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    RaiseConfigurationChanged(nameof(ErrorMessage));
                }
            }
        }

        /// <summary>
        /// Message to use when prompting for completion
        /// </summary>
        public string UrgingMessage
        {
            get => _urgingMessage;
            set
            {
                if (SetProperty(ref _urgingMessage, value))
                {
                    RaiseConfigurationChanged(nameof(UrgingMessage));
                }
            }
        }

        /// <summary>
        /// Whether error message input is visible (when ReportError is selected)
        /// </summary>
        public bool IsErrorMessageVisible => SelectedDanglingBehavior == DanglingBehavior.ReportError;

        /// <summary>
        /// Command to reset configuration to defaults
        /// </summary>
        public ICommand? ResetToDefaultsCommand { get; set; }

        /// <summary>
        /// Indicates whether the error reporting tool should be registered
        /// </summary>
        public bool IsErrorReportingEnabled
        {
            get => _isErrorReportingEnabled;
            set
            {
                if (SetProperty(ref _isErrorReportingEnabled, value))
                {
                    _addErrorParameterCommand.RaiseCanExecuteChanged();
                    _clearErrorParametersCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(IsErrorReportingConfigurationVisible));
                    
                    // Auto-load default parameters when enabling error reporting if no parameters exist
                    if (value && _errorReportingParameters.Count == 0)
                    {
                        LoadDefaultErrorReportingParameters();
                    }
                    
                    RaiseConfigurationChanged(nameof(IsErrorReportingEnabled));
                }
            }
        }

        /// <summary>
        /// Indicates whether the error reporting configuration section should be shown
        /// </summary>
        public bool IsErrorReportingConfigurationVisible => IsErrorReportingEnabled;

        /// <summary>
        /// Name of the error reporting tool
        /// </summary>
        public string ErrorToolName
        {
            get => _errorToolName;
            set
            {
                if (SetProperty(ref _errorToolName, value))
                {
                    RaiseConfigurationChanged(nameof(ErrorToolName));
                }
            }
        }

        /// <summary>
        /// Description of the error reporting tool
        /// </summary>
        public string ErrorToolDescription
        {
            get => _errorToolDescription;
            set
            {
                if (SetProperty(ref _errorToolDescription, value))
                {
                    RaiseConfigurationChanged(nameof(ErrorToolDescription));
                }
            }
        }

        /// <summary>
        /// Collection of error reporting parameters
        /// </summary>
        public ObservableCollection<ParameterConfig> ErrorReportingParameters => _errorReportingParameters;

        /// <summary>
        /// Whether any error reporting parameters are configured
        /// </summary>
        public bool HasErrorReportingParameters => _errorReportingParameters.Count > 0;

        /// <summary>
        /// Whether no error reporting parameters are configured
        /// </summary>
        public bool HasNoErrorReportingParameters => _errorReportingParameters.Count == 0;

        /// <summary>
        /// Selected error reporting behavior option (for UI binding)
        /// </summary>
        public ErrorReportingBehaviorOption? SelectedErrorReportingBehaviorOption
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

        /// <summary>
        /// Selected error reporting behavior
        /// </summary>
        public ErrorReportingBehavior SelectedErrorReportingBehavior
        {
            get => _selectedErrorReportingBehavior;
            set
            {
                if (SetProperty(ref _selectedErrorReportingBehavior, value))
                {
                    var newSelectedOption = AvailableErrorReportingBehaviors.FirstOrDefault(b => b.Behavior == value);
                    if (_selectedErrorReportingBehaviorOption != newSelectedOption)
                    {
                        _selectedErrorReportingBehaviorOption = newSelectedOption;
                        OnPropertyChanged(nameof(SelectedErrorReportingBehaviorOption));
                    }
                    OnPropertyChanged(nameof(IsCustomParentMessageVisible));
                    RaiseConfigurationChanged(nameof(SelectedErrorReportingBehavior));
                }
            }
        }

        /// <summary>
        /// Custom error message returned to parent session when behavior is ReportError
        /// </summary>
        public string CustomErrorMessageToParent
        {
            get => _customErrorMessageToParent;
            set
            {
                if (SetProperty(ref _customErrorMessageToParent, value))
                {
                    RaiseConfigurationChanged(nameof(CustomErrorMessageToParent));
                }
            }
        }

        /// <summary>
        /// Whether the custom parent message field should be visible
        /// </summary>
        public bool IsCustomParentMessageVisible => SelectedErrorReportingBehavior == ErrorReportingBehavior.ReportError;

        /// <summary>
        /// Command to add a new error reporting parameter
        /// </summary>
        public ICommand AddErrorParameterCommand => _addErrorParameterCommand;

        /// <summary>
        /// Command to remove an error reporting parameter
        /// </summary>
        public ICommand RemoveErrorParameterCommand => _removeErrorParameterCommand;

        /// <summary>
        /// Command to clear all error reporting parameters
        /// </summary>
        public ICommand ClearErrorParametersCommand => _clearErrorParametersCommand;

        #endregion

        #region Methods

        /// <summary>
        /// Initialize the view model with existing configuration
        /// </summary>
        public void Initialize(NamingConfig config)
        {
            _isInitializing = true;

            try
            {
                if (config != null)
                {
                    SelectedDanglingBehavior = config.DanglingBehavior;
                    ErrorMessage = config.ErrorMessage ?? string.Empty;
                    UrgingMessage = config.UrgingMessage ?? string.Empty;

                    if (config.ErrorReportingConfig != null)
                    {
                        IsErrorReportingEnabled = true;
                        ErrorToolName = config.ErrorReportingToolName;
                        ErrorToolDescription = config.ErrorReportingConfig.ToolDescription;
                        SelectedErrorReportingBehavior = config.ErrorReportingConfig.Behavior;
                        CustomErrorMessageToParent = config.ErrorReportingConfig.CustomErrorMessageToParent ?? string.Empty;

                        _errorReportingParameters.Clear();
                        foreach (var param in config.ErrorReportingConfig.Parameters)
                        {
                            _errorReportingParameters.Add(param);
                        }
                    }
                    else
                    {
                        IsErrorReportingEnabled = false;
                        ErrorToolName = Resources.ErrorReporting_DefaultToolName;
                        ErrorToolDescription = Resources.ErrorReporting_DefaultToolDescription;
                        SelectedErrorReportingBehavior = ErrorReportingBehavior.Pause;
                        CustomErrorMessageToParent = string.Empty;
                        _errorReportingParameters.Clear();
                    }
                }
                else
                {
                    // Set defaults when no config is provided
                    SelectedDanglingBehavior = DanglingBehavior.Urge;
                    ErrorMessage = string.Empty;
                    UrgingMessage = string.Empty;
                    IsErrorReportingEnabled = false;
                    ErrorToolName = Resources.ErrorReporting_DefaultToolName;
                    ErrorToolDescription = Resources.ErrorReporting_DefaultToolDescription;
                    SelectedErrorReportingBehavior = ErrorReportingBehavior.Pause;
                    CustomErrorMessageToParent = string.Empty;
                    _errorReportingParameters.Clear();
                }
            }
            finally
            {
                _isInitializing = false;
                UpdateErrorReportingParameterState();
            }
        }

        /// <summary>
        /// Get the current configuration state
        /// </summary>
        public ErrorHandlingConfiguration GetConfiguration()
        {
            return new ErrorHandlingConfiguration
            {
                DanglingBehavior = SelectedDanglingBehavior,
                ErrorMessage = ErrorMessage,
                UrgingMessage = UrgingMessage,
                ErrorReportingToolName = ErrorToolName,
                ErrorReportingConfig = IsErrorReportingEnabled
                    ? new ErrorReportingConfig
                    {
                        ToolDescription = ErrorToolDescription,
                        Behavior = SelectedErrorReportingBehavior,
                        CustomErrorMessageToParent = CustomErrorMessageToParent,
                        Parameters = _errorReportingParameters.ToList()
                    }
                    : null
            };
        }

        /// <summary>
        /// Reset to default values
        /// </summary>
        public void ResetToDefaults()
        {
            SelectedDanglingBehavior = DanglingBehavior.Urge;
            ErrorMessage = string.Empty;
            UrgingMessage = string.Empty;
            IsErrorReportingEnabled = false;
            ErrorToolName = Resources.ErrorReporting_DefaultToolName;
            ErrorToolDescription = Resources.ErrorReporting_DefaultToolDescription;
            SelectedErrorReportingBehavior = ErrorReportingBehavior.Pause;
            CustomErrorMessageToParent = string.Empty;
            ClearErrorReportingParameters();
            RaiseConfigurationChanged("Reset");
        }

        private void InitializeAvailableBehaviors()
        {
            AvailableBehaviors.Clear();
            AvailableBehaviors.Add(new DanglingBehaviorOption
            {
                Behavior = DanglingBehavior.Urge,
                DisplayName = Resources.DanglingBehavior_Urge,
                Description = "Send up to 3 reminder messages, then throw exception"
            });
            AvailableBehaviors.Add(new DanglingBehaviorOption
            {
                Behavior = DanglingBehavior.ReportError,
                DisplayName = Resources.DanglingBehavior_ReportError,
                Description = "Report error message and return immediately"
            });
            AvailableBehaviors.Add(new DanglingBehaviorOption
            {
                Behavior = DanglingBehavior.Pause,
                DisplayName = Resources.DanglingBehavior_Pause,
                Description = "Wait indefinitely for manual intervention"
            });
        }

        private void InitializeErrorReportingBehaviors()
        {
            AvailableErrorReportingBehaviors.Clear();
            AvailableErrorReportingBehaviors.Add(new ErrorReportingBehaviorOption
            {
                Behavior = ErrorReportingBehavior.Pause,
                DisplayName = Resources.ErrorReporting_Behavior_Pause,
                Description = Resources.ErrorReporting_Behavior_PauseDescription
            });
            AvailableErrorReportingBehaviors.Add(new ErrorReportingBehaviorOption
            {
                Behavior = ErrorReportingBehavior.ReportError,
                DisplayName = Resources.ErrorReporting_Behavior_ReportError,
                Description = Resources.ErrorReporting_Behavior_ReportErrorDescription
            });

            _selectedErrorReportingBehaviorOption = AvailableErrorReportingBehaviors.FirstOrDefault();
        }

        private void AddErrorReportingParameter()
        {
            var parameter = new ParameterConfig
            {
                Name = GenerateUniqueParameterName(),
                Description = Resources.ErrorReporting_DefaultParameterDescription,
                Type = ParameterType.String,
                IsRequired = true
            };

            _errorReportingParameters.Add(parameter);
            UpdateErrorReportingParameterState();
            RaiseConfigurationChanged("ErrorReportingParameters");
        }

        private void RemoveErrorReportingParameter(ParameterConfig? parameter)
        {
            if (parameter != null && _errorReportingParameters.Contains(parameter))
            {
                _errorReportingParameters.Remove(parameter);
                UpdateErrorReportingParameterState();
                RaiseConfigurationChanged("ErrorReportingParameters");
            }
        }

        private void ClearErrorReportingParameters()
        {
            if (_errorReportingParameters.Count > 0)
            {
                _errorReportingParameters.Clear();
                UpdateErrorReportingParameterState();
                RaiseConfigurationChanged("ErrorReportingParameters");
            }
        }

        private void LoadDefaultErrorReportingParameters()
        {
            _errorReportingParameters.Clear();
            _errorReportingParameters.Add(new ParameterConfig
            {
                Name = "error_message",
                Description = Resources.ErrorReporting_DefaultParameter_ErrorMessage,
                Type = ParameterType.String,
                IsRequired = true
            });

            UpdateErrorReportingParameterState();
            RaiseConfigurationChanged("ErrorReportingParameters");
        }

        private string GenerateUniqueParameterName()
        {
            var baseName = "parameter";
            var counter = 1;
            var candidate = baseName;

            while (_errorReportingParameters.Any(p => p.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = $"{baseName}{counter}";
                counter++;
            }

            return candidate;
        }

        private void OnErrorParametersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateErrorReportingParameterState();
            if (!_isInitializing)
            {
                RaiseConfigurationChanged("ErrorReportingParameters");
            }
        }

        private void UpdateErrorReportingParameterState()
        {
            OnPropertyChanged(nameof(HasErrorReportingParameters));
            OnPropertyChanged(nameof(HasNoErrorReportingParameters));
            _clearErrorParametersCommand.RaiseCanExecuteChanged();
        }

        private void RaiseConfigurationChanged(string propertyPath)
        {
            if (!_isInitializing)
            {
                _eventHub.RaiseConfigurationChanged("ErrorHandling", GetConfiguration(), ConfigurationChangeType.PropertyChanged, propertyPath);
            }
            
            // Notify UI about visibility changes
            if (propertyPath == nameof(SelectedDanglingBehavior))
            {
                OnPropertyChanged(nameof(IsErrorMessageVisible));
            }
            else if (propertyPath == nameof(SelectedErrorReportingBehavior))
            {
                OnPropertyChanged(nameof(IsCustomParentMessageVisible));
            }
        }

        public void OnErrorReportingParameterChanged(ParameterConfig parameter)
        {
            if (parameter == null)
            {
                return;
            }

            if (!_errorReportingParameters.Contains(parameter))
            {
                // Parameter editors work on reference types, so this should rarely happen
                return;
            }

            RaiseConfigurationChanged("ErrorReportingParameters");
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

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

        #endregion
    }

    /// <summary>
    /// Configuration data structure for error handling
    /// </summary>
    public class ErrorHandlingConfiguration
    {
        public DanglingBehavior DanglingBehavior { get; set; } = DanglingBehavior.Urge;
        public string ErrorMessage { get; set; } = string.Empty;
        public string UrgingMessage { get; set; } = string.Empty;
        public string ErrorReportingToolName { get; set; } = "report_error";
        public ErrorReportingConfig? ErrorReportingConfig { get; set; }
    }

    /// <summary>
    /// Display option for dangling behavior selection
    /// </summary>
    public class DanglingBehaviorOption
    {
        public DanglingBehavior Behavior { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Display option for error reporting behavior selection
    /// </summary>
    public class ErrorReportingBehaviorOption
    {
        public ErrorReportingBehavior Behavior { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}