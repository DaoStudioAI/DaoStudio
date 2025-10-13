using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Naming.AdvConfig.Events;

namespace Naming.AdvConfig.ViewModels
{
    /// <summary>
    /// ViewModel for the Input Parameters tab
    /// </summary>
    internal class InputParametersTabViewModel : INotifyPropertyChanged
    {
        private readonly IConfigurationEventHub _eventHub;
        private NamingConfig _config;
        private bool _isInitializing = false;

        public InputParametersTabViewModel(IConfigurationEventHub eventHub, NamingConfig config)
        {
            _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            Parameters = new ObservableCollection<ParameterConfig>();
            
            // Initialize commands
            AddParameterCommand = new RelayCommand(AddParameter);
            RemoveParameterCommand = new RelayCommand<ParameterConfig>(RemoveParameter);
            ClearAllParametersCommand = new RelayCommand(ClearAllParameters);

            LoadConfiguration();
        }

        #region Properties

        /// <summary>
        /// Collection of input parameters
        /// </summary>
        public ObservableCollection<ParameterConfig> Parameters { get; }

        /// <summary>
        /// Number of parameters currently configured
        /// </summary>
        public int ParameterCount => Parameters.Count;

        /// <summary>
        /// Whether there are any parameters configured
        /// </summary>
        public bool HasParameters => Parameters.Count > 0;

        /// <summary>
        /// Whether there are no parameters configured
        /// </summary>
        public bool HasNoParameters => Parameters.Count == 0;

        #endregion

        #region Commands

        public ICommand AddParameterCommand { get; }
        public ICommand RemoveParameterCommand { get; }
        public ICommand ClearAllParametersCommand { get; }

        #endregion

        #region Command Implementations

        private void AddParameter()
        {
            var newParameter = new ParameterConfig
            {
                Name = GenerateUniqueParameterName(),
                Description = "Parameter description",
                Type = ParameterType.String,
                IsRequired = false
            };

            Parameters.Add(newParameter);
            UpdateDependentProperties();
            RaiseConfigurationChanged();
        }

        private void RemoveParameter(ParameterConfig? parameter)
        {
            if (parameter != null && Parameters.Contains(parameter))
            {
                Parameters.Remove(parameter);
                UpdateDependentProperties();
                RaiseConfigurationChanged();
            }
        }

        private void ClearAllParameters()
        {
            if (Parameters.Count > 0)
            {
                Parameters.Clear();
                UpdateDependentProperties();
                RaiseConfigurationChanged();
            }
        }

        #endregion

        #region Helper Methods

        private string GenerateUniqueParameterName()
        {
            var baseName = "parameter";
            var counter = 1;
            var uniqueName = baseName;

            while (Parameters.Any(p => p.Name == uniqueName))
            {
                uniqueName = $"{baseName}{counter}";
                counter++;
            }

            return uniqueName;
        }

        private void UpdateDependentProperties()
        {
            OnPropertyChanged(nameof(ParameterCount));
            OnPropertyChanged(nameof(HasParameters));
            OnPropertyChanged(nameof(HasNoParameters));
        }

        private void LoadConfiguration()
        {
            _isInitializing = true;

            try
            {
                // Load parameters from config
                if (_config.InputParameters != null)
                {
                    Parameters.Clear();
                    foreach (var param in _config.InputParameters)
                    {
                        Parameters.Add(param);
                    }
                }

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
                _config.InputParameters = Parameters.ToList();

                // Raise event
                _eventHub.RaiseConfigurationChanged("Input Parameters", _config, ConfigurationChangeType.PropertyChanged, "InputParameters");
            }
        }

        #endregion

        #region Event Handlers

        public void OnParameterChanged(ParameterConfig parameter)
        {
            RaiseConfigurationChanged();
        }

        #endregion

        /// <summary>
        /// Get the current configuration
        /// </summary>
        public NamingConfig GetConfiguration()
        {
            // Ensure the latest in-memory parameter edits are captured
            _config.InputParameters = Parameters.ToList();
            return _config;
        }

        /// <summary>
        /// Update the configuration
        /// </summary>
        public void UpdateConfiguration(NamingConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            LoadConfiguration();
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Simple relay command implementation
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Generic relay command implementation
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

        public void Execute(object? parameter) => _execute((T?)parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}