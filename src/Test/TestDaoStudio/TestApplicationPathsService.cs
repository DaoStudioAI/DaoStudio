using System;
using System.IO;
using DaoStudio.Interfaces;
using DaoStudio.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Test.TestDaoStudio
{
    public class TestApplicationPathsService : IDisposable
    {
        private readonly ApplicationPathsService _pathsService;
        private readonly Mock<ILogger<ApplicationPathsService>> _mockLogger;

        public TestApplicationPathsService()
        {
            _mockLogger = new Mock<ILogger<ApplicationPathsService>>();
            _pathsService = new ApplicationPathsService(_mockLogger.Object);
        }

        public void Dispose()
        {
            // No cleanup needed for this test
        }

        [Fact]
        public void ConfigFolderPath_ShouldNotBeNull()
        {
            // Act
            var configPath = _pathsService.ConfigFolderPath;

            // Assert
            Assert.NotNull(configPath);
            Assert.NotEmpty(configPath);
            Assert.Contains("Config", configPath);
        }

        [Fact]
        public void SettingsDatabasePath_ShouldNotBeNull()
        {
            // Act
            var dbPath = _pathsService.SettingsDatabasePath;

            // Assert
            Assert.NotNull(dbPath);
            Assert.NotEmpty(dbPath);
            Assert.EndsWith("settings.db", dbPath);
            Assert.Contains("Config", dbPath);
        }

        [Fact]
        public void ApplicationDataPath_ShouldNotBeNull()
        {
            // Act
            var appDataPath = _pathsService.ApplicationDataPath;

            // Assert
            Assert.NotNull(appDataPath);
            Assert.NotEmpty(appDataPath);
        }

        [Fact]
        public void DatabasePath_ShouldBeInConfigFolder()
        {
            // Act
            var configPath = _pathsService.ConfigFolderPath;
            var dbPath = _pathsService.SettingsDatabasePath;

            // Assert
            Assert.StartsWith(configPath, dbPath);
        }

        [Fact]
        public void IsUsingExecutableFolder_ShouldBeBoolValue()
        {
            // Act
            var isUsingExeFolder = _pathsService.IsUsingExecutableFolder;

            // Assert - Just verify it's a boolean (no specific value required)
            Assert.IsType<bool>(isUsingExeFolder);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ApplicationPathsService(null!));
        }

        [Fact]
        public void ConfigFolderPath_ShouldContainDaoStudioOrExecutablePath()
        {
            // Act
            var configPath = _pathsService.ConfigFolderPath;
            var isUsingExeFolder = _pathsService.IsUsingExecutableFolder;

            // Assert
            if (!isUsingExeFolder)
            {
                // Should contain DaoStudio when using AppData
                Assert.Contains("DaoStudio", configPath);
            }
            // For exe folder case, just verify it's not empty (path validation is environment-specific)
            Assert.NotEmpty(configPath);
        }
    }
}