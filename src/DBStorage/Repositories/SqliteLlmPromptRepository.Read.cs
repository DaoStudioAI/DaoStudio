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
    public partial class SqliteLlmPromptRepository
    {
        /// <summary>
        /// Get a prompt by ID
        /// </summary>
        /// <param name="id">The ID of the prompt</param>
        /// <returns>LLM prompt or null if not found</returns>
        public async Task<LlmPrompt?> GetPromptAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, Category, Content, Parameters, IsEnabled, LastModified, CreatedAt
                FROM LlmPrompts 
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new LlmPrompt
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Category = reader.GetString(2),
                    Content = reader.GetString(3),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(4)) ?? new Dictionary<string, string>(),
                    IsEnabled = reader.GetInt32(5) == 1,
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(6)), TimeZoneInfo.Local),
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(7)), TimeZoneInfo.Local)
                };
            }

            return null;
        }

        /// <summary>
        /// Check if a prompt with the given ID exists
        /// </summary>
        /// <param name="id">The ID to check</param>
        /// <returns>True if the prompt exists</returns>
        public bool PromptExists(long id)
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM LlmPrompts WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// Get all prompts
        /// </summary>
        /// <returns>List of all prompts</returns>
        public async Task<IEnumerable<LlmPrompt>> GetAllPromptsAsync()
        {
            var prompts = new List<LlmPrompt>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, Category, Content, Parameters, IsEnabled, LastModified, CreatedAt
                FROM LlmPrompts;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                prompts.Add(new LlmPrompt
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Category = reader.GetString(2),
                    Content = reader.GetString(3),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(4)) ?? new Dictionary<string, string>(),
                    IsEnabled = reader.GetInt32(5) == 1,
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(6)), TimeZoneInfo.Local),
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(7)), TimeZoneInfo.Local)
                });
            }

            return prompts;
        }

        /// <summary>
        /// Get prompts by category
        /// </summary>
        /// <param name="category">The category to filter by</param>
        /// <returns>List of prompts in the category</returns>
        public async Task<IEnumerable<LlmPrompt>> GetPromptsByCategoryAsync(string category)
        {
            var prompts = new List<LlmPrompt>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, Category, Content, Parameters, IsEnabled, LastModified, CreatedAt
                FROM LlmPrompts
                WHERE Category = @Category;
            ";
            command.Parameters.AddWithValue("@Category", category);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                prompts.Add(new LlmPrompt
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Category = reader.GetString(2),
                    Content = reader.GetString(3),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(4)) ?? new Dictionary<string, string>(),
                    IsEnabled = reader.GetInt32(5) == 1,
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(6)), TimeZoneInfo.Local),
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(7)), TimeZoneInfo.Local)
                });
            }

            return prompts;
        }
    }
} 
