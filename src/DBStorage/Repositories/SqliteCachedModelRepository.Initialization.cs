using System;
using Microsoft.Data.Sqlite;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for initialization
    public partial class SqliteCachedModelRepository
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

            // Create CachedModels table if it doesn't exist
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS CachedModels (
                    Id INTEGER PRIMARY KEY,
                    ApiProviderId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    ModelId TEXT NOT NULL,
                    ProviderType INTEGER NOT NULL,
                    Catalog TEXT NOT NULL,
                    Parameters TEXT NOT NULL DEFAULT '{}'
                );

                CREATE INDEX IF NOT EXISTS idx_cached_models_provider_id ON CachedModels(ApiProviderId);
                CREATE INDEX IF NOT EXISTS idx_cached_models_compound ON CachedModels(ApiProviderId, ProviderType, Catalog);
            ";
            command.ExecuteNonQuery();
        }
    }
} 