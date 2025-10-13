namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Service interface for message operations
    /// </summary>
    public interface IMessageService
    {
        /// <summary>
        /// Gets a message by its ID
        /// </summary>
        /// <param name="id">The message ID</param>
        /// <returns>The message, or null if not found</returns>
        Task<IMessage?> GetMessageByIdAsync(long id);

        /// <summary>
        /// Gets all messages for a specific session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>A list of messages for the session</returns>
        Task<IEnumerable<IMessage>> GetMessagesBySessionIdAsync(long sessionId);

        /// <summary>
        /// Gets all messages for a specific parent session
        /// </summary>
        /// <param name="parentSessId">The parent session ID</param>
        /// <returns>A list of messages for the parent session</returns>
        Task<IEnumerable<IMessage>> GetMessagesByParentSessIdAsync(long parentSessId);

        /// <summary>
        /// Gets all messages in the storage
        /// </summary>
        /// <returns>A list of all messages</returns>
        Task<IEnumerable<IMessage>> GetAllMessagesAsync();

        /// <summary>
        /// Deletes a message by its ID
        /// </summary>
        /// <param name="id">The ID of the message to delete</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteMessageAsync(long id);

        /// <summary>
        /// Deletes all messages for a specific session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>Number of messages deleted</returns>
        Task<int> DeleteMessagesBySessionIdAsync(long sessionId);

        /// <summary>
        /// Deletes all messages for a specific parent session
        /// </summary>
        /// <param name="parentSessId">The parent session ID</param>
        /// <returns>Number of messages deleted</returns>
        Task<int> DeleteMessagesByParentSessIdAsync(long parentSessId);

        /// <summary>
        /// Deletes all the messages that belong to a specified session from the specified message.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="specifiedMessageId">The reference message ID within the session.</param>
        /// <param name="includeSpecifiedMessage">Whether to include the specified message in deletion.</param>
        /// <returns>The number of messages deleted.</returns>
        Task<int> DeleteMessageInSessionAsync(long sessionId, long specifiedMessageId, bool includeSpecifiedMessage);


        /// <summary>
        /// Creates a new message from parameters
        /// </summary>
        /// <param name="content">Message content</param>
        /// <param name="role">Message role</param>
        /// <param name="type">Message type</param>
        /// <param name="sessionId">Optional session ID to assign to the message when creating</param>
        /// <param name="saveToDisk">If true, persist the message immediately; if false, return an unsaved in-memory message</param>
        /// <param name="parentMsgId">Parent message ID</param>
        /// <param name="parentSessId">Parent session ID</param>
        /// <returns>The created message</returns>
        Task<IMessage> CreateMessageAsync(string content, MessageRole role, MessageType type,
            long? sessionId = null, bool saveToDisk = true, long parentMsgId = 0, long parentSessId = 0);

        /// <summary>
        /// Save changes to an existing message, or create a new message when allowCreate is true and the message ID is 0.
        /// </summary>
        /// <param name="message">The message to save</param>
        /// <param name="allowCreate">If true and message.Id == 0, creates the message; if false and Id == 0, throws an exception</param>
        /// <returns>True if successful</returns>
        Task<bool> SaveMessageAsync(IMessage message, bool allowCreate);
    }
}
