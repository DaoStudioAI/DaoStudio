using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Naming.AdvConfig.ViewModels;
using Naming.AdvConfig.Events;
using Naming.AdvConfig.Controls;
using System;
using System.Linq;
using Naming;

namespace Naming.AdvConfig.Tabs
{
    /// <summary>
    /// Advanced input parameters configuration tab
    /// </summary>
    internal partial class InputParametersTab : UserControl
    {
        private InputParametersTabViewModel? _viewModel;

        public InputParametersTab()
        {
            InitializeComponent();
        }

        public InputParametersTab(IConfigurationEventHub eventHub, NamingConfig config)
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
            _viewModel = new InputParametersTabViewModel(eventHub, config);
            DataContext = _viewModel;

            // Event wiring will be handled by the ParameterTypeEditor controls themselves
            // through their data binding and command patterns
        }

        /// <summary>
        /// Get the current configuration from the tab
        /// </summary>
        public NamingConfig GetConfiguration()
        {
            return _viewModel?.GetConfiguration() ?? new NamingConfig();
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
            // Basic validation is handled through the parameter collection
            return GetValidationErrors().Count == 0;
        }

        /// <summary>
        /// Get validation errors for the current configuration
        /// </summary>
        public System.Collections.Generic.List<string> GetValidationErrors()
        {
            var errors = new System.Collections.Generic.List<string>();

            if (_viewModel != null)
            {
                // Check for basic validation issues
                if (_viewModel.Parameters.Count == 0)
                {
                    // No parameters is valid - it's optional
                }
                else
                {
                    // Check for parameter-specific validation
                    var duplicateNames = _viewModel.Parameters
                        .GroupBy(p => p.Name)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key);

                    foreach (var name in duplicateNames)
                    {
                        errors.Add($"Duplicate parameter name: {name}");
                    }

                    var emptyNames = _viewModel.Parameters.Where(p => string.IsNullOrWhiteSpace(p.Name));
                    foreach (var param in emptyNames)
                    {
                        errors.Add("Parameter has empty name");
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
                _viewModel.ClearAllParametersCommand.Execute(null);
            }
        }

        /// <summary>
        /// Export current configuration
        /// </summary>
        public string ExportConfiguration()
        {
            if (_viewModel != null)
            {
                return System.Text.Json.JsonSerializer.Serialize(_viewModel.Parameters.ToList(), 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
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
                    var importedParameters = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<ParameterConfig>>(json);
                    if (importedParameters != null)
                    {
                        _viewModel.Parameters.Clear();
                        foreach (var param in importedParameters)
                        {
                            _viewModel.Parameters.Add(param);
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Handle import errors silently or through logging
                }
            }
        }

        /// <summary>
        /// Get tab metadata
        /// </summary>
        public TabMetadata GetTabMetadata()
        {
            return new TabMetadata
            {
                Name = "Input Parameters",
                Description = "Configure input parameters and validation",
                Icon = "DataUsage",
                IsValid = GetValidationErrors().Count == 0,
                HasChanges = _viewModel?.HasParameters ?? false
            };
        }

        /// <summary>
        /// Handle tab activation
        /// </summary>
        public void OnActivated()
        {
            // No special handling needed for activation
        }

        /// <summary>
        /// Handle tab deactivation
        /// </summary>
        public void OnDeactivated()
        {
            // Save any pending changes when leaving the tab
            // This is handled automatically through the ViewModel
        }

        private void ParameterEditor_RemoveRequested(object? sender, EventArgs e)
        {
            if (sender is ParameterTypeEditor editor && _viewModel != null)
            {
                var parameter = editor.GetParameter();
                if (parameter != null)
                {
                    _viewModel.RemoveParameterCommand.Execute(parameter);
                }
            }
        }

        private void ParameterEditor_ParameterChanged(object? sender, ParameterConfig e)
        {
            _viewModel?.OnParameterChanged(e);
        }
    }

    /// <summary>
    /// Tab metadata for display purposes
    /// </summary>
    public class TabMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public bool HasChanges { get; set; }
    }
}