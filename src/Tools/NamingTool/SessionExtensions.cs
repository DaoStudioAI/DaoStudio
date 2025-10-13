using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using NamingTool.Properties;
using NamingTool.Return;
using Naming.ParallelExecution;
using Serilog;

namespace Naming.Extensions
{

    /// <summary>
    /// Extension methods for IHostSession to support child session execution within Naming plugin
    /// </summary>
    internal static class SessionExtensions
    {
        /// <summary>
        /// Runs a child session with the specified message and waits for it to complete.
        /// This helper registers a CustomReturnResultTool inside the child session so the LLM
        /// can explicitly signal completion. It also handles timeout, cancellation and
        /// up-to-three reminder prompts when the result has not been provided.
        /// </summary>
        /// <param name="childSession">The pre-initialised child session.</param>
        /// <param name="message">The initial message to send.</param>
        /// <param name="config">The Naming configuration containing return parameters.</param>
        /// <param name="renderedUrgingMessage">The pre-rendered urging message for reminders.</param>
        /// <param name="timeout">Optional timeout, default 30 minutes.</param>
        /// <param name="cancellationToken">External cancellation token.</param>
        /// <returns>ChildSessionResult produced by CustomReturnResultTool.</returns>
        public static async Task<ChildSessionResult> WaitChildSessionAsync(
            this IHostSession childSession,
            string message,
            NamingConfig config,
            string renderedUrgingMessage,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            // Validate arguments
            ArgumentNullException.ThrowIfNull(childSession);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentException.ThrowIfNullOrWhiteSpace(renderedUrgingMessage);

            try
            {

                // Completion source fulfilled through CustomReturnResultTool
                var completionSource = new TaskCompletionSource<ChildSessionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

                TaskCompletionSource<ChildSessionResult>? errorCompletionSource = null;

                var tools = childSession.GetTools() ?? new Dictionary<string, List<FunctionWithDescription>>();

                RegisterCustomReturnResultTool(childSession, completionSource, config, tools);

                if (config.ErrorReportingConfig != null)
                {
                    errorCompletionSource = new TaskCompletionSource<ChildSessionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                    RegisterCustomErrorReportingTool(childSession, errorCompletionSource, config, tools);
                }

                childSession.SetTools(tools);

                childSession.ToolExecutionMode = ToolExecutionMode.RequireAny;
                Log.Debug("Sending initial message to child session {ChildSessionId}", childSession.Id);

                var sendMessageTask = childSession.SendMessageAsync(HostSessMsgType.Message, message);
                using var cancellationRegistration = cancellationToken.Register(() =>
                {
                    childSession.CurrentCancellationToken?.Cancel();
                });

                var pendingTasks = new List<Task> { completionSource.Task, sendMessageTask };
                if (errorCompletionSource != null)
                {
                    pendingTasks.Add(errorCompletionSource.Task);
                }

                bool pauseActivated = false;

                try
                {
                    while (pendingTasks.Count > 0)
                    {
                        var completedTask = await Task.WhenAny(pendingTasks).ConfigureAwait(false);

                        if (completedTask == completionSource.Task)
                        {
                            pendingTasks.Remove(completionSource.Task);
                            childSession.CurrentCancellationToken?.Cancel();
                            break;
                        }

                        if (completedTask == sendMessageTask)
                        {
                            pendingTasks.Remove(sendMessageTask);
                            await sendMessageTask.ConfigureAwait(false);

                            if (!completionSource.Task.IsCompleted && !pauseActivated)
                            {
                                await HandleDanglingBehaviorAsync(childSession, completionSource, config, renderedUrgingMessage, cancellationToken).ConfigureAwait(false);
                            }

                            continue;
                        }

                        if (errorCompletionSource != null && completedTask == errorCompletionSource.Task)
                        {
                            pendingTasks.Remove(errorCompletionSource.Task);
                            var errorResult = await errorCompletionSource.Task.ConfigureAwait(false);

                            var outcome = await HandleErrorReportingInvocationAsync(childSession, completionSource, errorResult, config, cancellationToken).ConfigureAwait(false);
                            pauseActivated = pauseActivated || outcome.PauseActivated;

                            if (!outcome.ShouldContinueWaiting)
                            {
                                break;
                            }

                            continue;
                        }

                        pendingTasks.Remove(completedTask);
                        await completedTask.ConfigureAwait(false);
                    }
                }
                catch
                {
                    if (!completionSource.Task.IsCompleted)
                        completionSource.TrySetCanceled(cancellationToken);
                    if (!sendMessageTask.IsCompleted)
                        childSession.CurrentCancellationToken?.Cancel();
                    throw;
                }


                var result = await completionSource.Task.ConfigureAwait(false);

                Log.Debug("Child session {ChildSessionId} completed. Success: {Success}", childSession.Id, result.Success);
                return result;
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
            {
                Log.Warning(ex, "Child session {ChildSessionId} ended due to timeout/cancellation", childSession.Id);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled error in WaitChildSessionAsync");
                throw;
            }
        }


        /// <summary>
        /// Registers the CustomReturnResultTool with the specified session using parameters from config.
        /// </summary>
        private static void RegisterCustomReturnResultTool(
            IHostSession hostSession,
            TaskCompletionSource<ChildSessionResult> tcs,
            NamingConfig config,
            Dictionary<string, List<FunctionWithDescription>> tools)
        {
            var sessionId = hostSession.Id;

            CustomReturnResultTool returnTool;

            // Use config parameters if specified, otherwise create tool with no parameters
            if (config.ReturnParameters?.Count > 0)
            {
                var builder = CustomReturnResultToolHelper.CreateBuilder()
                    .WithName(config.ReturnToolName)
                    .WithDescription(config.ReturnToolDescription);

                foreach (var paramConfig in config.ReturnParameters)
                {
                    builder.AddParameter(paramConfig.Name, typeof(string), paramConfig.Description, paramConfig.IsRequired);
                }

                returnTool = builder.Build(tcs, sessionId);
            }
            else
            {
                // Create tool with no parameters if no config is provided
                var builder = CustomReturnResultToolHelper.CreateBuilder()
                    .WithName(config.ReturnToolName)
                    .WithDescription(config.ReturnToolDescription);

                returnTool = builder.Build(tcs, sessionId);
            }

            // Create the function list from the tool methods as usual
            var functionList = IPluginExtensions.CreateFunctionsFromToolMethods(returnTool, nameof(CustomReturnResultTool));

            var desiredParameters = config.ReturnParameters?.Select(p => ParameterConfigConverter.ConvertToMetadata(p)).ToList() 
                ?? new List<FunctionTypeMetadata>();

            foreach (var fn in functionList)
            {
                // Overwrite the autogenerated name and description so they reflect the configuration
                fn.Description.Name = config.ReturnToolName;
                fn.Description.Description = config.ReturnToolDescription;

                // Overwrite the autogenerated parameter list
                fn.Description.Parameters = desiredParameters;
            }

            tools[nameof(CustomReturnResultTool)] = functionList;

            Log.Debug("CustomReturnResultTool registered for child session {ChildSessionId}", sessionId);
        }

        /// <summary>
        /// Registers the CustomErrorReportingTool with the specified session when configured.
        /// </summary>
        private static void RegisterCustomErrorReportingTool(
            IHostSession hostSession,
            TaskCompletionSource<ChildSessionResult> errorCompletionSource,
            NamingConfig config,
            Dictionary<string, List<FunctionWithDescription>> tools)
        {
            var sessionId = hostSession.Id;
            var errorConfig = config.ErrorReportingConfig;
            if (errorConfig == null) return;

            var builder = CustomErrorReportingToolHelper.CreateBuilder()
                .WithName(config.ErrorReportingToolName)
                .WithDescription(errorConfig.ToolDescription);

            var effectiveParameters = (errorConfig.Parameters?.Count ?? 0) > 0
                ? errorConfig.Parameters!
                : new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        Name = "error_message",
                        Description = "Human readable description of the issue",
                        IsRequired = true,
                        Type = ParameterType.String
                    },
                    new ParameterConfig
                    {
                        Name = "error_type",
                        Description = "Optional classification or category for the error",
                        IsRequired = false,
                        Type = ParameterType.String
                    }
                };

