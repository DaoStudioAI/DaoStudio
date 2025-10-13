using System;
using System.Collections.Generic;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Models
{

    public static class LlmToolParameterNames
    {
        public const string ShowConfigWin = "ShowConfigWin";
    }

    /// <summary>
    /// LLM tool configuration
    /// </summary>




    public class LlmTool
    {
        public long Id { get; set; }
        public string StaticId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ToolConfig { get; set; } = string.Empty;
        public int ToolType { get; set; } = 0; // 0=Normal
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public bool IsEnabled { get; set; } = true;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string DevMsg { get; set; } = string.Empty;
        
        public int State { get; set; } = 0; // 0=Stateless, 1=Stateful
        public byte[]? StateData { get; set; } = null;
        
        // New field
        public long AppId { get; set; } = 0;
    }
} 