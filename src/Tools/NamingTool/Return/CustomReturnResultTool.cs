using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.Common.Plugins;

namespace NamingTool.Return
{
    /// <summary>
    /// Represents a parameter definition for the customizable return result tool
    /// </summary>
    public class CustomReturnParameter
    {
        /// <summary>
        /// The name of the parameter (must be unique)
        /// </summary>
        public required string Name { get; set; }
        
        /// <summary>
        /// The type of the parameter
        /// </summary>
        public required Type Type { get; set; }
        
        /// <summary>
        /// Description of what this parameter represents
        /// </summary>
        public required string Description { get; set; }
        
        /// <summary>
        /// Whether this parameter is required
        /// </summary>
        public bool IsRequired { get; set; } = true;
    }

    /// <summary>
    /// A customizable tool that allows a child session to return results to its parent session
    /// with user-defined parameters
    /// </summary>
    public class CustomReturnResultTool
    {
        private readonly TaskCompletionSource<ChildSessionResult> _completionSource;
        private readonly long _sessionId;
        private readonly List<CustomReturnParameter> _parameters;
        private readonly string _toolName;
        private readonly string _toolDescription;
        private int _typeErrorCount = 0;
        private int _missingRequiredCount = 0;
        
        /// <summary>
        /// Initializes a new instance of the CustomReturnResultTool class
        /// </summary>
        /// <param name="completionSource">The task completion source to signal when the result is set</param>
        /// <param name="sessionId">The ID of the child session</param>
        /// <param name="parameters">List of custom parameters for this tool</param>
        /// <param name="toolName">Name of the tool (optional, defaults to "set_custom_result")</param>
        /// <param name="toolDescription">Description of the tool (optional)</param>
        public CustomReturnResultTool(
            TaskCompletionSource<ChildSessionResult> completionSource, 
            long sessionId,
            IEnumerable<CustomReturnParameter> parameters,
            string? toolName = null,
            string? toolDescription = null)
        {
            _completionSource = completionSource ?? throw new ArgumentNullException(nameof(completionSource));
            _sessionId = sessionId;
            _parameters = parameters?.ToList() ?? throw new ArgumentNullException(nameof(parameters));
            _toolName = toolName ?? "set_custom_result";
            _toolDescription = toolDescription ?? "Report back with the custom result after completion";
            
            // Validate parameter names are unique
            var duplicateNames = _parameters.GroupBy(p => p.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            
            if (duplicateNames.Any())
            {
                throw new ArgumentException($"Duplicate parameter names found: {string.Join(", ", duplicateNames)}");
            }
        }
        
        /// <summary>
        /// Gets the tool name
        /// </summary>
        public string ToolName => _toolName;
        
        /// <summary>
        /// Gets the tool description
        /// </summary>
        public string ToolDescription => _toolDescription;
        
        /// <summary>
        /// Gets the list of parameters for this tool
        /// </summary>
        public IReadOnlyList<CustomReturnParameter> Parameters => _parameters.AsReadOnly();
        
        /// <summary>
        /// Sets the custom result to return to the parent session and completes the task
        /// This method accepts a dictionary with custom parameters as defined during tool creation
        /// </summary>
        /// <param name="resultData">Dictionary containing the custom result parameters</param>
        /// <returns>A confirmation message</returns>
        [Description("Report back with the result after completion")]
        [DisplayName("set_result")]
        public async Task<string> SetCustomResult(Dictionary<string, object?> resultData)
        {
            if (resultData == null)
            {
                resultData = new Dictionary<string, object?>();
            }
            
            // Setting custom result with the provided parameters
            
            // Validate parameters (required presence and type compatibility)
            var (missingRequired, typeErrors) = ValidateParameters(resultData);
            
            bool hasMissingRequired = missingRequired.Any();
            bool hasTypeErrors = typeErrors.Any();
            
            if (hasMissingRequired || hasTypeErrors)
            {
                return handleParameterValidationError(missingRequired, typeErrors, hasMissingRequired, hasTypeErrors);
            }
            
            // Create successful result with only the parameters defined in _parameters
            var filteredData = new Dictionary<string, object?>();
            foreach (var parameter in _parameters)
            {
                if (resultData.TryGetValue(parameter.Name, out var value))
                {
                    filteredData[parameter.Name] = value;
                }
            }
            
            var sessionResult = new ChildSessionResult
            {
                Success = true,
                ErrorMessage = null,
                Result = System.Text.Json.JsonSerializer.Serialize(filteredData, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                })
            };
            
            // Try to set the result on the completion source
            // This will only succeed once - subsequent calls will be ignored
            bool wasSet = _completionSource.TrySetResult(sessionResult);
            if (!wasSet)
            {
                throw new InvalidOperationException($"Failed to set custom result for session {_sessionId}. Result was already set.");
            }
            
            // Custom result successfully set
            return $"Custom result set and returned to parent session. Session {_sessionId} will now close.";
        }
        
