using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using DaoStudioUI.Resources;
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.Generic;
using Serilog;
using DaoStudio.Interfaces;
using DryIoc;
using Microsoft.Extensions.Logging;

namespace DaoStudioUI.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isPaneOpen = true;

        [ObservableProperty]
        private int _selectedNavIndex = 0;

        [ObservableProperty]
        private ObservableObject? _currentViewModel;

        private readonly ISettings _settingsService;
        private readonly ILogger<MainWindowViewModel> _logger;

        public MainWindowViewModel(ISettings settingsService, ILogger<MainWindowViewModel> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
            
            // Load saved navigation index if available
            Task.Run(LoadNavigationIndexAsync);
            
            // Initialize with default content
            //UpdateCurrentView();
        }

        private T ResolveViewModel<T>() where T : class
        {
            return App.GetContainer().Resolve<T>();
        }

        [RelayCommand]
        private async Task NavigationItemSelectedAsync(int index)
        {
            SelectedNavIndex = index;
            UpdateCurrentView();
            
            // Save the selected index whenever it changes
            await SaveNavigationIndexAsync();
        }

        [RelayCommand]
        private void TogglePane()
        {
            IsPaneOpen = !IsPaneOpen;
        }

        private void UpdateCurrentView()
        {
            CurrentViewModel = SelectedNavIndex switch
            {
                0 => ResolveViewModel<HomeViewModel>(), // Home page with our new ViewModel
                1 => null, // Documents page (placeholder)
                2 => ResolveViewModel<SettingsViewModel>(), // Settings page
                3 => null, // About page (placeholder)
                4 => ResolveViewModel<PeopleViewModel>(), // People page
                5 => ResolveViewModel<ToolsViewModel>(), // Tools page
                _ => null
            };
        }
        
        private async Task LoadNavigationIndexAsync()
        {
            try
            {
                var index = _settingsService.NavigationIndex;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    SelectedNavIndex = index;
                    UpdateCurrentView();
                });
            }
            catch (Exception ex)
            {
                // Log error but continue with default index
                _logger.LogError(ex, "Error loading navigation index");
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    SelectedNavIndex = 0;
                    UpdateCurrentView();
                });
            }
        }
        
        private async Task SaveNavigationIndexAsync()
        {
            try
            {
                _settingsService.NavigationIndex = SelectedNavIndex;
                await _settingsService.SaveAsync();
            }
            catch (Exception ex)
            {
                // Log error but continue
                Console.WriteLine($"Error saving navigation index: {ex.Message}");
            }
        }
    }
}
