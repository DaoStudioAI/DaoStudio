using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;

namespace DaoStudio.Filters
{
    /// <summary>
    /// Mandatory filter that propagates filter execution to parent session
    /// </summary>
    internal class ParentSessionMandatoryFilter : IFilter
    {
        private readonly ISession _parentSession;
        private readonly FilterType _filterType;

        public ParentSessionMandatoryFilter(ISession parentSession, FilterType filterType)
        {
            _parentSession = parentSession ?? throw new ArgumentNullException(nameof(parentSession));
            _filterType = filterType;
        }

        public FilterType Type => _filterType;

        public FilterExecutionMode ExecutionMode => FilterExecutionMode.Mandatory;

        public async Task<bool> OnMessage(
            List<IMessage> messages,
            Dictionary<string, List<FunctionWithDescription>>? tools,
            ISession session,
            IFilter? next,
            CancellationToken cancellationToken = default)
        {
            // Execute parent's chain filters
            bool chainResult = await ((Session)_parentSession).ExecuteFilterChainAsync(
                _filterType, messages, tools, cancellationToken);
            if (!chainResult)
                return false;

            // Execute parent's mandatory filters
            bool mandatoryResult = await ((Session)_parentSession).ExecuteMandatoryFiltersAsync(
                _filterType, messages, tools, cancellationToken);
            if (!mandatoryResult)
                return false;

            return true;
        }
    }
}
