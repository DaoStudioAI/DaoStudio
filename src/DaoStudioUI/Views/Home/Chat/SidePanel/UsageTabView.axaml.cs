using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DaoStudioUI.ViewModels;

namespace DaoStudioUI.Views.Dialogs.Tabs
{
    public partial class UsageTabView : UserControl
    {
        public UsageTabView()
        {
            InitializeComponent();
            
            // Subscribe to the Unloaded event to dispose the DataContext
            Loaded += UsageTabView_Loaded;
            Unloaded += OnUnloaded;
        }

        private void UsageTabView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is UsageTabViewModel usageTabViewModel )
            {
                usageTabViewModel.OnLoad();
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is UsageTabViewModel usageTabViewModel)
            {
                usageTabViewModel.OnUnload();
            }

        }
    }
}
