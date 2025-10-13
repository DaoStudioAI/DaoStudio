using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming.Extensions;
using NamingTool.Properties;
using Serilog;
using Naming;
using Scriban;
using Scriban.Runtime;
using Scriban.Functions;

namespace Naming.ParallelExecution
{
    /// <summary>
    /// Manages parallel execution of multiple naming sessions with configurable concurrency and result strategies
    /// </summary>
    internal static class ParallelSessionManager
    {
        /// <summary>
        /// Executes multiple naming sessions in parallel with configurable concurrency and result aggregation
        /// </summary>
        /// <param name="host">The host for creating new sessions</param>
        /// <param name="hostSession">The parent host session</param>
        /// <param name="selectedPersonName">The person name to use for all parallel sessions</param>
        /// <param name="refsources">List of objects for parallel execution</param>
        /// <param name="config">The naming configuration</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Aggregated result from all parallel sessions</returns>
        public static async Task<ParallelExecutionResult> ExecuteParallelSessionsAsync(
            IHost host,
            IHostSession? hostSession,
            string selectedPersonName,
            Dictionary<string, object?> refsources,
            List<(string, object?)> parallelSources,
            NamingConfig config,
            CancellationToken cancellationToken = default)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (string.IsNullOrWhiteSpace(selectedPersonName)) throw new ArgumentException("Person name cannot be null or empty", nameof(selectedPersonName));
            if (refsources == null) throw new ArgumentNullException(nameof(refsources));
            if (config?.ParallelConfig == null) throw new ArgumentException("ParallelConfig cannot be null", nameof(config));

            var parallelConfig = config.ParallelConfig;
            var startTime = DateTime.UtcNow;

            Log.Information("Starting parallel execution with {SessionCount} parameters, strategy: {Strategy}, max concurrency: {MaxConcurrency}",
                parallelSources.Count, parallelConfig.ResultStrategy, parallelConfig.MaxConcurrency);

            var result = new ParallelExecutionResult
            {
                Strategy = parallelConfig.ResultStrategy,
                TotalSessions = parallelSources.Count,
                StartTime = startTime,
                Results = new List<ParallelSessionResult>()
            };

            try
            {
                // Execute based on selected strategy
                result = parallelConfig.ResultStrategy switch
                {
                    ParallelResultStrategy.StreamIndividual => 
                        await ExecuteWithStreaming(host, hostSession, selectedPersonName, refsources, parallelSources,config, cancellationToken),
                    ParallelResultStrategy.WaitForAll => 
                        await ExecuteWaitForAll(host, hostSession, selectedPersonName, refsources, parallelSources,config, cancellationToken),
                    ParallelResultStrategy.FirstResultWins => 
                        await ExecuteFirstWins(host, hostSession, selectedPersonName, refsources, parallelSources, config, cancellationToken),
                    _ => throw new NotSupportedException($"Result strategy {parallelConfig.ResultStrategy} is not supported")
                };

                result.EndTime = DateTime.UtcNow;
                result.ExecutionTime = result.EndTime - result.StartTime;

                Log.Information("Parallel execution completed: {CompletedSessions}/{TotalSessions} successful, {FailedSessions} failed, duration: {Duration}",
                    result.CompletedSessions, result.TotalSessions, result.FailedSessions, result.ExecutionTime);

                return result;
            }
            // Propagate unsupported strategy exceptions so that callers can handle them explicitly (e.g. tests)
            catch (NotSupportedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during parallel session execution");
                result.Success = false;
                result.ErrorMessage = $"Execution failed: {ex.Message}";
                result.EndTime = DateTime.UtcNow;
                result.ExecutionTime = result.EndTime - result.StartTime;
                return result;
            }
        }

