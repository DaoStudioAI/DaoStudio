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
    /// Load tests for ISessionRepository focusing on performance under high load conditions
    /// </summary>
    public class SessionRepositoryLoadTests : BaseLoadTest
    {
        private ISessionRepository? _sessionRepository;

        public SessionRepositoryLoadTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<ISessionRepository> GetSessionRepositoryAsync()
        {
            if (_sessionRepository == null)
            {
                await InitializeStorageAsync();
                _sessionRepository = await StorageFactory!.GetSessionRepositoryAsync();
            }
            return _sessionRepository;
        }

        [Fact]
        public async Task BulkSessionCreationTest_Small()
        {
            var repository = await GetSessionRepositoryAsync();
            var testSessions = TestDataGenerator.GenerateSessions(TestDataGenerator.Scenarios.Small.SessionsCount);

            await RunBulkOperationStressTestAsync<Session>(
                "Bulk Create Sessions (Small)",
                async () =>
                {
                    var results = new List<Session>();
                    foreach (var session in testSessions)
                    {
                        var created = await repository.CreateSessionAsync(session);
                        results.Add(created);
                    }
                    return results;
                },
                testSessions.Count);

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkSessionCreationTest_Medium()
        {
            var repository = await GetSessionRepositoryAsync();
            var testSessions = TestDataGenerator.GenerateSessions(TestDataGenerator.Scenarios.Medium.SessionsCount);

            var result = await RunBulkOperationStressTestAsync<Session>(
                "Bulk Create Sessions (Medium)",
                async () =>
                {
                    var results = new List<Session>();
                    foreach (var session in testSessions)
                    {
                        var created = await repository.CreateSessionAsync(session);
                        results.Add(created);
                    }
                    return results;
                },
                testSessions.Count);

            // Assert performance requirements - should create 800 sessions in under 20 seconds
            AssertPerformanceRequirements(result, TimeSpan.FromSeconds(20));
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task HierarchicalSessionCreationTest()
        {
            var repository = await GetSessionRepositoryAsync();
            
            // Create parent sessions first
            var parentSessions = TestDataGenerator.GenerateSessions(50);
            var createdParents = new List<Session>();
            
            foreach (var parent in parentSessions)
            {
                parent.ParentSessId = null; // Ensure these are root sessions
                var created = await repository.CreateSessionAsync(parent);
                createdParents.Add(created);
            }

            // Create child sessions with hierarchical relationships
            var childSessions = new List<Session>();
            for (int i = 0; i < 200; i++)
            {
                var parentIndex = i % createdParents.Count;
                var parent = createdParents[parentIndex];
                
                var childSession = TestDataGenerator.GenerateSessions(1)[0];
                childSession.ParentSessId = parent.Id;
                childSessions.Add(childSession);
            }

            var result = await MeasurePerformanceAsync(
                "Hierarchical Session Creation",
                async () =>
                {
                    var results = new List<Session>();
                    foreach (var child in childSessions)
                    {
                        var created = await repository.CreateSessionAsync(child);
                        results.Add(created);
                    }
                    return results;
                });

            Assert.True(result.Success);
            var createdChildren = (List<Session>)result.Result!;
            Assert.Equal(childSessions.Count, createdChildren.Count);

            // Verify hierarchical relationships
            foreach (var child in createdChildren)
            {
                Assert.NotNull(child.ParentSessId);
                Assert.Contains(child.ParentSessId.Value, createdParents.Select(p => p.Id));
            }

            Output.WriteLine($"Created {createdParents.Count} parent sessions and {createdChildren.Count} child sessions");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentTokenCountUpdatesTest()
        {
            var repository = await GetSessionRepositoryAsync();
            var testSessions = TestDataGenerator.GenerateSessions(150);

            // Create initial sessions
            var createdSessions = new List<Session>();
            foreach (var session in testSessions)
            {
                var created = await repository.CreateSessionAsync(session);
                createdSessions.Add(created);
            }

            // Create concurrent token count update operations
            var updateOperations = new List<Func<Task<object>>>();

            foreach (var session in createdSessions)
            {
                // Multiple token count updates per session
                for (int i = 0; i < 3; i++)
                {
                    var updatedSession = session;
                    updatedSession.TotalTokenCount += TestDataGenerator.Random.Next(100, 5000);
                    updatedSession.InputTokenCount += TestDataGenerator.Random.Next(50, 2000);
                    updatedSession.OutputTokenCount += TestDataGenerator.Random.Next(50, 3000);
                    updatedSession.LastModified = DateTime.UtcNow;
                    
                    updateOperations.Add(async () => (object)(await repository.SaveSessionAsync(updatedSession)));
                }
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Token Count Updates",
                updateOperations,
                maxConcurrency: 10);

            // Token updates should be fast and concurrent
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 30.0, maxFailureRate: 0.03);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task LargeLogoHandlingTest()
        {
            var repository = await GetSessionRepositoryAsync();
            var testSessions = TestDataGenerator.GenerateSessions(60);

            // Add large logo data to sessions
            foreach (var session in testSessions)
            {
                session.Logo = TestDataGenerator.GenerateRandomBinaryData(500000, 2000000); // 500KB-2MB logos
            }

            var result = await MeasurePerformanceAsync(
                "Large Logo Creation",
                async () =>
                {
                    var results = new List<Session>();
                    foreach (var session in testSessions)
                    {
                        var created = await repository.CreateSessionAsync(session);
                        results.Add(created);
                    }
                    return results;
                });

            Assert.True(result.Success);
            var createdSessions = (List<Session>)result.Result!;
            Assert.Equal(testSessions.Count, createdSessions.Count);

            // Test retrieval performance with large logos
            var retrievalResult = await MeasurePerformanceAsync(
                "Large Logo Retrieval",
                async () =>
                {
                    var results = new List<Session?>();
                    foreach (var session in createdSessions)
                    {
                        var retrieved = await repository.GetSessionAsync(session.Id);
                        results.Add(retrieved);
                    }
                    return results;
                });

            Assert.True(retrievalResult.Success);
            var retrievedSessions = (List<Session?>)retrievalResult.Result!;
            Assert.All(retrievedSessions, s => Assert.NotNull(s));
            Assert.All(retrievedSessions, s => Assert.NotNull(s!.Logo));
            Assert.All(retrievedSessions, s => Assert.True(s!.Logo!.Length > 500000));

            // Large logo operations should still complete in reasonable time
            Assert.True(result.Duration < TimeSpan.FromSeconds(25),
                $"Large logo creation took {result.Duration.TotalSeconds:F2}s, expected < 25s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ComplexParentChildQueriesTest()
        {
            var repository = await GetSessionRepositoryAsync();
            
            // Create a complex hierarchy: 20 root sessions, each with 5-15 children
            var rootSessions = TestDataGenerator.GenerateSessions(20);
            var createdRoots = new List<Session>();
            
            foreach (var root in rootSessions)
            {
                root.ParentSessId = null;
                var created = await repository.CreateSessionAsync(root);
                createdRoots.Add(created);
            }

            // Create children for each root
            var allChildren = new List<Session>();
            foreach (var root in createdRoots)
            {
                var childCount = TestDataGenerator.Random.Next(5, 16);
                var children = TestDataGenerator.GenerateSessions(childCount);
                
                foreach (var child in children)
                {
                    child.ParentSessId = root.Id;
                    var createdChild = await repository.CreateSessionAsync(child);
                    allChildren.Add(createdChild);
                }
            }

            // Test concurrent parent-child queries
            var queryOperations = createdRoots.Select<Session, Func<Task<object>>>(root =>
                async () => 
                {
                    var children = await repository.GetSessionsByParentSessIdAsync(root.Id);
                    return (object)children.ToList();
                }).ToList();

            var result = await MeasureConcurrentPerformanceAsync(
                "Complex Parent-Child Queries",
                queryOperations,
                maxConcurrency: 8);

            // Parent-child queries should be efficient
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 15.0, maxFailureRate: 0.02);

            // Verify query results
            foreach (var opResult in result.IndividualResults.Where(r => r.Success))
            {
                var children = (List<Session>)opResult.Result!;
                Assert.True(children.Count >= 5 && children.Count <= 15,
                    $"Expected 5-15 children, got {children.Count}");
            }

            Output.WriteLine($"Queried {createdRoots.Count} root sessions with {allChildren.Count} total children");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task DeepSessionHierarchyStressTest()
        {
            var repository = await GetSessionRepositoryAsync();
            
            // Create a deep hierarchy: 5 levels deep with branching
            var currentLevelSessions = new List<Session>();
            
            // Level 1: Root session
            var rootSession = TestDataGenerator.GenerateSessions(1)[0];
            rootSession.ParentSessId = null;
            var createdRoot = await repository.CreateSessionAsync(rootSession);
            currentLevelSessions.Add(createdRoot);

            var allCreatedSessions = new List<Session> { createdRoot };

            // Create 4 more levels, each level has 2-3x more sessions than previous
            for (int level = 2; level <= 5; level++)
            {
                var nextLevelSessions = new List<Session>();
                
                foreach (var parent in currentLevelSessions)
                {
                    var childCount = TestDataGenerator.Random.Next(2, 4); // 2-3 children per parent
                    var children = TestDataGenerator.GenerateSessions(childCount);
                    
                    foreach (var child in children)
                    {
                        child.ParentSessId = parent.Id;
                        var createdChild = await repository.CreateSessionAsync(child);
                        nextLevelSessions.Add(createdChild);
                        allCreatedSessions.Add(createdChild);
                    }
                }
                
                currentLevelSessions = nextLevelSessions;
                Output.WriteLine($"Level {level}: Created {nextLevelSessions.Count} sessions");
            }

            // Test performance of deep hierarchy queries
            var result = await MeasurePerformanceAsync(
                "Deep Hierarchy Traversal",
                async () =>
                {
                    var results = new List<List<Session>>();
                    
                    // Query all children for each level
                    var currentLevel = new List<Session> { createdRoot };
                    
                    while (currentLevel.Count > 0)
                    {
                        var nextLevel = new List<Session>();
                        
                        foreach (var parent in currentLevel)
                        {
                            var children = await repository.GetSessionsByParentSessIdAsync(parent.Id);
                            var childList = children.ToList();
                            if (childList.Count > 0)
                            {
                                nextLevel.AddRange(childList);
                                results.Add(childList);
                            }
                        }
                        
                        currentLevel = nextLevel;
                    }
                    
                    return results;
                });

            Assert.True(result.Success);
            var hierarchyResults = (List<List<Session>>)result.Result!;
            
            // Deep hierarchy traversal should complete efficiently
            Assert.True(result.Duration < TimeSpan.FromSeconds(10),
                $"Deep hierarchy traversal took {result.Duration.TotalSeconds:F2}s, expected < 10s");

            Output.WriteLine($"Created {allCreatedSessions.Count} sessions across 5 levels of hierarchy");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkGetAllSessionsPerformanceTest()
        {
            var repository = await GetSessionRepositoryAsync();
            var testSessions = TestDataGenerator.GenerateSessions(600);

            // Create all sessions
            foreach (var session in testSessions)
            {
                await repository.CreateSessionAsync(session);
            }

            // Test different inclusion options
            var allInclusionResult = await MeasurePerformanceAsync(
                "Get All Sessions - All Inclusion",
                async () =>
                {
                    var allSessions = await repository.GetAllSessionsAsync(SessionInclusionOptions.All);
                    return allSessions.ToList();
                });

            var rootOnlyResult = await MeasurePerformanceAsync(
                "Get All Sessions - Root Only",
                async () =>
                {
                    var rootSessions = await repository.GetAllSessionsAsync(SessionInclusionOptions.ParentsOnly);
                    return rootSessions.ToList();
                });

            Assert.True(allInclusionResult.Success);
            Assert.True(rootOnlyResult.Success);

            var allSessions = (List<Session>)allInclusionResult.Result!;
            var rootSessions = (List<Session>)rootOnlyResult.Result!;

            Assert.True(allSessions.Count >= testSessions.Count);
            Assert.True(rootSessions.Count <= allSessions.Count);

            // Bulk retrieval should be efficient
            Assert.True(allInclusionResult.Duration < TimeSpan.FromSeconds(6),
                $"All sessions retrieval took {allInclusionResult.Duration.TotalSeconds:F2}s, expected < 6s");

            Output.WriteLine($"Retrieved {allSessions.Count} sessions (all) and {rootSessions.Count} sessions (root only)");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentSessionDeletionTest()
        {
            var repository = await GetSessionRepositoryAsync();
            var testSessions = TestDataGenerator.GenerateSessions(200);

            // Create sessions to delete
            var createdSessions = new List<Session>();
            foreach (var session in testSessions)
            {
                var created = await repository.CreateSessionAsync(session);
                createdSessions.Add(created);
            }

            // Test concurrent deletion
            var deleteOperations = createdSessions.Take(120).Select<Session, Func<Task<object>>>(session =>
                async () => (object)(await repository.DeleteSessionAsync(session.Id))).ToList();

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Session Deletions",
                deleteOperations,
                maxConcurrency: 8);

            // Deletions should be fast
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 35.0, maxFailureRate: 0.02);

            // Verify deletions worked
            var remainingSessions = await repository.GetAllSessionsAsync();
            var remainingCount = remainingSessions.Count();
            
            Output.WriteLine($"Remaining sessions after deletion: {remainingCount}");
            Assert.True(remainingCount >= 80, "Should have some sessions remaining after partial deletion");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task MemoryUsageUnderSessionLoadTest()
        {
            var repository = await GetSessionRepositoryAsync();
            
            // Test with progressively larger datasets
            var smallSessions = TestDataGenerator.GenerateSessions(100);
            var mediumSessions = TestDataGenerator.GenerateSessions(200);
            var largeSessions = TestDataGenerator.GenerateSessions(150);

            // Add large logos to large sessions
            foreach (var session in largeSessions)
            {
                session.Logo = TestDataGenerator.GenerateRandomBinaryData(100000, 500000); // Large logos
            }

            var smallResult = await MeasurePerformanceAsync(
                "Memory Test - Small Sessions",
                async () =>
                {
                    foreach (var session in smallSessions)
                    {
                        await repository.CreateSessionAsync(session);
                    }
                    return smallSessions.Count;
                });

            var mediumResult = await MeasurePerformanceAsync(
                "Memory Test - Medium Sessions",
                async () =>
                {
                    foreach (var session in mediumSessions)
                    {
                        await repository.CreateSessionAsync(session);
                    }
                    return mediumSessions.Count;
                });

            var largeResult = await MeasurePerformanceAsync(
                "Memory Test - Large Sessions",
                async () =>
                {
                    foreach (var session in largeSessions)
                    {
                        await repository.CreateSessionAsync(session);
                    }
                    return largeSessions.Count;
                });

            // Memory usage should scale reasonably
            Assert.True(smallResult.MemoryUsed < 20 * 1024 * 1024, // Less than 20MB
                $"Small sessions used {smallResult.MemoryUsed / (1024 * 1024)}MB, expected < 20MB");
            
            Output.WriteLine($"Memory usage - Small: {smallResult.MemoryUsed / 1024}KB, " +
                           $"Medium: {mediumResult.MemoryUsed / 1024}KB, " +
                           $"Large: {largeResult.MemoryUsed / 1024}KB");

            await VerifyDatabaseIntegrityAsync();
        }
    }
}
