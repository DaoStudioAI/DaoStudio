using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Repositories;
using Xunit;

namespace Test.TestStorage
{
    public class TestSettingsRepository : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISettingsRepository _settingsRepository;

        public TestSettingsRepository()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_settings_repo_{Guid.NewGuid()}.db");
            
            // Initialize repository directly with SqliteSettingsRepository
            _settingsRepository = new SqliteSettingsRepository(_testDbPath);
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
        public async Task GetSettingsReturnsNullForNonExistentApplication()
        {
            // Arrange - nothing to arrange

            // Act
            var settings = await _settingsRepository.GetSettingsAsync("NonExistentApp");

            // Assert
            Assert.Null(settings);
        }

        [Fact]
        public async Task SaveAndGetSettingsWorks()
        {
            // Arrange
            var appName = "TestApp";
            var properties = new Dictionary<string, string>
            {
                { "Setting1", "Value1" },
                { "Setting2", 42.ToString() },
                { "Setting3", true.ToString() }
            };
            
            var newSettings = new Settings
            {
                ApplicationName = appName,
                Version = 1,
                Properties = properties,
                LastModified = DateTime.UtcNow,
                Theme = 0 // 0=Light
            };

            // Act
            var saveResult = await _settingsRepository.SaveSettingsAsync(newSettings);
            var retrievedSettings = await _settingsRepository.GetSettingsAsync(appName);

            // Assert
            Assert.True(saveResult);
            Assert.NotNull(retrievedSettings);
            Assert.Equal(appName, retrievedSettings.ApplicationName);
            Assert.Equal(1, retrievedSettings.Version);
            Assert.Equal(0, retrievedSettings.Theme); // 0=Light
            var v = retrievedSettings.Properties["Setting1"];
            Assert.Equal("Value1", retrievedSettings.Properties["Setting1"]);
            Assert.Equal("42", retrievedSettings.Properties["Setting2"]);
            Assert.Equal("True", retrievedSettings.Properties["Setting3"]);
        }

        [Fact]
        public async Task GetAllSettingsReturnsAllApplicationSettings()
        {
            // Arrange
            var app1Settings = new Settings
            {
                ApplicationName = "App1",
                Version = 1,
                Properties = new Dictionary<string, string> { { "Key", "Value" } },
                LastModified = DateTime.UtcNow,
                Theme = 1 // 1=Dark
            };
            
            var app2Settings = new Settings
            {
                ApplicationName = "App2",
                Version = 2,
                Properties = new Dictionary<string, string> { { "Key", "OtherValue" } },
                LastModified = DateTime.UtcNow,
                Theme = 2 // 2=System
            };
            
            await _settingsRepository.SaveSettingsAsync(app1Settings);
            await _settingsRepository.SaveSettingsAsync(app2Settings);

            // Act
            var allSettings = await _settingsRepository.GetAllSettingsAsync();

            // Assert
            Assert.Equal(2, allSettings.Count());
            Assert.Contains(allSettings, s => s.ApplicationName == "App1");
            Assert.Contains(allSettings, s => s.ApplicationName == "App2");
        }

        [Fact]
        public async Task DeleteSettingsWorks()
        {
            // Arrange
            var appName = "AppToDelete";
            var settings = new Settings
            {
                ApplicationName = appName,
                Version = 1,
                Properties = new Dictionary<string, string> { { "Key", "Value" } },
                LastModified = DateTime.UtcNow,
                Theme = 0 // 0=Light
            };
            
            await _settingsRepository.SaveSettingsAsync(settings);

            // Act
            var deleteResult = await _settingsRepository.DeleteSettingsAsync(appName);
            var retrievedSettings = await _settingsRepository.GetSettingsAsync(appName);

            // Assert
            Assert.True(deleteResult);
            Assert.Null(retrievedSettings);
        }

        [Fact]
        public async Task UpdateSettingsWorks()
        {
            // Arrange
            var appName = "AppToUpdate";
            var initialSettings = new Settings
            {
                ApplicationName = appName,
                Version = 1,
                Properties = new Dictionary<string, string> 
                { 
                    { "Setting1", "InitialValue" },
                    { "Setting2", 100.ToString() }
                },
                LastModified = DateTime.UtcNow,
                Theme = 0 // 0=Light
            };
            
            await _settingsRepository.SaveSettingsAsync(initialSettings);
            
            // Update settings
            var updatedSettings = new Settings
            {
                ApplicationName = appName,
                Version = 2,
                Properties = new Dictionary<string, string> 
                { 
                    { "Setting1", "UpdatedValue" },
                    { "Setting2", 200.ToString() },
                    { "Setting3", "NewSetting" }
                },
                LastModified = DateTime.UtcNow,
                Theme = 1 // 1=Dark
            };

            // Act
            var updateResult = await _settingsRepository.SaveSettingsAsync(updatedSettings);
            var retrievedSettings = await _settingsRepository.GetSettingsAsync(appName);

            // Assert
            Assert.True(updateResult);
            Assert.NotNull(retrievedSettings);
            Assert.Equal(appName, retrievedSettings.ApplicationName);
            Assert.Equal(2, retrievedSettings.Version);
            Assert.Equal(1, retrievedSettings.Theme); // 1=Dark
            Assert.Equal("UpdatedValue", retrievedSettings.Properties["Setting1"]);
            Assert.Equal(200.ToString(), retrievedSettings.Properties["Setting2"]);
            Assert.Equal("NewSetting", retrievedSettings.Properties["Setting3"]);
        }

        [Fact]
        public async Task NonExistentDeleteReturnsFalse()
        {
            // Arrange - nothing to arrange

            // Act
            var result = await _settingsRepository.DeleteSettingsAsync("NonExistentApp");

            // Assert
            Assert.False(result);
        }
    }
} 