        /// <summary>
        /// Executes sessions with streaming - sends each result as it completes
        /// </summary>
        private static async Task<ParallelExecutionResult> ExecuteWithStreaming(
            IHost host,
            IHostSession? hostSession,
            string selectedPersonName,
            Dictionary<string, object?> refSources,
            List<(string, object?)> parallelSources,
            NamingConfig config,
            CancellationToken cancellationToken)
        {
            var parallelConfig = config.ParallelConfig!;
            var result = new ParallelExecutionResult
            {
                Strategy = ParallelResultStrategy.StreamIndividual,
                TotalSessions = parallelSources.Count,
                StartTime = DateTime.UtcNow
            };

            var effectiveConcurrency = GetEffectiveConcurrency(parallelConfig.MaxConcurrency, parallelSources.Count);
            var semaphore = new SemaphoreSlim(effectiveConcurrency, effectiveConcurrency);
            var tasks = new List<Task>();

            for (int i = 0; i < parallelSources.Count; i++)
            {
                var parameterInfo = parallelSources[i];
                var task = ExecuteSingleSessionWithStreaming(
                    host, hostSession, selectedPersonName, refSources, (parameterInfo.Item1, parameterInfo.Item2), 
                    config, semaphore, result, cancellationToken);
                
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            result.CompletedSessions = result.Results.Count(r => r.IsSuccess);
            result.FailedSessions = result.Results.Count(r => !r.IsSuccess);
            // Consider the aggregated execution successful ONLY when at least one child
            // session completed successfully. If all sessions failed, propagate the failure
            // status so that callers can handle it appropriately (e.g., unit tests).
            result.Success = result.CompletedSessions > 0;

            // Populate error message if unsuccessful
            if (!result.Success && string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                result.ErrorMessage = string.Format(Resources.Error_ParallelExecutionDetails,
                    result.CompletedSessions, result.TotalSessions, result.FailedSessions);
            }

            return result;
        }

        /// <summary>
        /// Executes sessions and waits for all to complete before returning combined results
        /// </summary>
        private static async Task<ParallelExecutionResult> ExecuteWaitForAll(
            IHost host,
            IHostSession? hostSession,
            string selectedPersonName,
            Dictionary<string, object?> refSources,
            List<(string, object?)> parallelSources,
            NamingConfig config,
            CancellationToken cancellationToken)
        {
            var parallelConfig = config.ParallelConfig!;
            var result = new ParallelExecutionResult
            {
                Strategy = ParallelResultStrategy.WaitForAll,
                TotalSessions = parallelSources.Count,
                StartTime = DateTime.UtcNow
            };

            var effectiveConcurrency = GetEffectiveConcurrency(parallelConfig.MaxConcurrency, parallelSources.Count);
            var semaphore = new SemaphoreSlim(effectiveConcurrency, effectiveConcurrency);
            var tasks = new List<Task<ParallelSessionResult>>();

            for (int i = 0; i < parallelSources.Count; i++)
            {
                var parameterInfo = parallelSources[i];
                var task = ExecuteSingleSessionAsync(
                    host, hostSession, selectedPersonName, refSources, (parameterInfo.Item1, parameterInfo.Item2),
                    config, semaphore,  cancellationToken);

                tasks.Add(task);
            }

            var sessionResults = await Task.WhenAll(tasks);
            result.Results.AddRange(sessionResults);

            result.CompletedSessions = result.Results.Count(r => r.IsSuccess);
            result.FailedSessions = result.Results.Count(r => !r.IsSuccess);
            // Consider the aggregated execution successful ONLY when at least one child
            // session completed successfully. If all sessions failed, propagate the failure
            // status so that callers can handle it appropriately (e.g., unit tests).
            result.Success = result.CompletedSessions > 0;

            // Populate a generic error message when overall execution is unsuccessful and none provided yet
            if (!result.Success && string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                result.ErrorMessage = string.Format(Resources.Error_ParallelExecutionDetails,
                    result.CompletedSessions, result.TotalSessions, result.FailedSessions);
            }

            return result;
        }

        /// <summary>
        /// Executes sessions and returns as soon as the first one completes successfully
        /// </summary>
        private static async Task<ParallelExecutionResult> ExecuteFirstWins(
            IHost host,
            IHostSession? hostSession,
            string selectedPersonName,
            Dictionary<string, object?> refSources,
            List<(string, object?)> parallelSources,
            NamingConfig config,
            CancellationToken cancellationToken)
        {
            var parallelConfig = config.ParallelConfig!;
            var result = new ParallelExecutionResult
            {
                Strategy = ParallelResultStrategy.FirstResultWins,
                TotalSessions = parallelSources.Count,
                StartTime = DateTime.UtcNow
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var effectiveConcurrency = GetEffectiveConcurrency(parallelConfig.MaxConcurrency, parallelSources.Count);
            var semaphore = new SemaphoreSlim(effectiveConcurrency, effectiveConcurrency);
            var tasks = new List<Task<ParallelSessionResult>>();

            for (int i = 0; i < parallelSources.Count; i++)
            {
                var parameterInfo = parallelSources[i];
                var task = ExecuteSingleSessionAsync(
                    host, hostSession, selectedPersonName, refSources, (parameterInfo.Item1, parameterInfo.Item2),
                    config,semaphore, cancellationToken);

                tasks.Add(task);
            }

            try
            {
                // Wait for the first successful result
                var completedTask = await Task.WhenAny(tasks);
                var firstResult = await completedTask;

                if (firstResult.IsSuccess)
                {
                    // Cancel remaining tasks
                    cts.Cancel();
                    
                    result.Results.Add(firstResult);
                    result.CompletedSessions = 1;
                    result.FailedSessions = 0;
                    result.Success = true;

                    Log.Information("First successful result received from session {ParameterName}={ParameterValue}, cancelling remaining sessions", 
                        firstResult.ParameterName, firstResult.ParameterValue);
                }
                else
                {
                    // First result failed, wait for any successful result
                    var remainingTasks = tasks.Where(t => t != completedTask).ToList();
                    remainingTasks.Add(completedTask); // Include the failed task in results
                    
                    var allResults = await Task.WhenAll(remainingTasks);
                    result.Results.AddRange(allResults);
                    
                    var successfulResult = allResults.FirstOrDefault(r => r.IsSuccess);
                    if (successfulResult != null)
                    {
                        result.CompletedSessions = 1;
                        result.FailedSessions = allResults.Count(r => !r.IsSuccess);
                        result.Success = true;
                    }
                    else
                    {
                        result.CompletedSessions = 0;
                        result.FailedSessions = allResults.Length;
                        result.Success = false;
                        result.ErrorMessage = "All parallel sessions failed";
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result.Success = false;
                result.ErrorMessage = "Operation was cancelled";
            }

            return result;
        }

        /// <summary>
        /// Executes a single session and streams the result immediately
        /// </summary>
        private static async Task ExecuteSingleSessionWithStreaming(
            IHost host,
            IHostSession? hostSession,
            string selectedPersonName,
            Dictionary<string, object?> sourceData,
            (string? Name, object? Value) parameterInfo,
            NamingConfig config,
            SemaphoreSlim semaphore,
            ParallelExecutionResult aggregateResult,
            CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);
            
            try
            {
                var sessionResult = await ExecuteSingleSessionAsync(
                    host, hostSession, selectedPersonName, sourceData, parameterInfo, 
                    config, semaphore, cancellationToken);

                // Add to aggregate result in a thread-safe way
                lock (aggregateResult.Results)
                {
                    aggregateResult.Results.Add(sessionResult);
                }

                // Stream the result immediately if we have a host session
                if (hostSession != null)
                {
                    string message;
                    if (sessionResult.IsSuccess)
                    {
                        message = $"Parallel session {parameterInfo.Name}={parameterInfo.Value} completed successfully: {sessionResult.ChildResult?.Result}";
                    }
                    else if (sessionResult.ChildResult != null && !sessionResult.ChildResult.Success)
                    {
                        var errorMessage = !string.IsNullOrWhiteSpace(sessionResult.ChildResult.ErrorMessage)
                            ? sessionResult.ChildResult.ErrorMessage!
                            : Resources.ErrorReporting_DefaultParentMessage;

                        message = $"Parallel session {parameterInfo.Name}={parameterInfo.Value} reported an error: {errorMessage}";
                    }
                    else
                    {
                        message = $"Parallel session {parameterInfo.Name}={parameterInfo.Value} failed: {sessionResult.ChildResult?.ErrorMessage ?? sessionResult.Exception?.Message}";
                    }

                    try
                    {
                        await hostSession.SendMessageAsync(HostSessMsgType.Message, message);
                        Log.Debug("Streamed result for session {ParameterName}={ParameterValue}", parameterInfo.Name, parameterInfo.Value);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to stream result for session {ParameterName}={ParameterValue}", parameterInfo.Name, parameterInfo.Value);
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Executes a single session and returns the result
        /// </summary>
        private static async Task<ParallelSessionResult> ExecuteSingleSessionAsync(
            IHost host,
            IHostSession? hostSession,
            string selectedPersonName,
            Dictionary<string, object?> sourceData,
            (string? Name, object? Value) parameterInfo,
            NamingConfig config,
            SemaphoreSlim semaphore,
            CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);
            
            var sessionResult = new ParallelSessionResult
            {
                ParameterName = parameterInfo.Name,
                ParameterValue = parameterInfo.Value,
                InputParameters = sourceData,
                StartTime = DateTime.UtcNow
            };

            try
            {
                Log.Debug("Starting parallel session {ParameterName}={ParameterValue}", parameterInfo.Name, parameterInfo.Value);

                // Create timeout cancellation token
                using var timeoutCts = new CancellationTokenSource(config.ParallelConfig?.SessionTimeoutMs ?? 30 * 60 * 1000);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                // Delegate execution to the shared NamingSessionRunner
                var childResult = await NamingSessionRunner.RunSessionAsync(
                    host,
                    hostSession,
                    selectedPersonName,
                    sourceData,
                    config,
                    parameterInfo,
                    combinedCts.Token);

                sessionResult.ChildResult = childResult;
                sessionResult.EndTime = DateTime.UtcNow;

                Log.Debug("Parallel session {ParameterName}={ParameterValue} completed. Success: {Success}", 
                    parameterInfo.Name, parameterInfo.Value, childResult.Success);

                return sessionResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in parallel session {ParameterName}={ParameterValue}", parameterInfo.Name, parameterInfo.Value);
                
                sessionResult.Exception = ex;
                sessionResult.EndTime = DateTime.UtcNow;
                sessionResult.ChildResult = new ChildSessionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };

                return sessionResult;
            }
            finally
            {
                semaphore.Release();
            }
        }
        
        // Ensures a safe and sensible concurrency value for SemaphoreSlim
        private static int GetEffectiveConcurrency(int configured, int totalSessions)
        {
            var baseline = configured <= 0 ? Environment.ProcessorCount : configured;
            if (totalSessions <= 0) return Math.Max(1, baseline);
            return Math.Max(1, Math.Min(baseline, totalSessions));
        }

        /// <summary>
        /// Parses a Scriban template using the provided data - simplified version from NamingHandler
        /// </summary>
        private static string ParseScribanTemplate(string template, Dictionary<string, object?> data)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            try
            {
                var scribanTemplate = Scriban.Template.Parse(template);
                if (scribanTemplate.HasErrors)
                {
                    var errors = string.Join(", ", scribanTemplate.Messages.Select(m => m.Message));
                    Log.Warning("Template parsing errors: {Errors}", errors);
                    return template; // Return original template if parsing fails
                }

                var context = new TemplateContext();
                context.MemberRenamer = member => member.Name;

                var scriptObject = new ScriptObject();
                foreach (var kvp in data)
                {
                    scriptObject[kvp.Key] = kvp.Value;
                }

                context.PushGlobal(scriptObject);

                var builtins = context.BuiltinObject;
                builtins.Import(typeof(StringFunctions));
                builtins.Import(typeof(ObjectFunctions));
                builtins.Import(typeof(ArrayFunctions));
                builtins.Import(typeof(MathFunctions));
                builtins.Import(typeof(RegexFunctions));
                builtins.Import(typeof(DateTimeFunctions));
                builtins.Import(typeof(TimeSpanFunctions));
                builtins.Import(typeof(HtmlFunctions));

                var result = scribanTemplate.Render(context);
                return result;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Template rendering failed, using original template");
                return template; // Return original template if rendering fails
            }
        }
    }
}