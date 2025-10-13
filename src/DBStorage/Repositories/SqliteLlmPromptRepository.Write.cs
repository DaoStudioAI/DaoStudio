using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for write operations
    public partial class SqliteLlmPromptRepository
    {
        /// <summary>
        /// Create a new prompt
        /// </summary>
        /// <param name="prompt">The prompt to create</param>
        /// <returns>The created prompt with assigned ID</returns>
        public async Task<LlmPrompt> CreatePromptAsync(LlmPrompt prompt)
        {
            prompt.LastModified = DateTime.UtcNow;
            prompt.CreatedAt = DateTime.UtcNow;

            // Generate a new unique ID
            prompt.Id = IdGenerator.GenerateUniqueId(PromptExists);

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO LlmPrompts (Id, Name, Category, Content, Parameters, IsEnabled, LastModified, CreatedAt)
                VALUES (@Id, @Name, @Category, @Content, @Parameters, @IsEnabled, @LastModified, @CreatedAt);
            ";
            command.Parameters.AddWithValue("@Id", prompt.Id);
            command.Parameters.AddWithValue("@Name", prompt.Name);
            command.Parameters.AddWithValue("@Category", prompt.Category);
            command.Parameters.AddWithValue("@Content", prompt.Content);
            command.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(prompt.Parameters));
            command.Parameters.AddWithValue("@IsEnabled", prompt.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@LastModified", (ulong)prompt.LastModified.ToFileTimeUtc());
            command.Parameters.AddWithValue("@CreatedAt", (ulong)prompt.CreatedAt.ToFileTimeUtc());

            await command.ExecuteNonQueryAsync();
            return prompt;
        }

        /// <summary>
        /// Save changes to an existing prompt
        /// </summary>
        /// <param name="prompt">The prompt to update</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SavePromptAsync(LlmPrompt prompt)
        {
            if (prompt.Id == 0)
            {
                throw new ArgumentException("Cannot save prompt with ID 0. Use CreatePromptAsync for new prompts.");
            }

            prompt.LastModified = DateTime.UtcNow;

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE LlmPrompts 
                SET Name = @Name,
                    Category = @Category,
                    Content = @Content,
                    Parameters = @Parameters,
                    IsEnabled = @IsEnabled,
                    LastModified = @LastModified
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", prompt.Id);
            command.Parameters.AddWithValue("@Name", prompt.Name);
            command.Parameters.AddWithValue("@Category", prompt.Category);
            command.Parameters.AddWithValue("@Content", prompt.Content);
            command.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(prompt.Parameters));
            command.Parameters.AddWithValue("@IsEnabled", prompt.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@LastModified", (ulong)prompt.LastModified.ToFileTimeUtc());

            return await command.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>
        /// Delete a prompt
        /// </summary>
        /// <param name="id">The ID of the prompt to delete</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeletePromptAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM LlmPrompts WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }
} 
