using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Common;
using MessagePack;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// Write operations for the SQLite message repository
    /// </summary>
    public partial class SqliteMessageRepository
    {
        /// <summary>
        /// Check if a message with the given ID exists
        /// </summary>
        /// <param name="id">The ID to check</param>
        /// <returns>True if exists, false otherwise</returns>
        private async Task<bool> MessageExistsAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Messages WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            return count > 0;
        }

        /// <summary>
        /// Deletes a message by its ID
        /// </summary>
        /// <param name="id">The ID of the message to delete</param>
        /// <returns>True if deleted, false if not found</returns>
        public async Task<bool> DeleteAsync(long id)
        {
            var connection = await GetConnectionAsync();
            
            string sql = "DELETE FROM Messages WHERE Id = @Id;";
            
            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                
                int rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }

        /// <summary>
        /// Deletes all messages for a specific session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>Number of messages deleted</returns>
        public async Task<int> DeleteBySessionIdAsync(long sessionId)
        {
            var connection = await GetConnectionAsync();
            
            string sql = "DELETE FROM Messages WHERE SessionId = @SessionId;";
            
            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@SessionId", sessionId);
                
                return await command.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Deletes all messages for a specific parent session
        /// </summary>
        /// <param name="parentSessId">The parent session ID</param>
        /// <returns>Number of messages deleted</returns>
        public async Task<int> DeleteByParentSessIdAsync(long parentSessId)
        {
            var connection = await GetConnectionAsync();
            
            string sql = "DELETE FROM Messages WHERE ParentSessId = @ParentSessId;";
            
            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@ParentSessId", parentSessId);
                
                return await command.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Deletes all the messages that belong to a specified session from the specified message.
        /// Uses the specified message's CreatedAt as the cutoff time.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="specifiedMessageId">The reference message ID within the session.</param>
        /// <param name="includeSpecifiedMessage">Whether to include the specified message in deletion.</param>
        /// <returns>The number of messages deleted.</returns>
        public async Task<int> DeleteFromMessageInSessionAsync(long sessionId, long specifiedMessageId, bool includeSpecifiedMessage)
        {
            var connection = await GetConnectionAsync();

            // Build SQL based on inclusion flag
            string sql = includeSpecifiedMessage
                ? @"DELETE FROM Messages
                    WHERE SessionId = @SessionId AND (
                        CreatedAt > (SELECT CreatedAt FROM Messages WHERE Id = @SpecifiedId AND SessionId = @SessionId)
                        OR Id = @SpecifiedId
                    );"
                : @"DELETE FROM Messages
                    WHERE SessionId = @SessionId AND 
                        CreatedAt > (SELECT CreatedAt FROM Messages WHERE Id = @SpecifiedId AND SessionId = @SessionId);";

            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@SessionId", sessionId);
                command.Parameters.AddWithValue("@SpecifiedId", specifiedMessageId);

                var affected = await command.ExecuteNonQueryAsync();
                return affected;
            }
        }


        /// <summary>
        /// Create a new message
        /// </summary>
        /// <param name="message">The message to create</param>
        /// <returns>The created message with assigned ID</returns>
        public async Task<Message> CreateMessageAsync(Message message)
        {
            message.CreatedAt = DateTime.UtcNow;
            message.LastModified = DateTime.UtcNow;

            // Generate an ID and attempt insert. Avoid calling back into the DB to check existence
            // which caused an extra connection/round-trip per attempt. Instead, generate an Id and
            // retry on UNIQUE constraint violation a limited number of times.
            const int maxAttempts = 5;
            int attempt = 0;

            var connection = await GetConnectionAsync();

            while (true)
            {
                attempt++;
                message.Id = IdGenerator.GenerateId();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                INSERT INTO Messages (Id, SessionId, Content, Role, Type, BinaryContent, BinaryVersion, ParentMsgId, ParentSessId, CreatedAt, LastModified)
                    VALUES (@Id, @SessionId, @Content, @Role, @Type, @BinaryContent, @BinaryVersion, @ParentMsgId, @ParentSessId, @CreatedAt, @LastModified);
                ";
                command.Parameters.AddWithValue("@Id", message.Id);
                command.Parameters.AddWithValue("@SessionId", message.SessionId);
                command.Parameters.AddWithValue("@Content", message.Content as object ?? DBNull.Value);
                command.Parameters.AddWithValue("@Role", message.Role);
                command.Parameters.AddWithValue("@Type", (int)message.Type);

                // Inline binary serialization
                byte[]? serializedData = (message.BinaryContents == null || message.BinaryContents.Count == 0)
                    ? null
                    : MessagePackSerializer.Serialize(message.BinaryContents);
                command.Parameters.AddWithValue("@BinaryContent", serializedData as object ?? DBNull.Value);

                command.Parameters.AddWithValue("@BinaryVersion", message.BinaryVersion);
                command.Parameters.AddWithValue("@ParentMsgId", message.ParentMsgId);
                command.Parameters.AddWithValue("@ParentSessId", message.ParentSessId);
                command.Parameters.AddWithValue("@CreatedAt", (ulong)message.CreatedAt.ToFileTimeUtc());
                command.Parameters.AddWithValue("@LastModified", (ulong)message.LastModified.ToFileTimeUtc());

                try
                {
                    await command.ExecuteNonQueryAsync();
                    return message;
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && attempt < maxAttempts)
                {
                    // SQLITE_CONSTRAINT (usually UNIQUE constraint on Id). Try a new id and retry.
                    continue;
                }
            }
        }
        

        /// <summary>
        /// Save changes to an existing message
        /// </summary>
        /// <param name="message">The message to update</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SaveMessageAsync(Message message)
        {
            if (message.Id == 0)
            {
                throw new ArgumentException("Cannot save message with ID 0. Use CreateMessageAsync for new messages.");
            }

            message.LastModified = DateTime.UtcNow;

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Messages 
                SET SessionId = @SessionId,
                    Content = @Content,
                    Role = @Role,
                    Type = @Type,
                    BinaryContent = @BinaryContent,
                    BinaryVersion = @BinaryVersion,
                    ParentMsgId = @ParentMsgId,
                    ParentSessId = @ParentSessId,
                    LastModified = @LastModified
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", message.Id);
            command.Parameters.AddWithValue("@SessionId", message.SessionId);
            command.Parameters.AddWithValue("@Content", message.Content as object ?? DBNull.Value);
            command.Parameters.AddWithValue("@Role", message.Role);
            command.Parameters.AddWithValue("@Type", (int)message.Type);
            
            // Inline binary serialization
            byte[]? serializedData = (message.BinaryContents == null || message.BinaryContents.Count == 0) 
                ? null 
                : MessagePackSerializer.Serialize(message.BinaryContents);
            command.Parameters.AddWithValue("@BinaryContent", serializedData as object ?? DBNull.Value);
            
            command.Parameters.AddWithValue("@BinaryVersion", message.BinaryVersion);
            command.Parameters.AddWithValue("@ParentMsgId", message.ParentMsgId);
            command.Parameters.AddWithValue("@ParentSessId", message.ParentSessId);
            command.Parameters.AddWithValue("@LastModified", (ulong)message.LastModified.ToFileTimeUtc());

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }
} 