using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using DaoStudioUI.ViewModels;
using DaoStudioUI.Models;
using System;

namespace DaoStudioUI.Views
{
    public partial class ToolsView : UserControl
    {
        public ToolsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }        private void ToggleSwitch_OnChecked(object? sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch && DataContext is ToolsViewModel viewModel)
            {
                if (toggleSwitch.DataContext is ToolItem toolData && toolData.IsEnabled != true)
                {
                    viewModel.SaveToolCommand.Execute(toggleSwitch.DataContext);
                }
            }
        }

        private void ToggleSwitch_OnUnchecked(object? sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch && DataContext is ToolsViewModel viewModel)
            {
                if (toggleSwitch.DataContext is ToolItem toolData && toolData.IsEnabled != false)
                {
                    viewModel.SaveToolCommand.Execute(toggleSwitch.DataContext);
                }
            }
        }
    }
} 