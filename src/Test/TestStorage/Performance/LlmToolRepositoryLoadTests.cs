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
    /// Load tests for ILlmToolRepository focusing on performance under high load conditions
    /// </summary>
    public class LlmToolRepositoryLoadTests : BaseLoadTest
    {
        private ILlmToolRepository? _llmToolRepository;

        public LlmToolRepositoryLoadTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<ILlmToolRepository> GetLlmToolRepositoryAsync()
        {
            if (_llmToolRepository == null)
            {
                await InitializeStorageAsync();
                _llmToolRepository = await StorageFactory!.GetLlmToolRepositoryAsync();
            }
            return _llmToolRepository;
        }

        [Fact]
        public async Task BulkToolCreationTest_Small()
        {
            var repository = await GetLlmToolRepositoryAsync();
            var testTools = TestDataGenerator.GenerateLlmTools(TestDataGenerator.Scenarios.Small.ToolsCount);

            await RunBulkOperationStressTestAsync<LlmTool>(
                "Bulk Create Tools (Small)",
                async () =>
                {
                    var results = new List<LlmTool>();
                    foreach (var tool in testTools)
                    {
                        var created = await repository.CreateToolAsync(tool);
                        results.Add(created);
                    }
                    return results;
                },
                testTools.Count);

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkToolCreationWithStateDataTest_Medium()
        {
            var repository = await GetLlmToolRepositoryAsync();
            var testTools = TestDataGenerator.GenerateLlmTools(
                TestDataGenerator.Scenarios.Medium.ToolsCount, 
                includeStateData: true);

            var result = await RunBulkOperationStressTestAsync<LlmTool>(
                "Bulk Create Tools with State Data (Medium)",
                async () =>
                {
                    var results = new List<LlmTool>();
                    foreach (var tool in testTools)
                    {
                        var created = await repository.CreateToolAsync(tool);
                        results.Add(created);
                    }
                    return results;
                },
                testTools.Count);

            // Assert performance requirements - should create 500 tools with state data in under 30 seconds
            AssertPerformanceRequirements(result, TimeSpan.FromSeconds(30));
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentStateUpdatesTest()
        {
            var repository = await GetLlmToolRepositoryAsync();
            var testTools = TestDataGenerator.GenerateLlmTools(100, includeStateData: true);

            // First, create all tools
            var createdTools = new List<LlmTool>();
            foreach (var tool in testTools)
            {
                var created = await repository.CreateToolAsync(tool);
                createdTools.Add(created);
            }

            // Create concurrent operations to update tool states
            var operations = new List<Func<Task<object>>>();

            foreach (var tool in createdTools)
            {
                // Update state data
                var updatedTool = tool;
                updatedTool.State = updatedTool.State == 0 ? 1 : 0; // Toggle state
                updatedTool.StateData = TestDataGenerator.GenerateRandomBinaryData();
                updatedTool.LastModified = DateTime.UtcNow;
                
                operations.Add(async () => (object)(await repository.SaveToolAsync(updatedTool)));
                
                // Add concurrent read operation
                operations.Add(async () => (object)(await repository.GetToolAsync(updatedTool.Id) ?? new LlmTool()));
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent State Updates",
                operations,
                maxConcurrency: 8);

            // Assert good concurrency performance for state updates
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 20.0, maxFailureRate: 0.05);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task LazyLoadingStateDataPerformanceTest()
        {
            var repository = await GetLlmToolRepositoryAsync();
            var testTools = TestDataGenerator.GenerateLlmTools(200, includeStateData: true);

            // Create tools with state data
            var createdTools = new List<LlmTool>();
            foreach (var tool in testTools)
            {
                var created = await repository.CreateToolAsync(tool);
                createdTools.Add(created);
            }

            // Test retrieval without state data (should be faster)
            var withoutStateDataResult = await MeasurePerformanceAsync(
                "Get All Tools Without State Data",
                async () =>
                {
                    var tools = await repository.GetAllToolsAsync(includeStateData: false);
                    return tools.ToList();
                });

            Assert.True(withoutStateDataResult.Success);
            var toolsWithoutStateData = (List<LlmTool>)withoutStateDataResult.Result!;
            Assert.All(toolsWithoutStateData, t => Assert.Null(t.StateData));

            // Test retrieval with state data (should be slower but include state data)
            var withStateDataResult = await MeasurePerformanceAsync(
                "Get All Tools With State Data",
                async () =>
                {
                    var tools = await repository.GetAllToolsAsync(includeStateData: true);
                    return tools.ToList();
                });

            Assert.True(withStateDataResult.Success);
            var toolsWithStateData = (List<LlmTool>)withStateDataResult.Result!;
            
            // Verify state data is loaded for tools that have it
            var toolsWithData = toolsWithStateData.Where(t => createdTools.Any(ct => ct.Id == t.Id && ct.StateData != null));
            Assert.All(toolsWithData, t => Assert.NotNull(t.StateData));

            // Without state data should be faster
            Output.WriteLine($"Without state data: {withoutStateDataResult.Duration.TotalMilliseconds:F2}ms, " +
                           $"With state data: {withStateDataResult.Duration.TotalMilliseconds:F2}ms");
            
            Assert.True(withoutStateDataResult.Duration < withStateDataResult.Duration.Add(TimeSpan.FromSeconds(1)),
                "Lazy loading should make queries without state data faster");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task LargeToolConfigurationHandlingTest()
        {
            var repository = await GetLlmToolRepositoryAsync();
            var testTools = TestDataGenerator.GenerateLlmTools(50, parametersPerTool: 100); // Large parameter dictionaries

            // Modify tools to have very large configurations
            foreach (var tool in testTools)
            {
                tool.ToolConfig = TestDataGenerator.GenerateRandomString(5000, 20000); // 5-20KB configurations
                tool.Description = TestDataGenerator.GenerateRandomString(1000, 5000); // Large descriptions
            }

            var result = await MeasurePerformanceAsync(
                "Large Tool Configuration Creation",
                async () =>
                {
                    var results = new List<LlmTool>();
                    foreach (var tool in testTools)
                    {
                        var created = await repository.CreateToolAsync(tool);
                        results.Add(created);
                    }
                    return results;
                });

            Assert.True(result.Success);
            var createdTools = (List<LlmTool>)result.Result!;
            Assert.Equal(testTools.Count, createdTools.Count);

            // Test retrieval performance with large configurations
            var retrievalResult = await MeasurePerformanceAsync(
                "Large Tool Configuration Retrieval",
                async () =>
                {
                    var results = new List<LlmTool?>();
                    foreach (var tool in createdTools)
                    {
                        var retrieved = await repository.GetToolAsync(tool.Id);
                        results.Add(retrieved);
                    }
                    return results;
                });

            Assert.True(retrievalResult.Success);
            var retrievedTools = (List<LlmTool?>)retrievalResult.Result!;
            Assert.All(retrievedTools, t => Assert.NotNull(t));
            Assert.All(retrievedTools, t => Assert.True(t!.ToolConfig.Length > 5000));
            Assert.All(retrievedTools, t => Assert.Equal(100, t!.Parameters.Count));

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task StaticIdLookupStressTest()
        {
            var repository = await GetLlmToolRepositoryAsync();
            var testTools = TestDataGenerator.GenerateLlmTools(300);

            // Create tools with various static IDs (some duplicates for testing)
            var staticIds = new List<string>();
            for (int i = 0; i < 50; i++)
            {
                staticIds.Add($"com.test.tool{i}");
            }

            var createdTools = new List<LlmTool>();
            foreach (var tool in testTools)
            {
                tool.StaticId = staticIds[Random.Shared.Next(staticIds.Count)]; // Assign random static ID
                var created = await repository.CreateToolAsync(tool);
                createdTools.Add(created);
            }

            // Create high-frequency static ID lookup operations
            var lookupOperations = new List<Func<Task<object>>>();

            foreach (var staticId in staticIds)
            {
                lookupOperations.Add(async () => 
                {
                    var tools = await repository.GetToolsByStaticIdAsync(staticId);
                    return (object)tools.ToList();
                });
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Static ID Lookup Stress Test",
                lookupOperations,
                maxConcurrency: 12);

            // Static ID lookups should be very fast with proper indexing
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 50.0, maxFailureRate: 0.01);

            // Verify results
            foreach (var opResult in result.IndividualResults.Where(r => r.Success))
            {
                var tools = (List<LlmTool>)opResult.Result!;
                Assert.True(tools.Count > 0, "Each static ID should have at least one tool");
            }

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentToolUpdatesWithLargeParametersTest()
        {
            var repository = await GetLlmToolRepositoryAsync();
            var testTools = TestDataGenerator.GenerateLlmTools(150, parametersPerTool: 200);

            // Create initial tools
            var createdTools = new List<LlmTool>();
            foreach (var tool in testTools)
            {
                var created = await repository.CreateToolAsync(tool);
                createdTools.Add(created);
            }

            // Create concurrent update operations with parameter modifications
            var updateOperations = new List<Func<Task<object>>>();

            foreach (var tool in createdTools)
            {
                // Add new parameters
                var updatedTool = tool;
                for (int i = 0; i < 50; i++)
                {
                    updatedTool.Parameters[$"NewParam{i}_{DateTime.UtcNow.Ticks}"] = TestDataGenerator.GenerateRandomString(100, 500);
                }
                updatedTool.LastModified = DateTime.UtcNow;
                
                updateOperations.Add(async () => (object)(await repository.SaveToolAsync(updatedTool)));
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Tool Updates with Large Parameters",
                updateOperations,
                maxConcurrency: 10);

            // Should handle concurrent updates with large parameter dictionaries efficiently
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 15.0, maxFailureRate: 0.05);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ToolDeletionPerformanceTest()
        {
            var repository = await GetLlmToolRepositoryAsync();
            var testTools = TestDataGenerator.GenerateLlmTools(250, includeStateData: true);

            // Create tools to delete
            var createdTools = new List<LlmTool>();
            foreach (var tool in testTools)
            {
                var created = await repository.CreateToolAsync(tool);
                createdTools.Add(created);
            }

            // Test concurrent deletion performance
            var deleteOperations = createdTools.Take(150).Select<LlmTool, Func<Task<object>>>(tool =>
                async () => (object)(await repository.DeleteToolAsync(tool.Id))).ToList();

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Tool Deletions",
                deleteOperations,
                maxConcurrency: 8);

            // Deletions should be reasonably fast even with state data
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 25.0, maxFailureRate: 0.02);

            // Verify deletions worked
            var remainingTools = await repository.GetAllToolsAsync(includeStateData: false);
            var remainingCount = remainingTools.Count();
            
            Output.WriteLine($"Remaining tools after deletion: {remainingCount}");
            Assert.True(remainingCount >= 100, "Should have some tools remaining after partial deletion");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ToolTypeIndexPerformanceTest()
        {
            var repository = await GetLlmToolRepositoryAsync();
            var testTools = TestDataGenerator.GenerateLlmTools(600);

            // Create tools with various types and enabled states
            foreach (var tool in testTools)
            {
                await repository.CreateToolAsync(tool);
            }

            // Test filtering performance by tool type and enabled status
            var result = await MeasurePerformanceAsync(
                "Tool Type and Status Index Performance",
                async () =>
                {
                    var allTools = await repository.GetAllToolsAsync(includeStateData: false);
                    
                    // Simulate filtering by type and enabled status (in a real scenario, this would be database queries)
                    var groupedByType = allTools.GroupBy(t => t.ToolType).ToDictionary(g => g.Key, g => g.ToList());
                    var enabledTools = allTools.Where(t => t.IsEnabled).ToList();
                    var statefulTools = allTools.Where(t => t.State == 1).ToList();
                    var recentTools = allTools.Where(t => t.LastModified > DateTime.UtcNow.AddDays(-7)).ToList();
                    
                    return new
                    {
                        Total = allTools.Count(),
                        GroupedByType = groupedByType.Count,
                        Enabled = enabledTools.Count,
                        Stateful = statefulTools.Count,
                        Recent = recentTools.Count
                    };
                });

            Assert.True(result.Success);
            
            // Complex filtering operations should complete quickly with proper indexing
            Assert.True(result.Duration < TimeSpan.FromSeconds(3),
                $"Tool type filtering took {result.Duration.TotalSeconds:F2}s, expected < 3s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task MemoryUsageUnderToolLoadTest()
        {
            var repository = await GetLlmToolRepositoryAsync();
            
            // Test with progressively larger datasets and complexity
            var smallTools = TestDataGenerator.GenerateLlmTools(100, includeStateData: false, parametersPerTool: 10);
            var mediumTools = TestDataGenerator.GenerateLlmTools(150, includeStateData: true, parametersPerTool: 50);
            var largeTools = TestDataGenerator.GenerateLlmTools(75, includeStateData: true, parametersPerTool: 200);

            // Add large configurations to large tools
            foreach (var tool in largeTools)
            {
                tool.ToolConfig = TestDataGenerator.GenerateRandomString(10000, 50000); // Very large configs
                tool.Description = TestDataGenerator.GenerateRandomString(2000, 10000);
            }

            var smallResult = await MeasurePerformanceAsync(
                "Memory Test - Small Tools (No State)",
                async () =>
                {
                    foreach (var tool in smallTools)
                    {
                        await repository.CreateToolAsync(tool);
                    }
                    return smallTools.Count;
                });

            var mediumResult = await MeasurePerformanceAsync(
                "Memory Test - Medium Tools (With State)",
                async () =>
                {
                    foreach (var tool in mediumTools)
                    {
                        await repository.CreateToolAsync(tool);
                    }
                    return mediumTools.Count;
                });

            var largeResult = await MeasurePerformanceAsync(
                "Memory Test - Large Tools (Complex)",
                async () =>
                {
                    foreach (var tool in largeTools)
                    {
                        await repository.CreateToolAsync(tool);
                    }
                    return largeTools.Count;
                });

            // Memory usage should scale reasonably even with large configurations
            Assert.True(smallResult.MemoryUsed < 20 * 1024 * 1024, // Less than 20MB
                $"Small tools used {smallResult.MemoryUsed / (1024 * 1024)}MB, expected < 20MB");
            
            Output.WriteLine($"Memory usage - Small: {smallResult.MemoryUsed / 1024}KB, " +
                           $"Medium: {mediumResult.MemoryUsed / 1024}KB, " +
                           $"Large: {largeResult.MemoryUsed / 1024}KB");

            await VerifyDatabaseIntegrityAsync();
        }
    }
}
