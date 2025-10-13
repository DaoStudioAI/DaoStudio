using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Migrations;
using DaoStudio.DBStorage.Repositories;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;

namespace Test.TestStorage
{
    public class TestMigrationManager : IDisposable
    {
        private readonly string _testDbPath;
        private readonly IMigrationManager _migrationManager;

        public TestMigrationManager()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_migration_{Guid.NewGuid()}.db");
            
            // Initialize migration manager directly with SqliteMigrationManager
            _migrationManager = new SqliteMigrationManager(_testDbPath);
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
        public async Task GetCurrentVersionReturnsZeroForNewDatabase()
        {
            // Arrange - new database should have version 0

            // Act
            var version = await _migrationManager.GetCurrentVersionAsync();

            // Assert
            Assert.Equal(0, version);
        }

        [Fact]
        public void RegisteringMigrationsWorks()
        {
            // Arrange
            var mockMigration1 = new MockMigration(1, "Test migration 1");
            var mockMigration2 = new MockMigration(2, "Test migration 2");

            // Act
            _migrationManager.RegisterMigration(mockMigration1);
            _migrationManager.RegisterMigration(mockMigration2);
            var migrations = _migrationManager.GetRegisteredMigrations();

            // Assert
            Assert.Equal(2, migrations.Count);
            Assert.Contains(migrations, m => m.TargetVersion == 1);
            Assert.Contains(migrations, m => m.TargetVersion == 2);
        }

        [Fact]
        public async Task MigrateToLatestAppliesMigrationsInOrder()
        {
            // Arrange
            var mockMigration1 = new MockMigration(1, "Test migration 1");
            var mockMigration2 = new MockMigration(2, "Test migration 2");
            var mockMigration3 = new MockMigration(3, "Test migration 3");

            _migrationManager.RegisterMigration(mockMigration2); // Register out of order
            _migrationManager.RegisterMigration(mockMigration1);
            _migrationManager.RegisterMigration(mockMigration3);

            // Act
            var result = await _migrationManager.MigrateToLatestAsync();
            var version = await _migrationManager.GetCurrentVersionAsync();

            // Assert
            Assert.True(result);
            Assert.Equal(3, version);
            Assert.True(mockMigration1.WasApplied);
            Assert.True(mockMigration2.WasApplied);
            Assert.True(mockMigration3.WasApplied);
            
            // Verify they were applied in order
            Assert.True(mockMigration1.AppliedAt < mockMigration2.AppliedAt);
            Assert.True(mockMigration2.AppliedAt < mockMigration3.AppliedAt);
        }

        [Fact]
        public async Task MigrateToLatestReturnsFalseWhenNoMigrationsToApply()
        {
            // Arrange
            var mockMigration = new MockMigration(1, "Test migration");
            _migrationManager.RegisterMigration(mockMigration);
            
            // Apply the migration
            await _migrationManager.MigrateToLatestAsync();
            mockMigration.Reset(); // Reset tracking

            // Act
            var result = await _migrationManager.MigrateToLatestAsync();

            // Assert
            Assert.False(result); // No migrations were applied
            Assert.False(mockMigration.WasApplied); // The migration wasn't applied again
        }

        [Fact]
        public async Task MigrateToLatestHandlesFailedMigrations()
        {
            // Arrange
            var mockMigration1 = new MockMigration(1, "Test migration 1");
            var mockMigration2 = new MockMigration(2, "Test migration 2", willSucceed: false);
            var mockMigration3 = new MockMigration(3, "Test migration 3");

            _migrationManager.RegisterMigration(mockMigration1);
            _migrationManager.RegisterMigration(mockMigration2);
            _migrationManager.RegisterMigration(mockMigration3);

            // Act
            await Assert.ThrowsAsync<Exception>(() => _migrationManager.MigrateToLatestAsync());
            var version = await _migrationManager.GetCurrentVersionAsync();

            // Assert
            Assert.Equal(1, version); // Only first migration was applied
            Assert.True(mockMigration1.WasApplied);
            Assert.True(mockMigration2.WasApplied); // It was attempted
            Assert.False(mockMigration3.WasApplied); // Never reached
        }
    }

    // Mock implementation of IMigration for testing
    public class MockMigration : IMigration
    {
        public int TargetVersion { get; }
        public string Description { get; }
        public bool WasApplied { get; private set; }
        public DateTime AppliedAt { get; private set; }
        
        private readonly bool _willSucceed;

        public MockMigration(int targetVersion, string description, bool willSucceed = true)
        {
            TargetVersion = targetVersion;
            Description = description;
            _willSucceed = willSucceed;
        }

        public Task<bool> ApplyAsync(SqliteConnection connection)
        {
            WasApplied = true;
            AppliedAt = DateTime.UtcNow;

            if (!_willSucceed)
            {
                throw new InvalidOperationException("Migration failed by design");
            }

            return Task.FromResult(_willSucceed);
        }

        public void Reset()
        {
            WasApplied = false;
        }
    }
} 