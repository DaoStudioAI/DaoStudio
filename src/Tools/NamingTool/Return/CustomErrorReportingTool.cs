using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.Common.Plugins;

namespace NamingTool.Return
{
    /// <summary>
    /// Tool that enables child sessions to report errors back to the parent session
    /// using configurable parameters.
    /// </summary>
    public class CustomErrorReportingTool
    {
        private readonly TaskCompletionSource<ChildSessionResult> _errorCompletionSource;
        private readonly long _sessionId;
        private readonly List<CustomReturnParameter> _parameters;
        private readonly string _toolName;
        private readonly string _toolDescription;
        private int _typeErrorCount;
        private int _missingRequiredCount;

        public CustomErrorReportingTool(
            TaskCompletionSource<ChildSessionResult> errorCompletionSource,
            long sessionId,
            IEnumerable<CustomReturnParameter> parameters,
            string? toolName = null,
            string? toolDescription = null)
        {
            _errorCompletionSource = errorCompletionSource ?? throw new ArgumentNullException(nameof(errorCompletionSource));
            _sessionId = sessionId;
            _parameters = parameters?.ToList() ?? throw new ArgumentNullException(nameof(parameters));
            _toolName = toolName ?? "report_error";
            _toolDescription = toolDescription ?? "Report an error or issue encountered during task execution";

            var duplicateNames = _parameters
                .GroupBy(p => p.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateNames.Any())
            {
                throw new ArgumentException($"Duplicate parameter names found: {string.Join(", ", duplicateNames)}");
            }
        }

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public string ToolName => _toolName;

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public string ToolDescription => _toolDescription;

        /// <summary>
        /// Gets the configured parameters.
        /// </summary>
        public IReadOnlyList<CustomReturnParameter> Parameters => _parameters.AsReadOnly();

        /// <summary>
        /// Reports an error back to the parent session using the configured parameters.
        /// </summary>
        /// <param name="errorData">Dictionary containing error details.</param>
        /// <returns>Confirmation message for the calling assistant.</returns>
        [Description("Report an error or issue encountered during task execution")]
        [DisplayName("report_error")]
        public async Task<string> ReportError(Dictionary<string, object?> errorData)
        {
            errorData ??= new Dictionary<string, object?>();

            var (missingRequired, typeErrors) = ValidateParameters(errorData);
            var hasMissingRequired = missingRequired.Any();
            var hasTypeErrors = typeErrors.Any();

            if (hasMissingRequired || hasTypeErrors)
            {
                return HandleParameterValidationError(missingRequired, typeErrors, hasMissingRequired, hasTypeErrors);
            }

            var filteredData = new Dictionary<string, object?>();
            foreach (var parameter in _parameters)
            {
                if (errorData.TryGetValue(parameter.Name, out var value))
                {
                    filteredData[parameter.Name] = value;
                }
            }

            var errorMessage = filteredData.TryGetValue("error_message", out var messageValue)
                ? messageValue?.ToString() ?? ""
                : "";

            var errorResult = ChildSessionResult.CreateErrorReport(
                string.IsNullOrWhiteSpace(errorMessage) ? "An error was reported." : errorMessage);

            var wasSet = _errorCompletionSource.TrySetResult(errorResult);
            if (!wasSet)
            {
                throw new InvalidOperationException($"Failed to report error for session {_sessionId}. The result was already set.");
            }

            return $"Error reported to parent session. Session {_sessionId} will continue based on the configured behavior.";
        }

        private string HandleParameterValidationError(
            List<string> missingRequired,
            List<string> typeErrors,
            bool hasMissingRequired,
            bool hasTypeErrors)
        {
            var errorMessages = new List<string>();

            if (hasMissingRequired)
            {
                _missingRequiredCount++;
                errorMessages.Add($"Missing required parameters: {string.Join(", ", missingRequired)}");
            }

            if (hasTypeErrors)
            {
                _typeErrorCount++;
                errorMessages.Add($"Type validation errors: {string.Join("; ", typeErrors)}");
            }

            var errorMessage = string.Join(" AND ", errorMessages);
            const int maxFailures = 5;
            var exceededRetries = (hasMissingRequired && _missingRequiredCount >= maxFailures) ||
                                  (hasTypeErrors && _typeErrorCount >= maxFailures);

            if (exceededRetries)
            {
                var exception = new InvalidOperationException($"Validation failed after {maxFailures} attempts: {errorMessage}");
                _errorCompletionSource.TrySetException(exception);
                return $"Validation failed: {errorMessage}. Session {_sessionId} will now close due to repeated validation errors.";
            }

            return $"Validation failed: {errorMessage}.";
        }

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

                    if (expectedType.IsGenericType && expectedType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        expectedType = Nullable.GetUnderlyingType(expectedType)!;
                    }

                    if (!expectedType.IsAssignableFrom(actualType) && !CanConvert(actualType, expectedType))
                    {
                        typeErrors.Add($"Parameter '{param.Name}' expected type {expectedType.Name} but got {actualType.Name}");
                    }
                }
            }

            return (missingRequired, typeErrors);
        }

        private static bool CanConvert(Type from, Type to)
        {
            if (to == typeof(string))
                return true;

            if (from == typeof(string) && (to == typeof(int) || to == typeof(long) || to == typeof(double) ||
                to == typeof(float) || to == typeof(decimal) || to == typeof(bool) || to == typeof(DateTime)))
                return true;

            if (IsNumericType(from) && IsNumericType(to))
                return true;

            return false;
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
                   type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte) ||
                   type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        }
    }
}
