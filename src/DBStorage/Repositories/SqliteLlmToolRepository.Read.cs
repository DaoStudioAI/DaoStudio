using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for read operations
    public partial class SqliteLlmToolRepository
    {
        private LlmTool MapReaderToTool(SqliteDataReader reader)
        {
            return MapReaderToTool(reader, true);
        }

        private LlmTool MapReaderToTool(SqliteDataReader reader, bool includeStateData)
        {
            return new LlmTool
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                StaticId = reader.GetString(reader.GetOrdinal("StaticId")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                ToolConfig = reader.GetString(reader.GetOrdinal("ToolConfig")),
                ToolType = (int)reader.GetInt64(reader.GetOrdinal("ToolType")),
                Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(reader.GetOrdinal("Parameters"))) ?? new Dictionary<string, string>(),
                IsEnabled = reader.GetInt32(reader.GetOrdinal("IsEnabled")) == 1,
                LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(reader.GetOrdinal("LastModified"))), TimeZoneInfo.Local),
                State = reader.GetInt32(reader.GetOrdinal("State")),
                StateData = includeStateData && !reader.IsDBNull(reader.GetOrdinal("StateData")) ? reader.GetFieldValue<byte[]>(reader.GetOrdinal("StateData")) : null,
                DevMsg = !reader.IsDBNull(reader.GetOrdinal("DevMsg")) ? reader.GetString(reader.GetOrdinal("DevMsg")) : string.Empty,
                CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(reader.GetOrdinal("CreatedAt"))), TimeZoneInfo.Local),
                AppId = reader.GetInt64(reader.GetOrdinal("AppId"))
            };
        }

        /// <summary>
        /// Get a tool by ID
        /// </summary>
        /// <param name="id">The ID of the tool</param>
        /// <returns>LLM tool or null if not found</returns>
        public async Task<LlmTool?> GetToolAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, StaticId, Name, Description, ToolConfig, ToolType, Parameters, IsEnabled, LastModified, State, StateData, DevMsg, CreatedAt, AppId
                FROM LlmTools 
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapReaderToTool(reader);
            }

            return null;
        }

        /// <summary>
        /// Check if a tool with the given ID exists
        /// </summary>
        /// <param name="id">The ID to check</param>
        /// <returns>True if the tool exists</returns>
        public bool ToolExists(long id)
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM LlmTools WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// Check if a tool with the given name exists
        /// </summary>
        /// <param name="name">The name to check</param>
        /// <param name="excludeId">Optional ID to exclude from the check (for updates)</param>
        /// <returns>True if a tool with the name exists</returns>
        public bool ToolNameExists(string name, long? excludeId = null)
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            if (excludeId.HasValue)
            {
                command.CommandText = "SELECT COUNT(1) FROM LlmTools WHERE Name = @Name AND Id != @ExcludeId;";
                command.Parameters.AddWithValue("@Name", name);
                command.Parameters.AddWithValue("@ExcludeId", excludeId.Value);
            }
            else
            {
                command.CommandText = "SELECT COUNT(1) FROM LlmTools WHERE Name = @Name;";
                command.Parameters.AddWithValue("@Name", name);
            }

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// Get all tools with optional state data loading
        /// </summary>
        /// <param name="includeStateData">Whether to include StateData BLOB field</param>
        /// <returns>List of all tools</returns>
        public async Task<IEnumerable<LlmTool>> GetAllToolsAsync(bool includeStateData = true)
        {
            var tools = new List<LlmTool>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, StaticId, Name, Description, ToolConfig, ToolType, Parameters, IsEnabled, LastModified, State, StateData, DevMsg, CreatedAt, AppId
                FROM LlmTools
                ORDER BY CreatedAt;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tools.Add(MapReaderToTool(reader, includeStateData));
            }

            return tools;
        }

        /// <summary>
        /// Get tools by their static ID
        /// </summary>
        /// <param name="staticId">The static ID of the tools</param>
        /// <returns>List of LLM tools matching the static ID</returns>
        public async Task<IEnumerable<LlmTool>> GetToolsByStaticIdAsync(string staticId)
        {
            var tools = new List<LlmTool>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, StaticId, Name, Description, ToolConfig, ToolType, Parameters, IsEnabled, LastModified, State, StateData, DevMsg, CreatedAt, AppId
                FROM LlmTools 
                WHERE StaticId = @StaticId
                ORDER BY CreatedAt;
            ";
            command.Parameters.AddWithValue("@StaticId", staticId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tools.Add(MapReaderToTool(reader));
            }

            return tools;
        }
    }
}
