using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Windows.Input;
using Naming.AdvConfig.ViewModels;
using Naming.AdvConfig.Events;

namespace Naming.AdvConfig.Tabs
{
    /// <summary>
    /// BaseInfoTab user control for basic configuration settings
    /// </summary>
    public partial class BaseInfoTab : UserControl
    {
        /// <summary>
        /// Gets the view model for this tab
        /// </summary>
        internal BaseInfoTabViewModel? ViewModel => DataContext as BaseInfoTabViewModel;

        public BaseInfoTab()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initialize the tab with an event hub and existing configuration
        /// </summary>
        /// <param name="eventHub">Event hub for inter-tab communication</param>
        public void Initialize(IConfigurationEventHub eventHub)
        {
            var viewModel = new BaseInfoTabViewModel(eventHub);
            DataContext = viewModel;
        }



        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }


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

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();
    }
}