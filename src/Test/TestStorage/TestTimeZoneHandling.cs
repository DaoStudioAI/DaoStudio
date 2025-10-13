using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Test.TestStorage
{
    /// <summary>
    /// Comprehensive tests for time zone handling across all entities.
    /// Ensures all times are stored as UTC (Windows file time) and retrieved in local time zone.
    /// Tests the conversion accuracy and time zone handling behavior.
    /// </summary>
    public class TestTimeZoneHandling : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISettingsRepository _settingsRepository;
        private readonly IAPIProviderRepository _apiProviderRepository;
        private readonly IPersonRepository _personRepository;
        private readonly ILlmToolRepository _llmToolRepository;
        private readonly ILlmPromptRepository _llmPromptRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly IMessageRepository _messageRepository;

        public TestTimeZoneHandling()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_timezone_{Guid.NewGuid()}.db");
            
            // Initialize all repositories
            _settingsRepository = new SqliteSettingsRepository(_testDbPath);
            _apiProviderRepository = new SqliteAPIProviderRepository(_testDbPath);
            _personRepository = new SqlitePersonRepository(_testDbPath);
            _llmToolRepository = new SqliteLlmToolRepository(_testDbPath);
            _llmPromptRepository = new SqliteLlmPromptRepository(_testDbPath);
            _sessionRepository = new SqliteSessionRepository(_testDbPath);
            _messageRepository = new SqliteMessageRepository(_testDbPath);
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

        #region Settings Time Zone Tests

        [Fact]
        public async Task Settings_LastModified_StoredAsUtcRetrievedAsLocal()
        {
            // Arrange
            var beforeSave = DateTime.UtcNow;
            var settings = new Settings
            {
                ApplicationName = "TimeZoneTestApp",
                Version = 1,
                Properties = new Dictionary<string, string> { { "Key", "Value" } },
                Theme = 0
            };

            // Act
            await _settingsRepository.SaveSettingsAsync(settings);
            var afterSave = DateTime.UtcNow;
            var retrieved = await _settingsRepository.GetSettingsAsync("TimeZoneTestApp");

            // Assert
            Assert.NotNull(retrieved);
            
            // Verify the time is stored correctly in database as Windows file time (UTC)
            var storedUtcTime = await GetTimeFromDatabase("Settings", "LastModified", "ApplicationName", "'TimeZoneTestApp'");
            
            // The stored time should be between beforeSave and afterSave (in UTC)
            Assert.True(storedUtcTime >= beforeSave && storedUtcTime <= afterSave,
                $"Stored UTC time {storedUtcTime:yyyy-MM-dd HH:mm:ss.fff} should be between {beforeSave:yyyy-MM-dd HH:mm:ss.fff} and {afterSave:yyyy-MM-dd HH:mm:ss.fff}");
            
            // Verify retrieved time is the stored UTC time converted to local time
            var expectedLocalTime = TimeZoneInfo.ConvertTimeFromUtc(storedUtcTime, TimeZoneInfo.Local);
            AssertTimesEqual(expectedLocalTime, retrieved.LastModified);
            
            // Verify the retrieved time has the correct DateTimeKind
            Assert.Equal(DateTimeKind.Local, retrieved.LastModified.Kind);
        }

        [Fact]
        public async Task Settings_LastModified_AccuracyWithinTolerance()
        {
            // Test that time storage and retrieval is accurate within acceptable tolerance
            var settings = new Settings
            {
                ApplicationName = "AccuracyTest",
                Version = 1,
                Properties = new Dictionary<string, string>()
            };

            var justBeforeSave = DateTime.UtcNow;
            await _settingsRepository.SaveSettingsAsync(settings);
            var justAfterSave = DateTime.UtcNow;
            
            var retrieved = await _settingsRepository.GetSettingsAsync("AccuracyTest");
            Assert.NotNull(retrieved);
            
            // The retrieved time (in local) should correspond to a UTC time within our save window
            var retrievedAsUtc = TimeZoneInfo.ConvertTimeToUtc(retrieved.LastModified, TimeZoneInfo.Local);
            
            Assert.True(retrievedAsUtc >= justBeforeSave && retrievedAsUtc <= justAfterSave,
                $"Retrieved time converted to UTC ({retrievedAsUtc:yyyy-MM-dd HH:mm:ss.fff}) should be within save window " +
                $"({justBeforeSave:yyyy-MM-dd HH:mm:ss.fff} to {justAfterSave:yyyy-MM-dd HH:mm:ss.fff})");
        }

        #endregion

        #region APIProvider Time Zone Tests

        [Fact]
        public async Task ApiProvider_CreatedAtAndLastModified_StoredAsUtcRetrievedAsLocal()
        {
            // Arrange
            var beforeSave = DateTime.UtcNow;
            var provider = new APIProvider
            {
                Name = "TimeZoneTestProvider",
                ApiEndpoint = "https://test.api.com",
                IsEnabled = true
            };

            // Act
            var savedProvider = await _apiProviderRepository.CreateProviderAsync(provider);
            var afterSave = DateTime.UtcNow;
            var retrieved = await _apiProviderRepository.GetProviderAsync(savedProvider.Id);

            // Assert
            Assert.NotNull(retrieved);
            
            // Verify both times are stored as UTC in database
            var storedCreatedUtc = await GetTimeFromDatabase("APIProviders", "CreatedAt", "Id", savedProvider.Id.ToString());
            var storedModifiedUtc = await GetTimeFromDatabase("APIProviders", "LastModified", "Id", savedProvider.Id.ToString());
            
            // Both times should be within our save window
            Assert.True(storedCreatedUtc >= beforeSave && storedCreatedUtc <= afterSave);
            Assert.True(storedModifiedUtc >= beforeSave && storedModifiedUtc <= afterSave);
            
            // For a new provider, CreatedAt and LastModified should be the same (or very close)
            var timeDifference = Math.Abs((storedCreatedUtc - storedModifiedUtc).TotalMilliseconds);
            Assert.True(timeDifference < 100, $"CreatedAt and LastModified should be very close for new provider. Difference: {timeDifference}ms");
            
            // Verify retrieved times match expected local times
            var expectedCreatedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedCreatedUtc, TimeZoneInfo.Local);
            var expectedModifiedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedModifiedUtc, TimeZoneInfo.Local);
            
            AssertTimesEqual(expectedCreatedLocal, retrieved.CreatedAt);
            AssertTimesEqual(expectedModifiedLocal, retrieved.LastModified);
            
            // Verify DateTimeKind is Local
            Assert.Equal(DateTimeKind.Local, retrieved.CreatedAt.Kind);
            Assert.Equal(DateTimeKind.Local, retrieved.LastModified.Kind);
        }

        [Fact]
        public async Task ApiProvider_UpdatePreservesCreatedAtAndUpdatesLastModified()
        {
            // Arrange
            var provider = new APIProvider
            {
                Name = "UpdateTestProvider",
                ApiEndpoint = "https://original.api.com"
            };

            var savedProvider = await _apiProviderRepository.CreateProviderAsync(provider);
            var originalCreatedUtc = await GetTimeFromDatabase("APIProviders", "CreatedAt", "Id", savedProvider.Id.ToString());
            
            // Wait a small amount to ensure different timestamps
            await Task.Delay(50);
            
            // Update the provider
            var beforeUpdate = DateTime.UtcNow;
            savedProvider.ApiEndpoint = "https://updated.api.com";
            await _apiProviderRepository.SaveProviderAsync(savedProvider);
            var afterUpdate = DateTime.UtcNow;

            // Act
            var retrieved = await _apiProviderRepository.GetProviderAsync(savedProvider.Id);

            // Assert
            Assert.NotNull(retrieved);
            
            var finalCreatedUtc = await GetTimeFromDatabase("APIProviders", "CreatedAt", "Id", savedProvider.Id.ToString());
            var finalModifiedUtc = await GetTimeFromDatabase("APIProviders", "LastModified", "Id", savedProvider.Id.ToString());
            
            // CreatedAt should remain unchanged
            AssertTimesEqual(originalCreatedUtc, finalCreatedUtc, 1.0);
            
            // LastModified should be updated to within the update window
            Assert.True(finalModifiedUtc >= beforeUpdate && finalModifiedUtc <= afterUpdate,
                $"LastModified ({finalModifiedUtc:yyyy-MM-dd HH:mm:ss.fff}) should be within update window " +
                $"({beforeUpdate:yyyy-MM-dd HH:mm:ss.fff} to {afterUpdate:yyyy-MM-dd HH:mm:ss.fff})");
                
            // CreatedAt and LastModified should now be different
            var timeDifference = Math.Abs((finalCreatedUtc - finalModifiedUtc).TotalMilliseconds);
            Assert.True(timeDifference >= 40, $"CreatedAt and LastModified should be different after update. Difference: {timeDifference}ms");
        }

        #endregion

        #region Person Time Zone Tests

        [Fact]
        public async Task Person_CreatedAtAndLastModified_StoredAsUtcRetrievedAsLocal()
        {
            // Arrange
            var beforeSave = DateTime.UtcNow;
            var person = new Person
            {
                Name = "TimeZoneTestPerson",
                ProviderName = "TestProvider",
                ModelId = "test-model-1",
                IsEnabled = true
            };

            // Act
            var savedPerson = await _personRepository.CreatePersonAsync(person);
            var afterSave = DateTime.UtcNow;
            var retrieved = await _personRepository.GetPersonAsync(savedPerson.Id);

            // Assert
            Assert.NotNull(retrieved);
            
            // Verify times are stored as UTC in database
            var storedCreatedUtc = await GetTimeFromDatabase("Persons", "CreatedAt", "Id", savedPerson.Id.ToString());
            var storedModifiedUtc = await GetTimeFromDatabase("Persons", "LastModified", "Id", savedPerson.Id.ToString());
            
            // Times should be within our save window
            Assert.True(storedCreatedUtc >= beforeSave && storedCreatedUtc <= afterSave);
            Assert.True(storedModifiedUtc >= beforeSave && storedModifiedUtc <= afterSave);
            
            // Verify retrieved times match expected local times
            var expectedCreatedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedCreatedUtc, TimeZoneInfo.Local);
            var expectedModifiedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedModifiedUtc, TimeZoneInfo.Local);
            
            AssertTimesEqual(expectedCreatedLocal, retrieved.CreatedAt);
            AssertTimesEqual(expectedModifiedLocal, retrieved.LastModified);
        }

        #endregion

        #region LlmTool Time Zone Tests

        [Fact]
        public async Task LlmTool_CreatedAtAndLastModified_StoredAsUtcRetrievedAsLocal()
        {
            // Arrange
            var beforeSave = DateTime.UtcNow;
            var tool = new LlmTool
            {
                StaticId = "timezone.test.tool",
                Name = "TimeZoneTestTool",
                Description = "Tool for testing time zones",
                ToolConfig = "{}",
                IsEnabled = true
            };

            // Act
            var savedTool = await _llmToolRepository.CreateToolAsync(tool);
            var afterSave = DateTime.UtcNow;
            var retrieved = await _llmToolRepository.GetToolAsync(savedTool.Id);

            // Assert
            Assert.NotNull(retrieved);
            
            // Verify times are stored as UTC in database
            var storedCreatedUtc = await GetTimeFromDatabase("LlmTools", "CreatedAt", "Id", savedTool.Id.ToString());
            var storedModifiedUtc = await GetTimeFromDatabase("LlmTools", "LastModified", "Id", savedTool.Id.ToString());
            
            // Times should be within our save window
            Assert.True(storedCreatedUtc >= beforeSave && storedCreatedUtc <= afterSave);
            Assert.True(storedModifiedUtc >= beforeSave && storedModifiedUtc <= afterSave);
            
            // Verify retrieved times match expected local times
            var expectedCreatedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedCreatedUtc, TimeZoneInfo.Local);
            var expectedModifiedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedModifiedUtc, TimeZoneInfo.Local);
            
            AssertTimesEqual(expectedCreatedLocal, retrieved.CreatedAt);
            AssertTimesEqual(expectedModifiedLocal, retrieved.LastModified);
        }

        #endregion

        #region LlmPrompt Time Zone Tests

        [Fact]
        public async Task LlmPrompt_CreatedAtAndLastModified_StoredAsUtcRetrievedAsLocal()
        {
            // Arrange
            var beforeSave = DateTime.UtcNow;
            var prompt = new LlmPrompt
            {
                Name = "TimeZoneTestPrompt",
                Category = "Testing",
                Content = "This is a test prompt for time zone handling",
                IsEnabled = true
            };

            // Act
            var savedPrompt = await _llmPromptRepository.CreatePromptAsync(prompt);
            var afterSave = DateTime.UtcNow;
            var retrieved = await _llmPromptRepository.GetPromptAsync(savedPrompt.Id);

            // Assert
            Assert.NotNull(retrieved);
            
            // Verify times are stored as UTC in database
            var storedCreatedUtc = await GetTimeFromDatabase("LlmPrompts", "CreatedAt", "Id", savedPrompt.Id.ToString());
            var storedModifiedUtc = await GetTimeFromDatabase("LlmPrompts", "LastModified", "Id", savedPrompt.Id.ToString());
            
            // Times should be within our save window
            Assert.True(storedCreatedUtc >= beforeSave && storedCreatedUtc <= afterSave);
            Assert.True(storedModifiedUtc >= beforeSave && storedModifiedUtc <= afterSave);
            
            // Verify retrieved times match expected local times
            var expectedCreatedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedCreatedUtc, TimeZoneInfo.Local);
            var expectedModifiedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedModifiedUtc, TimeZoneInfo.Local);
            
            AssertTimesEqual(expectedCreatedLocal, retrieved.CreatedAt);
            AssertTimesEqual(expectedModifiedLocal, retrieved.LastModified);
        }

        #endregion

        #region Session Time Zone Tests

        [Fact]
        public async Task Session_CreatedAtAndLastModified_StoredAsUtcRetrievedAsLocal()
        {
            // Arrange
            var beforeSave = DateTime.UtcNow;
            var session = new Session
            {
                Title = "TimeZone Test Session",
                Description = "Session for testing time zone handling",
                PersonNames = new List<string> { "TestPerson" },
                ToolNames = new List<string> { "TestTool" }
            };

            // Act
            var savedSession = await _sessionRepository.CreateSessionAsync(session);
            var afterSave = DateTime.UtcNow;
            var retrieved = await _sessionRepository.GetSessionAsync(savedSession.Id);

            // Assert
            Assert.NotNull(retrieved);
            
            // Verify times are stored as UTC in database
            var storedCreatedUtc = await GetTimeFromDatabase("Sessions", "CreatedAt", "Id", savedSession.Id.ToString());
            var storedModifiedUtc = await GetTimeFromDatabase("Sessions", "LastModified", "Id", savedSession.Id.ToString());
            
            // Times should be within our save window
            Assert.True(storedCreatedUtc >= beforeSave && storedCreatedUtc <= afterSave);
            Assert.True(storedModifiedUtc >= beforeSave && storedModifiedUtc <= afterSave);
            
            // Verify retrieved times match expected local times
            var expectedCreatedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedCreatedUtc, TimeZoneInfo.Local);
            var expectedModifiedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedModifiedUtc, TimeZoneInfo.Local);
            
            AssertTimesEqual(expectedCreatedLocal, retrieved.CreatedAt);
            AssertTimesEqual(expectedModifiedLocal, retrieved.LastModified);
        }

        #endregion

        #region Message Time Zone Tests

        [Fact]
        public async Task Message_CreatedAtAndLastModified_StoredAsUtcRetrievedAsLocal()
        {
            // Arrange - First create a session
            var session = new Session
            {
                Title = "Message TimeZone Test Session",
                Description = "Session for message time zone testing"
            };
            var savedSession = await _sessionRepository.CreateSessionAsync(session);

            var beforeSave = DateTime.UtcNow;
            var message = new Message
            {
                SessionId = savedSession.Id,
                Content = "Test message for time zone handling",
                Role = 1, // User
                Type = 0 // Normal
            };

            // Act
            var savedMessage = await _messageRepository.CreateMessageAsync(message);
            var afterSave = DateTime.UtcNow;
            var retrieved = await _messageRepository.GetByIdAsync(savedMessage.Id);

            // Assert
            Assert.NotNull(retrieved);
            
            // Verify times are stored as UTC in database
            var storedCreatedUtc = await GetTimeFromDatabase("Messages", "CreatedAt", "Id", savedMessage.Id.ToString());
            var storedModifiedUtc = await GetTimeFromDatabase("Messages", "LastModified", "Id", savedMessage.Id.ToString());
            
            // Times should be within our save window
            Assert.True(storedCreatedUtc >= beforeSave && storedCreatedUtc <= afterSave);
            Assert.True(storedModifiedUtc >= beforeSave && storedModifiedUtc <= afterSave);
            
            // Verify retrieved times match expected local times
            var expectedCreatedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedCreatedUtc, TimeZoneInfo.Local);
            var expectedModifiedLocal = TimeZoneInfo.ConvertTimeFromUtc(storedModifiedUtc, TimeZoneInfo.Local);
            
            AssertTimesEqual(expectedCreatedLocal, retrieved.CreatedAt);
            AssertTimesEqual(expectedModifiedLocal, retrieved.LastModified);
        }

        #endregion

        #region Batch Operations Time Zone Tests

        [Fact]
        public async Task BatchOperations_PreserveTimeZoneConsistency()
        {
            // Test that batch operations (GetAll, etc.) maintain time zone consistency
            
            // Arrange - Create multiple entities
            var beforeSave = DateTime.UtcNow;
            
            var settings1 = new Settings { ApplicationName = "BatchTest_1" };
            var settings2 = new Settings { ApplicationName = "BatchTest_2" };
            var settings3 = new Settings { ApplicationName = "BatchTest_3" };

            await _settingsRepository.SaveSettingsAsync(settings1);
            await _settingsRepository.SaveSettingsAsync(settings2);
            await _settingsRepository.SaveSettingsAsync(settings3);
            
            var afterSave = DateTime.UtcNow;

            // Act
            var allSettings = await _settingsRepository.GetAllSettingsAsync();

            // Assert
            var batchTestSettings = allSettings.Where(s => s.ApplicationName.StartsWith("BatchTest_")).ToList();
            Assert.Equal(3, batchTestSettings.Count);
            
            foreach (var retrieved in batchTestSettings)
            {
                // Each retrieved time should be in local time zone
                Assert.Equal(DateTimeKind.Local, retrieved.LastModified.Kind);
                
                // When converted back to UTC, should be within our save window
                var retrievedAsUtc = TimeZoneInfo.ConvertTimeToUtc(retrieved.LastModified, TimeZoneInfo.Local);
                Assert.True(retrievedAsUtc >= beforeSave && retrievedAsUtc <= afterSave,
                    $"Retrieved time {retrieved.ApplicationName} converted to UTC ({retrievedAsUtc:yyyy-MM-dd HH:mm:ss.fff}) " +
                    $"should be within save window ({beforeSave:yyyy-MM-dd HH:mm:ss.fff} to {afterSave:yyyy-MM-dd HH:mm:ss.fff})");
            }
        }

        #endregion

        #region Time Zone Conversion Tests

        [Fact]
        public async Task TimeZoneConversion_ConsistentAcrossDifferentTimeZones()
        {
            // This test verifies that the conversion logic works correctly regardless of system time zone
            
            var settings = new Settings { ApplicationName = "TimeZoneConversionTest" };
            
            var beforeSave = DateTime.UtcNow;
            await _settingsRepository.SaveSettingsAsync(settings);
            var afterSave = DateTime.UtcNow;
            
            var retrieved = await _settingsRepository.GetSettingsAsync("TimeZoneConversionTest");
            Assert.NotNull(retrieved);
            
            // Get what's actually stored in the database (as UTC)
            var storedUtc = await GetTimeFromDatabase("Settings", "LastModified", "ApplicationName", "'TimeZoneConversionTest'");
            
            // Verify the stored UTC time is within our expected range
            Assert.True(storedUtc >= beforeSave && storedUtc <= afterSave);
            
            // Test conversion to different time zones
            var timeZones = new[]
            {
                TimeZoneInfo.Utc,
                TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"),
                TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"),
                TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time")
            };
            
            foreach (var timeZone in timeZones)
            {
                var convertedTime = TimeZoneInfo.ConvertTimeFromUtc(storedUtc, timeZone);
                
                // The converted time, when converted back to UTC, should equal the original stored time
                var backToUtc = TimeZoneInfo.ConvertTimeToUtc(convertedTime, timeZone);
                AssertTimesEqual(storedUtc, backToUtc, 1.0);
                
                // Log for verification (this will show in test output)
                Assert.True(true, $"UTC: {storedUtc:yyyy-MM-dd HH:mm:ss.fff}, " +
                                 $"{timeZone.Id}: {convertedTime:yyyy-MM-dd HH:mm:ss.fff}");
            }
            
            // The retrieved time should match the conversion to local time zone
            var expectedLocalTime = TimeZoneInfo.ConvertTimeFromUtc(storedUtc, TimeZoneInfo.Local);
            AssertTimesEqual(expectedLocalTime, retrieved.LastModified);
        }

        [Fact]
        public async Task TimeStorage_PrecisionTest()
        {
            // Test precision of time storage and retrieval
            var settings = new Settings { ApplicationName = "PrecisionTest" };

            var preciseTimeBeforeSave = DateTime.UtcNow;
            await _settingsRepository.SaveSettingsAsync(settings);
            var preciseTimeAfterSave = DateTime.UtcNow;
            
            var retrieved = await _settingsRepository.GetSettingsAsync("PrecisionTest");
            Assert.NotNull(retrieved);
            
            // Get the actual stored time from database
            var storedUtc = await GetTimeFromDatabase("Settings", "LastModified", "ApplicationName", "'PrecisionTest'");
            
            // Windows file time has 100-nanosecond precision, so we should get very close
            // to the original time when converting back and forth
            var fileTime = storedUtc.ToFileTimeUtc();
            var backFromFileTime = DateTime.FromFileTimeUtc(fileTime);
            
            // The round-trip conversion should be exact
            AssertTimesEqual(storedUtc, backFromFileTime, 0.1); // Allow 0.1ms tolerance for rounding
            
            // The stored time should be within our save window
            Assert.True(storedUtc >= preciseTimeBeforeSave && storedUtc <= preciseTimeAfterSave);
            
            // The precision should be better than 1 millisecond
            var retrievedAsUtc = TimeZoneInfo.ConvertTimeToUtc(retrieved.LastModified, TimeZoneInfo.Local);
            var timeDifference = Math.Abs((storedUtc - retrievedAsUtc).TotalMilliseconds);
            Assert.True(timeDifference < 1, $"Time precision should be better than 1ms, got {timeDifference}ms");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets a time field from the database and converts it from Windows file time to DateTime UTC
        /// </summary>
        private async Task<DateTime> GetTimeFromDatabase(string tableName, string timeColumnName, 
            string whereColumnName, string whereValue)
        {
            using var connection = new SqliteConnection($"Data Source={_testDbPath}");
            await connection.OpenAsync();
            
            var sql = $"SELECT {timeColumnName} FROM {tableName} WHERE {whereColumnName} = {whereValue}";
            using var command = new SqliteCommand(sql, connection);
            
            var result = await command.ExecuteScalarAsync();
            Assert.NotNull(result);
            
            var storedFileTime = Convert.ToInt64(result);
            return DateTime.FromFileTimeUtc(storedFileTime);
        }

        /// <summary>
        /// Asserts that two DateTime values are equal within a reasonable tolerance
        /// </summary>
        private static void AssertTimesEqual(DateTime expected, DateTime actual, double toleranceMs = 100.0)
        {
            var timeDifference = Math.Abs((expected - actual).TotalMilliseconds);
            Assert.True(timeDifference < toleranceMs, 
                $"Times do not match within tolerance. Expected: {expected:yyyy-MM-dd HH:mm:ss.fff} ({expected.Kind}), " +
                $"Actual: {actual:yyyy-MM-dd HH:mm:ss.fff} ({actual.Kind}), " +
                $"Difference: {timeDifference}ms (tolerance: {toleranceMs}ms)");
        }

        #endregion
    }
}
