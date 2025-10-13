using System;
using System.Collections.Generic;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Models
{

    public static class SessionPropertiesNames
    {
        public const string AdditionalCounts = "AdditionalCounts";
    }

    /// <summary>
    /// Session configuration
    /// </summary>
    public class Session
    {
        public long Id { get; set; } 
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public byte[]? Logo { get; set; } = null;
        public List<string> PersonNames { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public long? ParentSessId { get; set; } = null;
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        #region token infos
        public long TotalTokenCount { get; set; } = 0;
        public long OutputTokenCount { get; set; } = 0;
        public long InputTokenCount { get; set; } = 0;
        public long AdditionalCounts { get; set; } = 0;
        #endregion
        
        // New fields
        public int SessionType { get; set; } = 0; // Normal = 0
        public long AppId { get; set; } = 0;
        public long? PreviousId { get; set; } = null;
        public List<string> ToolNames { get; set; } = new List<string>();
    }
}