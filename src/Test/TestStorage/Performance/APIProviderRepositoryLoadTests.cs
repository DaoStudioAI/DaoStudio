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
    /// Load tests for IAPIProviderRepository focusing on performance under high load conditions
    /// </summary>
    public class APIProviderRepositoryLoadTests : BaseLoadTest
    {
        private IAPIProviderRepository? _apiProviderRepository;

        public APIProviderRepositoryLoadTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<IAPIProviderRepository> GetAPIProviderRepositoryAsync()
        {
            if (_apiProviderRepository == null)
            {
                await InitializeStorageAsync();
                _apiProviderRepository = await StorageFactory!.GetApiProviderRepositoryAsync();
            }
            return _apiProviderRepository;
        }

        [Fact]
        public async Task BulkProviderCreationTest_Small()
        {
            var repository = await GetAPIProviderRepositoryAsync();
            var testProviders = TestDataGenerator.GenerateAPIProviders(TestDataGenerator.Scenarios.Small.ProvidersCount);

            await RunBulkOperationStressTestAsync<APIProvider>(
                "Bulk Create Providers (Small)",
                async () =>
                {
                    var results = new List<APIProvider>();
                    foreach (var provider in testProviders)
                    {
                        var created = await repository.CreateProviderAsync(provider);
                        results.Add(created);
                    }
                    return results;
                },
                testProviders.Count);

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkProviderCreationTest_Medium()
        {
            var repository = await GetAPIProviderRepositoryAsync();
            var testProviders = TestDataGenerator.GenerateAPIProviders(TestDataGenerator.Scenarios.Medium.ProvidersCount);

            var result = await RunBulkOperationStressTestAsync<APIProvider>(
                "Bulk Create Providers (Medium)",
                async () =>
                {
                    var results = new List<APIProvider>();
                    foreach (var provider in testProviders)
                    {
                        var created = await repository.CreateProviderAsync(provider);
                        results.Add(created);
                    }
                    return results;
                },
                testProviders.Count);

            // Assert performance requirements - should create 200 providers in under 15 seconds
            AssertPerformanceRequirements(result, TimeSpan.FromSeconds(15));
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentEnabledDisabledUpdatesTest()
        {
            var repository = await GetAPIProviderRepositoryAsync();
            var testProviders = TestDataGenerator.GenerateAPIProviders(100);

            // First, create all providers
            var createdProviders = new List<APIProvider>();
            foreach (var provider in testProviders)
            {
                var created = await repository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            // Create concurrent operations to toggle enabled/disabled status
            var operations = new List<Func<Task<object>>>();

            foreach (var provider in createdProviders)
            {
                // Toggle enabled status
                var modifiedProvider = provider;
                modifiedProvider.IsEnabled = !modifiedProvider.IsEnabled;
                modifiedProvider.LastModified = DateTime.UtcNow;
                
                operations.Add(async () => (object)(await repository.SaveProviderAsync(modifiedProvider)));
                
                // Add concurrent read operation
                operations.Add(async () => (object)(await repository.GetProviderAsync(modifiedProvider.Id) ?? new APIProvider()));
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Enabled/Disabled Updates",
                operations,
                maxConcurrency: 10);

            // Assert good concurrency performance
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 25.0, maxFailureRate: 0.03);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task HighFrequencyProviderLookupsByTypeTest()
        {
            var repository = await GetAPIProviderRepositoryAsync();
            var testProviders = TestDataGenerator.GenerateAPIProviders(500);

            // Create providers with varied types
            var createdProviders = new List<APIProvider>();
            foreach (var provider in testProviders)
            {
                var created = await repository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            // Create high-frequency lookup operations
            var lookupOperations = new List<Func<Task<object>>>();

            // Add lookups by ID
            foreach (var provider in createdProviders.Take(100))
            {
                var providerId = provider.Id;
                lookupOperations.Add(async () => (object)(await repository.GetProviderAsync(providerId) ?? new APIProvider()));
            }

            // Add lookups by name
            foreach (var provider in createdProviders.Skip(100).Take(100))
            {
                var providerName = provider.Name;
                lookupOperations.Add(async () => (object)(await repository.GetProviderByNameAsync(providerName) ?? new APIProvider()));
            }

            // Add existence checks
            foreach (var provider in createdProviders.Skip(200).Take(100))
            {
                var providerName = provider.Name;
                lookupOperations.Add(async () => (object)repository.ProviderExistsByName(providerName));
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "High-Frequency Provider Lookups",
                lookupOperations,
                maxConcurrency: 12);

            // Lookups should be very fast with proper indexing
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 100.0, maxFailureRate: 0.01);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task LargeParameterDictionariesStressTest()
        {
            var repository = await GetAPIProviderRepositoryAsync();
            var testProviders = TestDataGenerator.GenerateAPIProviders(50, parametersPerProvider: 500); // Large parameter dictionaries

            var result = await MeasurePerformanceAsync(
                "Large Parameter Dictionaries Creation",
                async () =>
                {
                    var results = new List<APIProvider>();
                    foreach (var provider in testProviders)
                    {
                        var created = await repository.CreateProviderAsync(provider);
                        results.Add(created);
                    }
                    return results;
                });

            Assert.True(result.Success);
            var createdProviders = (List<APIProvider>)result.Result!;
            Assert.Equal(testProviders.Count, createdProviders.Count);

            // Test retrieval performance with large parameters
            var retrievalResult = await MeasurePerformanceAsync(
                "Large Parameter Dictionaries Retrieval",
                async () =>
                {
                    var results = new List<APIProvider?>();
                    foreach (var provider in createdProviders)
                    {
                        var retrieved = await repository.GetProviderAsync(provider.Id);
                        results.Add(retrieved);
                    }
                    return results;
                });

            Assert.True(retrievalResult.Success);
            var retrievedProviders = (List<APIProvider?>)retrievalResult.Result!;
            Assert.All(retrievedProviders, p => Assert.NotNull(p));
            Assert.All(retrievedProviders, p => Assert.Equal(500, p!.Parameters.Count));

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkGetAllProvidersPerformanceTest()
        {
            var repository = await GetAPIProviderRepositoryAsync();
            var testProviders = TestDataGenerator.GenerateAPIProviders(1000);

            // First, create all providers
            foreach (var provider in testProviders)
            {
                await repository.CreateProviderAsync(provider);
            }

            // Test bulk retrieval performance
            var result = await MeasurePerformanceAsync(
                "Bulk Get All Providers",
                async () =>
                {
                    var allProviders = await repository.GetAllProvidersAsync();
                    return allProviders.ToList();
                });

            Assert.True(result.Success);
            var allProviders = (List<APIProvider>)result.Result!;
            Assert.True(allProviders.Count >= testProviders.Count);

            // Should be able to retrieve 1000+ providers quickly
            Assert.True(result.Duration < TimeSpan.FromSeconds(5), 
                $"Bulk retrieval took {result.Duration.TotalSeconds:F2}s, expected < 5s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentProviderUpdatesStressTest()
        {
            var repository = await GetAPIProviderRepositoryAsync();
            var testProviders = TestDataGenerator.GenerateAPIProviders(200);

            // Create initial providers
            var createdProviders = new List<APIProvider>();
            foreach (var provider in testProviders)
            {
                var created = await repository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            // Create concurrent update operations
            var updateOperations = new List<Func<Task<object>>>();

            foreach (var provider in createdProviders)
            {
                // Multiple types of updates
                var provider1 = provider;
                provider1.ApiEndpoint = $"https://updated-{DateTime.UtcNow.Ticks}.example.com";
                provider1.LastModified = DateTime.UtcNow;
                updateOperations.Add(async () => (object)(await repository.SaveProviderAsync(provider1)));

                var provider2 = provider;
                provider2.Parameters["UpdatedParam"] = $"Updated_{DateTime.UtcNow.Ticks}";
                provider2.LastModified = DateTime.UtcNow;
                updateOperations.Add(async () => (object)(await repository.SaveProviderAsync(provider2)));

                var provider3 = provider;
                provider3.IsEnabled = !provider3.IsEnabled;
                provider3.LastModified = DateTime.UtcNow;
                updateOperations.Add(async () => (object)(await repository.SaveProviderAsync(provider3)));
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Provider Updates Stress Test",
                updateOperations,
                maxConcurrency: 16);

            // With proper indexing and WAL mode, should handle concurrent updates well
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 30.0, maxFailureRate: 0.05);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ProviderDeletionPerformanceTest()
        {
            var repository = await GetAPIProviderRepositoryAsync();
            var testProviders = TestDataGenerator.GenerateAPIProviders(300);

            // Create providers to delete
            var createdProviders = new List<APIProvider>();
            foreach (var provider in testProviders)
            {
                var created = await repository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            // Test concurrent deletion performance
            var deleteOperations = createdProviders.Select<APIProvider, Func<Task<object>>>(provider =>
                async () => (object)(await repository.DeleteProviderAsync(provider.Id))).ToList();

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Provider Deletions",
                deleteOperations,
                maxConcurrency: 8);

            // Deletions should be reasonably fast
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 20.0, maxFailureRate: 0.02);

            // Verify deletions worked
            var remainingProviders = await repository.GetAllProvidersAsync();
            var remainingCount = remainingProviders.Count();
            
            Output.WriteLine($"Remaining providers after deletion: {remainingCount}");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ProviderTypesIndexPerformanceTest()
        {
            var repository = await GetAPIProviderRepositoryAsync();
            var testProviders = TestDataGenerator.GenerateAPIProviders(800);

            // Create providers with various types
            var createdProviders = new List<APIProvider>();
            foreach (var provider in testProviders)
            {
                var created = await repository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            // Test filtering performance by provider type
            var result = await MeasurePerformanceAsync(
                "Provider Type Index Performance",
                async () =>
                {
                    var allProviders = await repository.GetAllProvidersAsync();
                    
                    // Simulate filtering by type (in a real scenario, this would be a database query)
                    var groupedByType = allProviders.GroupBy(p => p.ProviderType).ToDictionary(g => g.Key, g => g.ToList());
                    var enabledProviders = allProviders.Where(p => p.IsEnabled).ToList();
                    var recentProviders = allProviders.Where(p => p.LastModified > DateTime.UtcNow.AddDays(-7)).ToList();
                    
                    return new
                    {
                        Total = allProviders.Count(),
                        GroupedByType = groupedByType.Count,
                        Enabled = enabledProviders.Count,
                        Recent = recentProviders.Count
                    };
                });

            Assert.True(result.Success);
            
            // Complex filtering operations should complete quickly with proper indexing
            Assert.True(result.Duration < TimeSpan.FromSeconds(2),
                $"Provider type filtering took {result.Duration.TotalSeconds:F2}s, expected < 2s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task MemoryUsageUnderProviderLoadTest()
        {
            var repository = await GetAPIProviderRepositoryAsync();
            
            // Test with progressively larger datasets
            var smallProviders = TestDataGenerator.GenerateAPIProviders(100, parametersPerProvider: 10);
            var mediumProviders = TestDataGenerator.GenerateAPIProviders(200, parametersPerProvider: 50);
            var largeProviders = TestDataGenerator.GenerateAPIProviders(100, parametersPerProvider: 200);

            var smallResult = await MeasurePerformanceAsync(
                "Memory Test - Small Providers",
                async () =>
                {
                    foreach (var provider in smallProviders)
                    {
                        await repository.CreateProviderAsync(provider);
                    }
                    return smallProviders.Count;
                });

            var mediumResult = await MeasurePerformanceAsync(
                "Memory Test - Medium Providers",
                async () =>
                {
                    foreach (var provider in mediumProviders)
                    {
                        await repository.CreateProviderAsync(provider);
                    }
                    return mediumProviders.Count;
                });

            var largeResult = await MeasurePerformanceAsync(
                "Memory Test - Large Providers",
                async () =>
                {
                    foreach (var provider in largeProviders)
                    {
                        await repository.CreateProviderAsync(provider);
                    }
                    return largeProviders.Count;
                });

            // Memory usage should scale reasonably
            Assert.True(smallResult.MemoryUsed < 25 * 1024 * 1024, // Less than 25MB
                $"Small providers used {smallResult.MemoryUsed / (1024 * 1024)}MB, expected < 25MB");
            
            Output.WriteLine($"Memory usage - Small: {smallResult.MemoryUsed / 1024}KB, " +
                           $"Medium: {mediumResult.MemoryUsed / 1024}KB, " +
                           $"Large: {largeResult.MemoryUsed / 1024}KB");

            await VerifyDatabaseIntegrityAsync();
        }
    }
}
