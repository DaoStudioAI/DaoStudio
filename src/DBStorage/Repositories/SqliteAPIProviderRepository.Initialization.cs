using System;
using Microsoft.Data.Sqlite;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for initialization
    public partial class SqliteAPIProviderRepository
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

            // Create API providers table if it doesn't exist
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS APIProviders (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL,
                    ApiEndpoint TEXT NOT NULL,
                    ApiKey TEXT,
                    Parameters TEXT NOT NULL,
                    IsEnabled INTEGER NOT NULL,
                    LastModified INTEGER NOT NULL,
                    CreatedAt INTEGER NOT NULL,
                    ProviderType INTEGER NOT NULL DEFAULT 4,
                    Timeout INTEGER NOT NULL DEFAULT 30000,
                    MaxConcurrency INTEGER NOT NULL DEFAULT 10
                );
                
                CREATE UNIQUE INDEX IF NOT EXISTS idx_api_providers_name ON APIProviders(Name);
                CREATE INDEX IF NOT EXISTS idx_api_providers_enabled_type ON APIProviders(IsEnabled, ProviderType);
            ";
            command.ExecuteNonQuery();
        }
    }
} 