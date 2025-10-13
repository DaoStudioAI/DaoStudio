using System;
using Microsoft.Data.Sqlite;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for initialization
    public partial class SqliteApplicationRepository
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

            // Create Applications table if it doesn't exist
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Applications (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL,
                    BriefDescription TEXT,
                    Description TEXT,
                    LastModified INTEGER NOT NULL,
                    CreatedAt INTEGER NOT NULL
                );
                
                CREATE UNIQUE INDEX IF NOT EXISTS idx_applications_name ON Applications(Name);
                CREATE INDEX IF NOT EXISTS idx_applications_last_modified ON Applications(LastModified);
            ";
            command.ExecuteNonQuery();
        }
    }
}
