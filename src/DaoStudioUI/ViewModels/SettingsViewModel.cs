using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Specialized;
using System.ComponentModel;
using Serilog;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Styling;
using DaoStudioUI.Resources;
using static DaoStudioUI.ViewModels.SettingsViewModel;
using DesktopUI.Resources;
using DaoStudio.Interfaces;
using DaoStudio;
using DaoStudioUI.Services;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace DaoStudioUI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {

        [ObservableProperty]
        private int _theme = 2; // 0=Light, 1=Dark, 2=System

        [ObservableProperty]
        private bool _autoResolveToolNameConflicts = true;

        // Collections
        [ObservableProperty]
        private ObservableCollection<Provider> _providers = new();


        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private bool _isLoadingModels = false;

        [ObservableProperty]
        private string _loadingModelsMessage = string.Empty;

        // Version-related properties
        [ObservableProperty]
        private string _currentVersion = string.Empty;

        [ObservableProperty]
        private bool _isCheckingForUpdates = false;

        // Enhanced version/appcast info
        [ObservableProperty]
        private string _appCastUrl = string.Empty;

        [ObservableProperty]
        private string _latestVersion = string.Empty;

        [ObservableProperty]
        private string _buildTime = string.Empty;

        [ObservableProperty]
        private string _latestPublished = string.Empty;

        [ObservableProperty]
        private Uri? _latestReleaseNotesUri;

        [ObservableProperty]
        private bool _hasReleaseNotes = false;

        // NBGV (Nerdbank.GitVersioning) properties
        [ObservableProperty]
        private string _informationalVersion = string.Empty;

        [ObservableProperty]
        private string _assemblyVersion = string.Empty;

        [ObservableProperty]
        private string _fileVersion = string.Empty;

        [ObservableProperty]
        private string _gitBranch = string.Empty;

        [ObservableProperty]
        private string _gitCommitIdShort = string.Empty;

        [ObservableProperty]
        private string _gitCommitDate = string.Empty;

        // System information (not from appcast)
        [ObservableProperty]
        private string _osDescription = string.Empty;

        [ObservableProperty]
        private string _osArchitecture = string.Empty;

        [ObservableProperty]
        private string _processArchitecture = string.Empty;

        [ObservableProperty]
        private string _frameworkDescription = string.Empty;

        [ObservableProperty]
        private string _runtimeVersion = string.Empty;

        [ObservableProperty]
        private string _avaloniaVersion = string.Empty;

        [ObservableProperty]
        private string _appDirectory = string.Empty;

        [ObservableProperty]
        private string _executablePath = string.Empty;

        [ObservableProperty]
        private string _buildConfiguration = string.Empty;

        private readonly ISettings _settingsService;
        private readonly IApiProviderService _apiProviderService;
        private readonly ICachedModelService _cachedModelService;
        private readonly UpdateService? _updateService;

        

        public SettingsViewModel(ISettings settingsService, IApiProviderService apiProviderService, ICachedModelService cachedModelService, UpdateService? updateService = null)
        {
            _settingsService = settingsService;
            _apiProviderService = apiProviderService;
            _cachedModelService = cachedModelService;
            _updateService = updateService;
            
            // Initialize current version
            InitializeVersion();
            // Load appcast details if possible
            Task.Run(() => LoadVersionDetailsAsync());
            // Load system information
            InitializeSystemInfo();
            
            // Subscribe to provider changes
            _apiProviderService.ProviderChanged += OnProviderChanged;
            
            // Load data
            Task.Run(() => LoadSettingsAsync());
        }

        private void InitializeSystemInfo()
        {
            try
            {
                OsDescription = RuntimeInformation.OSDescription;
                OsArchitecture = RuntimeInformation.OSArchitecture.ToString();
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString();
                FrameworkDescription = RuntimeInformation.FrameworkDescription;
                RuntimeVersion = Environment.Version.ToString();
                AvaloniaVersion = typeof(Application).Assembly.GetName().Version?.ToString() ?? string.Empty;
                AppDirectory = AppContext.BaseDirectory ?? string.Empty;
                try
                {
                    ExecutablePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                }
                catch { ExecutablePath = string.Empty; }

#if DEBUG
                BuildConfiguration = "Debug";
#else
                BuildConfiguration = "Release";
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize system information");
            }
        }

        private void InitializeVersion()
        {
            try
            {
                // Prefer Nerdbank.GitVersioning values when available
                try
                {
                    CurrentVersion = ThisAssembly.AssemblyInformationalVersion;
                    InformationalVersion = ThisAssembly.AssemblyInformationalVersion;
                    AssemblyVersion = ThisAssembly.AssemblyVersion;
                    FileVersion = ThisAssembly.AssemblyFileVersion;
                }
                catch
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var version = assembly.GetName().Version;
                    CurrentVersion = version?.ToString() ?? "1.0.0.0";
                    InformationalVersion = CurrentVersion;
                    AssemblyVersion = version?.ToString() ?? string.Empty;
                    FileVersion = AssemblyVersion;
                }

                // Git details (best-effort) via reflection to avoid compile errors when Git class isn't generated
                try
                {
                    var gitType = typeof(ThisAssembly).GetNestedType("Git", BindingFlags.Public | BindingFlags.NonPublic);
                    if (gitType != null)
                    {
                        string? GetGitConst(string name)
                        {
                            var f = gitType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                            if (f == null) return null;
                            // const fields can be retrieved via GetRawConstantValue
                            var raw = f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null);
                            return raw as string;
                        }

                        GitBranch = GetGitConst("Branch") ?? string.Empty;
                        GitCommitIdShort = GetGitConst("CommitIdShort") ?? string.Empty;
                        var cd = GetGitConst("CommitDate");
                        if (!string.IsNullOrWhiteSpace(cd))
                        {
                            if (DateTimeOffset.TryParse(cd, out var dto))
                                GitCommitDate = dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                            else
                                GitCommitDate = cd;
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get application version");
                CurrentVersion = "Unknown";
            }
        }

        private async Task LoadVersionDetailsAsync()
        {
            try
            {
                if (_updateService == null)
                    return;


                await Dispatcher.UIThread.InvokeAsync(async() =>
                {
                    var summary = await _updateService.GetAppcastSummaryAsync(CurrentVersion);
                    if (summary == null)
                        return;

                    AppCastUrl = summary.AppCastUrl ?? string.Empty;
                    LatestVersion = summary.LatestVersion ?? string.Empty;
                    BuildTime = FormatDate(summary.CurrentPublishedOn);
                    LatestPublished = FormatDate(summary.LatestPublishedOn);
                    LatestReleaseNotesUri = TryCreateUri(summary.LatestReleaseNotesLink);
                    HasReleaseNotes = LatestReleaseNotesUri != null;
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading appcast details");
            }
        }

        private static string FormatDate(DateTimeOffset? dto)
        {
            if (dto == null) return string.Empty;
            // Show local time with readable format
            var local = dto.Value.ToLocalTime();
            return local.ToString("yyyy-MM-dd HH:mm");
        }

        private static Uri? TryCreateUri(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try { return new Uri(url); } catch { return null; }
        }

        private void OnProviderChanged(object? sender, ProviderOperationEventArgs e)
        {
            // Refresh the providers list when a provider is changed
            Task.Run(() => LoadSettingsAsync());
        }

        [RelayCommand]
        private async Task LoadSettingsAsync()
        {
            
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // Update UI thread properties
                    Theme = (int)_settingsService.Theme;
                    AutoResolveToolNameConflicts = _settingsService.AutoResolveToolNameConflicts;
                });

                // Load providers
                var providers = await _apiProviderService.GetAllApiProvidersAsync();

                Dispatcher.UIThread.Post(() =>{

                    if (IsLoading) return;

                    IsLoading = true;

                    InitFillData(providers.Select(p => Provider.FromApiProvider(p)).ToList());
                    IsLoading = false;
                });
                
            }
            catch (Exception ex)
            {
                // Handle errors
                Log.Error(ex, "Error loading settings");
            }
            finally
            {
            }
        }

        private void InitFillData(List<Provider> providers)
        {
            Providers.Clear();

            foreach (var provider in providers)
            {
                Providers.Add(provider);
            }

        }

        private async Task SaveProvidersAsync(Provider lp)
        {
            if (IsLoading) return;
            try
            {
                
                if (lp.Id == 0)
                {
                    await _apiProviderService.CreateApiProviderAsync(lp.Name,lp.ProviderType,lp.ApiEndpoint,lp.ApiKey);
                }
                else
                {
                    if (lp.ApiProvider==null)
                    {
                        throw new Exception("Saving Provider before creating it.");
                    }
                    //sync Provider properties back to Provider.ApiProvider
                    lp.ApiProvider.Name= lp.Name;
                    lp.ApiProvider.ApiEndpoint = lp.ApiEndpoint;
                    lp.ApiProvider.ApiKey = lp.ApiKey;
                    lp.ApiProvider.Parameters = lp.Parameters;
                    lp.ApiProvider.IsEnabled = lp.IsEnabled;
                    lp.ApiProvider.ProviderType = lp.ProviderType;
                    lp.HasChanges = false;
                    // Update existing provider using ApiProviderService
                    await _apiProviderService.UpdateApiProviderAsync(lp.ApiProvider);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error saving provider");
            }
        }

        private async Task DeleteProvidersAsync(long id)
        {
            if (IsLoading) return;
            try
            {
                // Delete provider using ApiProviderService
                await _apiProviderService.DeleteApiProviderAsync(id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving provider");
            }
        }


        [RelayCommand]
        private void AddProvider(Visual? anchor = null)
        {
            var supportedProviders = _apiProviderService.GetSupportProviders();
            var menu = new MenuFlyout();
            
            foreach (var (name, template) in supportedProviders)
            {
                var item = new MenuItem { Header = name };
                item.Click += async (s, e) => await AddProviderFromTemplate(template);
                menu.Items.Add(item);
            }

            // Show the menu below the button
            if (menu.Items.Count > 0 && anchor is Control control)
            {
                menu.ShowAt(control);
            }
        }

        private async Task AddProviderFromTemplate(IApiProvider template)
        {
            // Create new provider based on template
            var provider = new Provider
            {
                Id = 0,
                Name = template.Name,
                ApiEndpoint = template.ApiEndpoint,
                ApiKey = template.ApiKey,
                Parameters = new Dictionary<string, string>(template.Parameters),
                ProviderType = template.ProviderType,
                IsEnabled = true,
                LastModified = DateTime.UtcNow,
                HasChanges = true
            };

            Providers.Add(provider);
            
            // Add an await operation to make this truly async
            await Task.CompletedTask;
        }

        [RelayCommand]
        private void DeleteProvider(Provider provider)
        {
            if (IsLoading) return;
            if (provider != null)
            {
                Task.Run(async () =>
                {
                    await DeleteProvidersAsync(provider.Id);
                    await LoadSettingsAsync();
                });
            }
        }

        [RelayCommand]
        private async Task DeleteProviderWithConfirmation(Provider provider)
        {
            if (IsLoading) return;
            if (provider == null) return;

            var dialog = new ContentDialog
            {
                Title = Strings.Settings_ConfirmDeleteProvider,
                Content = Strings.Settings_ConfirmDeleteProviderMessage,
                PrimaryButtonText = Strings.Settings_DeleteButton,
                CloseButtonText = Strings.Settings_CancelButton,
                DefaultButton = ContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await DeleteProvidersAsync(provider.Id);
                await LoadSettingsAsync();
            }
        }

        [RelayCommand]
        private void SaveProvider(Provider provider)
        {
            if (provider != null)
            {
                LoadingModelsMessage = Strings.Settings_LoadingPeople;
                
                Task.Run(async () => 
                {
                    try
                    {

                        // Try to load the model list first
                        try
                        {
                            await SaveProvidersAsync(provider);
                            await _cachedModelService.RefreshCachedModelsAsync();
                            
                            
                            
                            // Update provider status
                            Dispatcher.UIThread.Post(() => 
                            {
                                provider.HasChanges = false;
                                LoadingModelsMessage = string.Empty;
                            });
                        }
                        catch (Exception modelEx)
                        {
                            // Show detailed error if model list loading fails
                            Dispatcher.UIThread.Post(async () => 
                            {
                                LoadingModelsMessage = string.Empty;
                                
                                var dialog = new ContentDialog
                                {
                                    Title = Strings.Settings_ErrorLoadingPeople,
                                    Content = string.Format(Strings.Settings_FailedLoadPeople, modelEx.Message),
                                    CloseButtonText = Strings.Settings_CancelButton,
                                };
                                await dialog.ShowAsync();
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error saving provider");
                        Dispatcher.UIThread.Post(() => 
                        {
                            LoadingModelsMessage = string.Empty;
                            
                            // Show error dialog
                            Dispatcher.UIThread.Post(async () =>
                            {
                                await new ContentDialog
                                {
                                    Title = Strings.Settings_Error,
                                    Content = string.Format(Strings.Settings_ErrorOccurred, ex.Message),
                                    CloseButtonText = Strings.Common_OK
                                }.ShowAsync();
                            });
                        });
                    }
                });
            }
        }
        
        /// <summary>
        /// Saves models to the cached model repository for any provider type,
        /// with special handling for OpenRouter models which use Catalog/ModelName format
        /// </summary>
        /// <param name="provider">The provider associated with these models</param>
        /// <param name="models">List of model names to cache</param>
        private async Task SaveModelsToCacheAsync(Provider provider, List<string> models)
        {
            if (models == null)
                return;
                
            try
            {
                // Use CachedModelService to save models
                await _cachedModelService.SaveModelsToCacheAsync(provider.Id, provider.ProviderType, models);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error caching models for {0} provider", provider.Name);
            }
        }

        [RelayCommand]
        private async Task EditProviderName(Provider provider)
        {
            if (IsLoading) return;
            if (provider == null) return;

            var dialog = new ContentDialog
            {
                Title = Strings.Settings_EditProviderTitle,
                PrimaryButtonText = Strings.Common_Save,
                CloseButtonText = Strings.Common_Cancel,
                DefaultButton = ContentDialogButton.Primary
            };

            var textBox = new TextBox
            {
                Text = provider.Name,
                Watermark = Strings.Settings_ProviderNameWatermark,
                MinWidth = 300
            };

            dialog.Content = textBox;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                provider.Name = textBox.Text;
                provider.LastModified = DateTime.UtcNow;
                await SaveProvidersAsync(provider);
            }
        }

        partial void OnThemeChanged(int value)
        {
            // Update application theme immediately
            var app = Application.Current;
            if (app != null)
            {
                var theme = value switch
                {
                    0 => ThemeVariant.Light,
                    1 => ThemeVariant.Dark,
                    _ => ThemeVariant.Default
                };
                app.RequestedThemeVariant = theme;
            }

            // Save theme setting to storage
            Task.Run(async () =>
            {
                try
                {
                    // Update theme
                    _settingsService.Theme = (Theme)value;
                    await _settingsService.SaveAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error saving theme setting");
                }
            });
        }

        partial void OnAutoResolveToolNameConflictsChanged(bool value)
        {
            // Save setting to storage
            Task.Run(async () =>
            {
                try
                {
                    // Update setting
                    _settingsService.AutoResolveToolNameConflicts = value;
                    await _settingsService.SaveAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error saving AutoResolveToolNameConflicts setting");
                }
            });
        }

        [RelayCommand]
        private async Task CheckForUpdates()
        {
            if (_updateService == null)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    var dialog = new ContentDialog
                    {
                        Title = Strings.Settings_Error,
                        Content = "Update service is not available.",
                        CloseButtonText = Strings.Common_OK
                    };
                    await dialog.ShowAsync();
                });
                return;
            }

            try
            {
                IsCheckingForUpdates = true;
                Log.Information("User requested manual update check");

                    _updateService.CheckForUpdatesManually();

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during manual update check");
                
                Dispatcher.UIThread.Post(async () =>
                {
                    var dialog = new ContentDialog
                    {
                        Title = Strings.Settings_Error,
                        Content = $"Failed to check for updates: {ex.Message}",
                        CloseButtonText = Strings.Common_OK
                    };
                    await dialog.ShowAsync();
                });
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }

        [RelayCommand]
        private void OpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open URL: {0}", url);
            }
        }
    }
}