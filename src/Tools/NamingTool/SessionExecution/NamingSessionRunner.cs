using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming.Extensions;
using Naming.ParallelExecution;
using Scriban;
using Scriban.Runtime;
using Scriban.Functions;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Naming
{
    /// <summary>
    /// Provides a unified way to execute a single naming child session so that both
    /// single-threaded and parallel execution paths can share the same implementation.
    /// </summary>
    internal static class NamingSessionRunner
    {
        /// <summary>
        /// Executes a child session and returns the <see cref="ChildSessionResult"/> produced by <see cref="IHostSession.WaitChildSessionAsync"/>.
        /// The method encapsulates the common logic that was previously duplicated between
        /// <c>NamingHandler.ExecuteNamingSessionAsync</c> and <c>ParallelSessionManager.ExecuteSingleSessionAsync</c>.
        /// </summary>
        /// <param name="host">The host used to create a new session.</param>
        /// <param name="contextSession">Optional parent session; <c>null</c> for a root session.</param>
        /// <param name="personName">Name of the assistant that will handle the session.</param>
        /// <param name="requestData">Dictionary containing request parameters (excluding _Parameter).</param>
        /// <param name="config">Active <see cref="NamingConfig"/>.</param>
        /// <param name="parameterInfo">Optional tuple containing parameter name and value for parallel execution.</param>
        /// <param name="cancellationToken">Cancellation token controlling the execution.</param>
        /// <returns>The <see cref="ChildSessionResult"/> returned by the child session.</returns>
        /// <exception cref="ArgumentNullException">Thrown when required arguments are <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when mandatory configuration is missing.</exception>
        public static async Task<ChildSessionResult> RunSessionAsync(
            IHost host,
            IHostSession? contextSession,
            string personName,
            Dictionary<string, object?> requestData,
            NamingConfig config,
            (string? Name, object? Value)? parameterInfo = null,
            CancellationToken cancellationToken = default)
        {
            // Validation
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (string.IsNullOrWhiteSpace(personName))
                throw new ArgumentException("Person name cannot be null or empty", nameof(personName));
            if (requestData == null) throw new ArgumentNullException(nameof(requestData));
            if (config == null) throw new ArgumentNullException(nameof(config));

            // Start the child session early so that creation is tracked even if subsequent
            // template parsing/rendering fails for a particular item. This better matches
            // test expectations that a session is created per parallel source.
            var childSession = await host.StartNewHostSessionAsync(contextSession, personName);

            // Compose prompt after session creation
            var parsedPrompt = ParseScribanTemplate(config.PromptMessage, requestData, config, parameterInfo);

            // Compose urging message
            string renderedUrgingMessage;
            if (!string.IsNullOrWhiteSpace(config.UrgingMessage))
            {
                renderedUrgingMessage = ParseScribanTemplate(config.UrgingMessage, requestData, config, parameterInfo);
            }
            else
            {
                throw new InvalidOperationException("UrgingMessage cannot be empty.");
            }

            // Await the child session result
            var childResult = await childSession.WaitChildSessionAsync(
                parsedPrompt,
                config,
                renderedUrgingMessage,
                cancellationToken: cancellationToken);

            return childResult;
        }

        #region Template Rendering Helpers
        private static string ParseScribanTemplate(string template, Dictionary<string, object?> requestData, NamingConfig config, (string? Name, object? Value)? parameterInfo = null)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            // Create Scriban template context for better parameter management
            var context = new TemplateContext();
            // Preserve original member casing so templates can access camelCase properties like contentType
            context.MemberRenamer = member => member.Name;
            // Throw exception when attempting to access undefined variables/fields
            //context.StrictVariables = true;
            var scriptObject = new ScriptObject();

            // 1. Parameters explicitly declared in NamingConfig
            if (config.InputParameters?.Count > 0)
            {
                foreach (var parameter in config.InputParameters)
                {
                    if (requestData.TryGetValue(parameter.Name, out var value))
                    {
                        scriptObject[parameter.Name] = value;
                    }
                    else if (parameter.IsRequired)
                    {
                        // Parameter is required but missing; keep key for transparency
                        scriptObject[parameter.Name] = null;
                    }
                }
            }

            // 2. Any additional entries provided by caller (excluding internal _Parameter entries)
            foreach (var kvp in requestData)
            {
                if (!scriptObject.ContainsKey(kvp.Key) && !kvp.Key.StartsWith("_Parameter"))
                {
                    scriptObject[kvp.Key] = kvp.Value;
                }
            }

            // 3. Expose the entire NamingConfig so that templates can access advanced settings
            //    Import as a ScriptObject to ensure properties are accessible regardless of type visibility
            var configObj = new ScriptObject();
            foreach (var prop in config.GetType().GetProperties())
            {
                try
                {
                    configObj[prop.Name] = prop.GetValue(config);
                }
                catch
                {
                    // Ignore properties that cannot be read for any reason
                }
            }
            scriptObject["_Config"] = configObj;

            // 4. Add _Parameter object for parallel execution support
            //    Always expose _Parameter to avoid null reference errors in templates
            //    that reference {{ _Parameter.Name }} or {{ _Parameter.Value }} even when
            //    running in single-execution mode.
            {
                var parameterObj = new ScriptObject();
                parameterObj["Name"] = parameterInfo.HasValue ? parameterInfo.Value.Name : null;
                parameterObj["Value"] = parameterInfo.HasValue ? parameterInfo.Value.Value : null;
                scriptObject["_Parameter"] = parameterObj;
            }

            // Set the script object as the global object for the template context
            context.PushGlobal(scriptObject);

            // Explicitly import Scriban built-in function namespaces so templates can use
            // helpers like string.contains, object.keys, array.size, math.round, etc.
            // This ensures consistent availability when using a custom TemplateContext.
            var builtins = context.BuiltinObject;
            builtins.Import(typeof(StringFunctions));
            builtins.Import(typeof(ObjectFunctions));
            builtins.Import(typeof(ArrayFunctions));
            builtins.Import(typeof(MathFunctions));
            builtins.Import(typeof(LiquidBuiltinsFunctions));
            builtins.Import(typeof(RegexFunctions));
            builtins.Import(typeof(DateTimeFunctions));
            builtins.Import(typeof(TimeSpanFunctions));
            builtins.Import(typeof(HtmlFunctions));
            builtins.Import(typeof(BuiltinFunctions));

            // Quick validation for common template typos (e.g. unmatched moustaches)
            int openCount = 0, closeCount = 0, idx = 0;
            while ((idx = template.IndexOf("{{", idx, StringComparison.Ordinal)) >= 0)
            {
                openCount++; idx += 2;
            }
            idx = 0;
            while ((idx = template.IndexOf("}}", idx, StringComparison.Ordinal)) >= 0)
            {
                closeCount++; idx += 2;
            }
            if (openCount != closeCount)
            {
                throw new InvalidOperationException("Template parsing error: unmatched '{{' and '}}' tokens.");
            }

            // Parse & render
            var scribanTemplate = Template.Parse(template);
            if (scribanTemplate.HasErrors)
            {
                var errors = string.Join(", ", scribanTemplate.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Template parsing error: {errors}");
            }

            try
            {
                // Render with the explicit TemplateContext so imported built-ins are available
                return scribanTemplate.Render(context);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Template rendering error: {ex.Message}", ex);
            }
        }

        #endregion
    }
}
