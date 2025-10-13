using System;

namespace DaoStudio.DBStorage.Models
{
    /// <summary>
    /// Application entity for storing application information
    /// </summary>
    public class Application
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BriefDescription { get; set; }
        public string? Description { get; set; }
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
