using DaoStudio.Interfaces;
using DaoStudio.DBStorage.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaoStudio
{
    /// <summary>
    /// Settings wrapper that extends DBStorage Settings and implements ISettings
    /// </summary>
    internal class Settings : DBStorage.Models.Settings, ISettings
    {
        private const string AutoResolveToolNameConflictsKey = "AutoResolveToolNameConflicts";
        private const string NavigationIndexKey = "NavigationIndex";
        private bool? _autoResolveToolNameConflictsCache;
        private int? _navigationIndexCache;
        
        private readonly ISettingsRepository _settingsRepository;
        private readonly ILogger<Settings> _logger;
        
        public Settings(ISettingsRepository settingsRepository, ILogger<Settings> logger)
        {
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Set default values
            ApplicationName = DaoStudio.Common.Constants.AppName;
            Version = 1;
            Properties = new Dictionary<string, string>();
            LastModified = DateTime.UtcNow;
            base.Theme = 2; // Default theme
        }

        // Explicit interface implementation for theme conversion
        Theme ISettings.Theme
        {
            get => (Theme)base.Theme;
            set => base.Theme = (int)value;
        }

        /// <summary>
        /// Auto-resolve tool function name conflicts by adding prefixes
        /// </summary>
        public bool AutoResolveToolNameConflicts
        {
            get
            {
                if (_autoResolveToolNameConflictsCache.HasValue)
                    return _autoResolveToolNameConflictsCache.Value;

                if (Properties.TryGetValue(AutoResolveToolNameConflictsKey, out var value))
                {
                    _autoResolveToolNameConflictsCache = bool.TryParse(value, out var result) ? result : true;
                }
                else
                {
                    _autoResolveToolNameConflictsCache = true; // Default value
                }
                return _autoResolveToolNameConflictsCache.Value;
            }
            set
            {
                Properties[AutoResolveToolNameConflictsKey] = value.ToString();
                _autoResolveToolNameConflictsCache = value; // Update cache
                
                // Save asynchronously in background (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SaveAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to auto-save settings after property change");
                    }
                });
            }
        }

        /// <summary>
        /// Selected navigation index for the main window
        /// </summary>
        public int NavigationIndex
        {
            get
            {
                if (_navigationIndexCache.HasValue)
                    return _navigationIndexCache.Value;

                if (Properties.TryGetValue(NavigationIndexKey, out var value))
                {
                    _navigationIndexCache = int.TryParse(value, out var result) ? result : 0;
                }
                else
                {
                    _navigationIndexCache = 0; // Default value
                }
                return _navigationIndexCache.Value;
            }
            set
            {
                Properties[NavigationIndexKey] = value.ToString();
                _navigationIndexCache = value; // Update cache
                
                // Save asynchronously in background (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SaveAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to auto-save settings after property change");
                    }
                });
            }
        }

        /// <summary>
        /// Creates a new DaoStudio.Models.Settings from a DBStorage.Models.Settings
        /// </summary>
        /// <param name="dbSettings">The DBStorage Settings to convert</param>
        /// <param name="settingsRepository">Settings repository for persistence</param>
        /// <param name="logger">Logger instance</param>
        /// <returns>A new DaoStudio.Models.Settings instance</returns>
        public static Settings FromDBSettings(DBStorage.Models.Settings dbSettings, ISettingsRepository settingsRepository, ILogger<Settings> logger)
        {
            if (dbSettings == null)
                throw new ArgumentNullException(nameof(dbSettings));
            if (settingsRepository == null)
                throw new ArgumentNullException(nameof(settingsRepository));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            var settings = new Settings(settingsRepository, logger)
            {
                ApplicationName = dbSettings.ApplicationName,
                Version = dbSettings.Version,
                Properties = new Dictionary<string, string>(dbSettings.Properties),
                LastModified = dbSettings.LastModified
            };

            // Set the Theme through the base class property
            settings.Theme = dbSettings.Theme;

            return settings;
        }

        /// <summary>
        /// Initialize settings by loading from database
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        public async Task InitializeAsync()
        {
            try
            {
                var dbSettings = await _settingsRepository.GetSettingsAsync(DaoStudio.Common.Constants.AppName);
                
                if (dbSettings != null)
                {
                    // Load existing settings
                    ApplicationName = dbSettings.ApplicationName;
                    Version = dbSettings.Version;
                    Properties = new Dictionary<string, string>(dbSettings.Properties);
                    LastModified = dbSettings.LastModified;
                    base.Theme = dbSettings.Theme;
                    
                    // Clear cache to force reload
                    _autoResolveToolNameConflictsCache = null;
                    _navigationIndexCache = null;
                }
                // If no settings exist, we keep the defaults set in constructor
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing settings");
                throw;
            }
        }

        /// <summary>
        /// Saves the current settings to storage
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> SaveAsync()
        {
            try
            {
                var dbSettings = ToDBSettings();
                dbSettings.LastModified = DateTime.UtcNow;
                LastModified = dbSettings.LastModified;
                
                await _settingsRepository.SaveSettingsAsync(dbSettings);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                return false;
            }
        }

        /// <summary>
        /// Converts to DBStorage.Models.Settings
        /// </summary>
        /// <returns>A new DBStorage.Models.Settings instance</returns>
        public DBStorage.Models.Settings ToDBSettings()
        {
            return new DBStorage.Models.Settings
            {
                ApplicationName = this.ApplicationName,
                Version = this.Version,
                Properties = new Dictionary<string, string>(this.Properties),
                LastModified = this.LastModified,
                Theme = this.Theme
            };
        }
    }
}