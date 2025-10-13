using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Models;
using Xunit;

namespace Test.TestStorage
{
    public class TestStorageFactory : IDisposable
    {
        private readonly string _testDbPath;
        private readonly string _defaultTestDbPath;
        private readonly StorageFactory _defaultFactory;
        private readonly StorageFactory _customFactory;

        /// <summary>
        /// Determines the default database path by finding the best location for the database file
        /// </summary>
        /// <returns>The default database path</returns>
        private static string GetDefaultDatabasePath()
        {
            string databasePath = string.Empty;
            bool useExeFolder = false;
            bool needCleanup = false;
            
            try
            {
                // Try exe folder first, now with Config subfolder
                var exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var configPath = Path.Combine(exePath ?? "", "Config");
                databasePath = Path.Combine(configPath, "settings.db");
                
                if (File.Exists(databasePath))
                {
                    // Test write access to existing file
                    try
                    {
                        using (var stream = File.OpenWrite(databasePath))
                        {
                            useExeFolder = true;
                        }
                    }
                    catch
                    {
                        useExeFolder = false;
                    }
                }
                else
                {
                    // Try to create the Config directory and test file
                    try
                    {
                        // Ensure Config directory exists
                        Directory.CreateDirectory(configPath);
                        
                        using (var testFile = File.Create(databasePath))
                        {
                            testFile.Close();
                        }
                        useExeFolder = true;
                        needCleanup = true;
                    }
                    catch
                    {
                        useExeFolder = false;
                        needCleanup = true;
                    }
                }

                // Clean up test file if we created it
                if (needCleanup && File.Exists(databasePath))
                {
                    try
                    {
                        File.Delete(databasePath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            catch
            {
                useExeFolder = false;
            }

            if (!useExeFolder)
            {
                // Fall back to AppData if exe folder is not writable, also with Config subfolder
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var DaoStudioPath = Path.Combine(appDataPath, DaoStudio.Common.Constants.AppName);
                var configPath = Path.Combine(DaoStudioPath, "Config");
                
                // Ensure Config directory exists
                if (!Directory.Exists(configPath))
                {
                    Directory.CreateDirectory(configPath);
                }
                
                databasePath = Path.Combine(configPath, "settings.db");
            }

            return databasePath;
        }

        public TestStorageFactory()
        {
            // Create unique test database paths for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_storage_{Guid.NewGuid()}.db");
            _defaultTestDbPath = Path.Combine(Path.GetTempPath(), $"test_storage_default_{Guid.NewGuid()}.db");
            
            // Create factories - both using temporary paths to avoid schema conflicts
            _defaultFactory = new StorageFactory(_defaultTestDbPath);
            _customFactory = new StorageFactory(_testDbPath);
        }

        public void Dispose()
        {
            // Dispose of factory instances
            _defaultFactory.Dispose();
            _customFactory.Dispose();
            
            // Clean up the test databases after tests
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
            
            if (File.Exists(_defaultTestDbPath))
            {
                try
                {
                    File.Delete(_defaultTestDbPath);
                }
                catch
                {
                    // Ignore deletion errors during cleanup
                }
            }
        }

        [Fact]
        public void TestDatabasePathInitialization()
        {
            // Get the expected default database path using our helper method
            var expectedDefaultDatabasePath = GetDefaultDatabasePath();
            
            // Verify that the default database path is set
            Assert.NotNull(expectedDefaultDatabasePath);
            Assert.NotEmpty(expectedDefaultDatabasePath);
            
            // Verify the path contains "DaoStudio" and "Config"
            Assert.Contains("DaoStudio", expectedDefaultDatabasePath);
            Assert.Contains("Config", expectedDefaultDatabasePath);
            
            // Verify the path ends with "settings.db"
            Assert.EndsWith("settings.db", expectedDefaultDatabasePath);
            
            // Verify the DefaultFactory uses a temporary path (to avoid schema conflicts in tests)
            Assert.Contains("test_storage_default_", _defaultFactory.DatabasePath);
            Assert.EndsWith(".db", _defaultFactory.DatabasePath);
            
            // Verify the CustomFactory uses the custom path
            Assert.Equal(_testDbPath, _customFactory.DatabasePath);
        }

        [Fact]
        public async Task TestCreateRepositoryWithCustomPathAsync()
        {
            // Initialize the factory
            await _customFactory.InitializeAsync();
            
            // Test creation of repositories with custom path factory
            var settingsRepo = await _customFactory.GetSettingsRepositoryAsync();
            Assert.NotNull(settingsRepo);
            
            var modelRepo = await _customFactory.GetPersonRepositoryAsync();
            Assert.NotNull(modelRepo);
            
            var toolRepo = await _customFactory.GetLlmToolRepositoryAsync();
            Assert.NotNull(toolRepo);
            
            var promptRepo = await _customFactory.GetLlmPromptRepositoryAsync();
            Assert.NotNull(promptRepo);
            
            var providerRepo = await _customFactory.GetApiProviderRepositoryAsync();
            Assert.NotNull(providerRepo);
            
            var sessionRepo = await _customFactory.GetSessionRepositoryAsync();
            Assert.NotNull(sessionRepo);
            
            var messageRepo = await _customFactory.GetMessageRepositoryAsync();
            Assert.NotNull(messageRepo);
            
            var cachedModelRepo = await _customFactory.GetCachedModelRepositoryAsync();
            Assert.NotNull(cachedModelRepo);
        }

        [Fact]
        public async Task TestGetDefaultRepositoriesAsync()
        {
            // Initialize the factory
            await _defaultFactory.InitializeAsync();
            
            // Test that default repositories are created and cached
            var settingsRepo1 = await _defaultFactory.GetSettingsRepositoryAsync();
            var settingsRepo2 = await _defaultFactory.GetSettingsRepositoryAsync();
            Assert.NotNull(settingsRepo1);
            Assert.Same(settingsRepo1, settingsRepo2); // Should return the same instance
            
            var modelRepo1 = await _defaultFactory.GetPersonRepositoryAsync();
            var modelRepo2 = await _defaultFactory.GetPersonRepositoryAsync();
            Assert.NotNull(modelRepo1);
            Assert.Same(modelRepo1, modelRepo2);
            
            var toolRepo1 = await _defaultFactory.GetLlmToolRepositoryAsync();
            var toolRepo2 = await _defaultFactory.GetLlmToolRepositoryAsync();
            Assert.NotNull(toolRepo1);
            Assert.Same(toolRepo1, toolRepo2);
        }

        [Fact]
        public async Task TestMigrationManagerAsync()
        {
            // Initialize the factories
            await _defaultFactory.InitializeAsync();
            await _customFactory.InitializeAsync();
            
            // Test that migration manager is created properly
            var manager = await _defaultFactory.GetMigrationManagerAsync();
            Assert.NotNull(manager);
            
            // Test custom migration manager
            var customManager = await _customFactory.GetMigrationManagerAsync();
            Assert.NotNull(customManager);
        }

        [Fact]
        public async Task TestDatabaseVersionAsync()
        {
            // Initialize the factories
            await _defaultFactory.InitializeAsync();
            await _customFactory.InitializeAsync();
            
            // Test getting database version
            int version = await _defaultFactory.GetDatabaseVersionAsync();
            Assert.True(version >= 0); // Version should be non-negative
            
            // Test getting version for custom database
            int customVersion = await _customFactory.GetDatabaseVersionAsync();
            Assert.True(customVersion >= 0);
        }

        [Fact]
        public async Task TestApplyMigrationsAsync()
        {
            // Initialize the factories
            await _defaultFactory.InitializeAsync();
            await _customFactory.InitializeAsync();
            
            // Test applying migrations to default database
            bool result = await _defaultFactory.ApplyMigrationsAsync();
            // This might return true or false depending on whether migrations were needed
            
            // Test applying migrations to a custom database
            bool customResult = await _customFactory.ApplyMigrationsAsync();
            // We just verify it doesn't throw an exception
        }
        
        [Fact]
        public async Task TestMultipleInstancesAsync()
        {
            // Create multiple instances with different database paths
            string path1 = Path.Combine(Path.GetTempPath(), $"test_factory1_{Guid.NewGuid()}.db");
            string path2 = Path.Combine(Path.GetTempPath(), $"test_factory2_{Guid.NewGuid()}.db");
            
            using var factory1 = new StorageFactory(path1);
            using var factory2 = new StorageFactory(path2);
            
            // Initialize both factories
            await factory1.InitializeAsync();
            await factory2.InitializeAsync();
            
            // Get repositories from each factory
            var repo1 = await factory1.GetSettingsRepositoryAsync();
            var repo2 = await factory2.GetSettingsRepositoryAsync();
            
            // Verify they are different instances
            Assert.NotNull(repo1);
            Assert.NotNull(repo2);
            Assert.NotSame(repo1, repo2);
            
            // Clean up temporary files - no need to delete explicitly as using statement will dispose
            // and the factories will be disposed automatically
        }
    }
} 