using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.LogicalTree;
using DaoStudioUI.ViewModels;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;

namespace DaoStudioUI.Views
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
        }
        
        /// <summary>
        /// Event handler for model button tapped event
        /// </summary>
        private void ModelButton_Tapped(object sender, TappedEventArgs e)
        {
            if (sender is Button button && button.DataContext is IPerson model && DataContext is HomeViewModel viewModel)
            {
                viewModel.StartNewSessCommand.Execute(model);
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// Event handler for session button tapped event
        /// </summary>
        private void SessionButton_Tapped(object sender, TappedEventArgs e)
        {
            if (sender is Button button && button.DataContext is SessionInfo session && DataContext is HomeViewModel viewModel)
            {
                viewModel.OpenSessionCommand.Execute(session);
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// Event handler for session delete button tapped event
        /// </summary>
        private void DeleteButton_Tapped(object sender, TappedEventArgs e)
        {
            // Mark as handled to prevent the event from bubbling up to the parent button
            e.Handled = true;
            
            // Get the session from the DataContext of the button
            if (sender is Button button && button.DataContext is SessionInfo session && DataContext is HomeViewModel viewModel)
            {
                // Execute the delete command
                viewModel.DeleteSessionCommand.Execute(session);
            }
        }

        /// <summary>
        /// Event handler for navigation requests from the getting started guide
        /// </summary>
        private void GettingStartedGuide_NavigationRequested(object? sender, string targetView)
        {
            // Find the main window and trigger navigation
            var mainWindow = this.FindLogicalAncestorOfType<Views.MainWindow>();
            if (mainWindow?.DataContext is MainWindowViewModel mainViewModel)
            {
                var navigationIndex = targetView switch
                {
                    "Settings" => 2,  // Settings page
                    "People" => 4,    // People page  
                    "Tools" => 5,     // Tools page
                    _ => 0            // Default to Home
                };
                
                mainViewModel.NavigationItemSelectedCommand.Execute(navigationIndex);
            }
        }
    }
}