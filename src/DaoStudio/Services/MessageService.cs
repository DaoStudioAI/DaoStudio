using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaoStudio.Interfaces;
using DaoStudio.DBStorage.Interfaces;

namespace DaoStudio.Services
{
    /// <summary>
    /// Service implementation for message operations
    /// </summary>
    public class MessageService : IMessageService
    {
        private readonly IMessageRepository messageRepository;
        private readonly ILogger logger;

        public MessageService(IMessageRepository messageRepository, ILogger<MessageService> logger)
        {
            this.messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a message by its ID
        /// </summary>
        /// <param name="id">The message ID</param>
        /// <returns>The message, or null if not found</returns>
        public async Task<IMessage?> GetMessageByIdAsync(long id)
        {
            try
            {
                var dbMessage = await messageRepository.GetByIdAsync(id);
                
                return dbMessage != null ? Message.FromDBMessage(dbMessage) : null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting message by ID {MessageId}", id);
                throw;
            }
        }

        /// <summary>
        /// Gets all messages for a specific session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>A list of messages for the session</returns>
        public async Task<IEnumerable<IMessage>> GetMessagesBySessionIdAsync(long sessionId)
        {
            if (sessionId==0)
                throw new ArgumentException("Session ID must be other than 0", nameof(sessionId));
                
            try
            {
                var dbMessages = await messageRepository.GetBySessionIdAsync(sessionId);
                
                return dbMessages.Select(Message.FromDBMessage).Cast<IMessage>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting messages by session ID {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Gets all messages for a specific parent session
        /// </summary>
        /// <param name="parentSessId">The parent session ID</param>
        /// <returns>A list of messages for the parent session</returns>
        public async Task<IEnumerable<IMessage>> GetMessagesByParentSessIdAsync(long parentSessId)
        {
            try
            {
                var dbMessages = await messageRepository.GetByParentSessIdAsync(parentSessId);
                
                return dbMessages.Select(Message.FromDBMessage).Cast<IMessage>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting messages by parent session ID {ParentSessId}", parentSessId);
                throw;
            }
        }

        /// <summary>
        /// Gets all messages in the storage
        /// </summary>
        /// <returns>A list of all messages</returns>
        public async Task<IEnumerable<IMessage>> GetAllMessagesAsync()
        {
            try
            {
                var dbMessages = await messageRepository.GetAllAsync();
                
                return dbMessages.Select(Message.FromDBMessage).Cast<IMessage>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting all messages");
                throw;
            }
        }

        /// <summary>
        /// Deletes a message by its ID
        /// </summary>
        /// <param name="id">The ID of the message to delete</param>
        /// <returns>True if deleted, false if not found</returns>
        public async Task<bool> DeleteMessageAsync(long id)
        {
            try
            {
                return await messageRepository.DeleteAsync(id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting message {MessageId}", id);
                throw;
            }
        }

        /// <summary>
        /// Deletes all messages for a specific session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>Number of messages deleted</returns>
        public async Task<int> DeleteMessagesBySessionIdAsync(long sessionId)
        {
            try
            {
                return await messageRepository.DeleteBySessionIdAsync(sessionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting messages by session ID {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Deletes all messages for a specific parent session
        /// </summary>
        /// <param name="parentSessId">The parent session ID</param>
        /// <returns>Number of messages deleted</returns>
        public async Task<int> DeleteMessagesByParentSessIdAsync(long parentSessId)
        {
            try
            {
                return await messageRepository.DeleteByParentSessIdAsync(parentSessId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting messages by parent session ID {ParentSessId}", parentSessId);
                throw;
            }
        }

        /// <summary>
        /// Creates a new message from parameters
        /// </summary>
        /// <param name="content">Message content</param>
        /// <param name="role">Message role</param>
        /// <param name="type">Message type</param>
        /// <param name="sessionId">Session ID</param>
        /// <param name="saveToDisk">Whether to persist the message to disk</param>
        /// <param name="parentMsgId">Parent message ID</param>
        /// <param name="parentSessId">Parent session ID</param>
        /// <returns>The created message</returns>
        public async Task<IMessage> CreateMessageAsync(string content, MessageRole role, MessageType type,
            long? sessionId = null, bool saveToDisk = true, long parentMsgId = 0, long parentSessId = 0)
        {

            if ((sessionId ?? 0) == 0)
                throw new ArgumentException("Session ID must be not 0", nameof(sessionId));

            try
            {
                var dbMessage = new DBStorage.Models.Message
                {
                    SessionId = sessionId ?? 0,
                    Content = content,
                    Role = (int)role,
                    Type = (int)type,
                    BinaryVersion = 0,
                    ParentMsgId = parentMsgId,
                    ParentSessId = parentSessId,
                    LastModified = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                if (saveToDisk)
                {
                    var createdDbMessage = await messageRepository.CreateMessageAsync(dbMessage);
                    return Message.FromDBMessage(createdDbMessage);
                }
                else
                {
                    // Return an unsaved in-memory message (Id will be 0 until saved)
                    return Message.FromDBMessage(dbMessage);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating message from parameters");
                throw;
            }
        }

        /// <summary>
        /// Deletes all the messages that belong to a specified session from the specified message.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="specifiedMessageId">The reference message ID within the session.</param>
        /// <param name="includeSpecifiedMessage">Whether to include the specified message in deletion.</param>
        /// <returns>The number of messages deleted.</returns>
        public async Task<int> DeleteMessageInSessionAsync(long sessionId, long specifiedMessageId, bool includeSpecifiedMessage)
        {
            try
            {
                logger.LogInformation("Deleting messages {Inclusion} message {MessageId} in session {SessionId}",
                    includeSpecifiedMessage ? "including" : "after", specifiedMessageId, sessionId);

                var deletedCount = await messageRepository.DeleteFromMessageInSessionAsync(sessionId, specifiedMessageId, includeSpecifiedMessage);

                logger.LogInformation("Deleted {Count} messages {Inclusion} message {MessageId} in session {SessionId}",
                    deletedCount, includeSpecifiedMessage ? "including" : "after", specifiedMessageId, sessionId);

                return deletedCount;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting messages {Inclusion} message {MessageId} in session {SessionId}",
                    includeSpecifiedMessage ? "including" : "after", specifiedMessageId, sessionId);
                return 0;
            }
        }


        /// <summary>
        /// Save changes to an existing message
        /// </summary>
        /// <param name="message">The message to save</param>
        /// <param name="allowCreate">If true and message.Id == 0, creates the message; if false and Id == 0, throws an exception</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SaveMessageAsync(IMessage message, bool allowCreate)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if ((!allowCreate)&& message.Id==0)
            {
                throw new InvalidOperationException("Cannot save message with ID 0 when allowCreate is false. Use CreateMessageAsync or set allowCreate to true.");
            }

            // Convert IMessage to DB model, preserving binary contents
            DBStorage.Models.Message dbMessage = new DBStorage.Models.Message
                {
                    Id = message.Id,
                    SessionId = message.SessionId,
                    Content = message.Content,
                    Role = (int)message.Role,
                    Type = (int)message.Type,
                    BinaryVersion = message.BinaryVersion,
                    ParentMsgId = message.ParentMsgId,
                    ParentSessId = message.ParentSessId,
                    CreatedAt = message.CreatedAt,
                    LastModified = DateTime.UtcNow
                };

                if (message.BinaryContents != null)
                {
                    dbMessage.BinaryContents = message.BinaryContents
                        .Select(bc => new DBStorage.Models.BinaryData
                        {
                            Name = bc.Name,
                            Type = (int)bc.Type,
                            Data = bc.Data
                        }).ToList();
                }

            try
            {
                if ((dbMessage.Id == 0) && (allowCreate))
                {
                    var createdDbMessage = await messageRepository.CreateMessageAsync(dbMessage);

                    // Propagate generated fields back to the interface message
                    message.Id = createdDbMessage.Id;
                    message.CreatedAt = createdDbMessage.CreatedAt;
                    message.LastModified = createdDbMessage.LastModified;
                    return true;
                }

                // Update path – allowCreate is false
                if (dbMessage.Id == 0)
                {
                    throw new InvalidOperationException("Cannot save message with ID 0 when allowCreate is false. Use CreateMessageAsync or set allowCreate to true.");
                }

                dbMessage.LastModified = DateTime.UtcNow;
                return await messageRepository.SaveMessageAsync(dbMessage);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving message {MessageId}", message.Id);
                return false;
            }
        }
    }
}
