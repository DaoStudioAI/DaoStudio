using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Interfaces;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// SQLite implementation of the migration manager
    /// </summary>
    public class SqliteMigrationManager : IMigrationManager
    {
        private readonly string _connectionString;
        private readonly List<IMigration> _migrations = new List<IMigration>();

        /// <summary>
        /// Constructor with database path
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public SqliteMigrationManager(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentNullException(nameof(databasePath));

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
        }

        /// <inheritdoc />
        public async Task<int> GetCurrentVersionAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        /// <inheritdoc />
        public async Task<bool> MigrateToLatestAsync()
        {
            // Get the current version
            int currentVersion = await GetCurrentVersionAsync();
            
            // Get migrations that need to be applied (sorted by target version)
            var migrationsToApply = _migrations
                .Where(m => m.TargetVersion > currentVersion)
                .OrderBy(m => m.TargetVersion)
                .ToList();
                
            if (migrationsToApply.Count == 0)
            {
                return false; // Already at latest version
            }
            
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            
            // Track if any migrations were applied
            bool anyMigrationsApplied = false;
            
            // Process each migration individually
            foreach (var migration in migrationsToApply)
            {
                // Use a transaction for each individual migration
                using var transaction = connection.BeginTransaction();
                
                try
                {
                    // Apply the migration
                    bool success = await migration.ApplyAsync(connection);
                    
                    if (!success)
                    {
                        // Roll back if the migration fails
                        transaction.Rollback();
                        return anyMigrationsApplied;
                    }
                    
                    // Update the user_version pragma for this migration
                    var versionCommand = connection.CreateCommand();
                    versionCommand.CommandText = $"PRAGMA user_version = {migration.TargetVersion};";
                    await versionCommand.ExecuteNonQueryAsync();
                    
                    // Commit the transaction for this migration
                    transaction.Commit();
                    anyMigrationsApplied = true;
                }
                catch (Exception ex)
                {
                    // Roll back the current migration on exception
                    transaction.Rollback();
                    throw new Exception($"Migration to version {migration.TargetVersion} failed: {ex.Message}", ex);
                }
            }
            
            return anyMigrationsApplied;
        }

        /// <inheritdoc />
        public void RegisterMigration(IMigration migration)
        {
            if (_migrations.Any(m => m.TargetVersion == migration.TargetVersion))
            {
                throw new InvalidOperationException(
                    $"A migration with target version {migration.TargetVersion} is already registered.");
            }
            
            _migrations.Add(migration);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<IMigration> GetRegisteredMigrations()
        {
            return _migrations.AsReadOnly();
        }
    }
} 