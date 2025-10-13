using DaoStudioUI.Services;
using DaoStudioUI.ViewModels;
using DaoStudioUI.Views.Dialogs;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaoStudio;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DesktopUI.Resources;
using DryIoc;
using FluentAvalonia.UI.Controls;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DaoStudioUI.ViewModels
{
    public partial class PeopleViewModel : ObservableObject
    {
        // Collections
        [ObservableProperty]
        private ObservableCollection<PersonItem> _models = new();

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private ObservableCollection<Provider> _providers = new();

        private readonly IPeopleService _peopleService;
        private readonly ICachedModelService _cachedModelService;
        private readonly IApiProviderService _apiProviderService;

        public PeopleViewModel(IPeopleService peopleService, ICachedModelService cachedModelService, IApiProviderService apiProviderService)
        {
            _peopleService = peopleService;
            _cachedModelService = cachedModelService;
            _apiProviderService = apiProviderService;
            
            // Subscribe to provider changes
            _apiProviderService.ProviderChanged += OnProviderChanged;
            
            // Load data
            Task.Run(() => LoadDataAsync());
        }

        private void OnProviderChanged(object? sender, ProviderOperationEventArgs e)
        {
            // Refresh the providers list when a provider is changed
            Task.Run(() => LoadDataAsync());
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            if (IsLoading) return;
            
            IsLoading = true;
            
            try
            {
                // Load models
                var models = await _peopleService.GetAllPeopleAsync();


                _ = Task.Run(() => _cachedModelService.RefreshCachedModelsAsync());


                var providers = await _apiProviderService.GetAllApiProvidersAsync();

                Dispatcher.UIThread.Post(() => {
                    Providers.Clear();
                    foreach (var provider in providers)
                    {
                        Providers.Add(Provider.FromApiProvider(provider));
                    }


                    Models.Clear();
                    foreach (var model in models)
                    {
                        Models.Add(PersonItem.FromIPerson(model));
                    }                    
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                // Handle errors
                Log.Error(ex, "Error loading models");
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task EditModel(PersonItem model)
        {
            if (model == null) return;
            if (model.person == null) return;

            try
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;

                    var getEditModelViewModel = App.GetContainer().Resolve<Func<IPerson?, AddPersonViewModel>>();
                    var editModelViewModel = getEditModelViewModel(model.person);

                    // Create and configure the window directly
                    var editModelWindow = new AddModelView
                    {
                        DataContext = editModelViewModel,
                        Icon = mainWindow!.Icon,
                    };
                    
                    // Hook up save and cancel events
                    editModelViewModel.SaveRequested += async (s, e) =>
                    {
                        await LoadDataAsync();
                        editModelWindow.Close();
                    };
                    
                    editModelViewModel.CancelRequested += (s, e) =>
                    {
                        editModelWindow.Close();
                    };
                    
                    // Show the window as a modal dialog
                    await editModelWindow.ShowDialog(mainWindow);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error editing model");
            }
        }
        
        [RelayCommand]
        private async Task AddModel(Visual? anchor = null)
        {
            // Check if there are providers available
            if (Providers.Count == 0)
            {
                // Show message that providers must be added first
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                {
                    await DialogService.ShowErrorAsync(
                        Strings.Settings_Error,
                        Strings.People_ProvidersRequired,
                        desktopLifetime.MainWindow);
                }
                return;
            }
            
            try
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
                {
                    var mainWindow = desktopApp.MainWindow;
                    
                    // Create the view model for the window
                    var getaddModelViewModel = App.GetContainer().Resolve<Func<IPerson?, AddPersonViewModel>>();
                    var addPersonViewModel = getaddModelViewModel(null);

                    // Create and configure the window directly
                    var addModelWindow = new AddModelView
                    {
                        DataContext = addPersonViewModel,
                        Icon = mainWindow!.Icon
                    };

                    // Hook up save and cancel events
                    addPersonViewModel.SaveRequested += async (s, e) =>
                    {
                        await LoadDataAsync();
                        addModelWindow.Close();
                    };
                    
                    addPersonViewModel.CancelRequested += (s, e) =>
                    {
                        addModelWindow.Close();
                    };
                    
                    // Show the window as a modal dialog
                    await addModelWindow.ShowDialog(mainWindow);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding model");
            }
        }

        [RelayCommand]
        private async Task DeleteModel(PersonItem model)
        {
            if (model == null || IsLoading) return;

            try
            {
                // Show confirmation dialog
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;
                    
                    var dialog = new ContentDialog
                    {
                        Title = Strings.People_ConfirmDeletePerson,
                        Content = string.Format(System.Globalization.CultureInfo.CurrentCulture, 
                            Strings.People_ConfirmDeletePersonMessage, model.Name),
                        PrimaryButtonText = Strings.Settings_DeleteButton,
                        CloseButtonText = Strings.Common_Cancel,
                        DefaultButton = ContentDialogButton.Close
                    };

                    var result = await dialog.ShowAsync(mainWindow);
                    if (result == ContentDialogResult.Primary)
                    {
                        await DeleteModelAsync(model.Id);
                        await LoadDataAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting model");
            }
        }

        private async Task DeleteModelAsync(long id)
        {
            try
            {
                await _peopleService.DeletePersonAsync(id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting model");
            }
        }

        [RelayCommand]
        private void EditModelName(PersonItem model)
        {
            if (model == null) return;

            // Implementation can be added for inline editing
        }


    }
}
