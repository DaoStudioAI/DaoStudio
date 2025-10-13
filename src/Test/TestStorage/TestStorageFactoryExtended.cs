using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Interfaces;
using Moq;
using Xunit;

namespace Test.TestStorage
{
    public class TestStorageFactoryExtended : IDisposable
    {
        private readonly string _testDbPath;
        private readonly Mock<ISettingsRepository> _mockSettingsRepo;
        private readonly Mock<IPersonRepository> _mockModelRepo;
        private readonly Mock<IAPIProviderRepository> _mockProviderRepo;
        private readonly StorageFactory _factory;

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

        public TestStorageFactoryExtended()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_storage_{Guid.NewGuid()}.db");
            
            // Create mocks for repositories
            _mockSettingsRepo = new Mock<ISettingsRepository>();
            _mockModelRepo = new Mock<IPersonRepository>();
            _mockProviderRepo = new Mock<IAPIProviderRepository>();
            
            // Create storage factory
            _factory = new StorageFactory(GetDefaultDatabasePath());
            
            // Setup common mock responses
            SetupMocks();
        }

        public void Dispose()
        {
            // Dispose of the factory
            _factory.Dispose();
            
            // Clean up the test database if it was created
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

        private void SetupMocks()
        {
            // Settings repository mock setup
            var testSettings = new Settings
            {
                ApplicationName = "TestApp",
                LastModified = DateTime.UtcNow
            };
            
            _mockSettingsRepo.Setup(repo => repo.GetSettingsAsync("TestApp"))
                .ReturnsAsync(testSettings);
            
            _mockSettingsRepo.Setup(repo => repo.SaveSettingsAsync(It.IsAny<Settings>()))
                .ReturnsAsync(true);
            
            // Model repository mock setup
            var testModel = new Person
            {
                Id = 1,
                Name = "TestModel",
                ProviderName = "TestProvider", // Renamed from ProviderId
                IsEnabled = true,
                LastModified = DateTime.UtcNow
            };
            
            _mockModelRepo.Setup(repo => repo.GetPersonAsync(1))
                .ReturnsAsync(testModel);
            
            _mockModelRepo.Setup(repo => repo.GetAllPersonsAsync(It.IsAny<bool>()))
                .ReturnsAsync(new List<Person> { testModel });
            
            _mockModelRepo.Setup(repo => repo.SavePersonAsync(It.IsAny<Person>()))
                .ReturnsAsync(true);
            
            // Provider repository mock setup
            var testProvider = new APIProvider
            {
                Id = 1,
                Name = "TestProvider",
                ApiEndpoint = "https://api.test.com",
                IsEnabled = true,
                LastModified = DateTime.UtcNow
            };
            
            _mockProviderRepo.Setup(repo => repo.GetProviderAsync(1))
                .ReturnsAsync(testProvider);
            
            _mockProviderRepo.Setup(repo => repo.GetAllProvidersAsync())
                .ReturnsAsync(new List<APIProvider> { testProvider });
            
            _mockProviderRepo.Setup(repo => repo.SaveProviderAsync(It.IsAny<APIProvider>()))
                .ReturnsAsync(true);
        }
        
        [Fact]
        public async Task TestRepositoryIntegrationAsync()
        {
            // Initialize the factory first
            await _factory.InitializeAsync();
            
            // Test integrated operations with the repositories
            var settingsRepo = await _factory.GetSettingsRepositoryAsync();
            var modelRepo = await _factory.GetPersonRepositoryAsync();
            var providerRepo = await _factory.GetApiProviderRepositoryAsync();
            var cachedModelRepo = await _factory.GetCachedModelRepositoryAsync();
            
            Assert.NotNull(settingsRepo);
            Assert.NotNull(modelRepo);
            Assert.NotNull(providerRepo);
            Assert.NotNull(cachedModelRepo);
            
            // Test database version
            var version = await _factory.GetDatabaseVersionAsync();
            Assert.True(version >= 0);
        }
        
        [Fact]
        public async Task TestCustomStorageFactoryAsync()
        {
            // Create a custom storage factory with test DB path
            using var customFactory = new StorageFactory(_testDbPath);
            await customFactory.InitializeAsync();
            
            // Get repositories from the custom factory
            var settingsRepo = await customFactory.GetSettingsRepositoryAsync();
            var modelRepo = await customFactory.GetPersonRepositoryAsync();
            var cachedModelRepo = await customFactory.GetCachedModelRepositoryAsync();
            
            // Verify repositories are created correctly
            Assert.NotNull(settingsRepo);
            Assert.NotNull(modelRepo);
            Assert.NotNull(cachedModelRepo);
            
            // Verify the database path is correct
            Assert.Equal(_testDbPath, customFactory.DatabasePath);
        }
    }
} 