using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using Naming.AdvConfig.Events;

namespace Naming.AdvConfig.ViewModels
{
    /// <summary>
    /// ViewModel for the Output Parameters tab
    /// </summary>
    internal class OutputParametersTabViewModel : INotifyPropertyChanged
    {
        private readonly IConfigurationEventHub _eventHub;
        private NamingConfig _config;
        private bool _isInitializing = false;
        private string _returnToolName = "set_result";
        private string _returnToolDescription = "Report back with the result after completion";

        public OutputParametersTabViewModel(IConfigurationEventHub eventHub, NamingConfig config)
        {
            _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            ReturnParameters = new ObservableCollection<ParameterConfig>();
            
            // Initialize commands
            AddReturnParameterCommand = new RelayCommand(AddReturnParameter);
            RemoveReturnParameterCommand = new RelayCommand<ParameterConfig>(RemoveReturnParameter);
            ClearAllReturnParametersCommand = new RelayCommand(ClearAllReturnParameters);

            LoadConfiguration();
        }

        #region Properties

        /// <summary>
        /// Collection of return parameters
        /// </summary>
        public ObservableCollection<ParameterConfig> ReturnParameters { get; }

        /// <summary>
        /// Name of the return tool
        /// </summary>
        public string ReturnToolName
        {
            get => _returnToolName;
            set
            {
                if (_returnToolName != value)
                {
                    _returnToolName = value;
                    OnPropertyChanged();
                    RaiseConfigurationChanged();
                }
            }
        }

        /// <summary>
        /// Description of the return tool
        /// </summary>
        public string ReturnToolDescription
        {
            get => _returnToolDescription;
            set
            {
                if (_returnToolDescription != value)
                {
                    _returnToolDescription = value;
                    OnPropertyChanged();
                    RaiseConfigurationChanged();
                }
            }
        }

        /// <summary>
        /// Number of return parameters currently configured
        /// </summary>
        public int ReturnParameterCount => ReturnParameters.Count;

        /// <summary>
        /// Whether there are any return parameters configured
        /// </summary>
        public bool HasReturnParameters => ReturnParameters.Count > 0;

        /// <summary>
        /// Whether there are no return parameters configured
        /// </summary>
        public bool HasNoReturnParameters => ReturnParameters.Count == 0;

        /// <summary>
        /// Whether tool configuration is complete
        /// </summary>
        public bool IsToolConfigurationComplete => 
            !string.IsNullOrWhiteSpace(ReturnToolName) && 
            !string.IsNullOrWhiteSpace(ReturnToolDescription) &&
            HasReturnParameters;

        #endregion

        #region Commands

        public ICommand AddReturnParameterCommand { get; }
        public ICommand RemoveReturnParameterCommand { get; }
        public ICommand ClearAllReturnParametersCommand { get; }

        #endregion

        #region Command Implementations

        private void AddReturnParameter()
        {
            var newParameter = new ParameterConfig
            {
                Name = GenerateUniqueParameterName(),
                Description = "Return parameter description",
                Type = ParameterType.String,
                IsRequired = false
            };

            ReturnParameters.Add(newParameter);
            UpdateDependentProperties();
            RaiseConfigurationChanged();
        }

        private void RemoveReturnParameter(ParameterConfig? parameter)
        {
            if (parameter != null && ReturnParameters.Contains(parameter))
            {
                ReturnParameters.Remove(parameter);
                UpdateDependentProperties();
                RaiseConfigurationChanged();
            }
        }

        private void ClearAllReturnParameters()
        {
            if (ReturnParameters.Count > 0)
            {
                ReturnParameters.Clear();
                UpdateDependentProperties();
                RaiseConfigurationChanged();
            }
        }

        #endregion

        #region Helper Methods

        private string GenerateUniqueParameterName()
        {
            var baseName = "returnParam";
            var counter = 1;
            var uniqueName = baseName;

            while (ReturnParameters.Any(p => p.Name == uniqueName))
            {
                uniqueName = $"{baseName}{counter}";
                counter++;
            }

            return uniqueName;
        }

        private void UpdateDependentProperties()
        {
            OnPropertyChanged(nameof(ReturnParameterCount));
            OnPropertyChanged(nameof(HasReturnParameters));
            OnPropertyChanged(nameof(HasNoReturnParameters));
            OnPropertyChanged(nameof(IsToolConfigurationComplete));
        }

        private void LoadConfiguration()
        {
            _isInitializing = true;

            try
            {
                // Load return tool configuration
                _returnToolName = _config.ReturnToolName;
                _returnToolDescription = _config.ReturnToolDescription;

                // Load return parameters from config
                if (_config.ReturnParameters != null)
                {
                    ReturnParameters.Clear();
                    foreach (var param in _config.ReturnParameters)
                    {
                        ReturnParameters.Add(param);
                    }
                }

                // Update all dependent properties
                OnPropertyChanged(nameof(ReturnToolName));
                OnPropertyChanged(nameof(ReturnToolDescription));
                UpdateDependentProperties();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void RaiseConfigurationChanged()
        {
            if (!_isInitializing)
            {
                // Update the config object
                _config.ReturnToolName = _returnToolName;
                _config.ReturnToolDescription = _returnToolDescription;
                _config.ReturnParameters = ReturnParameters.ToList();

                // Raise event
                _eventHub.RaiseConfigurationChanged("Output Parameters", _config, ConfigurationChangeType.PropertyChanged, "ReturnParameters");
            }
        }

        public NamingConfig GetConfiguration()
        {
            return _config;
        }

        public void UpdateConfiguration(NamingConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            LoadConfiguration();
        }

        public void OnParameterChanged(ParameterConfig parameter)
        {
            RaiseConfigurationChanged();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}