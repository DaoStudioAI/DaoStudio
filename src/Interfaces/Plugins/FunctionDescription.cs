using System.Collections.Generic;
namespace DaoStudio.Interfaces.Plugins
{

    /// <summary>
    /// Metadata for describing a function
    /// </summary>
    public  class FunctionDescription
    {
        /// <summary>
        /// Name of the function
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Description of what the function does
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// Parameter descriptions for the function
        /// </summary>
        public IList<FunctionTypeMetadata> Parameters { get; set; } = new List<FunctionTypeMetadata>();

        /// <summary>
        /// Return parameter metadata for the function
        /// </summary>
        public FunctionTypeMetadata? ReturnParameter { get; set; }
        
        /// <summary>
        /// Whether strict mode is enabled for this function (additionalProperties: false)
        /// </summary>
        public bool StrictMode { get; set; } = false;
    }

}