using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using DaoStudioUI.ViewModels;
using Avalonia;
using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Input;

namespace DaoStudioUI.Views
{
    public partial class MainWindow : Window
    {
        // Raised when the TransitioningContentControl finishes a transition
        public event EventHandler? ContentTransitionCompleted;

        private bool _suppressNavSelectionChanged;
        private MainWindowViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            // Subscribe to transition completed when the control is available
            //if (TransitionHost != null)
            //{
            //    TransitionHost.TransitionCompleted += TransitionHost_TransitionCompleted;
            //}
            if (NavView != null)
            {
                NavView.SelectionChanged += NavigationView_SelectionChanged;
            }

            // Ensure we unsubscribe when the window is closed
            //this.Closed += MainWindow_Closed;

            // Track DataContext changes to sync NavigationView selection with VM
            this.DataContextChanged += MainWindow_DataContextChanged;
            // Initialize selection if DataContext is already set
            HookViewModel(DataContext as MainWindowViewModel);
            UpdateNavViewSelectionFromViewModel();

#if DEBUG
            this.AttachDevTools();
#endif
        }

        //comment out for now, may be useful later
        private void TransitionHost_TransitionCompleted(object? sender, Avalonia.Controls.TransitionCompletedEventArgs e)
        {
            // After the visual transition finishes, ensure the NavigationView selection
            // matches the currently displayed view (CurrentViewModel).
            // If they don't match, fix by updating the ViewModel's SelectedNavIndex
            // and syncing the NavigationView selection.
            if (DataContext is MainWindowViewModel vm)
            {
                // Determine expected nav index from the current view model type
                int expectedIndex = vm.CurrentViewModel switch
                {
                    HomeViewModel => 0,
                    null => -1,
                    SettingsViewModel => 2,
                    PeopleViewModel => 4,
                    ToolsViewModel => 5,
                    _ => -1
                };

                // Get current selected index from NavView (if any)
                int currentNavIndex = -1;
                if (NavView?.SelectedItem is NavigationViewItem nvi && nvi.Tag is object tag)
                {
                    if (!int.TryParse(tag.ToString(), out currentNavIndex))
                        currentNavIndex = -1;
                }

                // If types map to a known index and there's a mismatch, fix it
                if (expectedIndex >= 0 && expectedIndex != currentNavIndex)
                {
                    // Update VM which will drive the Content as well
                    vm.SelectedNavIndex = currentNavIndex;
                    // Schedule UI-sync from the VM on the UI thread.
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            // Sync the NavigationView selection from ViewModel
                            UpdateNavViewSelectionFromViewModel();
                        });
                    });
                }
            }

            ContentTransitionCompleted?.Invoke(this, EventArgs.Empty);
        }

        //private void MainWindow_Closed(object? sender, EventArgs e)
        //{
        //    if (TransitionHost != null)
        //    {
        //        TransitionHost.TransitionCompleted -= TransitionHost_TransitionCompleted;
        //    }
        //}

        private void NavigationView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
        {
            if (_suppressNavSelectionChanged)
                return;

            if (e.SelectedItem is NavigationViewItem item && 
                DataContext is MainWindowViewModel viewModel &&
                item.Tag is object tag)
            {
                if (int.TryParse(tag.ToString(), out int index))
                {
                    viewModel.NavigationItemSelectedCommand.Execute(index);
                }
            }
        }

        private void MainWindow_DataContextChanged(object? sender, EventArgs e)
        {
            HookViewModel(DataContext as MainWindowViewModel);
            UpdateNavViewSelectionFromViewModel();
        }

        private void HookViewModel(MainWindowViewModel? vm)
        {
            if (_viewModel == vm)
                return;

            if (_viewModel is INotifyPropertyChanged oldInpc)
            {
                oldInpc.PropertyChanged -= ViewModelOnPropertyChanged;
            }

            _viewModel = vm;
            if (_viewModel is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += ViewModelOnPropertyChanged;
            }
        }

        private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedNavIndex))
            {
                UpdateNavViewSelectionFromViewModel();
            }
        }

        private void UpdateNavViewSelectionFromViewModel()
        {
            if (_viewModel == null)
                return;

            if (NavView == null)
                return;

            var targetIndex = _viewModel.SelectedNavIndex;
            var targetItem = FindNavItemByTag(NavView, targetIndex);

            _suppressNavSelectionChanged = true;
            try
            {
                NavView.SelectedItem = targetItem;
            }
            finally
            {
                _suppressNavSelectionChanged = false;
            }
        }

        private static NavigationViewItem? FindNavItemByTag(NavigationView nav, int index)
        {
            string tagStr = index.ToString();

            // Search in MenuItems
            foreach (var obj in nav.MenuItems)
            {
                if (obj is NavigationViewItem nvi && (nvi.Tag?.ToString() == tagStr))
                    return nvi;
            }

            // Search in FooterMenuItems
            foreach (var obj in nav.FooterMenuItems)
            {
                if (obj is NavigationViewItem nvi && (nvi.Tag?.ToString() == tagStr))
                    return nvi;
            }

            // Fallback: Home (tag 0) if present
            foreach (var obj in nav.MenuItems)
            {
                if (obj is NavigationViewItem nvi && (nvi.Tag?.ToString() == "0"))
                    return nvi;
            }

            return null;
        }

        private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Only handle left mouse button for dragging
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }
    }
}