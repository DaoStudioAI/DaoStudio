using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for initialization
    public partial class SqliteSettingsRepository
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

            // Create settings table if it doesn't exist
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Settings (
                    ApplicationName TEXT PRIMARY KEY,
                    Version INTEGER NOT NULL,
                    Properties TEXT NOT NULL,
                    LastModified INTEGER NOT NULL,
                    Theme INTEGER
                );
                
                CREATE INDEX IF NOT EXISTS idx_settings_last_modified ON Settings(LastModified);
            ";
            command.ExecuteNonQuery();
        }
    }
} 