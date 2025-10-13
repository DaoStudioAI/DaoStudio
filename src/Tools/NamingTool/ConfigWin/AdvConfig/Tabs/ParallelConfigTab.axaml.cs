using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Naming.AdvConfig.ViewModels;
using Naming.AdvConfig.Events;
using Naming.ParallelExecution;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Naming.AdvConfig.Tabs
{
    /// <summary>
    /// Advanced parallel execution configuration tab
    /// </summary>
    internal partial class ParallelConfigTab : UserControl
    {
        private ParallelConfigTabViewModel? _viewModel;

        public ParallelConfigTab()
        {
            InitializeComponent();
        }

        public ParallelConfigTab(IConfigurationEventHub eventHub, NamingConfig config)
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
            _viewModel = new ParallelConfigTabViewModel(eventHub, config);
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
        /// Validate the current configuration
        /// </summary>
        public bool ValidateConfiguration()
        {
            return _viewModel?.IsConfigurationValid ?? false;
        }

        /// <summary>
        /// Get validation errors for the current configuration
        /// </summary>
        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (_viewModel != null)
            {
                if (!_viewModel.IsConfigurationValid)
                {
                    if (_viewModel.ExecutionType == ParallelExecutionType.ListBased && 
                        string.IsNullOrWhiteSpace(_viewModel.ListParameterName))
                    {
                        errors.Add("List parameter name is required for list-based execution");
                    }

                    if (_viewModel.ExecutionType == ParallelExecutionType.ExternalList && 
                        _viewModel.ExternalStringList.Count == 0)
                    {
                        errors.Add("At least one item must be provided for external list execution");
                    }

                    if (_viewModel.MaxConcurrency <= 0)
                    {
                        errors.Add("Maximum concurrency must be greater than 0");
                    }

                    if (_viewModel.SessionTimeoutMs < 1000)
                    {
                        errors.Add("Session timeout must be at least 1 second");
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
                _viewModel.ResetToDefaultsCommand.Execute(null);
            }
        }

        /// <summary>
        /// Get tab metadata
        /// </summary>
        public TabMetadata GetTabMetadata()
        {
            return new TabMetadata
            {
                Name = "Parallel Config",
                Description = "Configure parallel execution behavior",
                Icon = "Play",
                IsValid = _viewModel?.IsConfigurationValid ?? false,
                HasChanges = _viewModel?.IsParallelExecutionEnabled ?? false
            };
        }

        /// <summary>
        /// Handle tab activation
        /// </summary>
        public void OnActivated()
        {
            // Refresh the view when tab becomes active
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
        /// Get parallel configuration summary
        /// </summary>
        public ParallelConfigurationSummary GetConfigurationSummary()
        {
            if (_viewModel == null)
            {
                return new ParallelConfigurationSummary();
            }

            return new ParallelConfigurationSummary
            {
                ExecutionType = _viewModel.ExecutionType,
                MaxConcurrency = _viewModel.MaxConcurrency,
                ResultStrategy = _viewModel.ResultStrategy,
                IsEnabled = _viewModel.IsParallelExecutionEnabled,
                IsValid = _viewModel.IsConfigurationValid,
                ListParameterName = _viewModel.ListParameterName,
                ExternalItemCount = _viewModel.ExternalStringList.Count,
                ExcludedParameterCount = _viewModel.ExcludedParameters.Count,
                SessionTimeoutMinutes = _viewModel.SessionTimeoutMs / (60 * 1000)
            };
        }

        /// <summary>
        /// Check if parallel execution is enabled
        /// </summary>
        public bool IsParallelExecutionEnabled()
        {
            return _viewModel?.IsParallelExecutionEnabled ?? false;
        }

        /// <summary>
        /// Get the configured execution type
        /// </summary>
        public ParallelExecutionType GetExecutionType()
        {
            return _viewModel?.ExecutionType ?? ParallelExecutionType.None;
        }

        /// <summary>
        /// Set execution type programmatically
        /// </summary>
        public void SetExecutionType(ParallelExecutionType executionType)
        {
            if (_viewModel != null)
            {
                _viewModel.ExecutionType = executionType;
            }
        }

        /// <summary>
        /// Add external list item
        /// </summary>
        public void AddExternalListItem(string item)
        {
            if (_viewModel != null && !string.IsNullOrWhiteSpace(item))
            {
                // Add the item to the text representation
                var currentText = _viewModel.ExternalListText;
                if (string.IsNullOrEmpty(currentText))
                {
                    _viewModel.ExternalListText = item;
                }
                else
                {
                    _viewModel.ExternalListText = currentText + Environment.NewLine + item;
                }
            }
        }

        /// <summary>
        /// Clear external list
        /// </summary>
        public void ClearExternalList()
        {
            if (_viewModel != null)
            {
                _viewModel.ExternalStringList.Clear();
            }
        }

        /// <summary>
        /// Add excluded parameter
        /// </summary>
        public void AddExcludedParameter(string parameter)
        {
            if (_viewModel != null && !string.IsNullOrWhiteSpace(parameter))
            {
                // Find the parameter in available input parameters
                var paramConfig = _viewModel.AvailableInputParameters.FirstOrDefault(p => p.Name == parameter);
                if (paramConfig != null)
                {
                    _viewModel.SelectedExcludedParameter = paramConfig;
                    _viewModel.AddSelectedExcludedParameterCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Clear excluded parameters
        /// </summary>
        public void ClearExcludedParameters()
        {
            if (_viewModel != null)
            {
                _viewModel.ExcludedParameters.Clear();
            }
        }
    }

    /// <summary>
    /// Parallel configuration summary data
    /// </summary>
    public class ParallelConfigurationSummary
    {
        public ParallelExecutionType ExecutionType { get; set; }
        public int MaxConcurrency { get; set; }
        public ParallelResultStrategy ResultStrategy { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsValid { get; set; }
        public string ListParameterName { get; set; } = string.Empty;
        public int ExternalItemCount { get; set; }
        public int ExcludedParameterCount { get; set; }
        public int SessionTimeoutMinutes { get; set; }
    }
}