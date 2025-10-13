using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for write operations
    public partial class SqliteLlmToolRepository
    {
        /// <summary>
        /// Create a new tool
        /// </summary>
        /// <param name="tool">The tool to create</param>
        /// <returns>The created tool with assigned ID</returns>
        public async Task<LlmTool> CreateToolAsync(LlmTool tool)
        {
            // Check if a tool with the same name already exists
            if (ToolNameExists(tool.Name))
            {
                throw new InvalidOperationException($"A tool with the name '{tool.Name}' already exists.");
            }
            
            tool.LastModified = DateTime.UtcNow;
            tool.CreatedAt = DateTime.UtcNow;

            // Generate a new unique ID
            tool.Id = IdGenerator.GenerateUniqueId(ToolExists);

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO LlmTools (Id, StaticId, Name, Description, ToolConfig, ToolType, Parameters, IsEnabled, LastModified, CreatedAt, DevMsg, State, StateData, AppId)
                VALUES (@Id, @StaticId, @Name, @Description, @ToolConfig, @ToolType, @Parameters, @IsEnabled, @LastModified, @CreatedAt, @DevMsg, @State, @StateData, @AppId);
            ";
            command.Parameters.AddWithValue("@Id", tool.Id);
            command.Parameters.AddWithValue("@StaticId", tool.StaticId);
            command.Parameters.AddWithValue("@Name", tool.Name);
            command.Parameters.AddWithValue("@Description", tool.Description);
            command.Parameters.AddWithValue("@ToolConfig", tool.ToolConfig);
            command.Parameters.AddWithValue("@ToolType", tool.ToolType);
            command.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(tool.Parameters));
            command.Parameters.AddWithValue("@IsEnabled", tool.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@LastModified", (ulong)tool.LastModified.ToFileTimeUtc());
            command.Parameters.AddWithValue("@CreatedAt", (ulong)tool.CreatedAt.ToFileTimeUtc());
            command.Parameters.AddWithValue("@DevMsg", tool.DevMsg);
            command.Parameters.AddWithValue("@State", tool.State);
            command.Parameters.AddWithValue("@StateData", tool.StateData != null ? (object)tool.StateData : DBNull.Value);
            command.Parameters.AddWithValue("@AppId", tool.AppId);

            await command.ExecuteNonQueryAsync();
            return tool;
        }

        /// <summary>
        /// Save changes to an existing tool
        /// </summary>
        /// <param name="tool">The tool to update</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SaveToolAsync(LlmTool tool)
        {
            if (tool.Id == 0)
            {
                throw new ArgumentException("Cannot save tool with ID 0. Use CreateToolAsync for new tools.");
            }
            
            // Check if a tool with the same name already exists (excluding the current tool)
            if (ToolNameExists(tool.Name, tool.Id))
            {
                throw new InvalidOperationException($"Another tool with the name '{tool.Name}' already exists.");
            }

            tool.LastModified = DateTime.UtcNow;

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE LlmTools
                SET StaticId = @StaticId,
                    Name = @Name,
                    Description = @Description,
                    ToolConfig = @ToolConfig,
                    ToolType = @ToolType,
                    Parameters = @Parameters,
                    IsEnabled = @IsEnabled,
                    LastModified = @LastModified,
                    DevMsg = @DevMsg,
                    State = @State,
                    StateData = @StateData,
                    AppId = @AppId
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", tool.Id);
            command.Parameters.AddWithValue("@StaticId", tool.StaticId);
            command.Parameters.AddWithValue("@Name", tool.Name);
            command.Parameters.AddWithValue("@Description", tool.Description);
            command.Parameters.AddWithValue("@ToolConfig", tool.ToolConfig);
            command.Parameters.AddWithValue("@ToolType", tool.ToolType);
            command.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(tool.Parameters));
            command.Parameters.AddWithValue("@IsEnabled", tool.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@LastModified", (ulong)tool.LastModified.ToFileTimeUtc());
            command.Parameters.AddWithValue("@DevMsg", tool.DevMsg);
            command.Parameters.AddWithValue("@State", tool.State);
            command.Parameters.AddWithValue("@StateData", tool.StateData != null ? (object)tool.StateData : DBNull.Value);
            command.Parameters.AddWithValue("@AppId", tool.AppId);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>
        /// Delete a tool
        /// </summary>
        /// <param name="id">The ID of the tool to delete</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeleteToolAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM LlmTools WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }
} 
