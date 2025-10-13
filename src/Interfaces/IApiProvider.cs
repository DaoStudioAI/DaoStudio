using System;
using System.Collections.Generic;

namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Core API provider interface for UI layer
    /// </summary>
    public interface IApiProvider
    {
        /// <summary>
        /// Unique identifier for the API provider
        /// </summary>
        long Id { get; set; }

        /// <summary>
        /// Display name of the provider
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// API endpoint URL
        /// </summary>
        string ApiEndpoint { get; set; }

        /// <summary>
        /// API key for authentication (optional)
        /// </summary>
        string? ApiKey { get; set; }

        /// <summary>
        /// Additional provider-specific parameters
        /// </summary>
        Dictionary<string, string> Parameters { get; set; }

        /// <summary>
        /// Whether the provider is enabled
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Last modification timestamp
        /// </summary>
        DateTime LastModified { get;  }

        /// <summary>
        /// Creation timestamp
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// Type of the provider
        /// </summary>
        ProviderType ProviderType { get; set; }
    }
}