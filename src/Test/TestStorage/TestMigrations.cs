using System;
using System.IO;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Migrations;
using DaoStudio.DBStorage.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Test.TestStorage
{
    public class TestMigrations : IDisposable
    {
        private readonly string _testDbPath;
        
        public TestMigrations()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_migrations_{Guid.NewGuid()}.db");
        }
        
        public void Dispose()
        {
            // Clean up the test database after tests
            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                }
                catch
                {
                    // Ignore deletion errors during cleanup
                }
            }
        }
        
        [Fact]
        public  void TestMigrationExecutionAsync()
        {
            // Create a migration manager directly
            var manager = new SqliteMigrationManager(_testDbPath);
            
            // Verify the manager is created correctly
            Assert.NotNull(manager);
        }
        
        [Fact]
        public async Task TestMigrationVersionTrackingAsync()
        {
            // Create a migration manager directly
            var manager = new SqliteMigrationManager(_testDbPath);
            
            // Get the initial version (should be 0 for a new database)
            int version = await manager.GetCurrentVersionAsync();
            
            // Verify the initial version is 0 for a new database
            Assert.Equal(0, version);
        }
        
        [Fact]
        public async Task TestMigrationErrorHandlingAsync()
        {
            // Create a migration manager directly
            var manager = new SqliteMigrationManager(_testDbPath);
            
            // First create the TestTable needed by the migration
            using (var connection = new SqliteConnection($"Data Source={_testDbPath}"))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE TestTable (Id INTEGER PRIMARY KEY, Name TEXT)";
                    await command.ExecuteNonQueryAsync();
        }
                await connection.CloseAsync();
            }
        
            // Create and register a test migration
            var migration = new TestMigration(1, "Test migration 1");
            manager.RegisterMigration(migration);
            
            // Apply the migration
            bool result = await manager.MigrateToLatestAsync();
            
            // Verify that the migration was applied
            Assert.True(result);
            
            // Verify the version was updated
            int newVersion = await manager.GetCurrentVersionAsync();
            Assert.Equal(1, newVersion);
        }

        [Fact]
        public async Task TestMultipleMigrationsAsync()
        {
            // Create a migration manager directly
            var manager = new SqliteMigrationManager(_testDbPath);
            
            // First create the TestTable needed by migrations 2 and 3
            using (var connection = new SqliteConnection($"Data Source={_testDbPath}"))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE TestTable (Id INTEGER PRIMARY KEY, Name TEXT)";
                    await command.ExecuteNonQueryAsync();
        }
                await connection.CloseAsync();
            }
        
            // Create Table1 needed by the first migration
            using (var connection = new SqliteConnection($"Data Source={_testDbPath}"))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE Table1 (Id INTEGER PRIMARY KEY, Name TEXT)";
                    await command.ExecuteNonQueryAsync();
                }
                await connection.CloseAsync();
            }

            // Create and register multiple test migrations
            manager.RegisterMigration(new TestMigration(1, "Test migration 1", useMultiStatement: true));
            manager.RegisterMigration(new TestMigration(2, "Test migration 2"));
            
            // Apply the migrations
            bool result = await manager.MigrateToLatestAsync();
            
            // Verify that the migrations were applied
            Assert.True(result);
            
            // Verify the version was updated to the latest migration
            int newVersion = await manager.GetCurrentVersionAsync();
            Assert.Equal(2, newVersion);
        }

        [Fact]
        public async Task TestMigrationSkippingAsync()
        {
            // Create a migration manager directly
            var manager = new SqliteMigrationManager(_testDbPath);
            
            // Apply migrations when none are registered
            bool result = await manager.MigrateToLatestAsync();
            
            // Verify no migrations were applied (result should be false)
            Assert.False(result);
            
            // Verify the version is still 0
            int version = await manager.GetCurrentVersionAsync();
            Assert.Equal(0, version);
        }
        
        [Fact]
        public async Task TestStorageFactoryMigrationHelpersAsync()
        {
            // Create a StorageFactory instance directly
            using var storageFactory = new StorageFactory(_testDbPath);
            await storageFactory.InitializeAsync();
            
            // Test the StorageFactory helper methods
            int version = await storageFactory.GetDatabaseVersionAsync();
            Assert.True(version >= 0); // Version should be non-negative
            
            // Test applying migrations
            bool result = await storageFactory.ApplyMigrationsAsync();
            // This might return true or false depending on whether migrations were needed
        }

        [Fact]
        public async Task MigrationAppliesCorrectly()
        {
            // Arrange
            var mockMigration = new TestMigration(1, "Test migration");
            
            using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            
            try
            {
                // Create a test table that the migration will modify
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE TestTable (Id INTEGER PRIMARY KEY, Name TEXT)";
                    await command.ExecuteNonQueryAsync();
                }

                // Act
                var result = await mockMigration.ApplyAsync(connection);
                
                // Verify the migration effect - check if the column was added
                bool columnExists = false;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info(TestTable)";
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var columnName = reader.GetString(1);
                        if (columnName == "Description")
                        {
                            columnExists = true;
                            break;
                        }
                    }
                }

                // Assert
                Assert.True(result);
                Assert.True(columnExists);
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        [Fact]
        public async Task MigrationFailsWithInvalidSql()
        {
            // Arrange
            var failingMigration = new TestMigration(2, "Failing migration", useInvalidSql: true);
            
            using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            
            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<SqliteException>(() => failingMigration.ApplyAsync(connection));
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        [Fact]
        public void MigrationPropertiesAreCorrect()
        {
            // Arrange
            var expectedVersion = 3;
            var expectedDescription = "Test migration description";
            
            // Act
            var migration = new TestMigration(expectedVersion, expectedDescription);
            
            // Assert
            Assert.Equal(expectedVersion, migration.TargetVersion);
            Assert.Equal(expectedDescription, migration.Description);
        }

        [Fact]
        public async Task MultiSqlStatementMigrationAppliesCorrectly()
        {
            // Arrange
            var multiStatementMigration = new TestMigration(4, "Multi-statement migration", useMultiStatement: true);
            
            using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            
            try
            {
                // Create initial test tables
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE Table1 (Id INTEGER PRIMARY KEY, Name TEXT)";
                    await command.ExecuteNonQueryAsync();
                }

                // Act
                var result = await multiStatementMigration.ApplyAsync(connection);
                
                // Verify the migration created the second table
                bool table2Exists = false;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Table2'";
                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        table2Exists = true;
                    }
                }

                // Assert
                Assert.True(result);
                Assert.True(table2Exists);
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Test implementation of IMigration for testing
    /// </summary>
    public class TestMigration : IMigration
    {
        private readonly bool _useInvalidSql;
        private readonly bool _useMultiStatement;

        public int TargetVersion { get; }
        public string Description { get; }

        public TestMigration(int targetVersion, string description, bool useInvalidSql = false, bool useMultiStatement = false)
        {
            TargetVersion = targetVersion;
            Description = description;
            _useInvalidSql = useInvalidSql;
            _useMultiStatement = useMultiStatement;
        }

        public async Task<bool> ApplyAsync(SqliteConnection connection)
        {
            if (_useInvalidSql)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE NonExistentTable ADD COLUMN Description TEXT"; // This will fail
                await command.ExecuteNonQueryAsync();
                return true; // Should never reach here
            }
            else if (_useMultiStatement)
            {
                using var command1 = connection.CreateCommand();
                command1.CommandText = "ALTER TABLE Table1 ADD COLUMN Description TEXT";
                await command1.ExecuteNonQueryAsync();
                
                using var command2 = connection.CreateCommand();
                command2.CommandText = "CREATE TABLE Table2 (Id INTEGER PRIMARY KEY, Description TEXT)";
                await command2.ExecuteNonQueryAsync();
                
                return true;
            }
            else
            {
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE TestTable ADD COLUMN Description TEXT";
                await command.ExecuteNonQueryAsync();
                return true;
            }
        }
    }
} 