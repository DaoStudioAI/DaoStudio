using System;
using DaoStudio.Interfaces.Plugins;

namespace DaoStudio.Interfaces
{
    //tokens used by the current chat round
    public class UsageDetails
    {
        public long? TotalTokens { get; set; }
        public long? InputTokens { get; set; }
        public long? OutputTokens { get; set; }
        public Dictionary<string, string>? AdditionalProperties { get; set; }
    }
    public enum HostSessMsgType
    {
        InfoForUserOnly = 1,
        StatusUpdate = 2,//Send to LLM until next user message, delay sending
        Message = 3,//Send to LLM immediately, but not break current sending message
    }

    /// <summary>
    /// Defines the tool execution mode for host sessions
    /// </summary>
    public enum ToolExecutionMode
    {
        /// <summary>
        /// Automatic tool mode - the AI decides when to use tools
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Require any tool - the AI must use at least one tool
        /// </summary>
        RequireAny = 1,

        /// <summary>
        /// No tools allowed - the AI cannot use any tools
        /// </summary>
        None = 2
    }
    /// <summary>
    /// Represents the current streaming/processing status of a session
    /// </summary>
    public enum SessionStatus
    {
        /// <summary>
        /// Session is idle and not processing any messages
        /// </summary>
        Idle,

        /// <summary>
        /// Session is currently sending/streaming messages
        /// </summary>
        Sending
    }

    /// <summary>
    /// Describes the type of change for a message event
    /// </summary>
    public enum MessageChangeType
    {
        /// <summary>
        /// A new message was created
        /// </summary>
        New = 0,

        /// <summary>
        /// An existing message was updated
        /// </summary>
        Updated = 1,

        Finished = 2
    }


    /// <summary>
    /// Represents a property change notification
    /// </summary>
    public readonly struct PropertyChangeNotification
    {
        /// <summary>
        /// Initializes a new instance of the PropertyChangeNotification struct
        /// </summary>
        /// <param name="propertyName">The name of the property that changed</param>
        public PropertyChangeNotification(string propertyName)
        {
            PropertyName = propertyName;
        }

        /// <summary>
        /// Gets the name of the property that changed
        /// </summary>
        public string PropertyName { get; }
    }


    public interface ISession : IDisposable
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
        /// Gets or sets the title of the session
        /// </summary>
        string Title { get; set; }

        /// <summary>
        /// Gets or sets the description of the session
        /// </summary>
        string Description { get; set; }

        public DateTime CreatedAt { get; } 
        public DateTime LastModified { get; } 

        /// <summary>
        /// Gets or sets the tool execution mode for chat interactions
        /// </summary>
        ToolExecutionMode ToolExecutionMode { get; set; }
        CancellationTokenSource? CurrentCancellationToken { get; }



        #region Session management
        event EventHandler<ISession>? SubsessionCreated;

        event EventHandler<PropertyChangeNotification>? PropertyChanged;

        SessionStatus SessionStatus { get; }


        Task UpdateSessionLastModifiedAsync();
        void FireSubsessionCreated(ISession subsession);
        #endregion

        #region tool calls


        /// <summary>
        /// Gets the list of enabled tools for the current person based on their configuration
        /// </summary>
        Task<List<ITool>> GetEnabledPersonToolsAsync();
        void SetTools(Dictionary<string, List<FunctionWithDescription>> moduleFunctions);
        Dictionary<string, List<FunctionWithDescription>>? GetTools();
        #endregion

        #region token statistics
        long TotalTokenCount { get; }
        long InputTokenCount { get; }
        long OutputTokenCount { get; }
        Dictionary<string, string>? AdditionalTokenProperties { get; }
        event EventHandler<UsageDetails>? UsageDetailsReceived;
        #endregion

        #region person
        IPerson CurrentPerson { get; }
        Task UpdatePersonAsync(IPerson person);
        Task<List<IPerson>> GetPersonsAsync();//current session's people
        #endregion

        #region messages
        event EventHandler<MessageChangedEventArgs>? OnMessageChanged;

        /// <summary>
        /// Sends a message to the LLM and streams the response with real-time notifications
        /// </summary>
        /// <param name="userMessage">The message text from the user</param>
        /// <returns>The final response message from the LLM</returns>
        Task<IMessage> SendMessageAsync(string userMessage);
        Task FireMessageChangedAsync(IMessage message, MessageChangeType change);

        #endregion

        #region filters
        /// <summary>
        /// Adds a filter to the session
        /// </summary>
        /// <param name="filter">The filter to add</param>
        void AddFilter(IFilter filter);

        /// <summary>
        /// Removes a filter from the session
        /// </summary>
        /// <param name="filter">The filter to remove</param>
        void RemoveFilter(IFilter filter);

        /// <summary>
        /// Clears filters based on type and/or mode
        /// </summary>
        /// <param name="type">Optional filter type to clear (null = all types)</param>
        /// <param name="mode">Optional execution mode to clear (null = all modes)</param>
        void ClearFilters(FilterType? type = null, FilterExecutionMode? mode = null);

        /// <summary>
        /// Gets a read-only list of filters for the specified type and mode
        /// </summary>
        /// <param name="type">The filter type</param>
        /// <param name="mode">The execution mode</param>
        /// <returns>Read-only list of filters</returns>
        IReadOnlyList<IFilter> GetFilters(FilterType type, FilterExecutionMode mode);
        #endregion

        //UpdateToolList
    }
}
