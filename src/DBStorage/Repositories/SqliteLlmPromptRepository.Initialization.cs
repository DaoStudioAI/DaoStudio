using System;
using Microsoft.Data.Sqlite;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for initialization
    public partial class SqliteLlmPromptRepository
    {
        /// <summary>
        /// Initialize the database
        /// </summary>
        protected override void Initialize()
        {
            var connection = GetConnection();

            // Enable WAL mode for better concurrency
            using (var walCommand = new SqliteCommand("PRAGMA journal_mode = WAL;", connection))
            {
                walCommand.ExecuteNonQuery();
            }

            // Create LLM prompts table if it doesn't exist
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS LlmPrompts (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    Parameters TEXT NOT NULL,
                    IsEnabled INTEGER NOT NULL,
                    LastModified INTEGER NOT NULL,
                    CreatedAt INTEGER NOT NULL
                );
                
                CREATE INDEX IF NOT EXISTS idx_llm_prompts_category_enabled ON LlmPrompts(Category, IsEnabled);
            ";
            command.ExecuteNonQuery();
        }
    }
} 