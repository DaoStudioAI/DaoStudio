using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using TestStorage.Performance;
using Xunit;
using Xunit.Abstractions;

namespace TestStorage.Performance
{
    /// <summary>
    /// Load tests for ISettingsRepository focusing on performance under high load conditions
    /// </summary>
    public class SettingsRepositoryLoadTests : BaseLoadTest
    {
        private ISettingsRepository? _settingsRepository;

        public SettingsRepositoryLoadTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<ISettingsRepository> GetSettingsRepositoryAsync()
        {
            if (_settingsRepository == null)
            {
                await InitializeStorageAsync();
                _settingsRepository = await StorageFactory!.GetSettingsRepositoryAsync();
            }
            return _settingsRepository;
        }

        [Fact]
        public async Task BulkSaveOperationsTest_Small()
        {
            var repository = await GetSettingsRepositoryAsync();
            var testSettings = TestDataGenerator.GenerateSettings(TestDataGenerator.Scenarios.Small.SettingsCount);

            await RunBulkOperationStressTestAsync<bool>(
                "Bulk Save Settings (Small)",
                async () =>
                {
                    var results = new List<bool>();
                    foreach (var setting in testSettings)
                    {
                        var result = await repository.SaveSettingsAsync(setting);
                        results.Add(result);
                    }
                    return results;
                },
                testSettings.Count);

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkSaveOperationsTest_Medium()
        {
            var repository = await GetSettingsRepositoryAsync();
            var testSettings = TestDataGenerator.GenerateSettings(TestDataGenerator.Scenarios.Medium.SettingsCount);

            var result = await RunBulkOperationStressTestAsync<bool>(
                "Bulk Save Settings (Medium)",
                async () =>
                {
                    var results = new List<bool>();
                    foreach (var setting in testSettings)
                    {
                        var saved = await repository.SaveSettingsAsync(setting);
                        results.Add(saved);
                    }
                    return results;
                },
                testSettings.Count);

            // Assert performance requirements - should save 1000 settings in under 30 seconds
            AssertPerformanceRequirements(result, TimeSpan.FromSeconds(30));
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentReadWriteOperationsTest()
        {
            var repository = await GetSettingsRepositoryAsync();
            var testSettings = TestDataGenerator.GenerateSettings(50); // Prepare test data

            // First, save some initial data
            foreach (var setting in testSettings.Take(25))
            {
                await repository.SaveSettingsAsync(setting);
            }

            // Create concurrent operations mix (reads and writes)
            var operations = new List<Func<Task<object>>>();

            // Add read operations
            for (int i = 0; i < 25; i++)
            {
                var appName = testSettings[i].ApplicationName;
                operations.Add(async () => await repository.GetSettingsAsync(appName) ?? new Settings());
            }

            // Add write operations
            for (int i = 25; i < testSettings.Count; i++)
            {
                var setting = testSettings[i];
                operations.Add(async () => (object)(await repository.SaveSettingsAsync(setting)));
            }

            // Add update operations
            for (int i = 0; i < 10; i++)
            {
                var setting = testSettings[i];
                setting.Properties["UpdatedProperty"] = $"Updated_{DateTime.UtcNow.Ticks}";
                operations.Add(async () => (object)(await repository.SaveSettingsAsync(setting)));
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Read/Write Operations",
                operations,
                maxConcurrency: 8);

            // Assert at least 10 operations per second with less than 5% failure rate
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 10.0, maxFailureRate: 0.05);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task LargePropertiesCollectionTest()
        {
            var repository = await GetSettingsRepositoryAsync();
            var testSettings = TestDataGenerator.GenerateSettings(10, propertiesPerSetting: 1000); // Large property collections

            var result = await MeasurePerformanceAsync(
                "Large Properties Collection Save",
                async () =>
                {
                    var results = new List<bool>();
                    foreach (var setting in testSettings)
                    {
                        var saved = await repository.SaveSettingsAsync(setting);
                        results.Add(saved);
                    }
                    return results;
                });

            Assert.True(result.Success);
            var savedResults = (List<bool>)result.Result!;
            Assert.All(savedResults, r => Assert.True(r));

            // Test retrieval performance with large properties
            var retrievalResult = await MeasurePerformanceAsync(
                "Large Properties Collection Retrieval",
                async () =>
                {
                    var results = new List<Settings?>();
                    foreach (var setting in testSettings)
                    {
                        var retrieved = await repository.GetSettingsAsync(setting.ApplicationName);
                        results.Add(retrieved);
                    }
                    return results;
                });

            Assert.True(retrievalResult.Success);
            var retrievedSettings = (List<Settings?>)retrievalResult.Result!;
            Assert.All(retrievedSettings, s => Assert.NotNull(s));
            Assert.All(retrievedSettings, s => Assert.Equal(1000, s!.Properties.Count));

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task RapidSequentialAccessTest()
        {
            var repository = await GetSettingsRepositoryAsync();
            var testSettings = TestDataGenerator.GenerateSettings(100);

            // First, save all settings
            foreach (var setting in testSettings)
            {
                await repository.SaveSettingsAsync(setting);
            }

            // Test rapid sequential reads
            var readResult = await MeasurePerformanceAsync(
                "Rapid Sequential Reads",
                async () =>
                {
                    var results = new List<Settings?>();
                    for (int iteration = 0; iteration < 5; iteration++) // 5 iterations
                    {
                        foreach (var setting in testSettings)
                        {
                            var retrieved = await repository.GetSettingsAsync(setting.ApplicationName);
                            results.Add(retrieved);
                        }
                    }
                    return results;
                });

            Assert.True(readResult.Success);
            var readResults = (List<Settings?>)readResult.Result!;
            Assert.Equal(testSettings.Count * 5, readResults.Count);
            Assert.All(readResults, s => Assert.NotNull(s));

            // Test rapid sequential updates
            var updateResult = await MeasurePerformanceAsync(
                "Rapid Sequential Updates",
                async () =>
                {
                    var results = new List<bool>();
                    foreach (var setting in testSettings)
                    {
                        setting.Properties["RapidUpdate"] = $"Updated_{DateTime.UtcNow.Ticks}";
                        setting.LastModified = DateTime.UtcNow;
                        var updated = await repository.SaveSettingsAsync(setting);
                        results.Add(updated);
                    }
                    return results;
                });

            Assert.True(updateResult.Success);
            var updateResults = (List<bool>)updateResult.Result!;
            Assert.Equal(testSettings.Count, updateResults.Count);
            Assert.All(updateResults, r => Assert.True(r));

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConnectionPoolingStressTest()
        {
            var repository = await GetSettingsRepositoryAsync();
            var testSettings = TestDataGenerator.GenerateSettings(200);

            // Create operations that will stress the connection pool
            var operations = testSettings.Select<Settings, Func<Task<object>>>(setting =>
                async () => (object)(await repository.SaveSettingsAsync(setting))).ToList();

            // Add concurrent read operations
            operations.AddRange(testSettings.Select<Settings, Func<Task<object>>>(setting =>
                async () => (object)(await repository.GetSettingsAsync(setting.ApplicationName) ?? new Settings())));

            var result = await MeasureConcurrentPerformanceAsync(
                "Connection Pool Stress Test",
                operations,
                maxConcurrency: 16); // High concurrency to stress connection pool

            // With connection pooling, we should achieve good throughput
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 20.0);
            
            Output.WriteLine($"Connection pool handled {result.OperationCount} operations with {result.OperationsPerSecond:F2} ops/sec");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task WALModePerformanceTest()
        {
            var repository = await GetSettingsRepositoryAsync();
            var testSettings = TestDataGenerator.GenerateSettings(100);

            // Test WAL mode benefits with concurrent read/write operations
            var writeOperations = testSettings.Take(50).Select<Settings, Func<Task<object>>>(setting =>
                async () => (object)(await repository.SaveSettingsAsync(setting))).ToList();

            var readOperations = testSettings.Skip(50).Select<Settings, Func<Task<object>>>(setting =>
                async () => (object)(await repository.GetSettingsAsync(setting.ApplicationName) ?? new Settings())).ToList();

            // Interleave read and write operations
            var interleavedOps = new List<Func<Task<object>>>();
            for (int i = 0; i < Math.Min(writeOperations.Count, readOperations.Count); i++)
            {
                interleavedOps.Add(writeOperations[i]);
                interleavedOps.Add(readOperations[i]);
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "WAL Mode Concurrent Read/Write",
                interleavedOps,
                maxConcurrency: 8);

            // WAL mode should allow good concurrency between readers and writers
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 15.0);
            
            Output.WriteLine("WAL mode test completed - readers and writers should not block each other significantly");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task MemoryUsageUnderLoadTest()
        {
            var repository = await GetSettingsRepositoryAsync();
            
            // Test with progressively larger datasets to monitor memory usage
            var smallSettings = TestDataGenerator.GenerateSettings(50, propertiesPerSetting: 10);
            var mediumSettings = TestDataGenerator.GenerateSettings(100, propertiesPerSetting: 50);
            var largeSettings = TestDataGenerator.GenerateSettings(50, propertiesPerSetting: 200);

            var smallResult = await MeasurePerformanceAsync(
                "Memory Test - Small Settings",
                async () =>
                {
                    foreach (var setting in smallSettings)
                    {
                        await repository.SaveSettingsAsync(setting);
                    }
                    return smallSettings.Count;
                });

            var mediumResult = await MeasurePerformanceAsync(
                "Memory Test - Medium Settings",
                async () =>
                {
                    foreach (var setting in mediumSettings)
                    {
                        await repository.SaveSettingsAsync(setting);
                    }
                    return mediumSettings.Count;
                });

            var largeResult = await MeasurePerformanceAsync(
                "Memory Test - Large Settings",
                async () =>
                {
                    foreach (var setting in largeSettings)
                    {
                        await repository.SaveSettingsAsync(setting);
                    }
                    return largeSettings.Count;
                });

            // Memory usage should not grow excessively
            Assert.True(smallResult.MemoryUsed < 50 * 1024 * 1024, // Less than 50MB
                $"Small settings used {smallResult.MemoryUsed / (1024 * 1024)}MB, expected < 50MB");
            
            Output.WriteLine($"Memory usage - Small: {smallResult.MemoryUsed / 1024}KB, " +
                           $"Medium: {mediumResult.MemoryUsed / 1024}KB, " +
                           $"Large: {largeResult.MemoryUsed / 1024}KB");

            await VerifyDatabaseIntegrityAsync();
        }
    }
}
