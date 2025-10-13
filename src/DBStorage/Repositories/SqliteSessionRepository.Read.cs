using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for read operations
    public partial class SqliteSessionRepository
    {
        private Session MapReaderToSession(SqliteDataReader reader)
        {
            return new Session
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                Logo = !reader.IsDBNull(reader.GetOrdinal("Logo")) ? reader.GetFieldValue<byte[]>(reader.GetOrdinal("Logo")) : null,
                PersonNames = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("PersonNames"))) ?? new List<string>(),
                ToolNames = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("ToolNames"))) ?? new List<string>(),
                CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(reader.GetOrdinal("CreatedAt"))), TimeZoneInfo.Local),
                LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(reader.GetOrdinal("LastModified"))), TimeZoneInfo.Local),
                ParentSessId = !reader.IsDBNull(reader.GetOrdinal("ParentSessId")) ? reader.GetInt64(reader.GetOrdinal("ParentSessId")) : null,
                TotalTokenCount = reader.GetInt64(reader.GetOrdinal("TotalTokenCount")),
                OutputTokenCount = reader.GetInt64(reader.GetOrdinal("OutputTokenCount")),
                InputTokenCount = reader.GetInt64(reader.GetOrdinal("InputTokenCount")),
                AdditionalCounts = reader.GetInt64(reader.GetOrdinal("AdditionalCounts")),
                Properties = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(reader.GetOrdinal("Properties"))) ?? new Dictionary<string, string>(),
                SessionType = reader.GetInt32(reader.GetOrdinal("SessionType")),
                AppId = reader.GetInt64(reader.GetOrdinal("AppId")),
                PreviousId = !reader.IsDBNull(reader.GetOrdinal("PreviousId")) ? reader.GetInt64(reader.GetOrdinal("PreviousId")) : null
            };
        }

        /// <summary>
        /// Get a session by ID
        /// </summary>
        /// <param name="id">The ID of the session</param>
        /// <returns>Session or null if not found</returns>
        public async Task<Session?> GetSessionAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Title, Description, Logo, PersonNames, ToolNames, CreatedAt, LastModified, ParentSessId,
                       TotalTokenCount, OutputTokenCount, InputTokenCount, AdditionalCounts, Properties,
                       SessionType, AppId, PreviousId
                FROM Sessions 
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapReaderToSession(reader);
            }

            return null;
        }

        /// <summary>
        /// Check if a session with the given ID exists
        /// </summary>
        /// <param name="id">The ID to check</param>
        /// <returns>True if the session exists</returns>
        public bool SessionExists(long id)
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM Sessions WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// Get all sessions
        /// </summary>
        /// <param name="inclusionOptions">Options for including child sessions</param>
        /// <returns>List of sessions based on inclusion options</returns>
        public async Task<IEnumerable<Session>> GetAllSessionsAsync(SessionInclusionOptions inclusionOptions = SessionInclusionOptions.All)
        {
            var sessions = new List<Session>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            
            // Build the WHERE clause based on inclusion options
            string whereClause = inclusionOptions switch
            {
                SessionInclusionOptions.ParentsOnly => "WHERE ParentSessId IS NULL",
                SessionInclusionOptions.ChildrenOnly => "WHERE ParentSessId IS NOT NULL",
                SessionInclusionOptions.All => "",
                _ => ""
            };

            command.CommandText = $@"
                SELECT Id, Title, Description, Logo, PersonNames, ToolNames, CreatedAt, LastModified, ParentSessId,
                       TotalTokenCount, OutputTokenCount, InputTokenCount, AdditionalCounts, Properties,
                       SessionType, AppId, PreviousId
                FROM Sessions
                {whereClause};
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sessions.Add(MapReaderToSession(reader));
            }

            return sessions;
        }

        /// <summary>
        /// Get sessions by parent session ID
        /// </summary>
        /// <param name="parentSessId">The parent session ID to filter by</param>
        /// <returns>List of sessions with the specified parent session ID</returns>
        public async Task<IEnumerable<Session>> GetSessionsByParentSessIdAsync(long parentSessId)
        {
            var sessions = new List<Session>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Title, Description, Logo, PersonNames, ToolNames, CreatedAt, LastModified, ParentSessId,
                       TotalTokenCount, OutputTokenCount, InputTokenCount, AdditionalCounts, Properties,
                       SessionType, AppId, PreviousId
                FROM Sessions
                WHERE ParentSessId = @ParentSessId;
            ";
            command.Parameters.AddWithValue("@ParentSessId", parentSessId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sessions.Add(MapReaderToSession(reader));
            }

            return sessions;
        }
    }
} 
