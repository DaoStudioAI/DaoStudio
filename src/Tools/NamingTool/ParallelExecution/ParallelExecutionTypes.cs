using System;
using System.Collections.Generic;

namespace Naming.ParallelExecution
{
    /// <summary>
    /// Types of parallel execution modes
    /// </summary>
    public enum ParallelExecutionType
    {
        /// <summary>
        /// No parallel execution (current behavior)
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Use POCO parameters from requestData as parallel source
        /// </summary>
        ParameterBased = 1,
        
        /// <summary>
        /// Use a specific array/list parameter as parallel source
        /// </summary>
        ListBased = 2,
        
        /// <summary>
        /// Use external string list provided by user
        /// </summary>
        ExternalList = 3
    }

    /// <summary>
    /// Result aggregation strategies for parallel execution
    /// </summary>
    public enum ParallelResultStrategy
    {
        /// <summary>
        /// Stream individual results as they complete using IHostSession.SendMessageAsync
        /// </summary>
        StreamIndividual = 0,
        
        /// <summary>
        /// Wait for all sessions to complete and return combined results
        /// </summary>
        WaitForAll = 1,
        
        /// <summary>
        /// Return first result and cancel remaining sessions
        /// </summary>
        FirstResultWins = 2
    }

    /// <summary>
    /// Configuration for parallel execution behavior
    /// </summary>
    public class ParallelExecutionConfig
    {
        /// <summary>
        /// Type of parallel execution
        /// </summary>
        public ParallelExecutionType ExecutionType { get; set; } = ParallelExecutionType.None;
        
        /// <summary>
        /// Maximum number of concurrent sessions (like ParallelOptions.MaxDegreeOfParallelism)
        /// </summary>
        public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
        
        /// <summary>
        /// Result aggregation strategy
        /// </summary>
        public ParallelResultStrategy ResultStrategy { get; set; } = ParallelResultStrategy.WaitForAll;
        
        /// <summary>
        /// For ListBased execution: name of the parameter containing the list
        /// </summary>
        public string? ListParameterName { get; set; }

        
        /// <summary>
        /// For ExternalList execution: the user-provided string list
        /// </summary>
        public List<string> ExternalList { get; set; } = new List<string>();
        
        /// <summary>
        /// Parameters to exclude from ParameterBased parallel execution
        /// Automatically includes non-POCO types like DasSession
        /// </summary>
        public List<string> ExcludedParameters { get; set; } = new List<string>();
        
        /// <summary>
        /// Timeout for individual parallel sessions in milliseconds
        /// Default is 30 minutes per session
        /// </summary>
        public int SessionTimeoutMs { get; set; } = 30 * 60 * 1000;
    }

    /// <summary>
    /// Result of parallel execution containing multiple child session results
    /// </summary>
    public class ParallelExecutionResult
    {
        /// <summary>
        /// Overall success status - true if at least one session succeeded
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Overall error message if execution failed
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Individual results from each parallel session
        /// </summary>
        public List<ParallelSessionResult> Results { get; set; } = new List<ParallelSessionResult>();
        
        /// <summary>
        /// The strategy used for result aggregation
        /// </summary>
        public ParallelResultStrategy Strategy { get; set; }
        
        /// <summary>
        /// Total number of parallel sessions that were started
        /// </summary>
        public int TotalSessions { get; set; }
        
        /// <summary>
        /// Number of sessions that completed successfully
        /// </summary>
        public int CompletedSessions { get; set; }
        
        /// <summary>
        /// Number of sessions that failed
        /// </summary>
        public int FailedSessions { get; set; }
        
        /// <summary>
        /// Total execution time for all parallel sessions
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }
        
        /// <summary>
        /// When the execution started
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// When the execution completed
        /// </summary>
        public DateTime EndTime { get; set; }
    }

    /// <summary>
    /// Result from a single parallel session execution
    /// </summary>
    public class ParallelSessionResult
    {
        /// <summary>
        /// The parameter name being processed in this session
        /// </summary>
        public string? ParameterName { get; set; }
        
        /// <summary>
        /// The parameter value being processed in this session
        /// </summary>
        public object? ParameterValue { get; set; }
        
        /// <summary>
        /// The input parameters used for this session
        /// </summary>
        public Dictionary<string, object?> InputParameters { get; set; } = new Dictionary<string, object?>();
        
        /// <summary>
        /// The result from the child session
        /// </summary>
        public DaoStudio.Common.Plugins.ChildSessionResult? ChildResult { get; set; }
        
        /// <summary>
        /// When this session started execution
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// When this session completed execution
        /// </summary>
        public DateTime EndTime { get; set; }
        
        /// <summary>
        /// Duration of this session execution
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;
        
        /// <summary>
        /// Whether this session completed successfully
        /// </summary>
        public bool IsSuccess => ChildResult?.Success == true;
        
        /// <summary>
        /// Exception that occurred during session execution, if any
        /// </summary>
        public Exception? Exception { get; set; }
    }
}