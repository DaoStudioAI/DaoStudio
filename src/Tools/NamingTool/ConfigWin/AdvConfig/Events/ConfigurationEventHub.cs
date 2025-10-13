using System;
using Serilog;

namespace Naming.AdvConfig.Events
{
    /// <summary>
    /// Central event hub implementation for communication between configuration tabs
    /// </summary>
    public class ConfigurationEventHub : IConfigurationEventHub
    {
        /// <summary>
        /// Event raised when configuration data changes in any tab
        /// </summary>
        public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

        /// <summary>
        /// Event raised when a tab requests validation
        /// </summary>
        public event EventHandler<ValidationRequestEventArgs>? ValidationRequested;

        /// <summary>
        /// Event raised when a tab requests configuration export
        /// </summary>
        public event EventHandler<ConfigurationExportEventArgs>? ExportRequested;

        /// <summary>
        /// Event raised when a tab requests configuration data from other tabs
        /// </summary>
        public event EventHandler<ConfigurationRequestEventArgs>? ConfigurationRequested;

        /// <summary>
        /// Raise a configuration changed event
        /// </summary>
        /// <param name="tabName">Name of the tab raising the event</param>
        /// <param name="configData">Configuration data that changed</param>
        /// <param name="changeType">Type of change</param>
        /// <param name="propertyPath">Optional property path for granular changes</param>
        public void RaiseConfigurationChanged(string tabName, object configData, ConfigurationChangeType changeType, string? propertyPath = null)
        {
            try
            {
                var args = new ConfigurationChangedEventArgs(tabName, configData, changeType, propertyPath);
                ConfigurationChanged?.Invoke(this, args);

                Log.Debug("Configuration changed in tab {TabName}: {ChangeType} at {PropertyPath}", 
                    tabName, changeType, propertyPath ?? "root");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error raising configuration changed event from tab {TabName}", tabName);
            }
        }

        /// <summary>
        /// Raise a validation request event
        /// </summary>
        /// <param name="tabName">Name of the tab requesting validation</param>
        /// <param name="configData">Configuration data to validate</param>
        /// <returns>Validation results</returns>
        public ValidationRequestEventArgs RaiseValidationRequest(string tabName, object configData)
        {
            var args = new ValidationRequestEventArgs(tabName, configData);
            
            try
            {
                ValidationRequested?.Invoke(this, args);
                
                Log.Debug("Validation requested from tab {TabName}, found {ErrorCount} errors", 
                    tabName, args.ValidationErrors.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during validation request from tab {TabName}", tabName);
                
                // Add a critical error to indicate validation system failure
                args.ValidationErrors.Add(new ValidationError
                {
                    PropertyPath = "system",
                    Message = $"Validation system error: {ex.Message}",
                    Severity = ValidationSeverity.Critical,
                    ErrorCode = "VALIDATION_SYSTEM_ERROR"
                });
            }

            return args;
        }

        /// <summary>
        /// Raise a configuration export request event
        /// </summary>
        /// <param name="tabName">Name of the tab requesting export</param>
        /// <param name="format">Export format</param>
        /// <returns>Export event args with results</returns>
        public ConfigurationExportEventArgs RaiseExportRequest(string tabName, ConfigurationExportFormat format)
        {
            var args = new ConfigurationExportEventArgs(tabName, format);
            
            try
            {
                ExportRequested?.Invoke(this, args);
                
                Log.Debug("Export requested from tab {TabName} in format {Format}, collected {DataCount} items", 
                    tabName, format, args.ExportData.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during export request from tab {TabName}", tabName);
            }

            return args;
        }

        /// <summary>
        /// Raise a configuration request event
        /// </summary>
        /// <param name="tabName">Name of the tab making the request</param>
        /// <param name="requestedType">Type of configuration data requested</param>
        /// <param name="filter">Optional filter criteria</param>
        /// <returns>Configuration request event args with response data</returns>
        public ConfigurationRequestEventArgs RaiseConfigurationRequest(string tabName, Type requestedType, string? filter = null)
        {
            var args = new ConfigurationRequestEventArgs(tabName, requestedType, filter);
            
            try
            {
                ConfigurationRequested?.Invoke(this, args);
                
                Log.Debug("Configuration request from tab {TabName} for type {RequestedType} with filter '{Filter}', response: {HasResponse}", 
                    tabName, requestedType.Name, filter ?? "none", args.ResponseData != null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during configuration request from tab {TabName}", tabName);
            }

            return args;
        }
    }
}