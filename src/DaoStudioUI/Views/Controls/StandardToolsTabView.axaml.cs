using DaoStudioUI.Models;
using DaoStudioUI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace DaoStudioUI.Views.Dialogs.Tabs
{
    public partial class StandardToolsTabView : UserControl
    {
        // Input tool collection dependency property
        public static readonly StyledProperty<IEnumerable> ToolItemsProperty = 
            AvaloniaProperty.Register<StandardToolsTabView, IEnumerable>(nameof(ToolItems));

        public IEnumerable ToolItems
        {
            get => GetValue(ToolItemsProperty);
            set => SetValue(ToolItemsProperty, value);
        }

        // Output selected tools collection dependency property
        public static readonly StyledProperty<ObservableCollection<object>> SelectedToolsProperty = 
            AvaloniaProperty.Register<StandardToolsTabView, ObservableCollection<object>>(nameof(SelectedTools));

        public ObservableCollection<object> SelectedTools
        {
            get => GetValue(SelectedToolsProperty);
            set => SetValue(SelectedToolsProperty, value);
        }

        // Backward compatibility: single selected tool property
        public static readonly StyledProperty<object> SelectedToolProperty = 
            AvaloniaProperty.Register<StandardToolsTabView, object>(nameof(SelectedTool));

        public object SelectedTool
        {
            get => GetValue(SelectedToolProperty);
            set => SetValue(SelectedToolProperty, value);
        }

        // Commands for Select All and Clear All
        public ICommand SelectAllToolsCommand { get; }
        public ICommand ClearAllToolsCommand { get; }

        public StandardToolsTabView()
        {
            SelectAllToolsCommand = new RelayCommand(SelectAllTools);
            ClearAllToolsCommand = new RelayCommand(ClearAllTools);
            
            // Initialize SelectedTools collection
            SelectedTools = new ObservableCollection<object>();
            
            InitializeComponent();
            
            // Listen for property changes
            this.PropertyChanged += OnPropertyChanged;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ToolItemsProperty)
            {
                UpdateSelectedToolsFromToolItems();
            }
        }

        private void SelectAllTools()
        {
            if (ToolItems == null) return;            foreach (var tool in ToolItems)
            {
                if (tool is ToolItem toolItem)
                {
                    toolItem.IsSelected = true;
                }
            }
            UpdateSelectedToolsFromToolItems();
        }

        private void ClearAllTools()
        {
            if (ToolItems == null) return;            foreach (var tool in ToolItems)
            {
                if (tool is ToolItem toolItem)
                {
                    toolItem.IsSelected = false;
                }
            }
            UpdateSelectedToolsFromToolItems();
        }        private void UpdateSelectedToolsFromToolItems()
        {
            if (ToolItems == null) return;

            SelectedTools.Clear();
            
            foreach (var tool in ToolItems)
            {
                if (tool is ToolItem toolItem && toolItem.IsSelected)
                {
                    SelectedTools.Add(tool);
                }
            }

            // Update SelectedTool for backward compatibility (last selected tool)
            SelectedTool = SelectedTools.LastOrDefault()!;
        }

        // Event handler for tool item selection
        private void OnToolItemPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is ToolItem tool)
            {
                // Toggle the tool selection
                tool.IsSelected = !tool.IsSelected;
                
                // Update collections
                UpdateSelectedToolsFromToolItems();
                
                // Backward compatibility: update SelectedTool if from ToolsPanelViewModel
                if (DataContext is ToolsPanelViewModel viewModel)
                {
                    viewModel.SelectedTool = tool;
                }
            }
        }
    }
}
