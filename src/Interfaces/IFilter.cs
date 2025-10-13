using DaoStudio.Interfaces.Plugins;

namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Defines the filter type for message processing pipeline
    /// </summary>
    public enum FilterType
    {
        /// <summary>
        /// Filter executes before engine call
        /// </summary>
        PreProcessing = 0,

        /// <summary>
        /// Filter executes after engine call
        /// </summary>
        PostProcessing = 1
    }

    /// <summary>
    /// Defines whether a filter is mandatory or part of the chain
    /// </summary>
    public enum FilterExecutionMode
    {
        /// <summary>
        /// Filter is part of the chain-of-responsibility pattern
        /// </summary>
        Chain = 0,

        /// <summary>
        /// Filter executes independently, not part of the chain
        /// </summary>
        Mandatory = 1
    }

    /// <summary>
    /// Interface for message filters in the Session processing pipeline
    /// </summary>
    public interface IFilter
    {
        /// <summary>
        /// Gets the type of this filter (PreProcessing or PostProcessing)
        /// </summary>
        FilterType Type { get; }

        /// <summary>
        /// Gets the execution mode of this filter (Chain or Mandatory)
        /// </summary>
        FilterExecutionMode ExecutionMode { get; }

        /// <summary>
        /// Process messages through the filter chain
        /// </summary>
        /// <param name="messages">The message history</param>
        /// <param name="tools">Available tools for function calling</param>
        /// <param name="session">The current session</param>
        /// <param name="next">The next filter in the chain (null if this is the last filter or if ExecutionMode is Mandatory)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True to continue processing, False to abort the pipeline</returns>
        Task<bool> OnMessage(
            List<IMessage> messages,
            Dictionary<string, List<FunctionWithDescription>>? tools,
            ISession session,
            IFilter? next,
            CancellationToken cancellationToken = default);
    }
}
