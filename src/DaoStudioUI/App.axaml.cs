using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaoStudioUI.Resources;
using DaoStudioUI.ViewModels;
using DaoStudioUI.Views;
using DaoStudioUI.Extensions;
using DaoStudioUI.Services;
using DaoStudioUI.Utilities;
using Avalonia;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using DaoStudio.Common.Plugins;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using DryIoc;
using DaoStudio.Interfaces;

namespace DaoStudioUI
{
    public partial class App : Application
    {
        static public DaoStudio.DaoStudioService DaoStudioService;
        private static readonly DryIoc.Container Container = new();
        public static DryIoc.Container GetContainer() => Container;
        
        
        static ILoggerFactory loggerFactory;
        static App()
        {
            // Create a Serilog logger factory
            loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevelHelper.GetMicrosoftLogLevel());
            }).AddSerilog(Log.Logger);

            Container.RegisterInstance(Container);
            Container.RegisterInstance<ILoggerFactory>(loggerFactory);
            DaoStudio.DaoStudioService.RegisterServices(Container);
            // Initialize DaoStudio with the logger factory
            DaoStudioService = Container.Resolve<DaoStudio.DaoStudioService>();
            
            // Register UI services
            Container.RegisterUIServices();
        }

        Task IntialTask;
        public App()
        {
            
            IntialTask = Task.Run(async () => 
            {
                await DaoStudioService.InitializeAsync(Container);
            });
        }
        public override void Initialize()
        {
            try 
            {
                Log.Information("Initializing application");
                AvaloniaXamlLoader.Load(this);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during application initialization");
                throw;
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            try
            {
                // Initialize default language
                Log.Information("Setting default language");
                LocalizationManager.SetDefaultLanguage();
                
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    Log.Information("Configuring desktop application");
                    // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                    // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                    DisableAvaloniaDataAnnotationValidation();

                    // Load and apply theme from settings
                    LoadAndApplyThemeFromSettings();
                    
                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = Container.Resolve<MainWindowViewModel>(),
                    };
                    
                    // Resolve services via DI
                    var trayIconService = Container.Resolve<TrayIconService>();
                    var updateService = Container.Resolve<UpdateService>();
                    updateService.Initialize(desktop.MainWindow);
                    
                    // Start automatic update checking after a short delay and ensure it runs on the UI thread
                    Task.Run(async () =>
                    {
                        await Task.Delay(2000); // Wait 2 seconds after startup
                        // Ensure StartAutomaticUpdateCheck runs on the UI thread
                        _ = Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            updateService.StartAutomaticUpdateCheck();
                        });
                    });
                    
                    // Handle window closing to minimize to tray instead of closing
                    desktop.MainWindow.Closing += (sender, e) =>
                    {
                        if (sender is Window window)
                        {
                            e.Cancel = true; // Cancel the close operation
                            trayIconService?.HideToTray();
                            Log.Debug("Window minimized to tray instead of closing");
                        }
                    };
                    
                    // Handle application exit to flush logs and dispose services
                    desktop.Exit += (s, e) => 
                    {
                        Log.Information("Application shutting down");
                        trayIconService?.Dispose();
                        updateService?.Dispose();
                        Log.CloseAndFlush();
                    };
                    IntialTask.Wait();
                }

                base.OnFrameworkInitializationCompleted();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during framework initialization");
                throw;
            }
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            try
            {
                // Get an array of plugins to remove
                var dataValidationPluginsToRemove =
                    BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

                // remove each entry found
                foreach (var plugin in dataValidationPluginsToRemove)
                {
                    BindingPlugins.DataValidators.Remove(plugin);
                }
                
                Log.Debug("Avalonia data annotation validation disabled");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to disable Avalonia data validation");
            }
        }

        private void LoadAndApplyThemeFromSettings()
        {
            try
            {
                Log.Information("Loading application theme from settings");
                var settingsService = Container.Resolve<DaoStudio.Interfaces.ISettings>();

                // Convert the theme setting to Avalonia ThemeVariant
                var themeVariant = settingsService.Theme switch
                {
                    Theme.Light => Avalonia.Styling.ThemeVariant.Light,
                    Theme.Dark => Avalonia.Styling.ThemeVariant.Dark,
                    _ => Avalonia.Styling.ThemeVariant.Default // System theme
                };

                // Apply the theme
                RequestedThemeVariant = themeVariant;
                Log.Information($"Applied theme: {settingsService.Theme}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading theme from settings");
                // If there's an error, the default theme will be used
            }
        }
    }
}