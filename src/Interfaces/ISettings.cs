using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Theme options for the application
    /// </summary>
    public enum Theme
    {
        Light,
        Dark,
        System
    }

    /// <summary>
    /// Core settings interface for UI layer
    /// </summary>
    public interface ISettings
    {
        /// <summary>
        /// Application name
        /// </summary>
        string ApplicationName { get; set; }

        /// <summary>
        /// Settings version
        /// </summary>
        int Version { get; set; }

        /// <summary>
        /// Key-value properties collection
        /// </summary>
        Dictionary<string, string> Properties { get; set; }

        /// <summary>
        /// Last modification timestamp
        /// </summary>
        DateTime LastModified { get; set; }

        /// <summary>
        /// Application theme
        /// </summary>
        Theme Theme { get; set; }

        /// <summary>
        /// Auto-resolve tool function name conflicts by adding prefixes
        /// </summary>
        bool AutoResolveToolNameConflicts { get; set; }

        /// <summary>
        /// Selected navigation index for the main window
        /// </summary>
        int NavigationIndex { get; set; }

        /// <summary>
        /// Saves the current settings to storage
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> SaveAsync();
    }
}