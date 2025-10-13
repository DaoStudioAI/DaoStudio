using Avalonia.Controls;
using Naming.AdvConfig.ViewModels;
using Naming.AdvConfig.Events;
using Naming.AdvConfig.Controls;
using System;
using Naming;

namespace Naming.AdvConfig.Tabs
{
    /// <summary>
    /// Code-behind for the Error Handling tab
    /// </summary>
    public partial class ErrorHandlingTab : UserControl
    {
        public ErrorHandlingTabViewModel? ViewModel { get; private set; }

        public ErrorHandlingTab()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initialize the tab with the event hub and configuration
        /// </summary>
        /// <param name="eventHub">The configuration event hub</param>
        /// <param name="config">The naming configuration</param>
        public void Initialize(IConfigurationEventHub eventHub, NamingConfig config)
        {
            ViewModel = new ErrorHandlingTabViewModel(eventHub);
            ViewModel.Initialize(config);
            DataContext = ViewModel;
        }

        /// <summary>
        /// Get the current configuration from this tab
        /// </summary>
        /// <returns>The current error handling configuration</returns>
        public ErrorHandlingConfiguration GetConfiguration()
        {
            return ViewModel?.GetConfiguration() ?? new ErrorHandlingConfiguration();
        }

        private void ErrorParameterEditor_RemoveRequested(object? sender, EventArgs e)
        {
            if (sender is ParameterTypeEditor editor && ViewModel != null)
            {
                var parameter = editor.GetParameter();
                if (parameter != null && ViewModel.RemoveErrorParameterCommand.CanExecute(parameter))
                {
                    ViewModel.RemoveErrorParameterCommand.Execute(parameter);
                }
            }
        }

        private void ErrorParameterEditor_ParameterChanged(object? sender, ParameterConfig parameter)
        {
            ViewModel?.OnErrorReportingParameterChanged(parameter);
        }
    }
}