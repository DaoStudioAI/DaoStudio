using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;

using Xunit;
using Xunit.Abstractions;

namespace PerformanceStorage
{
    /// <summary>
    /// Load tests for ILlmPromptRepository focusing on performance under high load conditions
    /// </summary>
    public class LlmPromptRepositoryLoadTests : BaseLoadTest
    {
        private ILlmPromptRepository? _llmPromptRepository;

        public LlmPromptRepositoryLoadTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<ILlmPromptRepository> GetLlmPromptRepositoryAsync()
        {
            if (_llmPromptRepository == null)
            {
                await InitializeStorageAsync();
                _llmPromptRepository = await StorageFactory!.GetLlmPromptRepositoryAsync();
            }
            return _llmPromptRepository;
        }

        [Fact]
        public async Task BulkPromptCreationTest_Small()
        {
            var repository = await GetLlmPromptRepositoryAsync();
            var testPrompts = TestDataGenerator.GenerateLlmPrompts(TestDataGenerator.Scenarios.Small.PromptsCount);

            await RunBulkOperationStressTestAsync<LlmPrompt>(
                "Bulk Create Prompts (Small)",
                async () =>
                {
                    var results = new List<LlmPrompt>();
                    foreach (var prompt in testPrompts)
                    {
                        var created = await repository.CreatePromptAsync(prompt);
                        results.Add(created);
                    }
                    return results;
                },
                testPrompts.Count);

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkPromptCreationTest_Medium()
        {
            var repository = await GetLlmPromptRepositoryAsync();
            var testPrompts = TestDataGenerator.GenerateLlmPrompts(TestDataGenerator.Scenarios.Medium.PromptsCount);

            var result = await RunBulkOperationStressTestAsync<LlmPrompt>(
                "Bulk Create Prompts (Medium)",
                async () =>
                {
                    var results = new List<LlmPrompt>();
                    foreach (var prompt in testPrompts)
                    {
                        var created = await repository.CreatePromptAsync(prompt);
                        results.Add(created);
                    }
                    return results;
                },
                testPrompts.Count);

            // Assert performance requirements - should create 1500 prompts in under 25 seconds
            AssertPerformanceRequirements(result, TimeSpan.FromSeconds(25));
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentCategoryBasedFilteringTest()
        {
            var repository = await GetLlmPromptRepositoryAsync();
            var testPrompts = TestDataGenerator.GenerateLlmPrompts(500);

            // Create all prompts
            var createdPrompts = new List<LlmPrompt>();
            foreach (var prompt in testPrompts)
            {
                var created = await repository.CreatePromptAsync(prompt);
                createdPrompts.Add(created);
            }

            // Get unique categories for testing
            var uniqueCategories = createdPrompts.Select(p => p.Category).Distinct().ToList();

            // Create concurrent category filtering operations
            var filterOperations = uniqueCategories.Select<string, Func<Task<object>>>(category =>
                async () => 
                {
                    var prompts = await repository.GetPromptsByCategoryAsync(category);
                    return (object)prompts.ToList();
                }).ToList();

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Category-Based Filtering",
                filterOperations,
                maxConcurrency: 8);

            // Category filtering should be efficient with proper indexing
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 20.0, maxFailureRate: 0.02);

            // Verify results
            foreach (var opResult in result.IndividualResults.Where(r => r.Success))
            {
                var prompts = (List<LlmPrompt>)opResult.Result!;
                Assert.True(prompts.Count > 0, "Each category should have at least one prompt");
                Assert.All(prompts, p => Assert.Contains(p.Category, uniqueCategories));
            }

            Output.WriteLine($"Tested filtering across {uniqueCategories.Count} different categories");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task LargeContentHandlingTest()
        {
            var repository = await GetLlmPromptRepositoryAsync();
            var testPrompts = TestDataGenerator.GenerateLlmPrompts(75);

            // Modify prompts to have very large content
            foreach (var prompt in testPrompts)
            {
                prompt.Content = TestDataGenerator.GenerateRandomString(50000, 200000); // 50-200KB content
                prompt.Name = TestDataGenerator.GenerateRandomString(100, 500); // Longer names
            }

            var result = await MeasurePerformanceAsync(
                "Large Content Creation",
                async () =>
                {
                    var results = new List<LlmPrompt>();
                    foreach (var prompt in testPrompts)
                    {
                        var created = await repository.CreatePromptAsync(prompt);
                        results.Add(created);
                    }
                    return results;
                });

            Assert.True(result.Success);
            var createdPrompts = (List<LlmPrompt>)result.Result!;
            Assert.Equal(testPrompts.Count, createdPrompts.Count);

            // Test retrieval performance with large content
            var retrievalResult = await MeasurePerformanceAsync(
                "Large Content Retrieval",
                async () =>
                {
                    var results = new List<LlmPrompt?>();
                    foreach (var prompt in createdPrompts)
                    {
                        var retrieved = await repository.GetPromptAsync(prompt.Id);
                        results.Add(retrieved);
                    }
                    return results;
                });

            Assert.True(retrievalResult.Success);
            var retrievedPrompts = (List<LlmPrompt?>)retrievalResult.Result!;
            Assert.All(retrievedPrompts, p => Assert.NotNull(p));
            Assert.All(retrievedPrompts, p => Assert.True(p!.Content.Length > 50000));

            // Large content operations should still complete in reasonable time
            Assert.True(result.Duration < TimeSpan.FromSeconds(20),
                $"Large content creation took {result.Duration.TotalSeconds:F2}s, expected < 20s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task RapidEnabledDisabledTogglesTest()
        {
            var repository = await GetLlmPromptRepositoryAsync();
            var testPrompts = TestDataGenerator.GenerateLlmPrompts(200);

            // First, create all prompts
            var createdPrompts = new List<LlmPrompt>();
            foreach (var prompt in testPrompts)
            {
                var created = await repository.CreatePromptAsync(prompt);
                createdPrompts.Add(created);
            }

            // Create operations to rapidly toggle enabled/disabled status
            var operations = new List<Func<Task<object>>>();

            foreach (var prompt in createdPrompts)
            {
                // Toggle enabled status multiple times
                var toggledPrompt = prompt;
                toggledPrompt.IsEnabled = !toggledPrompt.IsEnabled;
                toggledPrompt.LastModified = DateTime.UtcNow;
                
                operations.Add(async () => (object)(await repository.SavePromptAsync(toggledPrompt)));
                
                // Add concurrent read operation
                operations.Add(async () => (object)(await repository.GetPromptAsync(toggledPrompt.Id) ?? new LlmPrompt()));
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Rapid Enabled/Disabled Toggles",
                operations,
                maxConcurrency: 10);

            // Should handle rapid status changes efficiently
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 25.0, maxFailureRate: 0.03);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task CategoryIndexPerformanceStressTest()
        {
            var repository = await GetLlmPromptRepositoryAsync();
            var testPrompts = TestDataGenerator.GenerateLlmPrompts(800);

            // Create prompts with specific category distribution
            var categories = new[] { "ChatBot", "CodeAssistant", "DataAnalysis", "Creative", "Educational", "Technical" };
            for (int i = 0; i < testPrompts.Count; i++)
            {
                testPrompts[i].Category = categories[i % categories.Length];
            }

            // Create all prompts
            foreach (var prompt in testPrompts)
            {
                await repository.CreatePromptAsync(prompt);
            }

            // Test category index performance with high load
            var result = await MeasurePerformanceAsync(
                "Category Index Performance Stress Test",
                async () =>
                {
                    var results = new List<List<LlmPrompt>>();
                    
                    // Perform multiple category queries rapidly
                    for (int iteration = 0; iteration < 10; iteration++)
                    {
                        foreach (var category in categories)
                        {
                            var prompts = await repository.GetPromptsByCategoryAsync(category);
                            results.Add(prompts.ToList());
                        }
                    }
                    
                    return results;
                });

            Assert.True(result.Success);
            var results = (List<List<LlmPrompt>>)result.Result!;
            Assert.Equal(60, results.Count); // 10 iterations * 6 categories

            // Each category should have approximately equal number of prompts
            var expectedPerCategory = testPrompts.Count / categories.Length;
            foreach (var categoryResults in results)
            {
                Assert.True(categoryResults.Count >= expectedPerCategory - 10 && 
                           categoryResults.Count <= expectedPerCategory + 10,
                           $"Category should have ~{expectedPerCategory} prompts, got {categoryResults.Count}");
            }

            // Category index should make queries very fast
            Assert.True(result.Duration < TimeSpan.FromSeconds(5),
                $"Category index stress test took {result.Duration.TotalSeconds:F2}s, expected < 5s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkGetAllPromptsPerformanceTest()
        {
            var repository = await GetLlmPromptRepositoryAsync();
            var testPrompts = TestDataGenerator.GenerateLlmPrompts(1200);

            // First, create all prompts
            foreach (var prompt in testPrompts)
            {
                await repository.CreatePromptAsync(prompt);
            }

            // Test bulk retrieval performance
            var result = await MeasurePerformanceAsync(
                "Bulk Get All Prompts",
                async () =>
                {
                    var allPrompts = await repository.GetAllPromptsAsync();
                    return allPrompts.ToList();
                });

            Assert.True(result.Success);
            var allPrompts = (List<LlmPrompt>)result.Result!;
            Assert.True(allPrompts.Count >= testPrompts.Count);

            // Should be able to retrieve 1200+ prompts quickly
            Assert.True(result.Duration < TimeSpan.FromSeconds(8), 
                $"Bulk retrieval took {result.Duration.TotalSeconds:F2}s, expected < 8s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentPromptUpdatesStressTest()
        {
            var repository = await GetLlmPromptRepositoryAsync();
            var testPrompts = TestDataGenerator.GenerateLlmPrompts(250);

            // Create initial prompts
            var createdPrompts = new List<LlmPrompt>();
            foreach (var prompt in testPrompts)
            {
                var created = await repository.CreatePromptAsync(prompt);
                createdPrompts.Add(created);
            }

            // Create concurrent update operations
            var updateOperations = new List<Func<Task<object>>>();

            foreach (var prompt in createdPrompts)
            {
                // Content update
                var contentUpdatedPrompt = prompt;
                contentUpdatedPrompt.Content = TestDataGenerator.GenerateRandomString(5000, 20000);
                contentUpdatedPrompt.LastModified = DateTime.UtcNow;
                updateOperations.Add(async () => (object)(await repository.SavePromptAsync(contentUpdatedPrompt)));

                // Parameters update
                var paramUpdatedPrompt = prompt;
                for (int i = 0; i < 20; i++)
                {
                    paramUpdatedPrompt.Parameters[$"UpdatedParam{i}"] = TestDataGenerator.GenerateRandomString(50, 200);
                }
                paramUpdatedPrompt.LastModified = DateTime.UtcNow;
                updateOperations.Add(async () => (object)(await repository.SavePromptAsync(paramUpdatedPrompt)));
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Prompt Updates Stress Test",
                updateOperations,
                maxConcurrency: 12);

            // Should handle concurrent updates efficiently
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 20.0, maxFailureRate: 0.05);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task PromptDeletionPerformanceTest()
        {
            var repository = await GetLlmPromptRepositoryAsync();
            var testPrompts = TestDataGenerator.GenerateLlmPrompts(350);

            // Create prompts to delete
            var createdPrompts = new List<LlmPrompt>();
            foreach (var prompt in testPrompts)
            {
                var created = await repository.CreatePromptAsync(prompt);
                createdPrompts.Add(created);
            }

            // Test concurrent deletion performance
            var deleteOperations = createdPrompts.Take(200).Select<LlmPrompt, Func<Task<object>>>(prompt =>
                async () => (object)(await repository.DeletePromptAsync(prompt.Id))).ToList();

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Prompt Deletions",
                deleteOperations,
                maxConcurrency: 8);

            // Deletions should be fast
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 30.0, maxFailureRate: 0.02);

            // Verify deletions worked
            var remainingPrompts = await repository.GetAllPromptsAsync();
            var remainingCount = remainingPrompts.Count();
            
            Output.WriteLine($"Remaining prompts after deletion: {remainingCount}");
            Assert.True(remainingCount >= 150, "Should have some prompts remaining after partial deletion");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task MemoryUsageUnderPromptLoadTest()
        {
            var repository = await GetLlmPromptRepositoryAsync();
            
            // Test with progressively larger datasets and content sizes
            var smallPrompts = TestDataGenerator.GenerateLlmPrompts(150, parametersPerPrompt: 5);
            var mediumPrompts = TestDataGenerator.GenerateLlmPrompts(200, parametersPerPrompt: 20);
            var largePrompts = TestDataGenerator.GenerateLlmPrompts(100, parametersPerPrompt: 50);

            // Add large content to large prompts
            foreach (var prompt in largePrompts)
            {
                prompt.Content = TestDataGenerator.GenerateRandomString(20000, 100000); // Very large content
            }

            var smallResult = await MeasurePerformanceAsync(
                "Memory Test - Small Prompts",
                async () =>
                {
                    foreach (var prompt in smallPrompts)
                    {
                        await repository.CreatePromptAsync(prompt);
                    }
                    return smallPrompts.Count;
                });

            var mediumResult = await MeasurePerformanceAsync(
                "Memory Test - Medium Prompts",
                async () =>
                {
                    foreach (var prompt in mediumPrompts)
                    {
                        await repository.CreatePromptAsync(prompt);
                    }
                    return mediumPrompts.Count;
                });

            var largeResult = await MeasurePerformanceAsync(
                "Memory Test - Large Prompts",
                async () =>
                {
                    foreach (var prompt in largePrompts)
                    {
                        await repository.CreatePromptAsync(prompt);
                    }
                    return largePrompts.Count;
                });

            // Memory usage should scale reasonably even with large content
            Assert.True(smallResult.MemoryUsed < 25 * 1024 * 1024, // Less than 25MB
                $"Small prompts used {smallResult.MemoryUsed / (1024 * 1024)}MB, expected < 25MB");
            
            Output.WriteLine($"Memory usage - Small: {smallResult.MemoryUsed / 1024}KB, " +
                           $"Medium: {mediumResult.MemoryUsed / 1024}KB, " +
                           $"Large: {largeResult.MemoryUsed / 1024}KB");

            await VerifyDatabaseIntegrityAsync();
        }
    }
}
