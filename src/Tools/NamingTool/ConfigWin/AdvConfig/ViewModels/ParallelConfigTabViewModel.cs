using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Collections.Generic;
using Naming.AdvConfig.Events;
using Naming.ParallelExecution;

namespace Naming.AdvConfig.ViewModels
{
    /// <summary>
    /// ViewModel for parallel execution configuration
    /// </summary>
    internal class ParallelConfigTabViewModel : INotifyPropertyChanged
    {
        private readonly IConfigurationEventHub _eventHub;
        private NamingConfig _config;

        // Parallel execution properties based on existing ParallelExecutionConfig
        private ParallelExecutionType _executionType = ParallelExecutionType.None;
        private int _maxConcurrency = Environment.ProcessorCount;
        private ParallelResultStrategy _resultStrategy = ParallelResultStrategy.WaitForAll;
        private string _listParameterName = string.Empty;
        private ObservableCollection<string> _externalStringList = new ObservableCollection<string>();
        private ObservableCollection<string> _excludedParameters = new ObservableCollection<string>();
        private int _sessionTimeoutMs = 30 * 60 * 1000; // 30 minutes

        // Additional UI properties for better user experience
        private string _externalListText = string.Empty;
        private bool _isAdvancedSettingsExpanded = false;
        
        // Parameter selection properties
        private ParameterConfig? _selectedListParameter;
        private ParameterConfig? _selectedExcludedParameter;

        public ParallelConfigTabViewModel(IConfigurationEventHub eventHub, NamingConfig config)
        {
            _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
            _config = config ?? new NamingConfig();

            InitializeCommands();
            LoadFromConfiguration(_config);
            SubscribeToEvents();
        }

        #region Properties

        public ParallelExecutionType ExecutionType
        {
            get => _executionType;
            set
            {
                if (SetProperty(ref _executionType, value))
                {
                    UpdateConfiguration();
                    NotifyConfigurationChanged();
                    OnPropertyChanged(nameof(IsParallelExecutionEnabled));
                    OnPropertyChanged(nameof(IsListBasedExecution));
                    OnPropertyChanged(nameof(IsExternalListExecution));
                    OnPropertyChanged(nameof(IsParameterBasedExecution));
                }
            }
        }

        public int MaxConcurrency
        {
            get => _maxConcurrency;
            set
            {
                // Ensure strictly positive; no artificial upper bound
                var normalized = value < 1 ? 1 : value;
                if (SetProperty(ref _maxConcurrency, normalized))
                {
                    UpdateConfiguration();
                    NotifyConfigurationChanged();
                }
            }
        }

        public ParallelResultStrategy ResultStrategy
        {
            get => _resultStrategy;
            set
            {
                if (SetProperty(ref _resultStrategy, value))
                {
                    UpdateConfiguration();
                    NotifyConfigurationChanged();
                }
            }
        }

        public string ListParameterName
        {
            get => _listParameterName;
            set
            {
                if (SetProperty(ref _listParameterName, value))
                {
                    UpdateConfiguration();
                    NotifyConfigurationChanged();
                }
            }
        }

        public ObservableCollection<string> ExternalStringList
        {
            get => _externalStringList;
            set => SetProperty(ref _externalStringList, value);
        }

        public ObservableCollection<string> ExcludedParameters
        {
            get => _excludedParameters;
            set => SetProperty(ref _excludedParameters, value);
        }

        public int SessionTimeoutMs
        {
            get => _sessionTimeoutMs;
            set
            {
                var clampedValue = Math.Max(1000, Math.Min(3600000, value)); // 1 second to 1 hour
                if (SetProperty(ref _sessionTimeoutMs, clampedValue))
                {
                    UpdateConfiguration();
                    NotifyConfigurationChanged();
                }
            }
        }

        public string ExternalListText
        {
            get => _externalListText;
            set 
            { 
                if (SetProperty(ref _externalListText, value))
                {
                    UpdateExternalListFromText();
                }
            }
        }

        public bool IsAdvancedSettingsExpanded
        {
            get => _isAdvancedSettingsExpanded;
            set => SetProperty(ref _isAdvancedSettingsExpanded, value);
        }

        // Computed properties
        public bool IsParallelExecutionEnabled => _executionType != ParallelExecutionType.None;

        public bool IsListBasedExecution => _executionType == ParallelExecutionType.ListBased;

        public bool IsExternalListExecution => _executionType == ParallelExecutionType.ExternalList;

        public bool IsParameterBasedExecution => _executionType == ParallelExecutionType.ParameterBased;

        public string ConfigurationSummary
        {
            get
            {
                if (_executionType == ParallelExecutionType.None)
                    return "Parallel execution disabled";

                return $"{_executionType}: Max {_maxConcurrency} concurrent, Strategy: {_resultStrategy}";
            }
        }

        public bool IsConfigurationValid
        {
            get
            {
                if (_executionType == ParallelExecutionType.None)
                    return true;

                if (_maxConcurrency <= 0)
                    return false;

                if (_executionType == ParallelExecutionType.ListBased && string.IsNullOrWhiteSpace(_listParameterName))
                    return false;

                if (_executionType == ParallelExecutionType.ExternalList && _externalStringList.Count == 0)
                    return false;

                return true;
            }
        }

