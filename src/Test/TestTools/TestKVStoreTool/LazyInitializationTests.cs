using System;
using System.IO;
using DaoStudio.Plugins.KVStore;
using Xunit;

namespace TestKVStoreTool
{
    /// <summary>
    /// Tests to verify that LiteDB database files are only created when KV store functions are actually called (lazy initialization)
    /// </summary>
    public class LazyInitializationTests : IDisposable
    {
        private readonly string _testConfigPath;

        public LazyInitializationTests()
        {
            _testConfigPath = Path.Combine(Path.GetTempPath(), "KVStoreLazyTest_" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_testConfigPath))
            {
                Directory.Delete(_testConfigPath, true);
            }
        }

        [Fact]
        public void KeyValueStoreData_Initialize_ShouldNotCreateDatabaseFile()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            using var storeData = new KeyValueStoreData(config);
            var dbPath = Path.Combine(_testConfigPath, "init_test.db");
            const string sessionId = "session1";

            // Act - Initialize (should not create database file)
            storeData.Initialize(dbPath, sessionId);

            // Assert - Database file should not exist after Initialize
            Assert.False(File.Exists(dbPath), 
                $"Database file {dbPath} should not exist after Initialize() call");
        }

        [Fact]
        public void KeyValueStoreData_FirstOperation_ShouldCreateDatabaseFile()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            using var storeData = new KeyValueStoreData(config);
            var dbPath = Path.Combine(_testConfigPath, "first_op_test.db");
            const string sessionId = "session1";

            // Initialize without creating database
            storeData.Initialize(dbPath, sessionId);
            Assert.False(File.Exists(dbPath), "Database should not exist after Initialize");

            // Act - Perform first operation (should create database)
            var keys = storeData.GetKeys(sessionId);

            // Assert - Database file should now exist
            Assert.True(File.Exists(dbPath), 
                $"Database file {dbPath} should exist after first operation");
            Assert.Empty(keys); // Should be empty since no data was set yet
        }

        [Fact]
        public void KeyValueStoreData_SetValue_ShouldCreateDatabaseFile()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            using var storeData = new KeyValueStoreData(config);
            var dbPath = Path.Combine(_testConfigPath, "set_test.db");
            const string sessionId = "session1";

            // Initialize without creating database
            storeData.Initialize(dbPath, sessionId);
            Assert.False(File.Exists(dbPath), "Database should not exist after Initialize");

            // Act - Set a value (should create database)
            var success = storeData.SetValue(sessionId, "key1", "value1");

            // Assert - Operation should succeed and database should exist
            Assert.True(success, "SetValue should succeed");
            Assert.True(File.Exists(dbPath), 
                $"Database file {dbPath} should exist after SetValue operation");

            // Verify data was actually stored
            var retrievedValue = storeData.TryGetValue(sessionId, "key1", out var value);
            Assert.True(retrievedValue, "Should be able to retrieve the stored value");
            Assert.Equal("value1", value);
        }

        [Fact]
        public void KeyValueStoreData_TryGetValue_ShouldCreateDatabaseFile()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            using var storeData = new KeyValueStoreData(config);
            var dbPath = Path.Combine(_testConfigPath, "get_test.db");
            const string sessionId = "session1";

            // Initialize without creating database
            storeData.Initialize(dbPath, sessionId);
            Assert.False(File.Exists(dbPath), "Database should not exist after Initialize");

            // Act - Try to get a non-existent value (should create database)
            var found = storeData.TryGetValue(sessionId, "nonExistentKey", out var value);

            // Assert - Operation should fail but database should be created
            Assert.False(found, "Should not find non-existent key");
            Assert.Null(value);
            Assert.True(File.Exists(dbPath), 
                $"Database file {dbPath} should exist after TryGetValue operation");
        }

        [Fact]
        public void KeyValueStoreData_DeleteKey_ShouldCreateDatabaseFile()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            using var storeData = new KeyValueStoreData(config);
            var dbPath = Path.Combine(_testConfigPath, "delete_test.db");
            const string sessionId = "session1";

            // Initialize without creating database
            storeData.Initialize(dbPath, sessionId);
            Assert.False(File.Exists(dbPath), "Database should not exist after Initialize");

            // Act - Try to delete a non-existent key (should create database)
            var deleted = storeData.DeleteKey(sessionId, "nonExistentKey");

            // Assert - Operation should fail but database should be created
            Assert.False(deleted, "Should not delete non-existent key");
            Assert.True(File.Exists(dbPath), 
                $"Database file {dbPath} should exist after DeleteKey operation");
        }

        [Fact]
        public void KeyValueStoreData_ReInitialize_ShouldNotCreateMultipleDatabases()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            using var storeData = new KeyValueStoreData(config);
            var dbPath1 = Path.Combine(_testConfigPath, "reinit_test1.db");
            var dbPath2 = Path.Combine(_testConfigPath, "reinit_test2.db");
            const string sessionId = "session1";

            // Act - Initialize first time
            storeData.Initialize(dbPath1, sessionId);
            Assert.False(File.Exists(dbPath1), "First database should not exist after Initialize");

            // Re-initialize with different path
            storeData.Initialize(dbPath2, sessionId);
            Assert.False(File.Exists(dbPath2), "Second database should not exist after Initialize");

            // Perform operation - should only create the current database
            storeData.GetKeys(sessionId);

            // Assert - Only the current database should exist
            Assert.False(File.Exists(dbPath1), "First database should not exist");
            Assert.True(File.Exists(dbPath2), "Second database should exist after operation");
        }
    }
}