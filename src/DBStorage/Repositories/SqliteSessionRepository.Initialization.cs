using System;
using Microsoft.Data.Sqlite;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for initialization
    public partial class SqliteSessionRepository
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

            // Create Sessions table if it doesn't exist
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Sessions (
                    Id INTEGER PRIMARY KEY,
                    Title TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    Logo BLOB,
                    PersonNames TEXT NOT NULL,
                    ToolNames TEXT NOT NULL DEFAULT '[]',
                    ParentSessId INTEGER,
                    CreatedAt INTEGER NOT NULL,
                    LastModified INTEGER NOT NULL,
                    TotalTokenCount INTEGER NOT NULL DEFAULT 0,
                    OutputTokenCount INTEGER NOT NULL DEFAULT 0,
                    InputTokenCount INTEGER NOT NULL DEFAULT 0,
                    AdditionalCounts INTEGER NOT NULL DEFAULT 0,
                    Properties TEXT NOT NULL DEFAULT '',
                    SessionType INTEGER NOT NULL DEFAULT 0,
                    AppId INTEGER NOT NULL DEFAULT 0,
                    PreviousId INTEGER
                );

                CREATE INDEX IF NOT EXISTS idx_sessions_parent_sess_id 
                ON Sessions(ParentSessId);
                CREATE INDEX IF NOT EXISTS idx_sessions_session_type ON Sessions(SessionType);
                CREATE INDEX IF NOT EXISTS idx_sessions_app_id ON Sessions(AppId);
                CREATE INDEX IF NOT EXISTS idx_sessions_previous_id ON Sessions(PreviousId);
            ";
            command.ExecuteNonQuery();
        }
    }
} 