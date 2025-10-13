using System;
using Microsoft.Data.Sqlite;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for initialization
    public partial class SqlitePersonRepository
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

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Persons (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    ProviderName TEXT NOT NULL,
                    ModelId TEXT NOT NULL,
                    PresencePenalty REAL,
                    FrequencyPenalty REAL,
                    TopP REAL,
                    TopK INTEGER,
                    Temperature REAL,
                    Capability1 INTEGER,
                    Capability2 INTEGER,
                    Capability3 INTEGER,
                    Image BLOB,
                    ToolNames TEXT NOT NULL, 
                    Parameters TEXT NOT NULL,
                    IsEnabled INTEGER NOT NULL,
                    LastModified INTEGER NOT NULL,
                    DeveloperMessage TEXT,
                    CreatedAt INTEGER NOT NULL,
                    PersonType INTEGER NOT NULL DEFAULT 0,
                    AppId INTEGER NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS idx_persons_provider_name ON Persons(ProviderName); 
                CREATE INDEX IF NOT EXISTS idx_persons_is_enabled ON Persons(IsEnabled);
                CREATE UNIQUE INDEX IF NOT EXISTS idx_persons_name ON Persons(Name);
                CREATE INDEX IF NOT EXISTS idx_persons_person_type ON Persons(PersonType);
                CREATE INDEX IF NOT EXISTS idx_persons_app_id ON Persons(AppId);
                CREATE INDEX IF NOT EXISTS idx_persons_enabled_provider ON Persons(IsEnabled, ProviderName);
            ";
            command.ExecuteNonQuery();
        }
    }
}