            foreach (var param in effectiveParameters)
            {
                builder.AddParameter(param.Name, typeof(string), param.Description, param.IsRequired);
            }

            var errorTool = builder.Build(errorCompletionSource, sessionId);

            var functionList = IPluginExtensions.CreateFunctionsFromToolMethods(errorTool, nameof(CustomErrorReportingTool));

            var desiredParameters = effectiveParameters.Select(p => ParameterConfigConverter.ConvertToMetadata(p)).ToList();

            foreach (var fn in functionList)
            {
                fn.Description.Name = config.ErrorReportingToolName;
                fn.Description.Description = errorConfig.ToolDescription;
                fn.Description.Parameters = desiredParameters;
            }

            tools[nameof(CustomErrorReportingTool)] = functionList;

            Log.Debug("CustomErrorReportingTool registered for child session {ChildSessionId}", sessionId);
        }

        /// <summary>
        /// Handles the configured dangling behavior when child session doesn't call the return tool
        /// </summary>
        private static async Task HandleDanglingBehaviorAsync(IHostSession session, TaskCompletionSource<ChildSessionResult> tcs, NamingConfig config, string renderedUrgingMessage, CancellationToken token)
        {
            switch (config.DanglingBehavior)
            {
                case DanglingBehavior.Urge:
                    await HandleUrgeBehaviorAsync(session, tcs, renderedUrgingMessage, token).ConfigureAwait(false);
                    break;
                    
                case DanglingBehavior.ReportError:
                    await HandleReportErrorBehaviorAsync(session, tcs, config, token).ConfigureAwait(false);
                    break;
                    
                case DanglingBehavior.Pause:
                    await HandlePauseBehaviorAsync(session, tcs, token).ConfigureAwait(false);
                    break;
                    
                default:
                    // Fallback to urge behavior for any unknown values
                    Log.Warning("Unknown DanglingBehavior value {DanglingBehavior}, falling back to Urge", config.DanglingBehavior);
                    await HandleUrgeBehaviorAsync(session, tcs, renderedUrgingMessage, token).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        /// Handles the invocation of the error reporting tool based on configuration.
        /// </summary>
        private static Task<(bool ShouldContinueWaiting, bool PauseActivated)> HandleErrorReportingInvocationAsync(
            IHostSession session,
            TaskCompletionSource<ChildSessionResult> completionSource,
            ChildSessionResult errorResult,
            NamingConfig config,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var errorConfig = config.ErrorReportingConfig;
            if (errorConfig == null)
            {
                completionSource.TrySetResult(errorResult);
                session.CurrentCancellationToken?.Cancel();
                return Task.FromResult((false, false));
            }

            switch (errorConfig.Behavior)
            {
                case ErrorReportingBehavior.Pause:
                    Log.Debug("Child session {ChildSessionId} reported error and is entering pause state", session.Id);
                    return Task.FromResult((true, true));

                case ErrorReportingBehavior.ReportError:
                    var parentMessage = BuildParentErrorMessage(session, config, errorConfig, errorResult);
                    errorResult.ErrorMessage = parentMessage;
                    var wasSet = completionSource.TrySetResult(errorResult);
                    if (!wasSet)
                    {
                        Log.Warning("Failed to set error reporting result for child session {ChildSessionId} - completion source already completed", session.Id);
                    }
                    session.CurrentCancellationToken?.Cancel();
                    return Task.FromResult((false, false));

                default:
                    Log.Warning("Unknown ErrorReportingBehavior value {ErrorReportingBehavior}, defaulting to Pause", errorConfig.Behavior);
                    return Task.FromResult((true, true));
            }
        }

        private static string BuildParentErrorMessage(
            IHostSession session,
            NamingConfig config,
            ErrorReportingConfig errorConfig,
            ChildSessionResult errorResult)
        {
            string candidateMessage = errorResult.ErrorMessage ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(errorConfig.CustomErrorMessageToParent))
            {
                candidateMessage = ApplyErrorTemplate(errorConfig.CustomErrorMessageToParent, session, config, errorResult);
            }

            if (string.IsNullOrWhiteSpace(candidateMessage))
            {
                candidateMessage = Resources.ErrorReporting_DefaultParentMessage;
            }

            return candidateMessage;
        }

        private static string ApplyErrorTemplate(
            string template,
            IHostSession session,
            NamingConfig config,
            ChildSessionResult errorResult)
        {
            var timestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var errorMessage = errorResult.ErrorMessage ?? string.Empty;
            var toolName = config.ErrorReportingToolName ?? "report_error";

            return template
                .Replace("{FunctionName}", config.FunctionName)
                .Replace("{SessionId}", session.Id.ToString(CultureInfo.InvariantCulture))
                .Replace("{Timestamp}", timestamp)
                .Replace("{ErrorMessage}", errorMessage)
                .Replace("{ErrorToolName}", toolName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Handles urging behavior (current implementation)
        /// Sends up to three reminder prompts asking the LLM to call CustomReturnResultTool.
        /// </summary>
        private static async Task HandleUrgeBehaviorAsync(IHostSession session, TaskCompletionSource<ChildSessionResult> tcs, string renderedUrgingMessage, CancellationToken token)
        {
            const int MaxRetries = 3;

            // Use the pre-rendered urging message directly
            if (string.IsNullOrWhiteSpace(renderedUrgingMessage))
            {
                throw new InvalidOperationException("RenderedUrgingMessage must be provided and cannot be null or empty when calling HandleUrgeBehaviorAsync with Urge behavior.");
            }

            var reminder = renderedUrgingMessage;

            for (var attempt = 1; attempt <= MaxRetries && !tcs.Task.IsCompleted; attempt++)
            {
                Log.Debug("Result not provided yet – retry {Attempt}/{MaxRetries}", attempt, MaxRetries);
                session.ToolExecutionMode = ToolExecutionMode.RequireAny;
                await session.SendMessageAsync(HostSessMsgType.Message, reminder).ConfigureAwait(false);
            }

            if (!tcs.Task.IsCompleted)
            {
                throw new InvalidOperationException($"Child session failed to provide result after {MaxRetries} reminder attempts.");
            }
        }

        /// <summary>
        /// Handles error reporting behavior
        /// Reports error message to parent session and returns immediately with failure result
        /// </summary>
        private static async Task HandleReportErrorBehaviorAsync(IHostSession session, TaskCompletionSource<ChildSessionResult> tcs, NamingConfig config, CancellationToken token)
        {
            // Use custom error message or default
            var errorMessage = !string.IsNullOrWhiteSpace(config.ErrorMessage) 
                ? config.ErrorMessage 
                : Resources.ErrorHandling_DefaultErrorMessage;

            // Create failure result with plain error message
            var failureResult = ChildSessionResult.CreateError(errorMessage);
            
            Log.Debug("Reporting error for child session {ChildSessionId}: {ErrorMessage}", session.Id, errorMessage);
            
            // Try to set the error result on the completion source
            bool wasSet = tcs.TrySetResult(failureResult);
            if (!wasSet)
            {
                Log.Warning("Failed to set error result for child session {ChildSessionId} - result was already set", session.Id);
            }

            await Task.CompletedTask; // Make method async
        }

        /// <summary>
        /// Handles pause behavior
        /// Keep session alive indefinitely, wait for return tool call
        /// </summary>
        private static async Task HandlePauseBehaviorAsync(IHostSession session, TaskCompletionSource<ChildSessionResult> tcs, CancellationToken token)
        {
            Log.Debug("Pausing child session {ChildSessionId} - waiting for manual return tool call", session.Id);
            
            // Simply wait on the completion source without sending any urging messages
            // No timeout - wait indefinitely for return tool to be called manually
            // Session remains active and responsive to user messages
            // Return tool registration remains active throughout
            
            await Task.CompletedTask; // Make method async - actual waiting happens at calling level
        }
    }

    /// <summary>
    /// Extension methods for parallel execution support
    /// </summary>
    internal static class ParallelSessionExtensions
    {

        /// <summary>
        /// Creates multiple child sessions for parallel execution
        /// </summary>
        /// <param name="host">The host for creating sessions</param>
        /// <param name="parentSession">The parent session</param>
        /// <param name="selectedPersonName">The person name to use for all sessions</param>
        /// <param name="sessionCount">Number of sessions to create</param>
        /// <returns>List of created child sessions</returns>
        public static async Task<List<IHostSession>> CreateMultipleChildSessionsAsync(
            this IHost host,
            IHostSession? parentSession,
            string selectedPersonName,
            int sessionCount)
        {
            var sessions = new List<IHostSession>();

            for (int i = 0; i < sessionCount; i++)
            {
                var childSession = await host.StartNewHostSessionAsync(parentSession, selectedPersonName);
                sessions.Add(childSession);
            }

            return sessions;
        }


        /// <summary>
        /// Cancels multiple child sessions
        /// </summary>
        /// <param name="childSessions">List of child sessions to cancel</param>
        public static void CancelMultipleChildSessions(this List<IHostSession> childSessions)
        {
            foreach (var session in childSessions)
            {
                try
                {
                    session.CurrentCancellationToken?.Cancel();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error cancelling child session {SessionId}", session.Id);
                }
            }
        }

        /// <summary>
        /// Disposes multiple child sessions safely
        /// </summary>
        /// <param name="childSessions">List of child sessions to dispose</param>
        public static void DisposeMultipleChildSessions(this List<IHostSession> childSessions)
        {
            foreach (var session in childSessions)
            {
                try
                {
                    session.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error disposing child session {SessionId}", session.Id);
                }
            }
        }
    }
}
