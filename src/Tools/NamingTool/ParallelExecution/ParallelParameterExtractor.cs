using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaoStudio.Common.Plugins;
using Serilog;

namespace Naming.ParallelExecution
{
    /// <summary>
    /// Extracts parallel execution sources based on configuration
    /// </summary>
    public static class ParallelParameterExtractor
    {
        /// <summary>
        /// Well-known parameter names that should be excluded from parameter-based parallel execution
        /// These represent session infrastructure rather than user data
        /// </summary>
        private static readonly HashSet<string> DefaultExcludedParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DaoStudio.Common.Plugins.Constants.DasSession,
            "hostSession",
            "session",
            "parentSession",
            "cancellationToken"
        };

        /// <summary>
        /// Extracts parallel execution sources based on configuration
        /// </summary>
        /// <param name="requestData">The original request data dictionary</param>
        /// <param name="config">The parallel execution configuration</param>
        /// <returns>List of objects for parallel execution</returns>
        /// <exception cref="ArgumentException">Thrown when configuration is invalid</exception>
        public static List<(string, object?)> ExtractParallelSources(
            Dictionary<string, object?> requestData,
            ParallelExecutionConfig config)
        {

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            Log.Debug("Extracting parallel sources for execution type: {ExecutionType}", config.ExecutionType); 

            return config.ExecutionType switch
            {
                ParallelExecutionType.None => new List<(string, object?)>(),
                ParallelExecutionType.ParameterBased => ExtractPocoParameters(requestData, config.ExcludedParameters),
                ParallelExecutionType.ListBased => ExtractListElements(requestData, config.ListParameterName),
                ParallelExecutionType.ExternalList =>
                    (config.ExternalList == null || config.ExternalList.Count == 0)
                        ? throw new ArgumentException("ExternalStringList must not be null or empty")
                        : config.ExternalList.Select(x => ("ExternalList", (object?)x)).ToList(),
                _ => throw new ArgumentException($"Unsupported execution type: {config.ExecutionType}")
            };
        }

        /// <summary>
        /// Extracts POCO parameters from requestData, excluding non-POCO objects like DasSession
        /// </summary>
        /// <param name="requestData">The original request data</param>
        /// <param name="userExcludedParams">User-specified parameters to exclude</param>
        /// <returns>List of parameter dictionaries, one for each POCO parameter</returns>
        private static List<(string, object?)> ExtractPocoParameters(
            Dictionary<string, object?> requestData,
            List<string> userExcludedParams)
        {
            var allExcluded = new HashSet<string>(DefaultExcludedParameters, StringComparer.OrdinalIgnoreCase);
            if (userExcludedParams != null)
            {
                foreach (var param in userExcludedParams)
                {
                    allExcluded.Add(param);
                }
            }

            var parallelSources = new List<(string, object?)>();

            foreach (var kvp in requestData)
            {
                // Skip excluded parameters
                if (allExcluded.Contains(kvp.Key))
                {
                    Log.Debug("Skipping excluded parameter: {ParameterName}", kvp.Key);
                    continue;
                }

                // Include null values as valid sources so that templates like {{ _Parameter.Value }}
                // can still be rendered (as empty) and sessions are created for all parameters,
                // matching the expected behavior in edge-case tests.
                if (kvp.Value == null)
                {
                    parallelSources.Add((kvp.Key, null));
                    Log.Debug("Added parameter-based source with null value: {ParameterName}", kvp.Key);
                    continue;
                }

                // Skip non-POCO types (interfaces, complex framework types, etc.)
                if (!IsPocoType(kvp.Value))
                {
                    Log.Debug("Skipping non-POCO parameter: {ParameterName} (Type: {Type})",
                        kvp.Key, kvp.Value.GetType().Name);
                    continue;
                }

                parallelSources.Add((kvp.Key, kvp.Value));

                Log.Debug("Added parameter-based source: {ParameterName} = {Value}",
                    kvp.Key, kvp.Value?.ToString() ?? "null");
            }

            if (parallelSources.Count == 0)
            {
                Log.Warning("No valid POCO parameters found for parallel execution");
            }

            Log.Information("Extracted {Count} parameter-based parallel sources", parallelSources.Count);
            return parallelSources;
        }

