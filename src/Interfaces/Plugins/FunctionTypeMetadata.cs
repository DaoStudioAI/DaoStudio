using System;
using System.Collections.Generic;

namespace DaoStudio.Interfaces.Plugins
{
    public class FunctionTypeMetadata
    {
        /// <summary>
        /// Name of the parameter
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Description of the parameter
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// Parameter type
        /// </summary>
        public required Type ParameterType { get; set; }

        /// <summary>
        /// Whether the parameter is required
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Default value for the parameter
        /// </summary>
        public required object? DefaultValue { get; set; }

        /// <summary>
        /// For array types, this contains metadata about the array element type.
        /// This allows arrays to contain complex nested structures including objects and nested arrays.
        /// </summary>
        public FunctionTypeMetadata? ArrayElementMetadata { get; set; }

        /// <summary>
        /// For object types, this contains metadata for each property.
        /// Key is the property name, Value is the property metadata.
        /// This allows objects to have nested properties with full type information.
        /// </summary>
        public Dictionary<string, FunctionTypeMetadata>? ObjectProperties { get; set; }

        /// <summary>
        /// For enum types, this contains the allowed enum values as strings.
        /// </summary>
        public List<string>? EnumValues { get; set; }
    }
}