using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming.AdvConfig.Events;

namespace Naming.AdvConfig.ViewModels
{
    /// <summary>
    /// ViewModel for the Base Information tab
    /// </summary>
    internal class BaseInfoTabViewModel : INotifyPropertyChanged
    {
        private readonly IConfigurationEventHub _eventHub;
        private string _displayName = string.Empty;
        private string _functionName = "create_subtask";
        private string _functionDescription = "Create a task to handle specific operations";
        private int _maxRecursionLevel = 6;
        private ConfigPerson? _selectedExecutivePerson;
        private bool _isFunctionNameValid = true;

        /// <summary>
        /// Available persons for selection
        /// </summary>
        public ObservableCollection<ConfigPerson> AvailablePersons { get; set; } = new();

        public BaseInfoTabViewModel(IConfigurationEventHub eventHub)
        {
            _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
        }

        #region Properties

        /// <summary>
        /// Display name for the tool instance
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (SetProperty(ref _displayName, value))
                {
                    RaiseConfigurationChanged(nameof(DisplayName));
                }
            }
        }

        /// <summary>
        /// Function name for the naming tool
        /// </summary>
        public string FunctionName
        {
            get => _functionName;
            set
            {
                if (SetProperty(ref _functionName, value))
                {
                    ValidateFunctionName();
                    RaiseConfigurationChanged(nameof(FunctionName));
                }
            }
        }

        /// <summary>
        /// Function description
        /// </summary>
        public string FunctionDescription
        {
            get => _functionDescription;
            set
            {
                if (SetProperty(ref _functionDescription, value))
                {
                    RaiseConfigurationChanged(nameof(FunctionDescription));
                }
            }
        }

        /// <summary>
        /// Maximum recursion level
        /// </summary>
        public int MaxRecursionLevel
        {
            get => _maxRecursionLevel;
            set
            {
                if (SetProperty(ref _maxRecursionLevel, value))
                {
                    RaiseConfigurationChanged(nameof(MaxRecursionLevel));
                }
            }
        }

        /// <summary>
        /// Selected executive person
        /// </summary>
        public ConfigPerson? SelectedExecutivePerson
        {
            get => _selectedExecutivePerson;
            set
            {
                if (SetProperty(ref _selectedExecutivePerson, value))
                {
                    RaiseConfigurationChanged(nameof(SelectedExecutivePerson));
                }
            }
        }

        /// <summary>
        /// Whether the function name is valid
        /// </summary>
        public bool IsFunctionNameValid
        {
            get => _isFunctionNameValid;
            private set => SetProperty(ref _isFunctionNameValid, value);
        }

        /// <summary>
        /// Command to reset configuration to defaults
        /// </summary>
        public ICommand? ResetToDefaultsCommand { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Initialize the view model with existing configuration
        /// </summary>
        public void Initialize(NamingConfig config, List<IHostPerson> availablePersons, string displayName = "")
        {
            // Set display name
            DisplayName = displayName;

            // Load available persons
            AvailablePersons.Clear();
            
            // Add "Use current session's Person" option as the first item
            AvailablePersons.Add(new ConfigPerson 
            { 
                Name = NamingTool.Properties.Resources.Field_ExecutivePerson_UseCurrentSession, 
                Description = NamingTool.Properties.Resources.Field_ExecutivePerson_UseCurrentSession_Description,
                UseCurrentSession = true
            });
            
            if (availablePersons != null)
            {
                foreach (var person in availablePersons)
                {
                    AvailablePersons.Add(new ConfigPerson 
                    { 
                        Name = person.Name, 
                        Description =  person.Description
                    });
                }
            }

            // Load configuration values
            if (config != null)
            {
                FunctionName = config.FunctionName;
                FunctionDescription = config.FunctionDescription;
                MaxRecursionLevel = config.MaxRecursionLevel;

                // Set selected executive person
                if (config.ExecutivePerson != null)
                {
                    SelectedExecutivePerson = AvailablePersons.FirstOrDefault(p =>
                        p.Name == config.ExecutivePerson.Name) ?? config.ExecutivePerson;
                }
            }

            // If no person is selected, default to "Use current session's Person"
            if (SelectedExecutivePerson == null && AvailablePersons.Any())
            {
                SelectedExecutivePerson = AvailablePersons.First(); // This is "Use current session's Person"
            }
        }

        /// <summary>
        /// Get the current configuration state
        /// </summary>
        public BaseInfoConfiguration GetConfiguration()
        {
            return new BaseInfoConfiguration
            {
                DisplayName = DisplayName,
                FunctionName = FunctionName,
                FunctionDescription = FunctionDescription,
                MaxRecursionLevel = MaxRecursionLevel,
                ExecutivePerson = SelectedExecutivePerson?.UseCurrentSession == true ? null : SelectedExecutivePerson
            };
        }

        /// <summary>
        /// Reset to default values
        /// </summary>
        public void ResetToDefaults()
        {
            DisplayName = string.Empty;
            FunctionName = "create_subtask";
            FunctionDescription = "Create a task to handle specific operations";
            MaxRecursionLevel = 6;

            if (AvailablePersons.Any())
            {
                SelectedExecutivePerson = AvailablePersons.First();
            }

            RaiseConfigurationChanged("Reset");
        }

        private void ValidateFunctionName()
        {
            // Function name must contain only lowercase letters and underscores
            IsFunctionNameValid = !string.IsNullOrWhiteSpace(FunctionName) && 
                                 System.Text.RegularExpressions.Regex.IsMatch(FunctionName, @"^[a-z_]+$");
        }

        private void RaiseConfigurationChanged(string propertyPath)
        {
            _eventHub.RaiseConfigurationChanged("BaseInfo", GetConfiguration(), ConfigurationChangeType.PropertyChanged, propertyPath);
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
    /// Configuration data structure for base information
    /// </summary>
    internal class BaseInfoConfiguration
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FunctionName { get; set; } = "create_subtask";
        public string FunctionDescription { get; set; } = string.Empty;
        public int MaxRecursionLevel { get; set; } = 6;
        public ConfigPerson? ExecutivePerson { get; set; }
    }
}