        /// <summary>
        /// Handles parameter validation errors, including building error messages and managing retry attempts
        /// </summary>
        /// <param name="missingRequired">List of missing required parameter names</param>
        /// <param name="typeErrors">List of type validation error messages</param>
        /// <param name="hasMissingRequired">Whether there are any missing required parameters</param>
        /// <param name="hasTypeErrors">Whether there are any type validation errors</param>
        /// <returns>An error message string for the caller</returns>
        private string handleParameterValidationError(List<string> missingRequired, List<string> typeErrors, bool hasMissingRequired, bool hasTypeErrors)
        {
            // Build a comprehensive error message
            List<string> errorMessages = new List<string>();
            
            if (hasMissingRequired)
            {
                // Increment the missing required parameters counter
                _missingRequiredCount++;
                errorMessages.Add($"Missing required parameters: {string.Join(", ", missingRequired)}");
            }
            
            if (hasTypeErrors)
            {
                // Increment the validation failure counter
                _typeErrorCount++;
                errorMessages.Add($"Type validation errors: {string.Join("; ", typeErrors)}");
            }
            
            var errorMessage = string.Join(" AND ", errorMessages);
            int maxFailures = 5;
            bool exceededRetries = hasMissingRequired && _missingRequiredCount >= maxFailures || 
                                  hasTypeErrors && _typeErrorCount >= maxFailures;
            
            if (exceededRetries)
            {
                // After 5 failed validation attempts, set exception on the completion source
                var exception = new InvalidOperationException($"Validation failed after {maxFailures} attempts: {errorMessage}");
                // Attempt to transition the task to a faulted state only once; ignore if already completed
                _completionSource.TrySetException(exception);
                return $"Validation failed: {errorMessage}. Session {_sessionId} will now close due to exceeded retry attempts.";
            }
            
            // Do not complete the task yet, allow caller to retry
            return $"Validation failed: {errorMessage}.";
        }
        
        // Performs parameter validation and returns lists of missing required parameters and type errors
        private (List<string> MissingRequired, List<string> TypeErrors) ValidateParameters(Dictionary<string, object?> data)
        {
            var missingRequired = _parameters
                .Where(p => p.IsRequired && !data.ContainsKey(p.Name))
                .Select(p => p.Name)
                .ToList();

            var typeErrors = new List<string>();
            foreach (var param in _parameters)
            {
                if (data.TryGetValue(param.Name, out var value) && value != null)
                {
                    var expectedType = param.Type;
                    var actualType = value.GetType();

                    // Allow nullable types
                    if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        expectedType = Nullable.GetUnderlyingType(expectedType)!;
                    }

                    // Basic type compatibility check
                    if (!expectedType.IsAssignableFrom(actualType) && !CanConvert(actualType, expectedType))
                    {
                        typeErrors.Add($"Parameter '{param.Name}' expected type {expectedType.Name} but got {actualType.Name}");
                    }
                }
            }
            return (missingRequired, typeErrors);
        }

        /// <summary>
        /// Basic type conversion compatibility check
        /// </summary>
        private static bool CanConvert(Type from, Type to)
        {
            // Handle common conversions
            if (to == typeof(string))
                return true; // Most things can convert to string
                
            if (from == typeof(string) && (to == typeof(int) || to == typeof(long) || to == typeof(double) || 
                to == typeof(float) || to == typeof(decimal) || to == typeof(bool) || to == typeof(DateTime)))
                return true;
                
            if (IsNumericType(from) && IsNumericType(to))
                return true;
                
            return false;
        }
        
        /// <summary>
        /// Checks if a type is a numeric type
        /// </summary>
        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
                   type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte) ||
                   type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        }
    }
}
