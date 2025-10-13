using DaoStudio.Interfaces.Plugins;
using Naming.Types;
using System;
using System.Collections.Generic;

namespace Naming
{
    /// <summary>
    /// Utility class for converting ParameterConfig objects to FunctionTypeMetadata with full cascade information
    /// </summary>
    internal static class ParameterConfigConverter
    {
        /// <summary>
        /// Converts a ParameterConfig to FunctionTypeMetadata with full cascade information for complex types
        /// </summary>
        public static FunctionTypeMetadata ConvertToMetadata(ParameterConfig paramConfig)
        {
            var metadata = new FunctionTypeMetadata
            {
                Name = paramConfig.Name,
                Description = paramConfig.Description,
                ParameterType = ConvertToType(paramConfig),
                IsRequired = paramConfig.IsRequired,
                DefaultValue = null
            };

            // Handle array types - recursively convert element configuration
            if (paramConfig.Type == ParameterType.Array && paramConfig.ArrayElementConfig != null)
            {
                metadata.ArrayElementMetadata = ConvertToMetadata(paramConfig.ArrayElementConfig);
            }

            // Handle object types - recursively convert all properties
            if (paramConfig.Type == ParameterType.Object && paramConfig.ObjectProperties != null)
            {
                metadata.ObjectProperties = new Dictionary<string, FunctionTypeMetadata>();
                foreach (var property in paramConfig.ObjectProperties)
                {
                    metadata.ObjectProperties[property.Name] = ConvertToMetadata(property);
                }
            }

            return metadata;
        }

        /// <summary>
        /// Converts a ParameterConfig to the appropriate System.Type, handling composite types
        /// </summary>
        private static Type ConvertToType(ParameterConfig paramConfig)
        {
            return paramConfig.Type switch
            {
                ParameterType.String => typeof(string),
                ParameterType.Number => typeof(double), // Use double as the most flexible numeric type for JSON
                ParameterType.Bool => typeof(bool),
                ParameterType.Object => ConvertObjectParameterToType(paramConfig), // Handle complex objects with full hierarchy
                ParameterType.Array => GetArrayType(paramConfig),
                _ => typeof(string) // Default fallback to string
            };
        }

        /// <summary>
        /// Converts an Object-type ParameterConfig to an ObjectTypeDescriptor that captures the full hierarchy
        /// </summary>
        private static Type ConvertObjectParameterToType(ParameterConfig paramConfig)
        {
            if (paramConfig.Type != ParameterType.Object)
                throw new ArgumentException("ParameterConfig must be of type Object", nameof(paramConfig));

            // If no ObjectProperties are defined, fallback to generic object
            if (paramConfig.ObjectProperties == null || paramConfig.ObjectProperties.Count == 0)
                return typeof(object);

            // Create an ObjectTypeDescriptor that captures the full hierarchy
            return ObjectTypeDescriptor.FromParameterConfig(paramConfig);
        }

        /// <summary>
        /// Determines the appropriate array type based on the element configuration
        /// </summary>
        private static Type GetArrayType(ParameterConfig paramConfig)
        {
            if (paramConfig.ArrayElementConfig == null)
            {
                // If no element config is specified, default to object array
                return typeof(object[]);
            }

            // Get the element type recursively
            var elementType = ConvertToType(paramConfig.ArrayElementConfig);
            
            // Create array type - handle ObjectTypeDescriptor specially
            if (elementType is ObjectTypeDescriptor descriptor)
            {
                return new ObjectTypeDescriptorArrayType(descriptor);
            }
            
            return elementType.MakeArrayType();
        }
    }
}
