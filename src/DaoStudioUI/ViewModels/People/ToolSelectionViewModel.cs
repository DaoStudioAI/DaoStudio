using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;

namespace DaoStudioUI.ViewModels
{
    public partial class ToolSelectionViewModel : ObservableObject
    {
        // Event to notify when tools are selected
        public event EventHandler<List<string>>? ToolsSelected;
        public event EventHandler? CancelRequested;

        [ObservableProperty]
        private ObservableCollection<ToolItem> _tools = new();

        public ToolSelectionViewModel(List<ITool> availableTools, List<string> selectedToolNames)
        {
            // Convert tools to ToolItems and mark as selected if they're in the selectedToolNames list
            foreach (var tool in availableTools)
            {
                Tools.Add(new ToolItem
                {
                    Tool = tool,
                    IsSelected = selectedToolNames.Contains(tool.Name)
                });
            }
        }

        [RelayCommand]
        private void Save()
        {
            // Get the selected tool names
            var selectedTools = Tools.Where(t => t.IsSelected).Select(t => t.Tool.Name).ToList();
            
            // Trigger the ToolsSelected event
            ToolsSelected?.Invoke(this, selectedTools);
        }

        [RelayCommand]
        private void Cancel()
        {
            // Trigger the CancelRequested event
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        // Inner class for tool selection
        public partial class ToolItem : ObservableObject
        {
            [ObservableProperty]
            private ITool _tool = null!;

            [ObservableProperty]
            private bool _isSelected = false;
        }
    }
}