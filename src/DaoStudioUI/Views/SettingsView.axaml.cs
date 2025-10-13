using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using DaoStudioUI.ViewModels;
using System;

namespace DaoStudioUI.Views
{
    public partial class SettingsView : UserControl
    {

        public SettingsView()
        {
            InitializeComponent();
            
        }


        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ToggleSwitch_OnChecked(object? sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch && DataContext is SettingsViewModel viewModel)
            {
                if (toggleSwitch.DataContext is Provider providerData && providerData.IsEnabled != true)
                {
                    viewModel.SaveProviderCommand.Execute(toggleSwitch.DataContext);
                }
            }
        }

        private void ToggleSwitch_OnUnchecked(object? sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch && DataContext is SettingsViewModel viewModel)
            {
                if (toggleSwitch.DataContext is Provider providerData && providerData.IsEnabled != false)
                {
                    viewModel.SaveProviderCommand.Execute(toggleSwitch.DataContext);
                }
            }
        }
    }
} 