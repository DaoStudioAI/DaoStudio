using System;
namespace DaoStudio.Interfaces.Plugins
{
    /// <summary>
    /// Represents a function with its description metadata
    /// </summary>
    public class FunctionWithDescription
    {
        /// <summary>
        /// The delegate representing the function to be added
        /// </summary>
        public required Delegate Function { get; set; }

        /// <summary>
        /// Description metadata for the function
        /// </summary>
        public required FunctionDescription Description { get; set; }

        /// <summary>
        /// The module name for this function. For semantic kernel plugins, this is the name of the module that contains the function.
        /// </summary>
        [Obsolete("This property may be removed in a future version.")]
        public string ModuleName { get; set; } = string.Empty;
    }
}