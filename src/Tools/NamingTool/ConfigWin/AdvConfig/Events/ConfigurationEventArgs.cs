using System;
using System.Collections.Generic;

namespace Naming.AdvConfig.Events
{
    /// <summary>
    /// Base class for configuration-related event arguments
    /// </summary>
    public abstract class ConfigurationEventArgs : EventArgs
    {
        /// <summary>
        /// Name of the tab that raised the event
        /// </summary>
        public string TabName { get; }

        /// <summary>
        /// Timestamp when the event was raised
        /// </summary>
        public DateTime Timestamp { get; }

        protected ConfigurationEventArgs(string tabName)
        {
            TabName = tabName ?? throw new ArgumentNullException(nameof(tabName));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for configuration change events
    /// </summary>
    public class ConfigurationChangedEventArgs : ConfigurationEventArgs
    {
        /// <summary>
        /// The configuration data that changed
        /// </summary>
        public object ConfigData { get; }

        /// <summary>
        /// Type of configuration change
        /// </summary>
        public ConfigurationChangeType ChangeType { get; }

        /// <summary>
        /// Optional property path for granular changes
        /// </summary>
        public string? PropertyPath { get; }

        public ConfigurationChangedEventArgs(string tabName, object configData, ConfigurationChangeType changeType, string? propertyPath = null)
            : base(tabName)
        {
            ConfigData = configData ?? throw new ArgumentNullException(nameof(configData));
            ChangeType = changeType;
            PropertyPath = propertyPath;
        }
    }

    /// <summary>
    /// Event arguments for validation request events
    /// </summary>
    public class ValidationRequestEventArgs : ConfigurationEventArgs
    {
        /// <summary>
        /// Configuration data to validate
        /// </summary>
        public object ConfigData { get; }

        /// <summary>
        /// Validation results (populated by event handlers)
        /// </summary>
        public List<ValidationError> ValidationErrors { get; set; } = new List<ValidationError>();

        public ValidationRequestEventArgs(string tabName, object configData)
            : base(tabName)
        {
            ConfigData = configData ?? throw new ArgumentNullException(nameof(configData));
        }
    }

    /// <summary>
    /// Event arguments for configuration export events
    /// </summary>
    public class ConfigurationExportEventArgs : ConfigurationEventArgs
    {
        /// <summary>
        /// Export format requested
        /// </summary>
        public ConfigurationExportFormat Format { get; }

        /// <summary>
        /// Export data (populated by event handlers)
        /// </summary>
        public Dictionary<string, object> ExportData { get; set; } = new Dictionary<string, object>();

        public ConfigurationExportEventArgs(string tabName, ConfigurationExportFormat format)
            : base(tabName)
        {
            Format = format;
        }
    }

    /// <summary>
    /// Event arguments for requesting configuration data from other tabs
    /// </summary>
    public class ConfigurationRequestEventArgs : ConfigurationEventArgs
    {
        /// <summary>
        /// Type of configuration data requested
        /// </summary>
        public Type RequestedType { get; }

        /// <summary>
        /// Optional filter criteria
        /// </summary>
        public string? Filter { get; }

        /// <summary>
        /// Response data (populated by event handlers)
        /// </summary>
        public object? ResponseData { get; set; }

        public ConfigurationRequestEventArgs(string tabName, Type requestedType, string? filter = null)
            : base(tabName)
        {
            RequestedType = requestedType ?? throw new ArgumentNullException(nameof(requestedType));
            Filter = filter;
        }
    }

    /// <summary>
    /// Types of configuration changes
    /// </summary>
    public enum ConfigurationChangeType
    {
        /// <summary>
        /// Property value changed
        /// </summary>
        PropertyChanged,

        /// <summary>
        /// Item added to collection
        /// </summary>
        ItemAdded,

        /// <summary>
        /// Item removed from collection
        /// </summary>
        ItemRemoved,

        /// <summary>
        /// Collection cleared
        /// </summary>
        CollectionCleared,

        /// <summary>
        /// Complex object replaced
        /// </summary>
        ObjectReplaced,

        /// <summary>
        /// Configuration reset to defaults
        /// </summary>
        Reset
    }

    /// <summary>
    /// Configuration export formats
    /// </summary>
    public enum ConfigurationExportFormat
    {
        /// <summary>
        /// JSON format
        /// </summary>
        Json,

        /// <summary>
        /// MessagePack format
        /// </summary>
        MessagePack,

        /// <summary>
        /// Template format
        /// </summary>
        Template
    }

    /// <summary>
    /// Represents a validation error
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// Property path where the error occurred
        /// </summary>
        public string PropertyPath { get; set; } = string.Empty;

        /// <summary>
        /// Error message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Severity level
        /// </summary>
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

        /// <summary>
        /// Error code for programmatic handling
        /// </summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Validation error severity levels
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// Informational message
        /// </summary>
        Info,

        /// <summary>
        /// Warning message
        /// </summary>
        Warning,

        /// <summary>
        /// Error message
        /// </summary>
        Error,

        /// <summary>
        /// Critical error
        /// </summary>
        Critical
    }
}