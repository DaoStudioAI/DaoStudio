using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;

namespace DaoStudio.Filters
{
    /// <summary>
    /// Post-processing chain filter that pauses execution after tool call responses
    /// to allow stepping through messages one at a time for debugging purposes.
    /// </summary>
    public class StepDebuggingFilter : IFilter
    {
        private readonly object _lock = new object();
        private bool _isEnabled = false;
        private TaskCompletionSource<bool>? _waitingTcs = null;

        /// <summary>
        /// Event raised when the waiting state changes (entering or exiting wait state)
        /// </summary>
        public event EventHandler<bool>? WaitingStateChanged;

        /// <summary>
        /// Gets the filter type (PostProcessing)
        /// </summary>
        public FilterType Type => FilterType.PostProcessing;

        /// <summary>
        /// Gets the execution mode (Chain)
        /// </summary>
        public FilterExecutionMode ExecutionMode => FilterExecutionMode.Chain;

        /// <summary>
        /// Gets whether step debugging is currently enabled
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                lock (_lock)
                {
                    return _isEnabled;
                }
            }
        }

        /// <summary>
        /// Enables step debugging mode
        /// </summary>
        public void Enable()
        {
            lock (_lock)
            {
                _isEnabled = true;
            }
        }

        /// <summary>
        /// Disables step debugging mode and releases any waiting tasks
        /// </summary>
        public void Disable()
        {
            lock (_lock)
            {
                _isEnabled = false;
                
                // Release any waiting TaskCompletionSource
                if (_waitingTcs != null)
                {
                    _waitingTcs.TrySetResult(true);
                    _waitingTcs = null;
                    
                    // Notify that we're no longer waiting
                    RaiseWaitingStateChanged(false);
                }
            }
        }

        /// <summary>
        /// Advances execution by one step, releasing the current wait
        /// </summary>
        public void StepNext()
        {
            lock (_lock)
            {
                if (_waitingTcs != null)
                {
                    _waitingTcs.TrySetResult(true);
                    _waitingTcs = null;
                    
                    // Notify that we're no longer waiting
                    RaiseWaitingStateChanged(false);
                }
            }
        }

        /// <summary>
        /// Processes messages through the filter chain, pausing after tool call responses when enabled
        /// </summary>
        /// <param name="messages">The message history</param>
        /// <param name="tools">Available tools for function calling</param>
        /// <param name="session">The current session</param>
        /// <param name="next">The next filter in the chain</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True to continue processing, False to abort the pipeline</returns>
        public async Task<bool> OnMessage(
            List<IMessage> messages,
            Dictionary<string, List<FunctionWithDescription>>? tools,
            ISession session,
            IFilter? next,
            CancellationToken cancellationToken = default)
        {
            // Check if we should pause before processing this message
            bool shouldPause = false;
            TaskCompletionSource<bool>? currentTcs = null;

            lock (_lock)
            {
                // Only pause if enabled and we're not already waiting
                if (_isEnabled && _waitingTcs == null)
                {
                    // Check if the last message contains tool calls
                    // We pause after processing tool call responses (assistant messages with tool calls)
                    if (messages.Count > 0)
                    {
                        var lastMessage = messages[messages.Count - 1];
                        
                        // Check if this is an assistant message with tool calls
                        if (lastMessage.Role == MessageRole.Assistant && 
                            lastMessage.BinaryContents != null && 
                            lastMessage.BinaryContents.Any(bc => bc.Type == MsgBinaryDataType.ToolCall))
                        {
                            // Create a TaskCompletionSource to pause execution
                            _waitingTcs = new TaskCompletionSource<bool>();
                            currentTcs = _waitingTcs;
                            shouldPause = true;
                        }
                    }
                }
            }

            // Raise waiting state changed event if we're about to pause
            if (shouldPause && currentTcs != null)
            {
                RaiseWaitingStateChanged(true);
            }

            // Wait if we should pause, with cancellation support
            if (shouldPause && currentTcs != null)
            {
                using (cancellationToken.Register(() => currentTcs.TrySetCanceled()))
                {
                    try
                    {
                        await currentTcs.Task;
                    }
                    catch (OperationCanceledException)
                    {
                        // Clean up on cancellation
                        lock (_lock)
                        {
                            if (_waitingTcs == currentTcs)
                            {
                                _waitingTcs = null;
                                RaiseWaitingStateChanged(false);
                            }
                        }
                        
                        return false;
                    }
                }
            }

            // Continue with the filter chain
            if (next != null)
            {
                return await next.OnMessage(messages, tools, session, null, cancellationToken);
            }

            return true;
        }

        /// <summary>
        /// Raises the WaitingStateChanged event on a background thread
        /// </summary>
        /// <param name="isWaiting">True if entering wait state, false if exiting</param>
        private void RaiseWaitingStateChanged(bool isWaiting)
        {
            // Fire event asynchronously to avoid blocking
            Task.Run(() =>
            {
                try
                {
                    WaitingStateChanged?.Invoke(this, isWaiting);
                }
                catch
                {
                    // Ignore exceptions from event handlers
                }
            });
        }
    }
}
