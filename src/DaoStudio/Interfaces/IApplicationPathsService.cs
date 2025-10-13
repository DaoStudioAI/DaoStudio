using System;

namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Service interface for managing application paths including config folder and database paths
    /// </summary>
    public interface IApplicationPathsService
    {
        /// <summary>
        /// Gets the base config folder path where application configuration files are stored
        /// </summary>
        string ConfigFolderPath { get; }

        /// <summary>
        /// Gets the full path to the settings database file
        /// </summary>
        string SettingsDatabasePath { get; }

        /// <summary>
        /// Gets the root application data folder path (without Config subfolder)
        /// </summary>
        string ApplicationDataPath { get; }

        /// <summary>
        /// Indicates whether the application is using the executable folder for storage (true) 
        /// or the AppData folder (false)
        /// </summary>
        bool IsUsingExecutableFolder { get; }
    }
}