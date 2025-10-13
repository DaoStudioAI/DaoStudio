using System;
using System.IO;
using System.Reflection;
using DaoStudio.Interfaces;
using Microsoft.Extensions.Logging;

namespace DaoStudio.Services
{
    /// <summary>
    /// Service for managing application paths including config folder and database paths.
    /// Centralizes the logic for determining the best location for application data storage.
    /// </summary>
    public class ApplicationPathsService : IApplicationPathsService
    {
        private readonly ILogger<ApplicationPathsService> _logger;
        private readonly string _configFolderPath;
        private readonly string _settingsDatabasePath;
        private readonly string _applicationDataPath;
        private readonly bool _isUsingExecutableFolder;

        public string ConfigFolderPath => _configFolderPath;
        public string SettingsDatabasePath => _settingsDatabasePath;
        public string ApplicationDataPath => _applicationDataPath;
        public bool IsUsingExecutableFolder => _isUsingExecutableFolder;

        public ApplicationPathsService(ILogger<ApplicationPathsService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Determine the best paths during construction
            var pathInfo = DeterminePaths();
            _configFolderPath = pathInfo.ConfigPath;
            _settingsDatabasePath = pathInfo.DatabasePath;
            _applicationDataPath = pathInfo.AppDataPath;
            _isUsingExecutableFolder = pathInfo.UseExeFolder;

            _logger.LogInformation("Application paths initialized. Using executable folder: {UseExeFolder}, Config path: {ConfigPath}, Database path: {DatabasePath}", 
                _isUsingExecutableFolder, _configFolderPath, _settingsDatabasePath);
        }

        private (string ConfigPath, string DatabasePath, string AppDataPath, bool UseExeFolder) DeterminePaths()
        {
            bool useExeFolder = false;
            bool needCleanup = false;
            string configPath = string.Empty;
            string databasePath = string.Empty;
            string appDataPath = string.Empty;

            try
            {
                // Try exe folder first, with Config subfolder
                var exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                configPath = Path.Combine(exePath ?? "", "Config");
                databasePath = Path.Combine(configPath, "settings.db");
                appDataPath = exePath ?? "";

                if (File.Exists(databasePath))
                {
                    // Test write access to existing file
                    try
                    {
                        using (var stream = File.OpenWrite(databasePath))
                        {
                            useExeFolder = true;
                        }
                        _logger.LogDebug("Found existing database at exe folder location with write access: {DatabasePath}", databasePath);
                    }
                    catch (Exception ex)
                    {
                        useExeFolder = false;
                        _logger.LogDebug(ex, "Cannot write to existing database at exe folder location: {DatabasePath}", databasePath);
                    }
                }
                else
                {
                    // Try to create the Config directory and test file
                    try
                    {
                        // Ensure Config directory exists
                        Directory.CreateDirectory(configPath);
                        
                        using (var testFile = File.Create(databasePath))
                        {
                            testFile.Close();
                        }
                        useExeFolder = true;
                        needCleanup = true;
                        _logger.LogDebug("Successfully created test database at exe folder location: {DatabasePath}", databasePath);
                    }
                    catch (Exception ex)
                    {
                        useExeFolder = false;
                        needCleanup = true;
                        _logger.LogDebug(ex, "Cannot create database at exe folder location: {DatabasePath}", databasePath);
                    }
                }

                // Clean up test file if we created it
                if (needCleanup && File.Exists(databasePath))
                {
                    try
                    {
                        File.Delete(databasePath);
                        _logger.LogDebug("Cleaned up test database file: {DatabasePath}", databasePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clean up test database file: {DatabasePath}", databasePath);
                    }
                }
            }
            catch (Exception ex)
            {
                useExeFolder = false;
                _logger.LogDebug(ex, "Error testing exe folder location");
            }

            if (!useExeFolder)
            {
                // Fall back to AppData if exe folder is not writable, also with Config subfolder
                var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                appDataPath = Path.Combine(appDataRoot, DaoStudio.Common.Constants.AppName);
                configPath = Path.Combine(appDataPath, "Config");

                // Ensure Config directory exists
                try
                {
                    Directory.CreateDirectory(configPath);
                    databasePath = Path.Combine(configPath, "settings.db");
                    _logger.LogDebug("Using AppData folder location: {ConfigPath}", configPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create AppData config directory: {ConfigPath}", configPath);
                    throw new InvalidOperationException($"Cannot create configuration directory at {configPath}", ex);
                }
            }

            return (configPath, databasePath, appDataPath, useExeFolder);
        }
    }
}