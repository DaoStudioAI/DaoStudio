using DaoStudio.Common;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Properties;
using Microsoft.Extensions.Logging;

namespace DaoStudio;

/// <summary>
/// Contains filter management logic for the Session class
/// </summary>
internal partial class Session : ISession
{
    // Private filter storage fields
    private List<IFilter> _preProcessingChainFilters = new List<IFilter>();
    private List<IFilter> _postProcessingChainFilters = new List<IFilter>();
    private List<IFilter> _preProcessingMandatoryFilters = new List<IFilter>();
    private List<IFilter> _postProcessingMandatoryFilters = new List<IFilter>();

    /// <summary>
    /// Adds a filter to the session
    /// </summary>
    /// <param name="filter">The filter to add</param>
    public void AddFilter(IFilter filter)
    {
        if (filter == null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        lock (this)
        {
            var targetList = GetFilterList(filter.Type, filter.ExecutionMode);
            if (!targetList.Contains(filter))
            {
                targetList.Add(filter);
            }
        }
    }

    /// <summary>
    /// Removes a filter from the session
    /// </summary>
    /// <param name="filter">The filter to remove</param>
    public void RemoveFilter(IFilter filter)
    {
        if (filter == null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        lock (this)
        {
            var targetList = GetFilterList(filter.Type, filter.ExecutionMode);
            targetList.Remove(filter);
        }
    }

    /// <summary>
    /// Clears filters based on type and/or mode
    /// </summary>
    /// <param name="type">Optional filter type to clear (null = all types)</param>
    /// <param name="mode">Optional execution mode to clear (null = all modes)</param>
    public void ClearFilters(FilterType? type = null, FilterExecutionMode? mode = null)
    {
        lock (this)
        {
            if (type == null && mode == null)
            {
                // Clear all filters
                _preProcessingChainFilters.Clear();
                _postProcessingChainFilters.Clear();
                _preProcessingMandatoryFilters.Clear();
                _postProcessingMandatoryFilters.Clear();
            }
            else if (type != null && mode != null)
            {
                // Clear specific type and mode
                GetFilterList(type.Value, mode.Value).Clear();
            }
            else if (type != null)
            {
                // Clear all filters of the specified type
                if (type.Value == FilterType.PreProcessing)
                {
                    _preProcessingChainFilters.Clear();
                    _preProcessingMandatoryFilters.Clear();
                }
                else
                {
                    _postProcessingChainFilters.Clear();
                    _postProcessingMandatoryFilters.Clear();
                }
            }
            else // mode != null
            {
                // Clear all filters of the specified mode
                if (mode!.Value == FilterExecutionMode.Chain)
                {
                    _preProcessingChainFilters.Clear();
                    _postProcessingChainFilters.Clear();
                }
                else
                {
                    _preProcessingMandatoryFilters.Clear();
                    _postProcessingMandatoryFilters.Clear();
                }
            }
        }
    }

    /// <summary>
    /// Gets a read-only list of filters for the specified type and mode
    /// </summary>
    /// <param name="type">The filter type</param>
    /// <param name="mode">The execution mode</param>
    /// <returns>Read-only list of filters</returns>
    public IReadOnlyList<IFilter> GetFilters(FilterType type, FilterExecutionMode mode)
    {
        lock (this)
        {
            return GetFilterList(type, mode).ToList().AsReadOnly();
        }
    }

    private async Task RunFiltersAsync(FilterType filterType, List<IMessage> messages, CancellationToken token)
    {
        var phase = filterType == FilterType.PreProcessing ? "Pre-processing" : "Post-processing";

        var mandatoryResult = await ExecuteMandatoryFiltersAsync(filterType, messages, _availablePlugin, token).ConfigureAwait(false);
        if (!mandatoryResult)
        {
            logger.LogInformation("{Phase} mandatory filter aborted message processing for session {SessionId}", phase, Id);
            throw new UIException(Resources.Error_FilterAborted);
        }

        var chainResult = await ExecuteFilterChainAsync(filterType, messages, _availablePlugin, token).ConfigureAwait(false);
        if (!chainResult)
        {
            logger.LogInformation("{Phase} filter chain aborted message processing for session {SessionId}", phase, Id);
            throw new UIException(Resources.Error_FilterAborted);
        }
    }



    /// <summary>
    /// Executes the filter chain for the specified type
    /// </summary>
    /// <param name="filterType">The type of filters to execute</param>
    /// <param name="messages">The message history</param>
    /// <param name="tools">Available tools for function calling</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True to continue processing, False to abort</returns>
    internal async Task<bool> ExecuteFilterChainAsync(
        FilterType filterType,
        List<IMessage> messages,
        Dictionary<string, List<FunctionWithDescription>>? tools,
        CancellationToken cancellationToken)
    {
        // Create local copy with lock
        List<IFilter> filters;
        lock (this)
        {
            filters = filterType == FilterType.PreProcessing
                ? _preProcessingChainFilters.ToList()
                : _postProcessingChainFilters.ToList();
        }

        if (filters.Count == 0)
            return true;

        // Start the chain execution recursively
        return await ExecuteChainRecursively(filters, 0, messages, tools, cancellationToken);
    }

    /// <summary>
    /// Recursively executes filter chain
    /// </summary>
    private async Task<bool> ExecuteChainRecursively(
        List<IFilter> filters,
        int currentIndex,
        List<IMessage> messages,
        Dictionary<string, List<FunctionWithDescription>>? tools,
        CancellationToken cancellationToken)
    {
        if (currentIndex >= filters.Count)
            return true;

        var currentFilter = filters[currentIndex];
        var nextFilter = currentIndex + 1 < filters.Count ? filters[currentIndex + 1] : null;

        // Call the current filter
        // Note: The filter is responsible for calling next.OnMessage() if it wants to continue the chain
        // However, we need to provide a wrapper that will continue our recursion
        
        // Create a wrapper filter that continues the chain
        IFilter? nextWrapper = null;
        if (nextFilter != null)
        {
            nextWrapper = new ChainContinuationFilter(
                nextFilter,
                async (msgs, tls, sess, nxt, ct) => 
                    await ExecuteChainRecursively(filters, currentIndex + 1, msgs, tls, ct));
        }

        return await currentFilter.OnMessage(messages, tools, this, nextWrapper, cancellationToken);
    }

    /// <summary>
    /// Helper class to wrap filter chain continuation
    /// </summary>
    private class ChainContinuationFilter : IFilter
    {
        private readonly IFilter _actualFilter;
        private readonly Func<List<IMessage>, Dictionary<string, List<FunctionWithDescription>>?, ISession, IFilter?, CancellationToken, Task<bool>> _continuationFunc;

        public ChainContinuationFilter(
            IFilter actualFilter,
            Func<List<IMessage>, Dictionary<string, List<FunctionWithDescription>>?, ISession, IFilter?, CancellationToken, Task<bool>> continuationFunc)
        {
            _actualFilter = actualFilter;
            _continuationFunc = continuationFunc;
        }

        public FilterType Type => _actualFilter.Type;
        public FilterExecutionMode ExecutionMode => _actualFilter.ExecutionMode;

        public Task<bool> OnMessage(
            List<IMessage> messages,
            Dictionary<string, List<FunctionWithDescription>>? tools,
            ISession session,
            IFilter? next,
            CancellationToken cancellationToken = default)
        {
            return _continuationFunc(messages, tools, session, next, cancellationToken);
        }
    }

    /// <summary>
    /// Executes mandatory filters for the specified type
    /// </summary>
    /// <param name="filterType">The type of filters to execute</param>
    /// <param name="messages">The message history</param>
    /// <param name="tools">Available tools for function calling</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if all filters pass, False if any filter aborts</returns>
    internal async Task<bool> ExecuteMandatoryFiltersAsync(
        FilterType filterType,
        List<IMessage> messages,
        Dictionary<string, List<FunctionWithDescription>>? tools,
        CancellationToken cancellationToken)
    {
        // Create local copy with lock
        List<IFilter> filters;
        lock (this)
        {
            filters = filterType == FilterType.PreProcessing
                ? _preProcessingMandatoryFilters.ToList()
                : _postProcessingMandatoryFilters.ToList();
        }

        // Execute each mandatory filter independently
        foreach (var filter in filters)
        {
            bool result = await filter.OnMessage(messages, tools, this, null, cancellationToken);
            if (!result)
            {
                logger.LogWarning("Mandatory filter {FilterType} aborted processing", filter.GetType().Name);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the appropriate filter list based on type and mode
    /// </summary>
    /// <param name="type">Filter type</param>
    /// <param name="mode">Execution mode</param>
    /// <returns>The filter list</returns>
    private List<IFilter> GetFilterList(FilterType type, FilterExecutionMode mode)
    {
        if (type == FilterType.PreProcessing)
        {
            return mode == FilterExecutionMode.Chain
                ? _preProcessingChainFilters
                : _preProcessingMandatoryFilters;
        }
        else
        {
            return mode == FilterExecutionMode.Chain
                ? _postProcessingChainFilters
                : _postProcessingMandatoryFilters;
        }
    }
}
