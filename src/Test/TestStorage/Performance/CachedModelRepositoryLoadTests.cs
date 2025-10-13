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
    /// Load tests for ICachedModelRepository focusing on performance under high load conditions
    /// </summary>
    public class CachedModelRepositoryLoadTests : BaseLoadTest
    {
        private ICachedModelRepository? _cachedModelRepository;
        private IAPIProviderRepository? _apiProviderRepository;

        public CachedModelRepositoryLoadTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<ICachedModelRepository> GetCachedModelRepositoryAsync()
        {
            if (_cachedModelRepository == null)
            {
                await InitializeStorageAsync();
                _cachedModelRepository = await StorageFactory!.GetCachedModelRepositoryAsync();
            }
            return _cachedModelRepository;
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
        public async Task BulkCachedModelCreationTest_Small()
        {
            var modelRepository = await GetCachedModelRepositoryAsync();
            var providerRepository = await GetAPIProviderRepositoryAsync();

            // Create providers first
            var providers = TestDataGenerator.GenerateAPIProviders(5);
            var createdProviders = new List<APIProvider>();
            foreach (var provider in providers)
            {
                var created = await providerRepository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            var testModels = TestDataGenerator.GenerateCachedModels(
                TestDataGenerator.Scenarios.Small.CachedModelsCount, 
                createdProviders.Select(p => p.Id).ToList());

            await RunBulkOperationStressTestAsync<CachedModel>(
                "Bulk Create Cached Models (Small)",
                async () =>
                {
                    var results = new List<CachedModel>();
                    foreach (var model in testModels)
                    {
                        var created = await modelRepository.CreateModelAsync(model);
                        results.Add(created);
                    }
                    return results;
                },
                testModels.Count);

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkCachedModelCreationTest_Medium()
        {
            var modelRepository = await GetCachedModelRepositoryAsync();
            var providerRepository = await GetAPIProviderRepositoryAsync();

            // Create providers
            var providers = TestDataGenerator.GenerateAPIProviders(15);
            var createdProviders = new List<APIProvider>();
            foreach (var provider in providers)
            {
                var created = await providerRepository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            var testModels = TestDataGenerator.GenerateCachedModels(
                TestDataGenerator.Scenarios.Medium.CachedModelsCount, 
                createdProviders.Select(p => p.Id).ToList());

            var result = await RunBulkOperationStressTestAsync<CachedModel>(
                "Bulk Create Cached Models (Medium)",
                async () =>
                {
                    var results = new List<CachedModel>();
                    foreach (var model in testModels)
                    {
                        var created = await modelRepository.CreateModelAsync(model);
                        results.Add(created);
                    }
                    return results;
                },
                testModels.Count);

            // Assert performance requirements - should create 1000+ models in under 20 seconds
            AssertPerformanceRequirements(result, TimeSpan.FromSeconds(20));
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkCreateModelsAsyncPerformanceTest()
        {
            var modelRepository = await GetCachedModelRepositoryAsync();
            var providerRepository = await GetAPIProviderRepositoryAsync();

            // Create providers
            var providers = TestDataGenerator.GenerateAPIProviders(8);
            var createdProviders = new List<APIProvider>();
            foreach (var provider in providers)
            {
                var created = await providerRepository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            var testModels = TestDataGenerator.GenerateCachedModels(600, createdProviders.Select(p => p.Id).ToList());

            // Test bulk creation performance using the batch method
            var result = await MeasurePerformanceAsync(
                "Bulk Create Models Async (Batch)",
                async () =>
                {
                    var createdCount = await modelRepository.CreateModelsAsync(testModels);
                    return createdCount;
                });

            Assert.True(result.Success);
            var createdCount = (int)result.Result!;
            Assert.Equal(testModels.Count, createdCount);

            // Bulk creation should be more efficient than individual creation
            Assert.True(result.Duration < TimeSpan.FromSeconds(10),
                $"Bulk creation took {result.Duration.TotalSeconds:F2}s, expected < 10s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentProviderBasedAccessTest()
        {
            var modelRepository = await GetCachedModelRepositoryAsync();
            var providerRepository = await GetAPIProviderRepositoryAsync();

            // Create providers
            var providers = TestDataGenerator.GenerateAPIProviders(10);
            var createdProviders = new List<APIProvider>();
            foreach (var provider in providers)
            {
                var created = await providerRepository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            // Create models for each provider
            foreach (var provider in createdProviders)
            {
                var models = TestDataGenerator.GenerateCachedModels(30, new List<long> { provider.Id });
                foreach (var model in models)
                {
                    await modelRepository.CreateModelAsync(model);
                }
            }

            // Create concurrent provider-based read operations
            var readOperations = createdProviders.Select<APIProvider, Func<Task<object>>>(provider =>
                async () => 
                {
                    var models = await modelRepository.GetModelsByProviderIdAsync(provider.Id);
                    return (object)models.ToList();
                }).ToList();

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Provider-Based Access",
                readOperations,
                maxConcurrency: 8);

            // Provider-based queries should be efficient
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 30.0, maxFailureRate: 0.02);

            // Verify results
            foreach (var opResult in result.IndividualResults.Where(r => r.Success))
            {
                var models = (List<CachedModel>)opResult.Result!;
                Assert.True(models.Count >= 30, "Each provider should have at least 30 models");
            }

            Output.WriteLine($"Tested concurrent access across {createdProviders.Count} providers");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ModelCachingScenarioStressTest()
        {
            var modelRepository = await GetCachedModelRepositoryAsync();
            var providerRepository = await GetAPIProviderRepositoryAsync();

            // Create providers with different types
            var providers = TestDataGenerator.GenerateAPIProviders(6);
            var createdProviders = new List<APIProvider>();
            for (int i = 0; i < providers.Count; i++)
            {
                providers[i].ProviderType = i % 3; // 3 different provider types
                var created = await providerRepository.CreateProviderAsync(providers[i]);
                createdProviders.Add(created);
            }

            // Create models with specific catalog distributions
            var catalogs = new[] { "OpenAI", "Anthropic", "Google", "Local", "Custom" };
            var allModels = new List<CachedModel>();
            
            foreach (var provider in createdProviders)
            {
                foreach (var catalog in catalogs)
                {
                    var models = TestDataGenerator.GenerateCachedModels(20, new List<long> { provider.Id });
                    foreach (var model in models)
                    {
                        model.Catalog = catalog;
                        model.ProviderType = provider.ProviderType; // Fix: Ensure ProviderType matches the provider
                        var created = await modelRepository.CreateModelAsync(model);
                        allModels.Add(created);
                    }
                }
            }

            // Test criteria-based caching queries (simulating model cache lookups)
            var criteriaOperations = new List<Func<Task<object>>>();
            
            foreach (var provider in createdProviders)
            {
                foreach (var catalog in catalogs)
                {
                    criteriaOperations.Add(async () =>
                    {
                        var models = await modelRepository.GetModelsByCriteriaAsync(
                            provider.Id, provider.ProviderType, catalog);
                        return (object)models.ToList();
                    });
                }
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Model Caching Scenario Stress Test",
                criteriaOperations,
                maxConcurrency: 12);

            // Caching queries should be very fast
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 40.0, maxFailureRate: 0.02);

            // Verify criteria results
            foreach (var opResult in result.IndividualResults.Where(r => r.Success))
            {
                var models = (List<CachedModel>)opResult.Result!;
                Assert.True(models.Count == 20, $"Each criteria query should return exactly 20 models, got {models.Count}");
            }

            Output.WriteLine($"Executed {criteriaOperations.Count} criteria-based cache queries");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task CacheInvalidationPerformanceTest()
        {
            var modelRepository = await GetCachedModelRepositoryAsync();
            var providerRepository = await GetAPIProviderRepositoryAsync();

            // Create providers
            var providers = TestDataGenerator.GenerateAPIProviders(8);
            var createdProviders = new List<APIProvider>();
            foreach (var provider in providers)
            {
                var created = await providerRepository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            // Create models for each provider
            var allModels = new List<CachedModel>();
            foreach (var provider in createdProviders)
            {
                var models = TestDataGenerator.GenerateCachedModels(75, new List<long> { provider.Id });
                foreach (var model in models)
                {
                    var created = await modelRepository.CreateModelAsync(model);
                    allModels.Add(created);
                }
            }

            // Test cache invalidation (bulk deletion by provider)
            var invalidationOperations = createdProviders.Take(5).Select<APIProvider, Func<Task<object>>>(provider =>
                async () =>
                {
                    var deletedCount = await modelRepository.DeleteModelsByProviderIdAsync(provider.Id);
                    return (object)deletedCount;
                }).ToList();

            var result = await MeasureConcurrentPerformanceAsync(
                "Cache Invalidation Performance Test",
                invalidationOperations,
                maxConcurrency: 3); // Lower concurrency for deletion operations

            // Cache invalidation should be efficient
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 8.0, maxFailureRate: 0.02);

            // Verify invalidation results
            foreach (var opResult in result.IndividualResults.Where(r => r.Success))
            {
                var deletedCount = (int)opResult.Result!;
                Assert.Equal(75, deletedCount);
            }

            // Verify remaining models
            var remainingModels = await modelRepository.GetAllModelsAsync();
            var remainingCount = remainingModels.Count();
            
            Output.WriteLine($"Invalidated models for 5 providers, {remainingCount} models remaining");
            Assert.True(remainingCount >= 225, "Should have models remaining for non-invalidated providers");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task LargeModelDataHandlingTest()
        {
            var modelRepository = await GetCachedModelRepositoryAsync();
            var providerRepository = await GetAPIProviderRepositoryAsync();

            // Create providers
            var providers = TestDataGenerator.GenerateAPIProviders(3);
            var createdProviders = new List<APIProvider>();
            foreach (var provider in providers)
            {
                var created = await providerRepository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            // Create models with large data
            var testModels = TestDataGenerator.GenerateCachedModels(40, createdProviders.Select(p => p.Id).ToList());
            
            // Add large model data and extensive parameters
            foreach (var model in testModels)
            {
                model.Name = TestDataGenerator.GenerateRandomString(200, 1000); // Large names
                model.ModelId = TestDataGenerator.GenerateRandomString(50, 200); // Large model IDs
                
                // Add many parameters
                for (int i = 0; i < 100; i++)
                {
                    model.Parameters[$"LargeParameter{i}"] = TestDataGenerator.GenerateRandomString(500, 2000);
                }
            }

            var result = await MeasurePerformanceAsync(
                "Large Model Data Creation",
                async () =>
                {
                    var results = new List<CachedModel>();
                    foreach (var model in testModels)
                    {
                        var created = await modelRepository.CreateModelAsync(model);
                        results.Add(created);
                    }
                    return results;
                });

            Assert.True(result.Success);
            var createdModels = (List<CachedModel>)result.Result!;
            Assert.Equal(testModels.Count, createdModels.Count);

            // Test retrieval performance with large data
            var retrievalResult = await MeasurePerformanceAsync(
                "Large Model Data Retrieval",
                async () =>
                {
                    var allModels = await modelRepository.GetAllModelsAsync();
                    return allModels.ToList();
                });

            Assert.True(retrievalResult.Success);
            var retrievedModels = (List<CachedModel>)retrievalResult.Result!;
            Assert.True(retrievedModels.Count >= testModels.Count);
            Assert.All(retrievedModels.Take(testModels.Count), 
                m => Assert.True(m.Parameters.Count >= 100, "Should have many parameters"));

            // Large data operations should complete in reasonable time
            Assert.True(result.Duration < TimeSpan.FromSeconds(15),
                $"Large model creation took {result.Duration.TotalSeconds:F2}s, expected < 15s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkGetAllModelsPerformanceTest()
        {
            var modelRepository = await GetCachedModelRepositoryAsync();
            var providerRepository = await GetAPIProviderRepositoryAsync();

            // Create providers
            var providers = TestDataGenerator.GenerateAPIProviders(12);
            var createdProviders = new List<APIProvider>();
            foreach (var provider in providers)
            {
                var created = await providerRepository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            // Create large number of models
            var totalModels = 0;
            foreach (var provider in createdProviders)
            {
                var models = TestDataGenerator.GenerateCachedModels(100, new List<long> { provider.Id });
                foreach (var model in models)
                {
                    await modelRepository.CreateModelAsync(model);
                    totalModels++;
                }
            }

            // Test bulk retrieval performance
            var result = await MeasurePerformanceAsync(
                "Bulk Get All Models",
                async () =>
                {
                    var allModels = await modelRepository.GetAllModelsAsync();
                    return allModels.ToList();
                });

            Assert.True(result.Success);
            var allModels = (List<CachedModel>)result.Result!;
            Assert.Equal(totalModels, allModels.Count);

            // Should be able to retrieve 1200+ models quickly
            Assert.True(result.Duration < TimeSpan.FromSeconds(8), 
                $"Bulk model retrieval took {result.Duration.TotalSeconds:F2}s, expected < 8s");

            Output.WriteLine($"Retrieved {allModels.Count} models from {createdProviders.Count} providers");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentModelUpdatesStressTest()
        {
            var modelRepository = await GetCachedModelRepositoryAsync();
            var providerRepository = await GetAPIProviderRepositoryAsync();

            // Create providers
            var providers = TestDataGenerator.GenerateAPIProviders(5);
            var createdProviders = new List<APIProvider>();
            foreach (var provider in providers)
            {
                var created = await providerRepository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }

            // Create models to update
            var allModels = new List<CachedModel>();
            foreach (var provider in createdProviders)
            {
                var models = TestDataGenerator.GenerateCachedModels(60, new List<long> { provider.Id });
                foreach (var model in models)
                {
                    var created = await modelRepository.CreateModelAsync(model);
                    allModels.Add(created);
                }
            }

            // Create concurrent update operations
            var updateOperations = new List<Func<Task<object>>>();

            foreach (var model in allModels.Take(200)) // Update first 200 models
            {
                // Name and model ID update
                var updatedModel = model;
                updatedModel.Name = TestDataGenerator.GenerateRandomString(100, 500) + " [UPDATED]";
                updatedModel.ModelId = TestDataGenerator.GenerateRandomString(50, 200) + "_updated";
                
                // Add new parameters
                for (int i = 0; i < 10; i++)
                {
                    updatedModel.Parameters[$"UpdatedParam{i}"] = TestDataGenerator.GenerateRandomString(100, 300);
                }
                
                updateOperations.Add(async () => (object)(await modelRepository.SaveModelAsync(updatedModel)));
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Model Updates Stress Test",
                updateOperations,
                maxConcurrency: 10);

            // Should handle concurrent updates efficiently
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 25.0, maxFailureRate: 0.05);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task MemoryUsageUnderModelLoadTest()
        {
            var modelRepository = await GetCachedModelRepositoryAsync();
            var providerRepository = await GetAPIProviderRepositoryAsync();

            // Create providers
            var providers = TestDataGenerator.GenerateAPIProviders(4);
            var createdProviders = new List<APIProvider>();
            foreach (var provider in providers)
            {
                var created = await providerRepository.CreateProviderAsync(provider);
                createdProviders.Add(created);
            }
            
            // Test with progressively larger datasets
            var smallModels = TestDataGenerator.GenerateCachedModels(150, createdProviders.Select(p => p.Id).ToList());
            var mediumModels = TestDataGenerator.GenerateCachedModels(200, createdProviders.Select(p => p.Id).ToList());
            var largeModels = TestDataGenerator.GenerateCachedModels(100, createdProviders.Select(p => p.Id).ToList());

            // Add extensive parameters to large models
            foreach (var model in largeModels)
            {
                for (int i = 0; i < 200; i++)
                {
                    model.Parameters[$"Parameter{i}"] = TestDataGenerator.GenerateRandomString(200, 1000);
                }
            }

            var smallResult = await MeasurePerformanceAsync(
                "Memory Test - Small Models",
                async () =>
                {
                    foreach (var model in smallModels)
                    {
                        await modelRepository.CreateModelAsync(model);
                    }
                    return smallModels.Count;
                });

            var mediumResult = await MeasurePerformanceAsync(
                "Memory Test - Medium Models",
                async () =>
                {
                    foreach (var model in mediumModels)
                    {
                        await modelRepository.CreateModelAsync(model);
                    }
                    return mediumModels.Count;
                });

            var largeResult = await MeasurePerformanceAsync(
                "Memory Test - Large Models",
                async () =>
                {
                    foreach (var model in largeModels)
                    {
                        await modelRepository.CreateModelAsync(model);
                    }
                    return largeModels.Count;
                });

            // Memory usage should scale reasonably
            Assert.True(smallResult.MemoryUsed < 25 * 1024 * 1024, // Less than 25MB
                $"Small models used {smallResult.MemoryUsed / (1024 * 1024)}MB, expected < 25MB");
            
            Output.WriteLine($"Memory usage - Small: {smallResult.MemoryUsed / 1024}KB, " +
                           $"Medium: {mediumResult.MemoryUsed / 1024}KB, " +
                           $"Large: {largeResult.MemoryUsed / 1024}KB");

            await VerifyDatabaseIntegrityAsync();
        }
    }
}
