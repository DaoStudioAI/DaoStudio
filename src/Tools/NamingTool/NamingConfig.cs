using System.Collections.Generic;
using DaoStudio.Common.Plugins;
using Naming.ParallelExecution;
using NamingTool.Properties;

namespace Naming
{
    /// <summary>
    /// Supported parameter types for LLM JSON tool call parameters
    /// </summary>
    public enum ParameterType
    {
        /// <summary>
        /// String value type
        /// </summary>
        String,
        
        /// <summary>
        /// Numeric value type (int, double, decimal, etc.)
        /// </summary>
        Number,
        
        /// <summary>
        /// Boolean value type
        /// </summary>
        Bool,
        
        /// <summary>
        /// Object/dictionary type with nested properties
        /// </summary>
        Object,
        
        /// <summary>
        /// Array type containing elements of a specified type
        /// </summary>
        Array
    }

    /// <summary>
    /// Defines the behavior when a child session does not call the return tool
    /// </summary>
    public enum DanglingBehavior
    {
        /// <summary>
        /// Send urging messages (up to 3 attempts) then throw exception (current behavior)
        /// </summary>
        Urge = 0,
        
        /// <summary>
        /// Report error message to parent session and return immediately with failure result
        /// </summary>
        ReportError = 1,
        
        /// <summary>
        /// Pause execution and wait for manual user intervention
        /// </summary>
        Pause = 2
    }

    /// <summary>
    /// Defines the behavior when the error reporting tool is invoked
    /// </summary>
    public enum ErrorReportingBehavior
    {
        /// <summary>
        /// Pause execution and wait for manual user intervention
        /// </summary>
        Pause = 0,

        /// <summary>
        /// Report error message to parent session and return immediately with failure result
        /// </summary>
        ReportError = 1
    }

    /// <summary>
    /// Configuration for the error reporting tool functionality
    /// </summary>
    public class ErrorReportingConfig
    {
        /// <summary>
        /// Description of the error reporting tool
        /// </summary>
        public string ToolDescription { get; set; } = "Report an error or issue encountered during task execution";

        /// <summary>
        /// Configuration for the error reporting tool parameters
        /// If empty, default parameters are applied (error_message, error_type)
        /// </summary>
        public List<ParameterConfig> Parameters { get; set; } = new List<ParameterConfig>();

        /// <summary>
        /// Determines the behavior when the error reporting tool is called
        /// </summary>
        public ErrorReportingBehavior Behavior { get; set; } = ErrorReportingBehavior.Pause;

        /// <summary>
        /// Custom error message returned to the parent session when Behavior is ReportError
        /// </summary>
        public string CustomErrorMessageToParent { get; set; } = string.Empty;
    }

