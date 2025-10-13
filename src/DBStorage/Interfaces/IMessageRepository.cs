using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Interfaces
{
    /// <summary>
    /// Interface for managing message storage operations
    /// </summary>
    public interface IMessageRepository
    {
        /// <summary>
        /// Gets a message by its ID
        /// </summary>
        /// <param name="id">The message ID</param>
        /// <returns>The message, or null if not found</returns>
        Task<Message?> GetByIdAsync(long id);

        /// <summary>
        /// Gets all messages for a specific session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>A list of messages for the session</returns>
        Task<IEnumerable<Message>> GetBySessionIdAsync(long sessionId);

        /// <summary>
        /// Gets all messages for a specific session with option to skip binary data
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <param name="includeBinaryData">Whether to include and deserialize binary data</param>
        /// <returns>A list of messages for the session</returns>
        Task<IEnumerable<Message>> GetBySessionIdAsync(long sessionId, bool includeBinaryData);

        /// <summary>
        /// Gets all messages for a specific parent session
        /// </summary>
        /// <param name="parentSessId">The parent session ID</param>
        /// <returns>A list of messages for the parent session</returns>
        Task<IEnumerable<Message>> GetByParentSessIdAsync(long parentSessId);

        /// <summary>
        /// Gets all messages in the storage
        /// </summary>
        /// <returns>A list of all messages</returns>
        Task<IEnumerable<Message>> GetAllAsync();


        /// <summary>
        /// Deletes a message by its ID
        /// </summary>
        /// <param name="id">The ID of the message to delete</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteAsync(long id);

        /// <summary>
        /// Deletes all messages for a specific session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>Number of messages deleted</returns>
        Task<int> DeleteBySessionIdAsync(long sessionId);

        /// <summary>
        /// Deletes all messages for a specific parent session
        /// </summary>
        /// <param name="parentSessId">The parent session ID</param>
        /// <returns>Number of messages deleted</returns>
        Task<int> DeleteByParentSessIdAsync(long parentSessId);

        /// <summary>
        /// Deletes all the messages that belong to a specified session from the specified message.
        /// Uses the specified message's CreatedAt as the cutoff time.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="specifiedMessageId">The reference message ID within the session.</param>
        /// <param name="includeSpecifiedMessage">Whether to include the specified message in deletion.</param>
        /// <returns>The number of messages deleted.</returns>
        Task<int> DeleteFromMessageInSessionAsync(long sessionId, long specifiedMessageId, bool includeSpecifiedMessage);

        /// <summary>
        /// Create a new message
        /// </summary>
        /// <param name="message">The message to create</param>
        /// <returns>The created message with assigned ID</returns>
        Task<Message> CreateMessageAsync(Message message);

        /// <summary>
        /// Save changes to an existing message
        /// </summary>
        /// <param name="message">The message to update</param>
        /// <returns>True if successful</returns>
        Task<bool> SaveMessageAsync(Message message);
    }
} 