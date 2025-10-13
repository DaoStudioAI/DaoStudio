using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// Initialization methods for the SQLite message repository
    /// </summary>
    public partial class SqliteMessageRepository
    {
        /// <summary>
        /// Initializes the database tables
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        protected override void Initialize()
        {
            var connection = GetConnection();
            
            // Enable WAL mode for better concurrency
            using (var walCommand = new SqliteCommand("PRAGMA journal_mode = WAL;", connection))
            {
                walCommand.ExecuteNonQuery();
            }
            
            // Create the Messages table if it doesn't exist
            string createTableSql = @"
                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY,
                    SessionId INTEGER NOT NULL,
                    Content TEXT,
                    Role INTEGER NOT NULL,
                    Type INTEGER NOT NULL,
                    BinaryContent BLOB,
                    BinaryVersion INTEGER,
                    ParentMsgId INTEGER,
                    ParentSessId INTEGER,
                    CreatedAt INTEGER NOT NULL,
                    LastModified INTEGER NOT NULL
                );
                
                CREATE INDEX IF NOT EXISTS idx_messages_session_id 
                ON Messages(SessionId);
                
                CREATE INDEX IF NOT EXISTS idx_messages_session_created 
                ON Messages(SessionId, CreatedAt);
            ";
            
            using (var command = new SqliteCommand(createTableSql, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }
} 