using DaoStudio.Interfaces.Plugins;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace DaoStudio.Interfaces
{
    public static partial class IPluginExtensions
    {
        /// <summary>
        /// Checks if a type can be serialized to JSON
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>True if the type can be serialized to JSON, false otherwise</returns>
        private static bool IsJsonSerializable(Type type)
        {
            // Handle nullable types
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type) ?? type;
            }

            // Basic types that are JSON serializable
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || 
                type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) ||
                type == typeof(Guid) || type.IsEnum)
            {
                return true;
            }

            // Arrays and collections of serializable types
            if (type.IsArray)
            {
                return IsJsonSerializable(type.GetElementType()!);
            }

            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(List<>) || genericDef == typeof(IList<>) ||
                    genericDef == typeof(ICollection<>) || genericDef == typeof(IEnumerable<>) ||
                    genericDef == typeof(Dictionary<,>) || genericDef == typeof(IDictionary<,>))
                {
                    return type.GetGenericArguments().All(IsJsonSerializable);
                }
            }

            // Check if it's a simple class/struct with public properties (POCO)
            if (type.IsClass || (type.IsValueType && !type.IsPrimitive))
            {
                // Skip types with methods (excluding Object's methods and property getters/setters)
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object));
                
                if (methods.Any())
                {
                    return false; // Has methods, likely not a simple data object
                }

                return true; // Simple class/struct with only properties
            }

            return false;
        }

        /// <summary>
        /// Gets the actual return type, handling Task<T> by extracting T
        /// </summary>
        /// <param name="returnType">The method return type</param>
        /// <returns>The actual return type (T for Task<T>, or the original type)</returns>
        private static Type GetActualReturnType(Type returnType)
        {
            // Handle Task<T> - extract T
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                return returnType.GetGenericArguments()[0];
            }

            // Handle Task (non-generic) - return void equivalent
            if (returnType == typeof(Task))
            {
                return typeof(void);
            }

            return returnType;
        }

        /// <summary>
        /// Creates a list of FunctionWithDescription objects from the methods of the provided tool instance.
        /// </summary>
        /// <param name="tool">The tool instance containing methods to convert to functions</param>
        /// <param name="moduleName">The module name to associate with the functions (defaults to empty string)</param>
        /// <returns>A list of FunctionWithDescription objects</returns>
        public static List<FunctionWithDescription> CreateFunctionsFromToolMethods(object tool, string moduleName = "")
        {
            var functionlist = new List<FunctionWithDescription>();
            
            var methods = tool.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType != typeof(object) && !m.IsSpecialName);
                
            foreach (var method in methods)
            {
                var description = method.GetCustomAttribute<DescriptionAttribute>();
                if (description == null)
                {
                    continue; // Skip methods without a DescriptionAttribute
                }
                var displayName = method.GetCustomAttribute<DisplayNameAttribute>();
                var functionName = displayName?.DisplayName ?? method.Name;
                var functionDescription = description.Description ?? $"Function {functionName}";
                
                var parameters = method.GetParameters()
                    .Where(p => IsJsonSerializable(p.ParameterType)) // Only include JSON-serializable parameters
                    .Select(p => new FunctionTypeMetadata
                    {
                        Name = p.Name ?? "Param",
                        Description = p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? $"Parameter {p.Name}",
                        ParameterType = p.ParameterType,
                        IsRequired = !p.IsOptional,
                        DefaultValue = p.HasDefaultValue ? p.DefaultValue : null
                    })
                    .ToList();
                
                functionlist.Add(new FunctionWithDescription
                {
                    Function = Delegate.CreateDelegate(Expression.GetDelegateType(
                        (from parameter in method.GetParameters() select parameter.ParameterType)
                        .Concat(new[] { method.ReturnType })
                        .ToArray()), tool, method),
                    Description = new FunctionDescription
                    {
                        Name = functionName,
                        Description = functionDescription,
                        Parameters = parameters,
                        ReturnParameter = new FunctionTypeMetadata
                        {
                            Name = method.ReturnParameter?.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? "Return Value",
                            Description = method.ReturnParameter?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? $"Return value of {functionName}",
                            ParameterType = GetActualReturnType(method.ReturnType),
                            IsRequired = false,
                            DefaultValue = null
                        }
                    },
                });
            }
            
            return functionlist;
        }
    }
}
