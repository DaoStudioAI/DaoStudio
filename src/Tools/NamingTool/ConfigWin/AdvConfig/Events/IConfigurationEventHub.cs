using System;

namespace Naming.AdvConfig.Events
{
    /// <summary>
    /// Central event hub for communication between configuration tabs
    /// </summary>
    public interface IConfigurationEventHub
    {
        /// <summary>
        /// Event raised when configuration data changes in any tab
        /// </summary>
        event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        /// <summary>
        /// Event raised when a tab requests validation
        /// </summary>
        event EventHandler<ValidationRequestEventArgs> ValidationRequested;

        /// <summary>
        /// Event raised when a tab requests configuration export
        /// </summary>
        event EventHandler<ConfigurationExportEventArgs> ExportRequested;

        /// <summary>
        /// Event raised when a tab requests configuration data from other tabs
        /// </summary>
        event EventHandler<ConfigurationRequestEventArgs> ConfigurationRequested;

        /// <summary>
        /// Raise a configuration changed event
        /// </summary>
        /// <param name="tabName">Name of the tab raising the event</param>
        /// <param name="configData">Configuration data that changed</param>
        /// <param name="changeType">Type of change</param>
        /// <param name="propertyPath">Optional property path for granular changes</param>
        void RaiseConfigurationChanged(string tabName, object configData, ConfigurationChangeType changeType, string? propertyPath = null);

        /// <summary>
        /// Raise a validation request event
        /// </summary>
        /// <param name="tabName">Name of the tab requesting validation</param>
        /// <param name="configData">Configuration data to validate</param>
        /// <returns>Validation results</returns>
        ValidationRequestEventArgs RaiseValidationRequest(string tabName, object configData);

        /// <summary>
        /// Raise a configuration export request event
        /// </summary>
        /// <param name="tabName">Name of the tab requesting export</param>
        /// <param name="format">Export format</param>
        /// <returns>Export event args with results</returns>
        ConfigurationExportEventArgs RaiseExportRequest(string tabName, ConfigurationExportFormat format);

        /// <summary>
        /// Raise a configuration request event
        /// </summary>
        /// <param name="tabName">Name of the tab making the request</param>
        /// <param name="requestedType">Type of configuration data requested</param>
        /// <param name="filter">Optional filter criteria</param>
        /// <returns>Configuration request event args with response data</returns>
        ConfigurationRequestEventArgs RaiseConfigurationRequest(string tabName, Type requestedType, string? filter = null);
    }
}