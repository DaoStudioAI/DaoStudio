using System;
using System.Collections.Generic;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Models
{
    /// <summary>
    /// LLM prompt configuration
    /// </summary>
    public class LlmPrompt
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public bool IsEnabled { get; set; } = true;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
} 