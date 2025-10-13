using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using DaoStudioUI.Models;
using DaoStudioUI.ViewModels;
using Avalonia.Controls.Primitives;
using DaoStudioUI.Views.Dialogs.Tabs;
using System;

namespace DaoStudioUI.Views
{
    public partial class ToolsPanelControl : UserControl
    {
        private TabStrip? _headerTabStrip;
        private ContentControl? _contentFrame;
        private StandardToolsTabView? _standardToolsView;
        private UsageTabView? _usageTabView;
        
        public ToolsPanelControl()
        {
            InitializeComponent();
            DataContextChanged += ToolsPanelControl_DataContextChanged;

        }
          private void ToolsPanelControl_DataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is ToolsPanelViewModel viewModel)
            {
                // Update UsageTabView DataContext
                if (_usageTabView != null)
                {
                    _usageTabView.DataContext = viewModel.UsageTabViewModel;
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            _headerTabStrip = this.FindControl<TabStrip>("headerTabStrip");
            _contentFrame = this.FindControl<ContentControl>("contentFrame");
            
            // Create the tab views
            _standardToolsView = new StandardToolsTabView();
            _standardToolsView.Bind(StandardToolsTabView.ToolItemsProperty, new Avalonia.Data.Binding("AvailableTools"));
            _usageTabView = new UsageTabView();
            
            // Set initial content
            if (_contentFrame != null)
            {
                _contentFrame.Content = _standardToolsView;
            }
            
            if (_headerTabStrip != null)
            {
                _headerTabStrip.SelectionChanged += HeaderTabStrip_SelectionChanged;
            }
        }
        
        private void HeaderTabStrip_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_headerTabStrip != null && _contentFrame != null)
            {
                switch (_headerTabStrip.SelectedIndex)
                {
                    case 0:
                        _contentFrame.Content = _standardToolsView;
                        break;
                    case 1:
                        _contentFrame.Content = _usageTabView;
                        break;
                }
            }
        }


    }
}