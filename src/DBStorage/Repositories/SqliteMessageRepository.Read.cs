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
    /// Read operations for the SQLite message repository
    /// </summary>
    public partial class SqliteMessageRepository
    {
        /// <summary>
        /// Gets a message by its ID
        /// </summary>
        /// <param name="id">The message ID</param>
        /// <returns>The message, or null if not found</returns>
        public async Task<Message?> GetByIdAsync(long id)
        {
            var connection = await GetConnectionAsync();
            
            string sql = @"
                SELECT Id, SessionId, Content, Role, Type, BinaryContent, BinaryVersion, ParentMsgId, ParentSessId, CreatedAt, LastModified
                FROM Messages
                WHERE Id = @Id;
            ";
            
            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return ReadMessageFromReader(reader);
                    }
                    
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets all messages for a specific session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>A list of messages for the session</returns>
        public async Task<IEnumerable<Message>> GetBySessionIdAsync(long sessionId)
        {
            return await GetBySessionIdAsync(sessionId, true);
        }

        /// <summary>
        /// Gets all messages for a specific session with option to skip binary data
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <param name="includeBinaryData">Whether to include and deserialize binary data</param>
        /// <returns>A list of messages for the session</returns>
        public async Task<IEnumerable<Message>> GetBySessionIdAsync(long sessionId, bool includeBinaryData)
        {
            var messages = new List<Message>();
            
            var connection = await GetConnectionAsync();
            
            string sql = @"
                SELECT Id, SessionId, Content, Role, Type, BinaryContent, BinaryVersion, ParentMsgId, ParentSessId, CreatedAt, LastModified
                FROM Messages
                WHERE SessionId = @SessionId
                ORDER BY CreatedAt;
            ";
            
            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@SessionId", sessionId);
                
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        messages.Add(ReadMessageFromReader(reader, includeBinaryData));
                    }
                }
            }
            
            return messages;
        }

        /// <summary>
        /// Gets all messages for a specific parent session
        /// </summary>
        /// <param name="parentSessId">The parent session ID</param>
        /// <returns>A list of messages for the parent session</returns>
        public async Task<IEnumerable<Message>> GetByParentSessIdAsync(long parentSessId)
        {
            var messages = new List<Message>();

            var connection = await GetConnectionAsync();

            // Include messages that either have ParentSessId explicitly set,
            // belong to the parent session itself, or belong to any child session
            // where Sessions.ParentSessId = @ParentSessId.
            string sql = @"
                SELECT Id, SessionId, Content, Role, Type, BinaryContent, BinaryVersion, ParentMsgId, ParentSessId, CreatedAt, LastModified
                FROM Messages
                WHERE ParentSessId = @ParentSessId
                   OR SessionId = @ParentSessId
                   OR SessionId IN (SELECT Id FROM Sessions WHERE ParentSessId = @ParentSessId)
                ORDER BY CreatedAt;
            ";

            using (var command = new SqliteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@ParentSessId", parentSessId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        messages.Add(ReadMessageFromReader(reader));
                    }
                }
            }

            return messages;
        }

        /// <summary>
        /// Gets all messages in the storage
        /// </summary>
        /// <returns>A list of all messages</returns>
        public async Task<IEnumerable<Message>> GetAllAsync()
        {
            var messages = new List<Message>();
            
            var connection = await GetConnectionAsync();
            
            string sql = @"
                SELECT Id, SessionId, Content, Role, Type, BinaryContent, BinaryVersion, ParentMsgId, ParentSessId, CreatedAt, LastModified
                FROM Messages
                ORDER BY SessionId, CreatedAt;
            ";
            
            using (var command = new SqliteCommand(sql, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        messages.Add(ReadMessageFromReader(reader));
                    }
                }
            }
            
            return messages;
        }

        /// <summary>
        /// Reads a message from the current position of the data reader
        /// </summary>
        /// <param name="reader">The SQLite data reader</param>
        /// <returns>A new Message object</returns>
        private Message ReadMessageFromReader(SqliteDataReader reader)
        {
            return ReadMessageFromReader(reader, true);
        }

        /// <summary>
        /// Reads a message from the current position of the data reader
        /// </summary>
        /// <param name="reader">The SQLite data reader</param>
        /// <param name="includeBinaryData">Whether to deserialize binary data</param>
        /// <returns>A new Message object</returns>
        private Message ReadMessageFromReader(SqliteDataReader reader, bool includeBinaryData)
        {
            // Only read binary blob when requested to avoid unnecessary IO/deserialization
            List<BinaryData>? binaryContents = null;
            if (includeBinaryData)
            {
                var binaryOrdinal = reader.GetOrdinal("BinaryContent");
                if (!reader.IsDBNull(binaryOrdinal))
                {
                    var binaryData = (byte[])reader.GetValue(binaryOrdinal);
                    if (binaryData != null && binaryData.Length > 0)
                    {
                        binaryContents = MessagePackSerializer.Deserialize<List<BinaryData>>(binaryData);
                    }
                }
            }
            
            var message = new Message
            {
                Id = reader.GetInt64(0),
                SessionId = reader.GetInt64(1),
                Content = reader.IsDBNull(2) ? null : reader.GetString(2),
                Role = reader.GetInt32(3), // Role is now INTEGER in database
                Type = reader.GetInt32(4),
                BinaryContents = binaryContents,
                BinaryVersion = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                ParentMsgId = reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                ParentSessId = reader.IsDBNull(8) ? 0 : reader.GetInt64(8),
                CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(reader.GetOrdinal("CreatedAt"))), TimeZoneInfo.Local),
                LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(reader.GetOrdinal("LastModified"))), TimeZoneInfo.Local)
            };
            
            return message;
        }
    }
} 