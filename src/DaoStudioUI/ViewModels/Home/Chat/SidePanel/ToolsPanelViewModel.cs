using DaoStudioUI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Collections.ObjectModel;

namespace DaoStudioUI.ViewModels
{
    public partial class ToolsPanelViewModel : ObservableObject
    {
        private readonly ChatViewModel _chatViewModel;        [ObservableProperty]
        private ObservableCollection<ToolItem> _availableTools = new();

        [ObservableProperty]
        private ToolItem? _selectedTool;

        [ObservableProperty]
        private UsageTabViewModel _usageTabViewModel;

        // Constructor that takes a ChatViewModel to delegate operations to
        public ToolsPanelViewModel(ChatViewModel chatViewModel)
        {
            _chatViewModel = chatViewModel;
            _usageTabViewModel = new UsageTabViewModel(chatViewModel);
        }


        // Command to toggle the panel visibility
        [RelayCommand]
        private void TogglePane()
        {
            if (_chatViewModel != null)
            {
                _chatViewModel.TogglePaneCommand.Execute(null);
            }
        }

        // Command to select all tools
        [RelayCommand]
        private void SelectAllTools()
        {
            if (AvailableTools == null)
                return;

            foreach (var tool in AvailableTools)
            {
                tool.IsSelected = true;
            }

            Log.Information("Selected all tools");
        }

        // Command to clear all tool selections
        [RelayCommand]
        private void ClearAllTools()
        {
            if (AvailableTools == null)
                return;

            foreach (var tool in AvailableTools)
            {
                tool.IsSelected = false;
            }

            Log.Information("Cleared all tool selections");
        }


    }
}