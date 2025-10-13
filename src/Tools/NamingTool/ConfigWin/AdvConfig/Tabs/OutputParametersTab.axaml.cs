using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Naming.AdvConfig.ViewModels;
using Naming.AdvConfig.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Naming.AdvConfig.Controls;
using Naming;

namespace Naming.AdvConfig.Tabs
{
    /// <summary>
    /// Advanced output parameters configuration tab
    /// </summary>
    internal partial class OutputParametersTab : UserControl
    {
        private OutputParametersTabViewModel? _viewModel;

        public OutputParametersTab()
        {
            InitializeComponent();
        }

        public OutputParametersTab(IConfigurationEventHub eventHub, NamingConfig config)
        {
            InitializeComponent();
            Initialize(eventHub, config);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Initialize the tab with event hub and configuration
        /// </summary>
        public void Initialize(IConfigurationEventHub eventHub, NamingConfig config)
        {
            _viewModel = new OutputParametersTabViewModel(eventHub, config);
            DataContext = _viewModel;
        }

        /// <summary>
        /// Get the current configuration from the tab
        /// </summary>
        public NamingConfig GetConfiguration()
        {
            return _viewModel?.GetConfiguration() ?? new NamingConfig();
        }

        private void ParameterEditor_RemoveRequested(object? sender, EventArgs e)
        {
            if (sender is ParameterTypeEditor editor && _viewModel != null)
            {
                var parameter = editor.GetParameter();
                if (parameter != null)
                {
                    _viewModel.RemoveReturnParameterCommand.Execute(parameter);
                }
            }
        }

        private void ParameterEditor_ParameterChanged(object? sender, ParameterConfig e)
        {
            _viewModel?.OnParameterChanged(e);
        }

        /// <summary>
        /// Update the tab with new configuration
        /// </summary>
        public void UpdateConfiguration(NamingConfig config)
        {
            _viewModel?.UpdateConfiguration(config);
        }

        /// <summary>
        /// Validate the current configuration
        /// </summary>
        public bool ValidateConfiguration()
        {
            // Simple validation without relying on removed ValidateOutputsCommand
            return GetValidationErrors().Count == 0;
        }

        /// <summary>
        /// Get validation errors for the current configuration
        /// </summary>
        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (_viewModel != null)
            {
                // Check for basic validation issues
                if (string.IsNullOrWhiteSpace(_viewModel.ReturnToolName))
                {
                    errors.Add("Return tool name is required");
                }

                if (string.IsNullOrWhiteSpace(_viewModel.ReturnToolDescription))
                {
                    errors.Add("Return tool description is required");
                }

                // Check for parameter-specific validation
                if (_viewModel.HasReturnParameters)
                {
                    var duplicateNames = _viewModel.ReturnParameters
                        .GroupBy(p => p.Name)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key);

                    foreach (var name in duplicateNames)
                    {
                        errors.Add($"Duplicate return parameter name: {name}");
                    }

                    var emptyNames = _viewModel.ReturnParameters.Where(p => string.IsNullOrWhiteSpace(p.Name));
                    foreach (var param in emptyNames)
                    {
                        errors.Add("Return parameter has empty name");
                    }

                    // Validate complex types
                    foreach (var param in _viewModel.ReturnParameters)
                    {
                        if (param.Type == ParameterType.Object && 
                            (param.ObjectProperties == null || param.ObjectProperties.Count == 0))
                        {
                            errors.Add($"Object parameter '{param.Name}' has no properties defined");
                        }
                        else if (param.Type == ParameterType.Array && param.ArrayElementConfig == null)
                        {
                            errors.Add($"Array parameter '{param.Name}' has no element type defined");
                        }
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Reset the tab to default state
        /// </summary>
        public void Reset()
        {
            if (_viewModel != null)
            {
                _viewModel.ClearAllReturnParametersCommand.Execute(null);
                _viewModel.ReturnToolName = "set_result";
                _viewModel.ReturnToolDescription = "Report back with the result after completion";
            }
        }

        /// <summary>
        /// Load default return parameters
        /// </summary>
        public void LoadDefaults()
        {
            if (_viewModel != null)
            {
                _viewModel.ClearAllReturnParametersCommand.Execute(null);
                
                // Add default return parameters directly
                var defaultParams = new[]
                {
                    new ParameterConfig
                    {
                        Name = "success",
                        Description = "Whether the operation was successful",
                        Type = ParameterType.Bool,
                        IsRequired = true
                    },
                    new ParameterConfig
                    {
                        Name = "message",
                        Description = "Status message or result description",
                        Type = ParameterType.String,
                        IsRequired = false
                    },
                    new ParameterConfig
                    {
                        Name = "data",
                        Description = "Operation result data",
                        Type = ParameterType.Object,
                        IsRequired = false,
                        ObjectProperties = new List<ParameterConfig>
                        {
                            new ParameterConfig 
                            { 
                                Name = "result", 
                                Description = "Main result value", 
                                Type = ParameterType.String, 
                                IsRequired = false 
                            }
                        }
                    }
                };

                foreach (var param in defaultParams)
                {
                    _viewModel.ReturnParameters.Add(param);
                }
            }
        }

        /// <summary>
        /// Export current configuration
        /// </summary>
        public string ExportConfiguration()
        {
            if (_viewModel != null)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(_viewModel.ReturnParameters.ToList(), 
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    return json;
                }
                catch
                {
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Import configuration from JSON
        /// </summary>
        public void ImportConfiguration(string json)
        {
            if (_viewModel != null && !string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var importedParameters = System.Text.Json.JsonSerializer.Deserialize<List<ParameterConfig>>(json);
                    if (importedParameters != null)
                    {
                        _viewModel.ReturnParameters.Clear();
                        foreach (var param in importedParameters)
                        {
                            _viewModel.ReturnParameters.Add(param);
                        }
                    }
                }
                catch
                {
                    // Silently ignore import errors for now
                }
            }
        }

        /// <summary>
        /// Generate test output based on current configuration
        /// </summary>
        public void GenerateTestOutput()
        {
            // Method kept for compatibility but functionality simplified
            // since the Test Output button was removed from UI
        }

        /// <summary>
        /// Generate JSON schema for current configuration
        /// </summary>
        public void GenerateSchema()
        {
            // Method kept for compatibility but functionality simplified
            // since the Generate Schema button was removed from UI
        }

        /// <summary>
        /// Add a new return parameter
        /// </summary>
        public void AddReturnParameter()
        {
            if (_viewModel != null)
            {
                _viewModel.AddReturnParameterCommand.Execute(null);
            }
        }

        /// <summary>
        /// Get tab metadata
        /// </summary>
        public TabMetadata GetTabMetadata()
        {
            return new TabMetadata
            {
                Name = "Output Parameters",
                Description = "Configure return tool and output parameters",
                Icon = "ArrowExport",
                IsValid = GetValidationErrors().Count == 0,
                HasChanges = _viewModel?.HasReturnParameters ?? false
            };
        }

        /// <summary>
        /// Handle tab activation
        /// </summary>
        public void OnActivated()
        {
            // Refresh the view when tab becomes active
            // Simplified since schema generation was removed
        }

        /// <summary>
        /// Handle tab deactivation
        /// </summary>
        public void OnDeactivated()
        {
            // Save any pending changes when leaving the tab
            // This is handled automatically through the ViewModel
        }

        /// <summary>
        /// Get output configuration summary
        /// </summary>
        public OutputConfigurationSummary GetConfigurationSummary()
        {
            if (_viewModel == null)
            {
                return new OutputConfigurationSummary();
            }

            return new OutputConfigurationSummary
            {
                ToolName = _viewModel.ReturnToolName,
                ToolDescription = _viewModel.ReturnToolDescription,
                ParameterCount = _viewModel.ReturnParameterCount,
                HasParameters = _viewModel.HasReturnParameters,
                IsComplete = _viewModel.IsToolConfigurationComplete
            };
        }

        /// <summary>
        /// Set tool configuration
        /// </summary>
        public void SetToolConfiguration(string toolName, string toolDescription)
        {
            if (_viewModel != null)
            {
                _viewModel.ReturnToolName = toolName;
                _viewModel.ReturnToolDescription = toolDescription;
            }
        }

        /// <summary>
        /// Check if current configuration has required parameters
        /// </summary>
        public bool HasRequiredParameters()
        {
            return _viewModel?.ReturnParameters.Any(p => p.IsRequired) ?? false;
        }

        /// <summary>
        /// Get list of parameter names
        /// </summary>
        public List<string> GetParameterNames()
        {
            return _viewModel?.ReturnParameters.Select(p => p.Name).ToList() ?? new List<string>();
        }

        /// <summary>
        /// Check if a parameter name already exists
        /// </summary>
        public bool HasParameterName(string parameterName)
        {
            return _viewModel?.ReturnParameters.Any(p => p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase)) ?? false;
        }

        /// <summary>
        /// Get parameter by name
        /// </summary>
        public ParameterConfig? GetParameterByName(string parameterName)
        {
            return _viewModel?.ReturnParameters.FirstOrDefault(p => p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Update parameter configuration
        /// </summary>
        public void UpdateParameter(string parameterName, ParameterConfig newConfig)
        {
            if (_viewModel != null)
            {
                var existing = _viewModel.ReturnParameters.FirstOrDefault(p => p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    var index = _viewModel.ReturnParameters.IndexOf(existing);
                    _viewModel.ReturnParameters[index] = newConfig;
                }
            }
        }
    }

    /// <summary>
    /// Output configuration summary data
    /// </summary>
    public class OutputConfigurationSummary
    {
        public string ToolName { get; set; } = string.Empty;
        public string ToolDescription { get; set; } = string.Empty;
        public int ParameterCount { get; set; }
        public bool HasParameters { get; set; }
        public bool IsComplete { get; set; }
    }
}