        /// <summary>
        /// Extracts elements from a list parameter for parallel execution
        /// </summary>
        /// <param name="requestData">The original request data</param>
        /// <param name="listParameterName">The name of the parameter containing the list</param>
        /// <returns>List of parameter dictionaries, one for each list element</returns>
        private static List<(string, object?)> ExtractListElements(
            Dictionary<string, object?> requestData,
            string? listParameterName)
        {
            if (string.IsNullOrWhiteSpace(listParameterName))
            {
                throw new ArgumentException("ListParameterName must be specified for ListBased execution", nameof(listParameterName));
            }

            if (!requestData.TryGetValue(listParameterName, out var listValue) || listValue == null)
            {
                throw new ArgumentException($"Parameter '{listParameterName}' not found or is null", nameof(listParameterName));
            }

            // Strings are technically IEnumerable<char>, but for our purposes they are not valid list sources
            if (listValue is string)
            {
                throw new ArgumentException($"Parameter '{listParameterName}' is not enumerable", nameof(listParameterName));
            }

            // Accept any IEnumerable (e.g., arrays, List<T>, collections of value types),
            // then normalize to List<object?> via Cast<object?> for uniform processing.
            if (listValue is not IEnumerable enumerable)
            {
                throw new ArgumentException($"Parameter '{listParameterName}' is not enumerable", nameof(listParameterName));
            }

            // Convert to list to evaluate the enumerable only once and to be able to check for emptiness safely.
            var elements = enumerable.Cast<object?>().ToList();
            if (elements.Count == 0)
            {
                // Important: Explicitly notify caller that the provided list is empty so that higher-level logic can surface
                // a meaningful error message to the user/tests.
                throw new ArgumentException("List must not be null or empty", nameof(listParameterName));
            }

            // Use the actual list parameter name so templates can access the correct source name via {{ _Parameter.Name }}
            return elements.Select(x => (listParameterName, x)).ToList();
        }


        /// <summary>
        /// Determines if a type is a POCO (Plain Old CLR Object) suitable for parallel execution
        /// </summary>
        /// <param name="obj">The object to check</param>
        /// <returns>True if the object is a POCO type</returns>
        private static bool IsPocoType(object obj)
        {
            if (obj == null)
                return false;

            var type = obj.GetType();

            // Primitive types and strings are POCO
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Guid))
                return true;

            // Nullable types are POCO if their underlying type is POCO
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = Nullable.GetUnderlyingType(type);
                return underlyingType != null && (underlyingType.IsPrimitive || underlyingType == typeof(decimal) ||
                    underlyingType == typeof(DateTime) || underlyingType == typeof(Guid));
            }

            // Exclude interfaces (like IHostSession, IHost, etc.)
            if (type.IsInterface)
                return false;

            // Treat collection types (e.g., List<>, Dictionary<,>) as valid POCO containers **before** we apply the
            // namespace-based exclusion. This ensures common CLR collection types located in System.* namespaces are not
            // filtered out, otherwise objects like Dictionary<string, object?> would never be processed and no parallel
            // sources would be extracted.
            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                // For now, consider collections as POCO – we could add stricter validation later if needed.
                return true;
            }

            // Exclude types from system or framework namespaces that are unlikely to represent user-data POCOs.
            // (The collection check above already early-returns for the common generic collections under System.*)
            var namespaceName = type.Namespace ?? string.Empty;
            if (namespaceName == "System" ||
                namespaceName.StartsWith("System.") ||
                namespaceName == "Microsoft" ||
                namespaceName.StartsWith("Microsoft.") ||
                namespaceName.StartsWith("DaoStudio.") ||
                namespaceName.StartsWith("Serilog."))
                return false;

            // Exclude delegates and events
            if (typeof(Delegate).IsAssignableFrom(type))
                return false;

            // Simple classes with public properties are likely POCO
            // This is a heuristic - we could make this more sophisticated if needed
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            // If it has public properties or fields, consider it POCO
            return properties.Length > 0 || fields.Length > 0;
        }
    }
}