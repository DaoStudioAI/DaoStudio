using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Naming.AdvConfig.Events;

namespace Naming.AdvConfig.ViewModels
{
    /// <summary>
    /// ViewModel for the Prompt Template tab
    /// </summary>
    internal class PromptTemplateTabViewModel : INotifyPropertyChanged
    {
        private readonly IConfigurationEventHub _eventHub;
        private NamingConfig _config;
        private bool _isInitializing = false;
        private string _promptTemplate = string.Empty;

        public PromptTemplateTabViewModel(IConfigurationEventHub eventHub, NamingConfig config)
        {
            _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            DetectedParameters = new ObservableCollection<ParameterPlaceholder>();
            
            // Initialize commands
            InsertInputParametersCommand = new RelayCommand(InsertInputParameters);
            ResetTemplateCommand = new RelayCommand(ResetTemplate);

            LoadConfiguration();
            UpdateParameterDetection();
        }

        #region Properties

        /// <summary>
        /// The main prompt template
        /// </summary>
        public string PromptTemplate
        {
            get => _promptTemplate;
            set
            {
                if (_promptTemplate != value)
                {
                    _promptTemplate = value;
                    OnPropertyChanged();
                    UpdateParameterDetection();
                    RaiseConfigurationChanged();
                }
            }
        }

        /// <summary>
        /// Collection of detected parameter placeholders
        /// </summary>
        public ObservableCollection<ParameterPlaceholder> DetectedParameters { get; }

        /// <summary>
        /// Number of characters in the template
        /// </summary>
        public int TemplateLength => _promptTemplate.Length;

        /// <summary>
        /// Number of lines in the template
        /// </summary>
        public int TemplateLines => _promptTemplate.Count(c => c == '\n') + 1;

        /// <summary>
        /// Number of detected parameters
        /// </summary>
        public int ParameterCount => DetectedParameters.Count;

        #endregion

        #region Commands

        public ICommand InsertInputParametersCommand { get; }
        public ICommand ResetTemplateCommand { get; }

        #endregion

        #region Command Implementations

        private void InsertInputParameters()
        {
            if (_config.InputParameters == null || _config.InputParameters.Count == 0)
            {
                return;
            }

            var parametersToInsert = new List<string>();
            
            foreach (var parameter in _config.InputParameters)
            {
                if (!string.IsNullOrWhiteSpace(parameter.Name))
                {
                    var placeholder = $"{{{{{parameter.Name}}}}}";
                    // Only add if not already present in the template
                    if (!_promptTemplate.Contains(placeholder))
                    {
                        parametersToInsert.Add(placeholder);
                    }
                }
            }

            if (parametersToInsert.Count > 0)
            {
                var currentTemplate = _promptTemplate;
                
                // Add a separator if the template is not empty
                if (!string.IsNullOrEmpty(currentTemplate) && !currentTemplate.EndsWith("\n"))
                {
                    currentTemplate += "\n";
                }
                
                // Insert parameters as placeholders, each on a new line for readability
                var parametersText = string.Join("\n", parametersToInsert);
                PromptTemplate = currentTemplate + parametersText;
            }
        }

        private void ResetTemplate()
        {
            PromptTemplate = string.Empty;
        }

        #endregion

        #region Helper Methods

        private void UpdateParameterDetection()
        {
            DetectedParameters.Clear();

            if (string.IsNullOrWhiteSpace(_promptTemplate))
            {
                OnParameterCountChanged();
                return;
            }

            // Detect parameter placeholders: {parameterName}
            var parameterPattern = @"\{([^}]+)\}";
            var matches = Regex.Matches(_promptTemplate, parameterPattern);

            var detectedParams = new HashSet<string>();
            foreach (Match match in matches)
            {
                var paramName = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(paramName) && detectedParams.Add(paramName))
                {
                    DetectedParameters.Add(new ParameterPlaceholder
                    {
                        Name = paramName,
                        Position = match.Index,
                        IsDefined = IsParameterDefined(paramName)
                    });
                }
            }

            OnParameterCountChanged();
        }

        private bool IsParameterDefined(string parameterName)
        {
            // Check if parameter is defined in input parameters
            // This would need to query the input parameters configuration
            return _config.InputParameters?.Any(p => p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase)) ?? false;
        }

        private bool AreParameterBracesBalanced(string template)
        {
            int openCount = 0;
            foreach (char c in template)
            {
                if (c == '{') openCount++;
                else if (c == '}') openCount--;
                if (openCount < 0) return false;
            }
            return openCount == 0;
        }

        private void OnParameterCountChanged()
        {
            OnPropertyChanged(nameof(ParameterCount));
        }

        private void LoadConfiguration()
        {
            _isInitializing = true;

            try
            {
                // Load template from config - using PromptMessage as the prompt template
                if (!string.IsNullOrEmpty(_config.PromptMessage))
                {
                    _promptTemplate = _config.PromptMessage;
                }
                
                OnPropertyChanged(nameof(PromptTemplate));
                OnPropertyChanged(nameof(TemplateLength));
                OnPropertyChanged(nameof(TemplateLines));
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
                // Update the config object - using PromptMessage as the prompt template
                _config.PromptMessage = _promptTemplate;

                // Raise event
                _eventHub.RaiseConfigurationChanged("Prompt Template", _config, ConfigurationChangeType.PropertyChanged, "PromptMessage");
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

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Represents a parameter placeholder in the template
    /// </summary>
    public class ParameterPlaceholder
    {
        /// <summary>
        /// Parameter name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Position in the template
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Whether this parameter is defined in input parameters
        /// </summary>
        public bool IsDefined { get; set; }

        /// <summary>
        /// Display text for the parameter
        /// </summary>
        public string DisplayText => $"{{{Name}}}";

        /// <summary>
        /// Status text
        /// </summary>
        public string Status => IsDefined ? "Defined" : "Undefined";
    }
}