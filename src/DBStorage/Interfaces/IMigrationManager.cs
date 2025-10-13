using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaoStudio.DBStorage.Interfaces
{
    /// <summary>
    /// Interface for managing database migrations
    /// </summary>
    public interface IMigrationManager
    {
        /// <summary>
        /// Gets the current database version
        /// </summary>
        /// <returns>Current database version</returns>
        Task<int> GetCurrentVersionAsync();

        /// <summary>
        /// Migrates the database to the latest version
        /// </summary>
        /// <returns>True if migrations were applied, false if already at latest version</returns>
        Task<bool> MigrateToLatestAsync();

        /// <summary>
        /// Registers a migration to be applied when upgrading the database
        /// </summary>
        /// <param name="migration">The migration to register</param>
        void RegisterMigration(IMigration migration);

        /// <summary>
        /// Gets all registered migrations
        /// </summary>
        /// <returns>Collection of all registered migrations</returns>
        IReadOnlyCollection<IMigration> GetRegisteredMigrations();
    }
} 