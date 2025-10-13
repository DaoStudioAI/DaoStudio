using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace DaoStudio.DBStorage.Interfaces
{
    /// <summary>
    /// Interface for database migration operations
    /// </summary>
    public interface IMigration
    {
        /// <summary>
        /// The version this migration will upgrade the database to
        /// </summary>
        int TargetVersion { get; }
        
        /// <summary>
        /// Description of the migration for logging and debugging
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Applies the migration to the database
        /// </summary>
        /// <param name="connection">An open SQLite connection</param>
        /// <returns>True if migration was successful</returns>
        Task<bool> ApplyAsync(SqliteConnection connection);
    }
} 