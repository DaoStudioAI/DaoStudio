using DaoStudio.Common.Plugins;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NamingTool.Properties;
using Naming.Extensions;
using Naming.ParallelExecution;
using Scriban;
using System.Text.RegularExpressions;
using DaoStudio.Interfaces.Plugins;
using System.Text;

namespace Naming
{
    /// <summary>
    /// Handler class for individual Naming sessions.
    /// 
    /// The Naming function has been rewritten to use dynamic parameters from configuration:
    /// - Function name, description, and parameters are now configurable via NamingConfig
    /// - Parameters are passed as Dictionary&lt;string, object?&gt; instead of fixed method parameters
    /// - Validates parameters against configuration similar to CustomReturnResultTool.SetCustomResult
    /// - Falls back to default parameters if no configuration is provided
    /// </summary>
    internal class NamingHandler
    {
        private readonly IHost _host;
        private readonly NamingConfig _config;
        private readonly IHostSession? _hostSession;

        public NamingHandler(IHost host, NamingConfig config, IHostSession? contextSession)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _hostSession = contextSession ?? throw new ArgumentNullException(nameof(contextSession));
        }

        /// <summary>
        /// Ask for help from another AI assistant to complete a subtask
        /// This method accepts a dictionary with custom parameters as defined in configuration
        /// </summary>
        /// <param name="requestData">Dictionary containing the request parameters</param>
        /// <returns>Information about the started help session</returns>
        [Description("Arbitrarily redefining a concept and acting on the new definition")]
        [DisplayName("Naming")]
        public async Task<string> Naming(Dictionary<string, object?> requestData)
        {
            requestData ??= new Dictionary<string, object?>();

            // Determine context session (parent) if provided via requestData
            var contextSession = GetParameterValue<IHostSession>(requestData, DaoStudio.Common.Plugins.Constants.DasSession) ?? _hostSession;

            // Validate configuration and recursion level
            var validationResult = await ValidateConfigurationAsync(contextSession);
            if (!validationResult.IsValid)
            {
                return validationResult.ErrorMessage;
            }

            // Validate input parameters
            var parameterValidationResult = ValidateInputParameters(requestData);
            if (!parameterValidationResult.IsValid)
            {
                return parameterValidationResult.ErrorMessage;
            }

            // Select appropriate assistant using unified method
            IHostPerson selectedPersonObj = await GetExecutivePersonAsync(requestData);

            var selectedPerson = selectedPersonObj.Name;

            try
            {

                // Check if parallel execution is enabled
                if (_config.ParallelConfig != null && _config.ParallelConfig.ExecutionType != ParallelExecutionType.None)
                {
                    return await ExecuteParallelNamingAsync(requestData, selectedPerson, contextSession);
                }

                // Execute the single naming session (current behavior)
                return await ExecuteNamingSessionAsync(requestData, selectedPerson, contextSession);
            }
            catch (InvalidOperationException)
            {
                // Configuration validation failures (e.g., empty UrgingMessage, template errors) should propagate to the caller so that tests can assert on them.
                throw;
            }
            catch (Exception ex)
            {
                // For other runtime errors unrelated to configuration, keep the previous behaviour and return the error message so that the LLM can surface it naturally.
                return ex.Message;
            }
        }