        public string SessionTimeoutDisplay
        {
            get
            {
                var minutes = _sessionTimeoutMs / (60 * 1000);
                return $"{minutes} minutes";
            }
        }

        // Collections for UI binding
        public ObservableCollection<ParallelExecutionType> AvailableExecutionTypes { get; } =
            new ObservableCollection<ParallelExecutionType>(Enum.GetValues<ParallelExecutionType>());

        public ObservableCollection<ParallelResultStrategy> AvailableResultStrategies { get; } =
            new ObservableCollection<ParallelResultStrategy>(Enum.GetValues<ParallelResultStrategy>());

        // Collections for parameter selection
        public ObservableCollection<ParameterConfig> AvailableInputParameters { get; } = 
            new ObservableCollection<ParameterConfig>();
            
        public ObservableCollection<ParameterConfig> AvailableArrayParameters { get; } = 
            new ObservableCollection<ParameterConfig>();

        public ParameterConfig? SelectedListParameter
        {
            get => _selectedListParameter;
            set 
            { 
                if (SetProperty(ref _selectedListParameter, value))
                {
                    // Update ListParameterName when selection changes
                    if (value != null)
                    {
                        ListParameterName = value.Name;
                    }
                    else
                    {
                        ListParameterName = string.Empty;
                    }
                    UpdateConfiguration();
                    NotifyConfigurationChanged();
                }
            }
        }

        public ParameterConfig? SelectedExcludedParameter
        {
            get => _selectedExcludedParameter;
            set => SetProperty(ref _selectedExcludedParameter, value);
        }

        #endregion

        #region Commands

        public ICommand ResetToDefaultsCommand { get; private set; } = null!;
        public ICommand RemoveExcludedParameterCommand { get; private set; } = null!;
        public ICommand AddSelectedExcludedParameterCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
            RemoveExcludedParameterCommand = new RelayCommand<string>(RemoveExcludedParameter);
            AddSelectedExcludedParameterCommand = new RelayCommand(AddSelectedExcludedParameter);
        }

        #endregion

        #region Command Implementations

        private void ResetToDefaults()
        {
            _executionType = ParallelExecutionType.None;
            _maxConcurrency = Environment.ProcessorCount;
            _resultStrategy = ParallelResultStrategy.WaitForAll;
            _listParameterName = string.Empty;
            _externalStringList.Clear();
            _excludedParameters.Clear();
            _sessionTimeoutMs = 30 * 60 * 1000; // 30 minutes
            _externalListText = string.Empty;
            _isAdvancedSettingsExpanded = false;

            NotifyAllPropertiesChanged();
            UpdateConfiguration();
            NotifyConfigurationChanged();
        }

        private void AddExternalListItem()
        {
            // This method is no longer needed as text changes are handled automatically
            // by the ExternalListText property setter
        }

        private void RemoveExcludedParameter(string? parameter)
        {
            if (!string.IsNullOrEmpty(parameter) && _excludedParameters.Contains(parameter))
            {
                _excludedParameters.Remove(parameter);
                UpdateConfiguration();
                NotifyConfigurationChanged();
            }
        }

        private void AddSelectedExcludedParameter()
        {
            if (_selectedExcludedParameter != null && !_excludedParameters.Contains(_selectedExcludedParameter.Name))
            {
                _excludedParameters.Add(_selectedExcludedParameter.Name);
                SelectedExcludedParameter = null; // Clear selection after adding
                UpdateConfiguration();
                NotifyConfigurationChanged();
            }
        }

        private void UpdateExternalListFromText()
        {
            _externalStringList.Clear();
            
            if (!string.IsNullOrWhiteSpace(_externalListText))
            {
                var lines = _externalListText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrEmpty(line))
                    .Distinct();

                foreach (var line in lines)
                {
                    _externalStringList.Add(line);
                }
            }

