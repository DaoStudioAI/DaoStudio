using System;
using Microsoft.Data.Sqlite;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for initialization
    public partial class SqliteLlmToolRepository
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

            // Create LLM tools table if it doesn't exist
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS LlmTools (
                    Id INTEGER PRIMARY KEY,
                    StaticId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    ToolConfig TEXT NOT NULL,
                    ToolType INTEGER NOT NULL,
                    Parameters TEXT NOT NULL,
                    IsEnabled INTEGER NOT NULL,
                    LastModified INTEGER NOT NULL,
                    State INTEGER NOT NULL DEFAULT 0,
                    StateData BLOB,
                    DevMsg TEXT,
                    CreatedAt INTEGER NOT NULL,
                    AppId INTEGER NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS idx_llm_tools_static_id ON LlmTools(StaticId);
                CREATE UNIQUE INDEX IF NOT EXISTS idx_llm_tools_name ON LlmTools(Name);
                CREATE INDEX IF NOT EXISTS idx_llm_tools_app_id ON LlmTools(AppId);
                CREATE INDEX IF NOT EXISTS idx_llm_tools_enabled_type ON LlmTools(IsEnabled, ToolType);
            ";
            command.ExecuteNonQuery();
        }
    }
} 