        /// <summary>
        /// Validates the configuration and recursion level
        /// </summary>
        /// <returns>Validation result</returns>
        private async Task<ValidationResult> ValidateConfigurationAsync(IHostSession? contextSession)
        {
            // Validate configuration values first
            if (_config.MaxRecursionLevel < 0)
            {
                // A negative recursion level is an invalid configuration. Throw so that callers can handle it explicitly.
                throw new InvalidOperationException(Resources.Error_InvalidRecursionLevel);
            }

            var errorReportingValidation = ValidateErrorReportingConfiguration();
            if (!errorReportingValidation.IsValid)
            {
                return errorReportingValidation;
            }

            if (contextSession == null)
            {
                return ValidationResult.Valid();
            }

            // Check recursion level only after confirming the configuration is valid.
            var currentLevel = await NamingLevelCalculator.CalculateCurrentLevelAsync(contextSession, _host);
            if (currentLevel >= _config.MaxRecursionLevel)
            {
                return ValidationResult.Invalid(string.Format(Resources.ErrorMaxRecursionLevelReached, _config.MaxRecursionLevel, currentLevel));
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Validates the input parameters against the configuration
        /// </summary>
        /// <param name="requestData">The request data to validate</param>
        /// <returns>Validation result</returns>
        private ValidationResult ValidateInputParameters(Dictionary<string, object?> requestData)
        {
            var functionParameters = _config.InputParameters?.Count > 0 
                ? _config.InputParameters 
                : new List<ParameterConfig>();

            var missingRequired = functionParameters
                .Where(p => p.IsRequired && !requestData.ContainsKey(p.Name))
                .ToList();

            if (missingRequired.Any())
            {
                var missingDetails = missingRequired
                    .Select(p => string.IsNullOrWhiteSpace(p.Description) 
                        ? $"'{p.Name}'" 
                        : $"'{p.Name}' ({p.Description})")
                    .ToList();
                
                return ValidationResult.Invalid(string.Format(Resources.Error_MissingRequiredParameters, string.Join(", ", missingDetails)));
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Validates the error reporting configuration when enabled.
        /// </summary>
        private ValidationResult ValidateErrorReportingConfiguration()
        {
            var errorConfig = _config.ErrorReportingConfig;
            if (errorConfig == null)
            {
                return ValidationResult.Valid();
            }

            if (string.IsNullOrWhiteSpace(_config.ErrorReportingToolName))
            {
                return ValidationResult.Invalid(Resources.ErrorReporting_Validation_ToolNameRequired);
            }

            if (string.Equals(_config.ErrorReportingToolName, _config.ReturnToolName, StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Invalid(Resources.ErrorReporting_Validation_ToolNameConflict);
            }

            var parameters = errorConfig.Parameters ?? new List<ParameterConfig>();

            if (parameters.Any(p => string.IsNullOrWhiteSpace(p.Name)))
            {
                return ValidationResult.Invalid(Resources.ErrorReporting_Validation_InvalidParameterName);
            }

            var duplicateNames = parameters
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateNames.Any())
            {
                return ValidationResult.Invalid(string.Format(Resources.ErrorReporting_Validation_DuplicateParameters, string.Join(", ", duplicateNames)));
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Determines which assistant (person) should handle the naming session.
        /// The selection logic follows this priority order:
        /// 1. A specifically configured ExecutivePerson (must be available).
        /// 2. The first person from a provided parent session (if any).
        /// 3. The first available person returned by the host.
        /// 
        /// Throws if no suitable assistant is found.
        /// </summary>
        /// <param name="requestData">Current request data, potentially containing a parent session reference.</param>
        /// <returns>The selected <see cref="IHostPerson"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when no assistants are available.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the configured ExecutivePerson is not available.</exception>
        private async Task<IHostPerson> GetExecutivePersonAsync(Dictionary<string, object?> requestData)
        {
            // 1. Prefer the explicitly configured executive assistant.
            if (_config.ExecutivePerson != null)
            {
                var allAvailable = await _host.GetHostPersonsAsync(null);
                if (allAvailable == null || allAvailable.Count == 0)
                {
                    throw new ArgumentException(Resources.ErrorNoAssistantsAvailable);
                }

                var executiveMatch = allAvailable.FirstOrDefault(p =>
                    string.Equals(p.Name, _config.ExecutivePerson.Name, StringComparison.OrdinalIgnoreCase));
                if (executiveMatch == null)
                {
                    throw new InvalidOperationException(string.Format(Resources.ErrorConfiguredAssistantsNotAvailable,
                        _config.ExecutivePerson.Name));
                }

                return executiveMatch;
            }

            // 2. Fallback: first person from the parent session (if provided).
            string? selectedPersonName = null;
            var parentSession = GetParameterValue<IHostSession>(requestData, DaoStudio.Common.Plugins.Constants.DasSession);
            if (parentSession != null)
            {
                var parentPersons = await parentSession.GetPersonsAsync();
                selectedPersonName = parentPersons?.FirstOrDefault()?.Name;
            }

            // 3. Fallback: first available person from host.
            List<IHostPerson>? availablePersons = null;
            if (selectedPersonName == null)
            {
                try
                {
                    availablePersons = await _host.GetHostPersonsAsync(null);
                }
                catch
                {
                    // Ignore errors and treat as no available persons.
                }
                selectedPersonName = availablePersons?.FirstOrDefault()?.Name;
            }

            if (string.IsNullOrEmpty(selectedPersonName))
            {
                throw new ArgumentException(Resources.ErrorNoAssistantsAvailable);
            }

            // Reuse the list we already fetched earlier if available to avoid duplicate host calls
            IHostPerson? confirmedPerson = null;
            if (availablePersons != null)
            {
                confirmedPerson = availablePersons.FirstOrDefault(p => string.Equals(p.Name, selectedPersonName, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                var persons = await _host.GetHostPersonsAsync(null);
                confirmedPerson = persons?.FirstOrDefault(p => string.Equals(p.Name, selectedPersonName, StringComparison.OrdinalIgnoreCase));
            }

            if (confirmedPerson == null)
            {
                throw new ArgumentException(Resources.ErrorNoAssistantsAvailable);
            }

            return confirmedPerson;
        }

        /// <summary>
        /// Executes the naming session with the selected person
        /// </summary>
        /// <param name="requestData">The request data</param>
        /// <param name="selectedPerson">The selected person</param>
        /// <returns>Session result</returns>
        private async Task<string> ExecuteNamingSessionAsync(
            Dictionary<string, object?> requestData,
            string selectedPersonName,
            IHostSession? contextSession)
        {
            // Use the shared runner to avoid duplicated logic
            var cancellationToken = contextSession?.CurrentCancellationToken?.Token ?? CancellationToken.None;

            var childResult = await NamingSessionRunner.RunSessionAsync(
                _host,
                contextSession,
                selectedPersonName,
                requestData,
                _config,
                null, // No parameter info for single session execution
                cancellationToken);

            if (childResult.Success)
            {
                return Resources.Status_Succeeded;
            }

            if (!childResult.Success)
            {
                var errorMessage = !string.IsNullOrWhiteSpace(childResult.ErrorMessage)
                    ? childResult.ErrorMessage!
                    : Resources.ErrorReporting_DefaultParentMessage;

                return $"{Resources.Status_Failed}: {errorMessage}";
            }

            if (!string.IsNullOrWhiteSpace(childResult.ErrorMessage))
            {
                return $"{Resources.Status_Failed}: {childResult.ErrorMessage}";
            }

            return Resources.Status_Failed;
        }


        /// <summary>
        /// Gets a parameter value from the request data dictionary with type safety
        /// </summary>
        private static T? GetParameterValue<T>(Dictionary<string, object?> requestData, string parameterName)
        {
            if (requestData.TryGetValue(parameterName, out var value) && value != null)
            {
                try
                {
                    if (value is T directValue)
                        return directValue;
                    
                    if (typeof(T) == typeof(string))
                        return (T)(object)value.ToString()!;
                    
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default(T);
                }
            }
            return default(T);
        }

        /// <summary>
        /// Executes parallel naming sessions based on the configuration
        /// </summary>
        /// <param name="requestData">The request data</param>
        /// <param name="selectedPerson">The selected person for all sessions</param>
        /// <returns>Formatted result of parallel execution</returns>
        private async Task<string> ExecuteParallelNamingAsync(Dictionary<string, object?> requestData, string selectedPersonName, IHostSession? contextSession)
        {
            try
            {
                // Extract parallel sources based on configuration
                var parallelSources = ParallelParameterExtractor.ExtractParallelSources(requestData, _config.ParallelConfig!);

                if (parallelSources.Count == 0)
                {
                    return Resources.ErrorNoValidParametersForParallelExecution;
                }

                // Execute parallel sessions
                var parentCancellationToken = contextSession?.CurrentCancellationToken?.Token ?? CancellationToken.None;
                var parallelResult = await ParallelSessionManager.ExecuteParallelSessionsAsync(
                    _host,
                    contextSession,
                    selectedPersonName,
                    requestData,
                    parallelSources,
                    _config,
                    parentCancellationToken);

                // Format and return the result
                return FormatParallelResult(parallelResult);
            }
            catch (Exception ex)
            {
                return string.Format(Resources.ErrorParallelExecution, ex.Message);
            }
        }

        /// <summary>
        /// Formats the parallel execution result into a user-friendly string
        /// </summary>
        /// <param name="parallelResult">The parallel execution result</param>
        /// <returns>Formatted result string</returns>
        private string FormatParallelResult(ParallelExecutionResult parallelResult)
        {
            var resultBuilder = new StringBuilder();

            if (parallelResult.Success)
            {
                // Add success summary based on strategy
                var successSummary = _config.ParallelConfig!.ResultStrategy switch
                {
                    ParallelResultStrategy.StreamIndividual =>
                        string.Format(Resources.Status_ParallelStreamingCompleted,
                            parallelResult.CompletedSessions, parallelResult.TotalSessions),
                    
                    ParallelResultStrategy.WaitForAll =>
                        string.Format(Resources.Status_ParallelWaitForAllCompleted,
                            parallelResult.CompletedSessions, parallelResult.TotalSessions,
                            FormatResultSummary(parallelResult.Results)) + $" {Resources.Status_Succeeded}",
                    
                    ParallelResultStrategy.FirstResultWins =>
                        string.Format(Resources.Status_ParallelFirstWinsCompleted,
                            parallelResult.Results.FirstOrDefault()?.ChildResult?.Result ?? Resources.Status_NoResult),
                    
                    _ => Resources.Status_Succeeded
                };

                resultBuilder.AppendLine(successSummary);

                // Append errors if any sessions failed
                if (parallelResult.FailedSessions > 0)
                {
                    var errors = FormatErrors(parallelResult.Results);
                    if (!string.IsNullOrWhiteSpace(errors))
                    {
                        resultBuilder.AppendLine();
                        resultBuilder.AppendLine($"Errors ({parallelResult.FailedSessions} failed):");
                        resultBuilder.Append(errors);
                    }
                }
            }
            else
            {
                var errorDetails = string.IsNullOrEmpty(parallelResult.ErrorMessage)
                    ? string.Format(Resources.Error_ParallelExecutionDetails,
                        parallelResult.CompletedSessions, parallelResult.TotalSessions, parallelResult.FailedSessions)
                    : parallelResult.ErrorMessage;
                
                resultBuilder.AppendLine(string.Format(Resources.Status_ParallelFailed, errorDetails));

                // Append detailed errors from individual sessions
                var errors = FormatErrors(parallelResult.Results);
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    resultBuilder.AppendLine();
                    resultBuilder.Append(errors);
                }
            }

            return resultBuilder.ToString().TrimEnd();
        }

        /// <summary>
        /// Creates a summary of results for WaitForAll strategy
        /// </summary>
        /// <param name="sessionResults">The individual session results</param>
        /// <returns>Formatted summary string</returns>
        private string FormatResultSummary(List<ParallelSessionResult> sessionResults)
        {
            if (sessionResults == null || sessionResults.Count == 0)
                return Resources.Status_NoResults;

            var successfulResults = sessionResults
                .Where(r => r.IsSuccess && !string.IsNullOrEmpty(r.ChildResult?.Result))
                .Select((r, index) => $"{index + 1}. {r.ChildResult!.Result}")
                .ToList();

            if (successfulResults.Count == 0)
                return Resources.Status_NoSuccessfulResults;

            return string.Join("\n", successfulResults);
        }

        /// <summary>
        /// Formats error messages from failed sessions
        /// </summary>
        /// <param name="sessionResults">The individual session results</param>
        /// <returns>Concatenated error messages</returns>
        private string FormatErrors(List<ParallelSessionResult> sessionResults)
        {
            if (sessionResults == null || sessionResults.Count == 0)
                return string.Empty;

            var errorMessages = new List<string>();

            foreach (var result in sessionResults.Where(r => !r.IsSuccess))
            {
                var paramInfo = result.ParameterName != null 
                    ? $"[{result.ParameterName}={result.ParameterValue}]" 
                    : "[Unknown]";

                var errorMessage = result.Exception?.Message 
                    ?? result.ChildResult?.ErrorMessage 
                    ?? "Unknown error";

                errorMessages.Add($"- {paramInfo}: {errorMessage}");
            }

            return errorMessages.Count > 0 
                ? string.Join("\n", errorMessages) 
                : string.Empty;
        }

        /// <summary>
        /// Represents the result of a validation operation
        /// </summary>
        private class ValidationResult
        {
            public bool IsValid { get; private set; }
            public string ErrorMessage { get; private set; }

            private ValidationResult(bool isValid, string errorMessage = "")
            {
                IsValid = isValid;
                ErrorMessage = errorMessage;
            }

            public static ValidationResult Valid() => new ValidationResult(true);
            public static ValidationResult Invalid(string errorMessage) => new ValidationResult(false, errorMessage);
        }

    }
}
