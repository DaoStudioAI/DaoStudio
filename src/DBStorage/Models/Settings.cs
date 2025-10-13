using System;
using System.Collections.Generic;

namespace DaoStudio.DBStorage.Models
{

    /// <summary>
    /// Application settings
    /// </summary>
    public class Settings
    {
        public string ApplicationName { get; set; } = DaoStudio.Common.Constants.AppName;
        public int Version { get; set; } = 1;
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public int Theme { get; set; } = 2; // 0=Light, 1=Dark, 2=System
    }
} 