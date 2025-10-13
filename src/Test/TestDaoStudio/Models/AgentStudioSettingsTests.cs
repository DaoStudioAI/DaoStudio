using DaoStudio;
using DaoStudio.DBStorage.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace TestDaoStudio.Models
{
    /// <summary>
    /// Unit tests for DaoStudio.Settings class (wrapper around DBStorage.Models.Settings)
    /// Tests focus on the AutoResolveToolNameConflicts and NavigationIndex properties and their caching behavior.
    /// </summary>
    public class DaoStudioSettingsTests
    {
        private Settings CreateTestSettings()
        {
            var mockRepo = Mock.Of<ISettingsRepository>();
            var mockLogger = Mock.Of<ILogger<Settings>>();
            return new Settings(mockRepo, mockLogger);
        }

        [Fact]
        public void AutoResolveToolNameConflicts_CachesValue_AvoidingRepeatedDictionaryLookups()
        {
            // Arrange
            var settings = CreateTestSettings();
            var originalProperties = settings.Properties;
            
            // Set the property value directly in the dictionary to simulate persistence layer
            settings.Properties["AutoResolveToolNameConflicts"] = "true";

            // Act - Read the property multiple times
            var result1 = settings.AutoResolveToolNameConflicts;
            var result2 = settings.AutoResolveToolNameConflicts;
            var result3 = settings.AutoResolveToolNameConflicts;

            // Assert - All reads should return the same value
            result1.Should().BeTrue();
            result2.Should().BeTrue();
            result3.Should().BeTrue();

            // Verify that the property dictionary wasn't modified by the repeated reads
            settings.Properties.Should().BeSameAs(originalProperties);
            settings.Properties["AutoResolveToolNameConflicts"].Should().Be("true");
        }

        [Fact]
        public void AutoResolveToolNameConflicts_UpdatesCache_WhenValueChanges()
        {
            // Arrange
            var mockRepository = new Mock<ISettingsRepository>();
            var mockLogger = new Mock<ILogger<DaoStudio.Settings>>();
            var settings = new DaoStudio.Settings(mockRepository.Object, mockLogger.Object);
            
            // Set initial value
            settings.AutoResolveToolNameConflicts = true;

            // Act & Assert - First read after setting
            settings.AutoResolveToolNameConflicts.Should().BeTrue();

            // Change the value
            settings.AutoResolveToolNameConflicts = false;

            // Act & Assert - Read after change should return new value
            settings.AutoResolveToolNameConflicts.Should().BeFalse();
        }

        [Fact]
        public void AutoResolveToolNameConflicts_ReturnsDefaultTrue_WhenKeyNotFound()
        {
            // Arrange
            var mockRepository = new Mock<ISettingsRepository>();
            var mockLogger = new Mock<ILogger<DaoStudio.Settings>>();
            var settings = new DaoStudio.Settings(mockRepository.Object, mockLogger.Object);
            settings.Properties.Clear(); // Ensure the key is not present

            // Act
            var result = settings.AutoResolveToolNameConflicts;

            // Assert
            result.Should().BeTrue(); // Default value
        }

        [Fact]
        public void AutoResolveToolNameConflicts_ReturnsDefaultTrue_WhenValueIsInvalid()
        {
            // Arrange
            var mockRepository = new Mock<ISettingsRepository>();
            var mockLogger = new Mock<ILogger<DaoStudio.Settings>>();
            var settings = new DaoStudio.Settings(mockRepository.Object, mockLogger.Object);
            settings.Properties["AutoResolveToolNameConflicts"] = "invalid_value";

            // Act
            var result = settings.AutoResolveToolNameConflicts;

            // Assert
            result.Should().BeTrue(); // Default value when parsing fails
        }

        [Fact]
        public void AutoResolveToolNameConflicts_ReturnsFalse_WhenExplicitlySetToFalse()
        {
            // Arrange
            var mockRepository = new Mock<ISettingsRepository>();
            var mockLogger = new Mock<ILogger<DaoStudio.Settings>>();
            var settings = new DaoStudio.Settings(mockRepository.Object, mockLogger.Object);
            
            // Act
            settings.AutoResolveToolNameConflicts = false;
            var result = settings.AutoResolveToolNameConflicts;

            // Assert
            result.Should().BeFalse();
            settings.Properties["AutoResolveToolNameConflicts"].Should().Be("False");
        }

        [Fact]
        public void AutoResolveToolNameConflicts_PersistsCacheAcrossMultipleWrites()
        {
            // Arrange
            var mockRepository = new Mock<ISettingsRepository>();
            var mockLogger = new Mock<ILogger<DaoStudio.Settings>>();
            var settings = new DaoStudio.Settings(mockRepository.Object, mockLogger.Object);
            
            // Act - Multiple writes and reads
            settings.AutoResolveToolNameConflicts = true;
            var result1 = settings.AutoResolveToolNameConflicts;
            
            settings.AutoResolveToolNameConflicts = false;
            var result2 = settings.AutoResolveToolNameConflicts;
            
            settings.AutoResolveToolNameConflicts = true;
            var result3 = settings.AutoResolveToolNameConflicts;

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeFalse();
            result3.Should().BeTrue();
            settings.Properties["AutoResolveToolNameConflicts"].Should().Be("True");
        }

        #region NavigationIndex Tests

        [Fact]
        public void NavigationIndex_CachesValue_AvoidingRepeatedDictionaryLookups()
        {
            // Arrange
            var settings = CreateTestSettings();
            var originalProperties = settings.Properties;
            
            // Set the property value directly in the dictionary to simulate persistence layer
            settings.Properties["NavigationIndex"] = "2";

            // Act - Read the property multiple times
            var result1 = settings.NavigationIndex;
            var result2 = settings.NavigationIndex;
            var result3 = settings.NavigationIndex;

            // Assert - All reads should return the same value
            result1.Should().Be(2);
            result2.Should().Be(2);
            result3.Should().Be(2);

            // Verify that the property dictionary wasn't modified by the repeated reads
            settings.Properties.Should().BeSameAs(originalProperties);
            settings.Properties["NavigationIndex"].Should().Be("2");
        }

        [Fact]
        public void NavigationIndex_UpdatesCache_WhenValueChanges()
        {
            // Arrange
            var mockRepository = new Mock<ISettingsRepository>();
            var mockLogger = new Mock<ILogger<DaoStudio.Settings>>();
            var settings = new DaoStudio.Settings(mockRepository.Object, mockLogger.Object);
            
            // Set initial value
            settings.NavigationIndex = 1;

            // Act & Assert - First read after setting
            settings.NavigationIndex.Should().Be(1);

            // Change the value
            settings.NavigationIndex = 3;

            // Act & Assert - Read after change should return new value
            settings.NavigationIndex.Should().Be(3);
        }

        [Fact]
        public void NavigationIndex_ReturnsDefaultZero_WhenKeyNotFound()
        {
            // Arrange
            var mockRepository = new Mock<ISettingsRepository>();
            var mockLogger = new Mock<ILogger<DaoStudio.Settings>>();
            var settings = new DaoStudio.Settings(mockRepository.Object, mockLogger.Object);
            settings.Properties.Clear(); // Ensure the key is not present

            // Act
            var result = settings.NavigationIndex;

            // Assert
            result.Should().Be(0); // Default value
        }

        [Fact]
        public void NavigationIndex_ReturnsDefaultZero_WhenValueIsInvalid()
        {
            // Arrange
            var mockRepository = new Mock<ISettingsRepository>();
            var mockLogger = new Mock<ILogger<DaoStudio.Settings>>();
            var settings = new DaoStudio.Settings(mockRepository.Object, mockLogger.Object);
            settings.Properties["NavigationIndex"] = "invalid_number";

            // Act
            var result = settings.NavigationIndex;

            // Assert
            result.Should().Be(0); // Default value when parsing fails
        }

        [Fact]
        public void NavigationIndex_ReturnsCorrectValue_WhenExplicitlySet()
        {
            // Arrange
            var mockRepository = new Mock<ISettingsRepository>();
            var mockLogger = new Mock<ILogger<DaoStudio.Settings>>();
            var settings = new DaoStudio.Settings(mockRepository.Object, mockLogger.Object);
            
            // Act
            settings.NavigationIndex = 5;
            var result = settings.NavigationIndex;

            // Assert
            result.Should().Be(5);
            settings.Properties["NavigationIndex"].Should().Be("5");
        }

        [Fact]
        public void NavigationIndex_PersistsCacheAcrossMultipleWrites()
        {
            // Arrange
            var mockRepository = new Mock<ISettingsRepository>();
            var mockLogger = new Mock<ILogger<DaoStudio.Settings>>();
            var settings = new DaoStudio.Settings(mockRepository.Object, mockLogger.Object);
            
            // Act - Multiple writes and reads
            settings.NavigationIndex = 1;
            var result1 = settings.NavigationIndex;
            
            settings.NavigationIndex = 4;
            var result2 = settings.NavigationIndex;
            
            settings.NavigationIndex = 2;
            var result3 = settings.NavigationIndex;

            // Assert
            result1.Should().Be(1);
            result2.Should().Be(4);
            result3.Should().Be(2);
            settings.Properties["NavigationIndex"].Should().Be("2");
        }

        #endregion
    }
}