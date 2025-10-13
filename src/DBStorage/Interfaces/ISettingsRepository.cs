using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Interfaces
{
    /// <summary>
    /// Interface for settings repository operations
    /// </summary>
    public interface ISettingsRepository
    {
        /// <summary>
        /// Get settings by application name
        /// </summary>
        /// <param name="applicationName">The name of the application</param>
        /// <returns>Settings object or null if not found</returns>
        Task<Settings?> GetSettingsAsync(string applicationName);

        /// <summary>
        /// Save settings for an application
        /// </summary>
        /// <param name="settings">The settings to save</param>
        /// <returns>True if successful</returns>
        Task<bool> SaveSettingsAsync(Settings settings);

        /// <summary>
        /// Delete settings for an application
        /// </summary>
        /// <param name="applicationName">The name of the application</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteSettingsAsync(string applicationName);

        /// <summary>
        /// Get all application settings
        /// </summary>
        /// <returns>List of all settings</returns>
        Task<IEnumerable<Settings>> GetAllSettingsAsync();
    }
} 