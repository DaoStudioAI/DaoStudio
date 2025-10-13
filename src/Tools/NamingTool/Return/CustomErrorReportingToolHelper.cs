using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.Common.Plugins;

namespace NamingTool.Return
{
    /// <summary>
    /// Builder for creating instances of <see cref="CustomErrorReportingTool"/>.
    /// </summary>
    public class CustomErrorReportingToolBuilder
    {
        private readonly List<CustomReturnParameter> _parameters = new();
        private string? _toolName;
        private string? _toolDescription;

        /// <summary>
        /// Sets the name of the tool.
        /// </summary>
        public CustomErrorReportingToolBuilder WithName(string name)
        {
            _toolName = name;
            return this;
        }

        /// <summary>
        /// Sets the description of the tool.
        /// </summary>
        public CustomErrorReportingToolBuilder WithDescription(string description)
        {
            _toolDescription = description;
            return this;
        }

        /// <summary>
        /// Adds a parameter definition.
        /// </summary>
        public CustomErrorReportingToolBuilder AddParameter(string name, Type type, string description, bool isRequired)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Parameter name cannot be null or whitespace", nameof(name));

            if (_parameters.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Parameter with name '{name}' already exists", nameof(name));

            _parameters.Add(new CustomReturnParameter
            {
                Name = name,
                Type = type ?? throw new ArgumentNullException(nameof(type)),
                Description = description ?? throw new ArgumentNullException(nameof(description)),
                IsRequired = isRequired
            });

            return this;
        }

        /// <summary>
        /// Convenience helper for required string parameters.
        /// </summary>
        public CustomErrorReportingToolBuilder AddStringParameter(string name, string description)
        {
            return AddParameter(name, typeof(string), description, true);
        }

        /// <summary>
        /// Convenience helper for optional string parameters.
        /// </summary>
        public CustomErrorReportingToolBuilder AddOptionalStringParameter(string name, string description)
        {
            return AddParameter(name, typeof(string), description, false);
        }

        /// <summary>
        /// Builds the error reporting tool instance.
        /// </summary>
        public CustomErrorReportingTool Build(TaskCompletionSource<ChildSessionResult> completionSource, long sessionId)
        {
            return new CustomErrorReportingTool(
                completionSource,
                sessionId,
                _parameters,
                _toolName,
                _toolDescription);
        }

        /// <summary>
        /// Returns the configured parameters.
        /// </summary>
        public IReadOnlyList<CustomReturnParameter> GetParameters() => _parameters.AsReadOnly();
    }

    /// <summary>
    /// Helper utilities for working with <see cref="CustomErrorReportingTool"/>.
    /// </summary>
    public static class CustomErrorReportingToolHelper
    {
        /// <summary>
        /// Creates a new builder.
        /// </summary>
        public static CustomErrorReportingToolBuilder CreateBuilder()
        {
            return new CustomErrorReportingToolBuilder();
        }

        /// <summary>
        /// Creates a tool with standard error reporting parameters (error_message, error_type).
        /// </summary>
        public static CustomErrorReportingTool CreateStandardErrorTool(
            TaskCompletionSource<ChildSessionResult> completionSource,
            long sessionId,
            string? toolName = null,
            string? toolDescription = null)
        {
            return CreateBuilder()
                .WithName(toolName ?? "report_error")
                .WithDescription(toolDescription ?? "Report an error or issue encountered during task execution")
                .AddStringParameter("error_message", "Human readable description of the issue")
                .AddOptionalStringParameter("error_type", "Optional classification or category for the error")
                .Build(completionSource, sessionId);
        }
    }
}
