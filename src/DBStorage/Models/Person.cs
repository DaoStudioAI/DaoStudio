using System;
using System.Collections.Generic;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Models
{
    public class Person
    {
        public long Id { get; set; } 
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public byte[]? Image { get; set; }
        public bool IsEnabled { get; set; } = true;

        public string ProviderName { get; set; } = string.Empty; 
        public string ModelId { get; set; } = string.Empty;
        public double? PresencePenalty { get; set; }
        public double? FrequencyPenalty { get; set; }
        public double? TopP { get; set; }
        public int? TopK { get; set; }
        public double? Temperature { get; set; }
        public long? Capability1 { get; set; }
        public long? Capability2 { get; set; }
        public long? Capability3 { get; set; }
        public string? DeveloperMessage { get; set; }
        public string[] ToolNames { get; set; } = Array.Empty<string>();
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // New fields
        public int PersonType { get; set; } = 0; // Normal = 0
        public long AppId { get; set; } = 0;
    }
}
