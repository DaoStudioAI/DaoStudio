using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Interfaces;

namespace DaoStudio.DBStorage.Migrations
{
    /// <summary>
    /// Base class for database migrations
    /// </summary>
    public abstract class BaseMigration : IMigration
    {
        /// <inheritdoc />
        public abstract int TargetVersion { get; }
        
        /// <inheritdoc />
        public abstract string Description { get; }
        
        /// <inheritdoc />
        public abstract Task<bool> ApplyAsync(SqliteConnection connection);
        
        /// <summary>
        /// Executes a SQL command as part of a migration
        /// </summary>
        /// <param name="connection">An open SQLite connection</param>
        /// <param name="sql">The SQL command to execute</param>
        /// <returns>True if execution was successful</returns>
        protected async Task<bool> ExecuteSqlAsync(SqliteConnection connection, string sql)
        {
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Migration failed: {ex.Message}");
                return false;
            }
        }
    }
} 