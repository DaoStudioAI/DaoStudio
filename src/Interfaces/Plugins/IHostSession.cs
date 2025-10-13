using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaoStudio.Interfaces.Plugins
{
    /// <summary>
    /// Minimal interface for session information used by Tools.
    /// Provides only the essential properties and methods needed by plugin tools.
    /// </summary>
    public interface IHostSession : IDisposable
    {
        /// <summary>
        /// Gets the unique identifier of the session
        /// </summary>
        long Id { get; }

        /// <summary>
        /// Gets the parent session ID if this is a child session
        /// </summary>
        long? ParentSessionId { get; }

        /// <summary>
        /// Gets the current cancellation token source.
        /// Tools code accesses both .Cancel() and .Token
        /// </summary>
        CancellationTokenSource? CurrentCancellationToken { get; }

        /// <summary>
        /// Gets or sets the tool execution mode for chat interactions
        /// </summary>
        ToolExecutionMode ToolExecutionMode { get; set; }

        /// <summary>
        /// Sends a message to the host session
        /// </summary>
        /// <param name="msgType">The type of message to send</param>
        /// <param name="message">The message content</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SendMessageAsync(HostSessMsgType msgType, string message);

        /// <summary>
        /// Gets the custom tool registry for this session.
        /// Used by NamingTool for custom tool management.
        /// </summary>
        /// <returns>Dictionary of tool modules and their functions, or null if not set</returns>
        Dictionary<string, List<FunctionWithDescription>>? GetTools();

        /// <summary>
        /// Sets the custom tool registry for this session.
        /// Used by NamingTool for custom tool management.
        /// </summary>
        /// <param name="tools">Dictionary of tool modules and their functions</param>
        void SetTools(Dictionary<string, List<FunctionWithDescription>> tools);

        /// <summary>
        /// Gets the persons associated with this session
        /// </summary>
        /// <returns>List of persons in this session</returns>
        Task<List<IHostPerson>?> GetPersonsAsync();

    }
}
