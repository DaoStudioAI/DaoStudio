using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces.Plugins;

namespace NamingTool.Return
{
    /// <summary>
    /// Builder class for creating CustomReturnResultTool instances with fluent API
    /// </summary>
    public class CustomReturnResultToolBuilder
    {
        private readonly List<CustomReturnParameter> _parameters = new();
        private string? _toolName;
        private string? _toolDescription;
        
        /// <summary>
        /// Sets the name of the tool
        /// </summary>
        /// <param name="name">The tool name</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder WithName(string name)
        {
            _toolName = name;
            return this;
        }
        
        /// <summary>
        /// Sets the description of the tool
        /// </summary>
        /// <param name="description">The tool description</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder WithDescription(string description)
        {
            _toolDescription = description;
            return this;
        }
        
        /// <summary>
        /// Adds a required parameter to the tool
        /// </summary>
        /// <param name="name">Parameter name (must be unique)</param>
        /// <param name="type">Parameter type</param>
        /// <param name="description">Parameter description</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder AddParameter(string name, Type type, string description)
        {
            return AddParameter(name, type, description, isRequired: true);
        }
        
        /// <summary>
        /// Adds a parameter to the tool
        /// </summary>
        /// <param name="name">Parameter name (must be unique)</param>
        /// <param name="type">Parameter type</param>
        /// <param name="description">Parameter description</param>
        /// <param name="isRequired">Whether the parameter is required</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder AddParameter(string name, Type type, string description, bool isRequired)
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
        /// Adds a required string parameter
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="description">Parameter description</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder AddStringParameter(string name, string description)
        {
            return AddParameter(name, typeof(string), description, isRequired: true);
        }
        
        /// <summary>
        /// Adds an optional string parameter
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="description">Parameter description</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder AddOptionalStringParameter(string name, string description)
        {
            return AddParameter(name, typeof(string), description, isRequired: false);
        }
        
        /// <summary>
        /// Adds a required integer parameter
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="description">Parameter description</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder AddIntParameter(string name, string description)
        {
            return AddParameter(name, typeof(int), description, isRequired: true);
        }
        
        /// <summary>
        /// Adds an optional integer parameter
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="description">Parameter description</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder AddOptionalIntParameter(string name, string description)
        {
            return AddParameter(name, typeof(int?), description, isRequired: false);
        }
        
        /// <summary>
        /// Adds a required boolean parameter
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="description">Parameter description</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder AddBoolParameter(string name, string description)
        {
            return AddParameter(name, typeof(bool), description, isRequired: true);
        }
        
        /// <summary>
        /// Adds an optional boolean parameter
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="description">Parameter description</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder AddOptionalBoolParameter(string name, string description)
        {
            return AddParameter(name, typeof(bool?), description, isRequired: false);
        }
        
        /// <summary>
        /// Adds a required double parameter
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="description">Parameter description</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder AddDoubleParameter(string name, string description)
        {
            return AddParameter(name, typeof(double), description, isRequired: true);
        }
        
        /// <summary>
        /// Adds an optional double parameter
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="description">Parameter description</param>
        /// <returns>This builder instance for chaining</returns>
        public CustomReturnResultToolBuilder AddOptionalDoubleParameter(string name, string description)
        {
            return AddParameter(name, typeof(double?), description, isRequired: false);
        }
        
        /// <summary>
        /// Builds the CustomReturnResultTool instance
        /// </summary>
        /// <param name="completionSource">The task completion source</param>
        /// <param name="sessionId">The session ID</param>
        /// <returns>A new CustomReturnResultTool instance</returns>
        public CustomReturnResultTool Build(TaskCompletionSource<ChildSessionResult> completionSource, long sessionId)
        {                
            return new CustomReturnResultTool(
                completionSource, 
                sessionId, 
                _parameters, 
                _toolName, 
                _toolDescription);
        }
        
        /// <summary>
        /// Gets the current list of parameters (for inspection)
        /// </summary>
        /// <returns>A read-only list of the current parameters</returns>
        public IReadOnlyList<CustomReturnParameter> GetParameters()
        {
            return _parameters.AsReadOnly();
        }
    }
    
    /// <summary>
    /// Helper class for creating and working with CustomReturnResultTool instances
    /// </summary>
    public static class CustomReturnResultToolHelper
    {
        /// <summary>
        /// Creates a new builder for CustomReturnResultTool
        /// </summary>
        /// <returns>A new builder instance</returns>
        public static CustomReturnResultToolBuilder CreateBuilder()
        {
            return new CustomReturnResultToolBuilder();
        }
        
        /// <summary>
        /// Creates a CustomReturnResultTool with common result parameters (success, message, data)
        /// </summary>
        /// <param name="completionSource">The task completion source</param>
        /// <param name="sessionId">The session ID</param>
        /// <param name="toolName">Optional tool name</param>
        /// <param name="toolDescription">Optional tool description</param>
        /// <returns>A CustomReturnResultTool with standard parameters</returns>
        public static CustomReturnResultTool CreateStandardResultTool(
            TaskCompletionSource<ChildSessionResult> completionSource, 
            long sessionId,
            string? toolName = null,
            string? toolDescription = null)
        {
            return CreateBuilder()
                .WithName(toolName ?? "set_result")
                .WithDescription(toolDescription ?? "Report back with the result after completion")
                .AddBoolParameter("success", "Whether the operation completed successfully")
                .AddOptionalStringParameter("message", "A message describing the result or any errors")
                .AddOptionalStringParameter("data", "Any additional data from the operation")
                .Build(completionSource, sessionId);
        }
        
        /// <summary>
        /// Creates a CustomReturnResultTool for task completion with status and details
        /// </summary>
        /// <param name="completionSource">The task completion source</param>
        /// <param name="sessionId">The session ID</param>
        /// <param name="toolName">Optional tool name</param>
        /// <param name="toolDescription">Optional tool description</param>
        /// <returns>A CustomReturnResultTool for task completion</returns>
        public static CustomReturnResultTool CreateTaskCompletionTool(
            TaskCompletionSource<ChildSessionResult> completionSource, 
            long sessionId,
            string? toolName = null,
            string? toolDescription = null)
        {
            return CreateBuilder()
                .WithName(toolName ?? "complete_task")
                .WithDescription(toolDescription ?? "Report task completion with status and details")
                .AddStringParameter("status", "The completion status (completed, failed, partial)")
                .AddOptionalStringParameter("summary", "A summary of what was accomplished")
                .AddOptionalStringParameter("details", "Detailed information about the task execution")
                .AddOptionalIntParameter("progress", "Progress percentage (0-100)")
                .Build(completionSource, sessionId);
        }
        
        /// <summary>
        /// Creates a function description that can be used with PlainAIFunction for the custom tool
        /// </summary>
        /// <param name="tool">The custom return result tool</param>
        /// <returns>A FunctionDescription for the tool</returns>
        public static FunctionDescription CreateFunctionDescription(CustomReturnResultTool tool)
        {
            var description = new FunctionDescription
            {
                Name = tool.ToolName,
                Description = tool.ToolDescription,
                Parameters = tool.Parameters.Select(p => new FunctionTypeMetadata
                {
                    Name = p.Name,
                    Description = p.Description,
                    ParameterType = p.Type,
                    IsRequired = p.IsRequired,
                    DefaultValue = null
                }).ToList()
            };
            
            return description;
        }
    }
}
