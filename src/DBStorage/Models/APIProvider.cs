using System;
using System.Collections.Generic;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Models
{
    /// <summary>
    /// API provider configuration
    /// </summary>
    public class APIProvider
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ApiEndpoint { get; set; } = string.Empty;
        public string? ApiKey { get; set; } = null;
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public bool IsEnabled { get; set; } = true;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int ProviderType { get; set; } = 0; // Unknown = 0
        
        // New fields
        public int Timeout { get; set; } = 30000; // Default 30 seconds in milliseconds
        public int MaxConcurrency { get; set; } = 10; // Default max 10 concurrent requests
    }
}