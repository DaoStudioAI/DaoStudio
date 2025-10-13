using System;
using System.Collections.Generic;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Models
{
    /// <summary>
    /// Cached LLM model information
    /// </summary>
    public class CachedModel
    {
        public long Id { get; set; } 
        public long ApiProviderId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public int ProviderType { get; set; } = 0; // Unknown = 0
        public string Catalog { get; set; } = string.Empty;

        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }
}