    /// <summary>
    /// Serializable representation of a person for configuration storage
    /// </summary>
    public class ConfigPerson
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// Indicates that this item represents the sentinel option to use the current session's person
        /// </summary>
        public bool UseCurrentSession { get; set; } = false;
    }

    /// <summary>
    /// Configuration for a return parameter that can be used by the CustomReturnResultTool
    /// </summary>
    public class ParameterConfig
    {
        /// <summary>
        /// The name of the parameter (must be unique)
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Description of what this parameter represents
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether this parameter is required
        /// </summary>
        public bool IsRequired { get; set; } = true;
        
        /// <summary>
        /// The type of this parameter
        /// </summary>
        public ParameterType Type { get; set; } = ParameterType.String;
        
        /// <summary>
        /// For Array types, defines the complete configuration of elements in the array
        /// This allows arrays to contain complex nested structures including objects and nested arrays
        /// </summary>
        public ParameterConfig? ArrayElementConfig { get; set; }
        
        /// <summary>
        /// For Object types, defines the properties as a list of nested ParameterConfig
        /// Each ParameterConfig already has the Name field which serves as the property name
        /// </summary>
        public List<ParameterConfig>? ObjectProperties { get; set; }
        
        /// <summary>
        /// Helper property to check if this parameter is an array type
        /// </summary>
        public bool IsArray => Type == ParameterType.Array;
        
        /// <summary>
        /// Helper property to check if this parameter is an object type
        /// </summary>
        public bool IsObject => Type == ParameterType.Object;
        
        /// <summary>
        /// Helper property to check if this parameter is a primitive type (String, Number, Bool)
        /// </summary>
        public bool IsPrimitive => Type == ParameterType.String || Type == ParameterType.Number || Type == ParameterType.Bool;
    }

    /// <summary>
    /// Configuration data model for the Naming plugin.
    /// 
    /// This configuration now supports dynamic function definition:
    /// - FunctionName: Customizable name for the Naming function (default: "create_subtask")
    /// - FunctionDescription: Customizable description for the function
    /// - InputParameters: Customizable parameters for the function (similar to ReturnParameters)
    /// - ReturnParameters: Customizable parameters for the return tool used in child sessions
    /// </summary>
    public class NamingConfig
    {
        public NamingConfig()
        {
            UrgingMessage = Resources.Message_FinalizeTaskReminder;
        }
        /// <summary>
        /// Configuration version for future compatibility
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// The executive person that can be used for help sessions
        /// </summary>
        public ConfigPerson? ExecutivePerson { get; set; }

        /// <summary>
        /// Maximum recursion level constraint (always enabled)
        /// Default of 1 level allows reasonable task subdivision without infinite loops
        /// </summary>
        public int MaxRecursionLevel { get; set; } = 1;

        /// <summary>
        /// Name of the Naming function (defaults to "create_subtask")
        /// </summary>
        public string FunctionName { get; set; } = "create_subtask";

        /// <summary>
        /// Description of the Naming function
        /// </summary>
        public string FunctionDescription { get; set; } = "Arbitrarily redefining a concept and acting on the new definition";

        /// <summary>
        /// Configuration for the Naming function parameters
        /// If empty, uses default parameters (subtask, background, problemScope, personName)
        /// </summary>
        public List<ParameterConfig> InputParameters { get; set; } = new List<ParameterConfig>();

        /// <summary>
        /// Configuration for the return tool parameters
        /// If empty, uses default parameters (success, message, data)
        /// </summary>
        public List<ParameterConfig> ReturnParameters { get; set; } = new List<ParameterConfig>();

        /// <summary>
        /// Name of the return tool (defaults to "set_result")
        /// </summary>
        public string ReturnToolName { get; set; } = "set_result";

        /// <summary>
        /// Description of the return tool
        /// </summary>
        public string ReturnToolDescription { get; set; } = "Report back with the result after completion";

        /// <summary>
        /// Message to use when urging for child session completion
        /// </summary>
        public string UrgingMessage { get; set; } = string.Empty;


        /// <summary>
        /// Message to start the session
        /// </summary>
        public string PromptMessage { get; set; } = string.Empty;


        /// <summary>
        /// Parallel execution configuration
        /// If null, parallel execution is disabled and uses current single-session behavior
        /// </summary>
        public ParallelExecutionConfig? ParallelConfig { get; set; }

        /// <summary>
        /// Indicates whether to show the configuration dialog in simple mode (NamingConfigWindow.axaml) 
        /// or advanced mode (AdvancedNamingConfigWindow.axaml).
        /// Default is true for simple mode.
        /// </summary>
        public bool UseSimpleConfigMode { get; set; } = true;

        /// <summary>
        /// Determines the behavior when a child session doesn't call the return tool
        /// Default is Urge
        /// </summary>
        public DanglingBehavior DanglingBehavior { get; set; } = DanglingBehavior.Urge;
        
        /// <summary>
        /// Plain error message to report to parent when DanglingBehavior is ReportError
        /// If empty, uses a default message from resources.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Configuration for the optional error reporting tool
        /// If null, the tool is not registered in child sessions.
        /// </summary>
        public ErrorReportingConfig? ErrorReportingConfig { get; set; }

        /// <summary>
        /// Name of the error reporting tool (defaults to "report_error")
        /// </summary>
        public string ErrorReportingToolName { get; set; } = "report_error";

    }
}