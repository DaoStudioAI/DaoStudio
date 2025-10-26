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
using Naming.ParallelExecution;

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

        /// <summary>
        /// Auto-populate the template with appropriate parameter placeholders based on configuration
        /// </summary>
        public void AutoPopulateTemplate()
        {
            var placeholders = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void AddPlaceholder(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                if (seen.Add(value))
                {
                    placeholders.Add(value);
                }
            }

            void AddBlankLine()
            {
                if (placeholders.Count == 0)
                {
                    return;
                }

                if (placeholders[placeholders.Count - 1].Length == 0)
                {
                    return;
                }

                placeholders.Add(string.Empty);
            }

            bool isParallelEnabled = _config.ParallelConfig != null &&
                                     _config.ParallelConfig.ExecutionType != ParallelExecutionType.None;

            if (isParallelEnabled)
            {
                AddPlaceholder("{{_Parameter.Name}}");
                AddPlaceholder("{{_Parameter.Value}}");

                var parallelConfig = _config.ParallelConfig!;

                switch (parallelConfig.ExecutionType)
                {
                    case ParallelExecutionType.ParameterBased:
                        foreach (var parameter in _config.InputParameters ?? Enumerable.Empty<ParameterConfig>())
                        {
                            foreach (var nested in EnumerateParallelValuePlaceholders(parameter, "_Parameter.Value"))
                            {
                                AddPlaceholder(nested);
                            }
                        }
                        break;

                    case ParallelExecutionType.ListBased:
                        var listParameter = _config.InputParameters?.FirstOrDefault(p =>
                            p != null &&
                            !string.IsNullOrWhiteSpace(p.Name) &&
                            string.Equals(p.Name, parallelConfig.ListParameterName, StringComparison.Ordinal));

                        if (listParameter != null)
                        {
                            var elementConfig = listParameter.ArrayElementConfig ?? listParameter;
                            foreach (var nested in EnumerateParallelValuePlaceholders(elementConfig, "_Parameter.Value"))
                            {
                                AddPlaceholder(nested);
                            }
                        }
                        break;

                    case ParallelExecutionType.ExternalList:
                        // Value is a string; no additional placeholders beyond _Parameter.Value
                        break;
                }
            }

            if (_config.InputParameters != null && _config.InputParameters.Count > 0)
            {
                if (placeholders.Count > 0)
                {
                    AddBlankLine();
                }

                foreach (var parameter in _config.InputParameters)
                {
                    foreach (var placeholder in EnumerateParameterPlaceholders(parameter))
                    {
                        AddPlaceholder(placeholder);
                    }
                }
            }

            if (placeholders.Count > 0)
            {
                PromptTemplate = string.Join("\n", placeholders);
            }
        }

        private static IEnumerable<string> EnumerateParameterPlaceholders(ParameterConfig? parameter, string? parentPath = null)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
            {
                yield break;
            }

            var currentPath = string.IsNullOrEmpty(parentPath) ? parameter.Name : $"{parentPath}.{parameter.Name}";
            yield return $"{{{{{currentPath}}}}}";

            if (parameter.IsObject && parameter.ObjectProperties != null)
            {
                foreach (var property in parameter.ObjectProperties)
                {
                    foreach (var nested in EnumerateParameterPlaceholders(property, currentPath))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateParallelValuePlaceholders(ParameterConfig? parameter, string basePath)
        {
            if (parameter == null)
            {
                yield break;
            }

            if (parameter.IsArray && parameter.ArrayElementConfig != null)
            {
                foreach (var nested in EnumerateParallelValuePlaceholders(parameter.ArrayElementConfig, basePath))
                {
                    yield return nested;
                }
                yield break;
            }

            if (parameter.IsObject && parameter.ObjectProperties != null)
            {
                foreach (var property in parameter.ObjectProperties)
                {
                    if (string.IsNullOrWhiteSpace(property.Name))
                    {
                        continue;
                    }

                    var currentPath = $"{basePath}.{property.Name}";
                    yield return $"{{{{{currentPath}}}}}";

                    foreach (var nested in EnumerateParallelValuePlaceholders(property, currentPath))
                    {
                        yield return nested;
                    }
                }
            }
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