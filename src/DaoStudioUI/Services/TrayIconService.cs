using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using DesktopUI.Resources;
using Serilog;

namespace DaoStudioUI.Services
{
    public class TrayIconService : IDisposable
    {
        private readonly TrayIcon? _trayIcon;
        private readonly IClassicDesktopStyleApplicationLifetime? _desktop;
        private bool _disposed = false;

        public TrayIconService()
        {
            try
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    _desktop = desktop;
                    _trayIcon = CreateTrayIcon();
                    Log.Information("Tray icon service initialized successfully");
                }
                else
                {
                    Log.Warning("Desktop application lifetime not available, tray icon will not be created");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize tray icon service");
            }
        }

        private TrayIcon CreateTrayIcon()
        {
            var trayIcon = new TrayIcon();

            // Set the tray icon using the application icon
            var assemblyName = typeof(TrayIconService).Assembly.GetName().Name;
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                throw new InvalidOperationException("Unable to determine assembly name for tray icon resources.");
            }

            var iconUri = new Uri($"avares://{assemblyName}/Assets/logo.ico");
            try
            {
                using var iconStream = AssetLoader.Open(iconUri);
                trayIcon.Icon = new WindowIcon(iconStream);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load tray icon from {IconUri}", iconUri);
            }
            
            // Set tooltip
            trayIcon.ToolTipText = Strings.MainWindow_Title;
            
            // Create context menu
            var contextMenu = new NativeMenu();
            
            // Open Main Window menu item
            var openMenuItem = new NativeMenuItem(Strings.TrayIcon_OpenMainWindow);
            openMenuItem.Click += OnOpenMainWindow;
            contextMenu.Add(openMenuItem);
            
            // Separator
            contextMenu.Add(new NativeMenuItemSeparator());
            
            // Exit menu item
            var exitMenuItem = new NativeMenuItem(Strings.TrayIcon_Exit);
            exitMenuItem.Click += OnExit;
            contextMenu.Add(exitMenuItem);
            
            trayIcon.Menu = contextMenu;
            
            // Handle tray icon click to show/hide main window
            trayIcon.Clicked += OnTrayIconClicked;
            
            return trayIcon;
        }

        private void OnTrayIconClicked(object? sender, EventArgs e)
        {
            try
            {
                ShowMainWindow();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling tray icon click");
            }
        }

        private void OnOpenMainWindow(object? sender, EventArgs e)
        {
            try
            {
                ShowMainWindow();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error opening main window from tray menu");
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            try
            {
                Log.Information("Exit requested from tray icon");
                _desktop?.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error shutting down application from tray menu");
            }
        }

        private void ShowMainWindow()
        {
            if (_desktop?.MainWindow != null)
            {
                var mainWindow = _desktop.MainWindow;
                
                // Show the window if it's hidden
                mainWindow.Show();
                
                // Bring to front and activate
                mainWindow.Activate();
                mainWindow.BringIntoView();
                
                // If minimized, restore it
                if (mainWindow.WindowState == WindowState.Minimized)
                {
                    mainWindow.WindowState = WindowState.Normal;
                }
                
                Log.Debug("Main window restored from tray");
            }
        }

        public void HideToTray()
        {
            try
            {
                if (_desktop?.MainWindow != null)
                {
                    _desktop.MainWindow.Hide();
                    Log.Debug("Main window hidden to tray");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error hiding window to tray");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _trayIcon?.Dispose();
                    Log.Debug("Tray icon service disposed");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error disposing tray icon service");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}
