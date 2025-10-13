using System;
using System.Collections.Generic;

namespace DaoStudio.Interfaces
{

    /// <summary>
    /// Defines the state of a tool
    /// </summary>
    public enum ToolState
    {
        Stateless = 0,
        Stateful = 1
    }

    /// <summary>
    /// Defines whether a tool type
    /// </summary>
    public enum ToolType
    {
        Normal = 0,
    }

    /// <summary>
    /// Represents a tool available in the system
    /// </summary>
    public interface ITool
    {
        /// <summary>
        /// Gets or sets the unique identifier of the tool
        /// </summary>
        long Id { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the tool
        /// </summary>
        string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the description of the tool
        /// </summary>
        string Description { get; set; }
        
        /// <summary>
        /// Gets or sets the static identifier of the tool (plugin identifier)
        /// </summary>
        string StaticId { get; set; }
        
        /// <summary>
        /// Gets or sets whether the tool is enabled
        /// </summary>
        bool IsEnabled { get; set; }
        
        
        /// <summary>
        /// Gets or sets the tool configuration
        /// </summary>
        string ToolConfig { get; set; }
        
        /// <summary>
        /// Gets or sets the type of the tool
        /// </summary>
        ToolType ToolType { get; set; }
        
        /// <summary>
        /// Gets or sets the tool parameters
        /// </summary>
        Dictionary<string, string> Parameters { get; set; }
        
        /// <summary>
        /// Gets or sets the last modified timestamp
        /// </summary>
        DateTime LastModified { get; set; }
        
        /// <summary>
        /// Gets or sets the current state of the tool
        /// </summary>
        ToolState State { get; set; }
        
        /// <summary>
        /// Gets or sets the state data for stateful tools
        /// </summary>
        byte[]? StateData { get; set; }
        
        /// <summary>
        /// Gets or sets the developer message for the tool
        /// </summary>
        string DevMsg { get; set; }
        
        /// <summary>
        /// Gets or sets the creation timestamp
        /// </summary>
        DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Gets or sets the app identifier associated with the tool
        /// </summary>
        long AppId { get; set; }
    }
}