            UpdateConfiguration();
            NotifyConfigurationChanged();
        }

        private void UpdateExternalListTextFromCollection()
        {
            _externalListText = string.Join(Environment.NewLine, _externalStringList);
        }

        #endregion

        #region Configuration Management

        public NamingConfig GetConfiguration()
        {
            // Update the configuration object with current values
            if (_executionType == ParallelExecutionType.None)
            {
                _config.ParallelConfig = null;
            }
            else
            {
                if (_config.ParallelConfig == null)
                    _config.ParallelConfig = new ParallelExecutionConfig();

                _config.ParallelConfig.ExecutionType = _executionType;
                _config.ParallelConfig.MaxConcurrency = _maxConcurrency;
                _config.ParallelConfig.ResultStrategy = _resultStrategy;
                _config.ParallelConfig.ListParameterName = _listParameterName;
                _config.ParallelConfig.ExternalList = new List<string>(_externalStringList);
                _config.ParallelConfig.ExcludedParameters = new List<string>(_excludedParameters);
                _config.ParallelConfig.SessionTimeoutMs = _sessionTimeoutMs;
            }

            return _config;
        }

        public void UpdateConfiguration(NamingConfig config)
        {
            _config = config ?? new NamingConfig();
            LoadFromConfiguration(_config);
        }

        private void LoadFromConfiguration(NamingConfig config)
        {
            if (config.ParallelConfig != null)
            {
                var parallel = config.ParallelConfig;
                
                _executionType = parallel.ExecutionType;
                _maxConcurrency = parallel.MaxConcurrency;
                _resultStrategy = parallel.ResultStrategy;
                _listParameterName = parallel.ListParameterName ?? string.Empty;
                
                _externalStringList.Clear();
                if (parallel.ExternalList != null)
                {
                    foreach (var item in parallel.ExternalList)
                    {
                        _externalStringList.Add(item);
                    }
                }

                _excludedParameters.Clear();
                if (parallel.ExcludedParameters != null)
                {
                    foreach (var param in parallel.ExcludedParameters)
                    {
                        _excludedParameters.Add(param);
                    }
                }

                _sessionTimeoutMs = parallel.SessionTimeoutMs;
            }
            else
            {
                // Set defaults when no parallel config exists
                _executionType = ParallelExecutionType.None;
                _maxConcurrency = Environment.ProcessorCount;
                _resultStrategy = ParallelResultStrategy.WaitForAll;
                _listParameterName = string.Empty;
                _externalStringList.Clear();
                _excludedParameters.Clear();
                _sessionTimeoutMs = 30 * 60 * 1000;
            }

            UpdateAvailableParameters(config);
            UpdateExternalListTextFromCollection();
            NotifyAllPropertiesChanged();
        }

        private void UpdateAvailableParameters(NamingConfig config)
        {
            var preservedListParam = _listParameterName;
            AvailableInputParameters.Clear();
            AvailableArrayParameters.Clear();

            if (config.InputParameters != null)
            {
                foreach (var param in config.InputParameters)
                {
                    AvailableInputParameters.Add(param);
                    
                    // Only add array parameters to the array collection
                    if (param.Type == ParameterType.Array)
                    {
                        AvailableArrayParameters.Add(param);
                    }
                }
            }
            
            // Restore the selected list parameter if it exists in the available parameters
            if (!string.IsNullOrEmpty(preservedListParam))
            {
                _selectedListParameter = AvailableArrayParameters.FirstOrDefault(p => p.Name == preservedListParam);
                ListParameterName= _selectedListParameter?.Name??string.Empty;
            }
            else
            {
                _selectedListParameter = null;
                ListParameterName = string.Empty;
            }
        }

        private void UpdateConfiguration()
        {
            // This method ensures the internal config object stays in sync
            GetConfiguration();
        }

        #endregion

        #region Event Handling

        private void SubscribeToEvents()
        {
            _eventHub.ConfigurationChanged += OnConfigurationChanged;
            _eventHub.ValidationRequested += OnValidationRequested;
        }

        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            if (e.TabName != "ParallelConfig" && e.ConfigData is NamingConfig newConfig)
            {
                UpdateConfiguration(newConfig);
            }
        }

        private void OnValidationRequested(object? sender, ValidationRequestEventArgs e)
        {
            if (e.TabName == "ParallelConfig")
            {
                // Handle validation if needed
            }
        }

        private void NotifyConfigurationChanged()
        {
            _eventHub.RaiseConfigurationChanged("ParallelConfig", GetConfiguration(),
                ConfigurationChangeType.PropertyChanged, "Parallel execution settings updated");
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
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void NotifyAllPropertiesChanged()
        {
            OnPropertyChanged(nameof(ExecutionType));
            OnPropertyChanged(nameof(MaxConcurrency));
            OnPropertyChanged(nameof(ResultStrategy));
            OnPropertyChanged(nameof(ListParameterName));
            OnPropertyChanged(nameof(SelectedListParameter));
            OnPropertyChanged(nameof(ExternalStringList));
            OnPropertyChanged(nameof(ExcludedParameters));
            OnPropertyChanged(nameof(SessionTimeoutMs));
            OnPropertyChanged(nameof(ExternalListText));
            OnPropertyChanged(nameof(IsAdvancedSettingsExpanded));
            OnPropertyChanged(nameof(IsParallelExecutionEnabled));
            OnPropertyChanged(nameof(IsListBasedExecution));
            OnPropertyChanged(nameof(IsExternalListExecution));
            OnPropertyChanged(nameof(IsParameterBasedExecution));
            OnPropertyChanged(nameof(ConfigurationSummary));
            OnPropertyChanged(nameof(IsConfigurationValid));
            OnPropertyChanged(nameof(SessionTimeoutDisplay));
        }

        #endregion

        public void Dispose()
        {
            if (_eventHub != null)
            {
                _eventHub.ConfigurationChanged -= OnConfigurationChanged;
                _eventHub.ValidationRequested -= OnValidationRequested;
            }
        }
    }

}