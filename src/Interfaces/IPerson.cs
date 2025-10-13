using System;
using System.Collections.Generic;

namespace DaoStudio.Interfaces
{

    public enum PersonType
    {
        Normal = 0,
    }

    /// <summary>
    /// Represents a person/agent available in the system
    /// </summary>
    public interface IPerson
    {
        /// <summary>
        /// Gets or sets the unique identifier of the person
        /// </summary>
        long Id { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the person
        /// </summary>
        string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the description of the person
        /// </summary>
        string Description { get; set; }
        
        /// <summary>
        /// Gets or sets the image data of the person
        /// </summary>
        byte[]? Image { get; set; }
        
        /// <summary>
        /// Gets or sets whether the person is enabled
        /// </summary>
        bool IsEnabled { get; set; }
        
        /// <summary>
        /// Gets or sets the provider name for the person
        /// </summary>
        string ProviderName { get; set; }
        
        /// <summary>
        /// Gets or sets the model identifier
        /// </summary>
        string ModelId { get; set; }
        
        /// <summary>
        /// Gets or sets the presence penalty parameter
        /// </summary>
        double? PresencePenalty { get; set; }
        
        /// <summary>
        /// Gets or sets the frequency penalty parameter
        /// </summary>
        double? FrequencyPenalty { get; set; }
        
        /// <summary>
        /// Gets or sets the top-p sampling parameter
        /// </summary>
        double? TopP { get; set; }
        
        /// <summary>
        /// Gets or sets the top-k sampling parameter
        /// </summary>
        int? TopK { get; set; }
        
        /// <summary>
        /// Gets or sets the temperature parameter
        /// </summary>
        double? Temperature { get; set; }
        
        /// <summary>
        /// Gets or sets capability 1
        /// </summary>
        long? Capability1 { get; set; }
        
        /// <summary>
        /// Gets or sets capability 2
        /// </summary>
        long? Capability2 { get; set; }
        
        /// <summary>
        /// Gets or sets capability 3
        /// </summary>
        long? Capability3 { get; set; }
        
        /// <summary>
        /// Gets or sets the developer message for the person
        /// </summary>
        string? DeveloperMessage { get; set; }
        
        /// <summary>
        /// Gets or sets the tool names associated with the person
        /// </summary>
        string[] ToolNames { get; set; }
        
        /// <summary>
        /// Gets or sets the person parameters
        /// </summary>
        Dictionary<string, string> Parameters { get; set; }
        
        /// <summary>
        /// Gets or sets the last modified timestamp
        /// </summary>
        DateTime LastModified { get; set; }
        
        /// <summary>
        /// Gets or sets the creation timestamp
        /// </summary>
        DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Gets or sets the type of the person
        /// </summary>
        PersonType PersonType { get; set; }
        
        /// <summary>
        /// Gets or sets the app identifier associated with the person
        /// </summary>
        long AppId { get; set; }
    }
}
