using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Repositories;
using Xunit;

namespace Test.TestStorage
{
    /// <summary>
    /// Simple test to verify current DateTime behavior
    /// </summary>
    public class TestCurrentTimezoneBehavior : IDisposable
    {
        private readonly string _testDbPath;
        private readonly SqliteSettingsRepository _settingsRepository;

        public TestCurrentTimezoneBehavior()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_current_behavior_{Guid.NewGuid()}.db");
            _settingsRepository = new SqliteSettingsRepository(_testDbPath);
        }

        public void Dispose()
        {
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
        public async Task CurrentBehavior_WhatDateTimeKindIsReturned()
        {
            // Arrange
            var settings = new Settings
            {
                ApplicationName = "CurrentBehaviorTest",
                Version = 1,
                Properties = new Dictionary<string, string>()
            };

            // Act
            await _settingsRepository.SaveSettingsAsync(settings);
            var retrieved = await _settingsRepository.GetSettingsAsync("CurrentBehaviorTest");

            // Assert and Log
            Assert.NotNull(retrieved);
            
            // Log what we actually get
            var timeZoneInfo = TimeZoneInfo.Local;
            var utcOffset = timeZoneInfo.GetUtcOffset(DateTime.Now);
            
            // This will show in test output what the current behavior is
            // Verify the time is returned as local time with correct DateTimeKind
            Assert.Equal(DateTimeKind.Local, retrieved.LastModified.Kind);
            
            // Log what we get for debugging
            Assert.True(true, $"Retrieved LastModified: {retrieved.LastModified:yyyy-MM-dd HH:mm:ss.fff}, Kind: {retrieved.LastModified.Kind}, TimeZone: {timeZoneInfo.Id}, Offset: {utcOffset}");
            Assert.True(true, $"Current Local TimeZone: {timeZoneInfo.Id}");
            Assert.True(true, $"Current UTC Offset: {utcOffset}");
            Assert.True(true, $"Current UTC Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
            Assert.True(true, $"Current Local Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            
            // Show what the time would be if converted to local
            DateTime convertedToLocal;
            if (retrieved.LastModified.Kind == DateTimeKind.Utc)
            {
                convertedToLocal = TimeZoneInfo.ConvertTimeFromUtc(retrieved.LastModified, TimeZoneInfo.Local);
                Assert.True(true, $"If converted to Local: {convertedToLocal:yyyy-MM-dd HH:mm:ss.fff}");
            }
            else
            {
                Assert.True(true, "Time is already in local or unspecified kind");
            }
        }
    }
}
