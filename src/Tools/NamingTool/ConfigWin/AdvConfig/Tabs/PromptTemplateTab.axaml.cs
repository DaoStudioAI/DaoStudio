using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Naming.AdvConfig.ViewModels;
using Naming.AdvConfig.Events;
using System;
using System.Collections.Generic;

namespace Naming.AdvConfig.Tabs
{
    /// <summary>
    /// Advanced prompt template configuration tab
    /// </summary>
    internal partial class PromptTemplateTab : UserControl
    {
        private PromptTemplateTabViewModel? _viewModel;

        public PromptTemplateTab()
        {
            InitializeComponent();
        }

        public PromptTemplateTab(IConfigurationEventHub eventHub, NamingConfig config)
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
            _viewModel = new PromptTemplateTabViewModel(eventHub, config);
            DataContext = _viewModel;
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
        /// Get validation errors for the current configuration
        /// </summary>
        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (_viewModel != null)
            {
                // Check for basic template validation
                if (string.IsNullOrWhiteSpace(_viewModel.PromptTemplate))
                {
                    errors.Add("Prompt template is empty");
                }

                // Check for undefined parameters
                var undefinedParams = new List<string>();
                foreach (var param in _viewModel.DetectedParameters)
                {
                    if (!param.IsDefined)
                    {
                        undefinedParams.Add(param.Name);
                    }
                }

                if (undefinedParams.Count > 0)
                {
                    errors.Add($"Undefined parameters: {string.Join(", ", undefinedParams)}");
                }

                // Check for balanced braces
                if (!AreParameterBracesBalanced(_viewModel.PromptTemplate))
                {
                    errors.Add("Template contains unbalanced parameter braces");
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
                _viewModel.ResetTemplateCommand.Execute(null);
            }
        }

        /// <summary>
        /// Export current template configuration
        /// </summary>
        public string ExportConfiguration()
        {
            if (_viewModel != null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    promptTemplate = _viewModel.PromptTemplate,
                    detectedParameters = _viewModel.DetectedParameters
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            return string.Empty;
        }

        /// <summary>
        /// Import template configuration from JSON
        /// </summary>
        public void ImportConfiguration(string json)
        {
            if (_viewModel != null && !string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    using var document = System.Text.Json.JsonDocument.Parse(json);
                    var root = document.RootElement;
                    
                    if (root.TryGetProperty("promptTemplate", out var promptTemplate))
                    {
                        _viewModel.PromptTemplate = promptTemplate.GetString() ?? string.Empty;
                    }
                }
                catch (Exception)
                {
                    // Handle import error - could show a message or log
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
                Name = "Prompt Template",
                Description = "Configure prompt templates and AI settings",
                Icon = "Document",
                IsValid = GetValidationErrors().Count == 0,
                HasChanges = !string.IsNullOrEmpty(_viewModel?.PromptTemplate)
            };
        }

        /// <summary>
        /// Handle tab activation
        /// </summary>
        public void OnActivated()
        {
            // Tab activated - could refresh data if needed
        }

        /// <summary>
        /// Handle tab deactivation
        /// </summary>
        public void OnDeactivated()
        {
            // Auto-save when leaving the tab - handled automatically through ViewModel
        }

        /// <summary>
        /// Check if template braces are balanced
        /// </summary>
        private bool AreParameterBracesBalanced(string template)
        {
            if (string.IsNullOrEmpty(template)) return true;
            
            int openCount = 0;
            foreach (char c in template)
            {
                if (c == '{') openCount++;
                else if (c == '}') openCount--;
                if (openCount < 0) return false;
            }
            return openCount == 0;
        }

        /// <summary>
        /// Load a template by type
        /// </summary>
        public void LoadTemplate(string templateType)
        {
            // This functionality has been removed - only basic parameter insertion is available now
        }

        /// <summary>
        /// Get statistics about the template
        /// </summary>
        public TemplateStatistics GetTemplateStatistics()
        {
            if (_viewModel == null)
            {
                return new TemplateStatistics();
            }

            return new TemplateStatistics
            {
                Length = _viewModel.TemplateLength,
                Lines = _viewModel.TemplateLines,
                ParameterCount = _viewModel.ParameterCount
            };
        }
    }

    /// <summary>
    /// Template statistics data
    /// </summary>
    public class TemplateStatistics
    {
        public int Length { get; set; }
        public int Lines { get; set; }
        public int ParameterCount { get; set; }
    }
}