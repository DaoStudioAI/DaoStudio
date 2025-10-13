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
    /// Load tests for IMessageRepository focusing on performance under high load conditions
    /// </summary>
    public class MessageRepositoryLoadTests : BaseLoadTest
    {
        private IMessageRepository? _messageRepository;
        private ISessionRepository? _sessionRepository;

        public MessageRepositoryLoadTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<IMessageRepository> GetMessageRepositoryAsync()
        {
            if (_messageRepository == null)
            {
                await InitializeStorageAsync();
                _messageRepository = await StorageFactory!.GetMessageRepositoryAsync();
            }
            return _messageRepository;
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
        public async Task BulkMessageCreationTest_Small()
        {
            var messageRepository = await GetMessageRepositoryAsync();
            var sessionRepository = await GetSessionRepositoryAsync();

            // Create a session first
            var testSession = TestDataGenerator.GenerateSessions(1)[0];
            var createdSession = await sessionRepository.CreateSessionAsync(testSession);

            var testMessages = TestDataGenerator.GenerateMessages(TestDataGenerator.Scenarios.Small.MessagesCount, createdSession.Id);

            await RunBulkOperationStressTestAsync<Message>(
                "Bulk Create Messages (Small)",
                async () =>
                {
                    var results = new List<Message>();
                    foreach (var message in testMessages)
                    {
                        var created = await messageRepository.CreateMessageAsync(message);
                        results.Add(created);
                    }
                    return results;
                },
                testMessages.Count);

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkMessageCreationTest_Medium()
        {
            var messageRepository = await GetMessageRepositoryAsync();
            var sessionRepository = await GetSessionRepositoryAsync();

            // Create multiple sessions for better distribution
            var testSessions = TestDataGenerator.GenerateSessions(10);
            var createdSessions = new List<Session>();
            foreach (var session in testSessions)
            {
                var created = await sessionRepository.CreateSessionAsync(session);
                createdSessions.Add(created);
            }

            var testMessages = new List<Message>();
            var messagesPerSession = TestDataGenerator.Scenarios.Medium.MessagesCount / createdSessions.Count;
            
            foreach (var session in createdSessions)
            {
                var messages = TestDataGenerator.GenerateMessages(messagesPerSession, session.Id);
                testMessages.AddRange(messages);
            }

            var result = await RunBulkOperationStressTestAsync<Message>(
                "Bulk Create Messages (Medium)",
                async () =>
                {
                    var results = new List<Message>();
                    foreach (var message in testMessages)
                    {
                        var created = await messageRepository.CreateMessageAsync(message);
                        results.Add(created);
                    }
                    return results;
                },
                testMessages.Count);

            // Assert performance requirements - should create 50000+ messages in under 100 seconds
            AssertPerformanceRequirements(result, TimeSpan.FromSeconds(100));
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentSessionBasedOperationsTest()
        {
            var messageRepository = await GetMessageRepositoryAsync();
            var sessionRepository = await GetSessionRepositoryAsync();

            // Create multiple sessions
            var testSessions = TestDataGenerator.GenerateSessions(8);
            var createdSessions = new List<Session>();
            foreach (var session in testSessions)
            {
                var created = await sessionRepository.CreateSessionAsync(session);
                createdSessions.Add(created);
            }

            // Create messages for each session
            var allMessages = new List<Message>();
            foreach (var session in createdSessions)
            {
                var messages = TestDataGenerator.GenerateMessages(50, session.Id);
                foreach (var message in messages)
                {
                    var created = await messageRepository.CreateMessageAsync(message);
                    allMessages.Add(created);
                }
            }

            // Create concurrent session-based read operations
            var readOperations = createdSessions.Select<Session, Func<Task<object>>>(session =>
                async () => 
                {
                    var messages = await messageRepository.GetBySessionIdAsync(session.Id);
                    return (object)messages.ToList();
                }).ToList();

            // Add concurrent write operations
            var writeOperations = createdSessions.Select<Session, Func<Task<object>>>(session =>
                async () =>
                {
                    var newMessage = TestDataGenerator.GenerateMessages(1, session.Id)[0];
                    var created = await messageRepository.CreateMessageAsync(newMessage);
                    return (object)created;
                }).ToList();

            var allOperations = readOperations.Concat(writeOperations).ToList();

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Session-Based Operations",
                allOperations,
                maxConcurrency: 10);

            // Session-based operations should be efficient
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 25.0, maxFailureRate: 0.03);

            // Verify read results
            var readResults = result.IndividualResults.Take(createdSessions.Count).Where(r => r.Success);
            foreach (var opResult in readResults)
            {
                var messages = (List<Message>)opResult.Result!;
                Assert.True(messages.Count >= 50, "Each session should have at least 50 messages");
            }

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task LazyLoadingBinaryContentTest()
        {
            var messageRepository = await GetMessageRepositoryAsync();
            var sessionRepository = await GetSessionRepositoryAsync();

            // Create a session
            var testSession = TestDataGenerator.GenerateSessions(1)[0];
            var createdSession = await sessionRepository.CreateSessionAsync(testSession);

            // Create messages with large binary content
            var testMessages = TestDataGenerator.GenerateMessages(80, createdSession.Id);
            foreach (var message in testMessages)
            {
                // Add large binary content to some messages
                if (TestDataGenerator.Random.NextDouble() < 0.6) // 60% of messages have binary content
                {
                    message.BinaryContents = new List<BinaryData>
                    {
                        new BinaryData
                        {
                            Name = $"Binary_{TestDataGenerator.Random.Next(1000, 9999)}",
                            Type = 1, // Image type
                            Data = TestDataGenerator.GenerateRandomBinaryData(100000, 500000) // 100KB-500KB
                        }
                    };
                }
            }

            // Ensure at least one message has binary content to avoid flaky test when random produces none
            if (!testMessages.Any(m => m.BinaryContents != null && m.BinaryContents.Count > 0) && testMessages.Count > 0)
            {
                testMessages[0].BinaryContents = new List<BinaryData>
                {
                    new BinaryData
                    {
                        Name = "Binary_For_Test",
                        Type = 1,
                        Data = TestDataGenerator.GenerateRandomBinaryData(120000, 120000)
                    }
                };
            }

            // Create all messages
            var createdMessages = new List<Message>();
            foreach (var message in testMessages)
            {
                var created = await messageRepository.CreateMessageAsync(message);
                createdMessages.Add(created);
            }


            // Warm-up queries to reduce variability from cold caches
            await messageRepository.GetBySessionIdAsync(createdSession.Id, includeBinaryData: false);
            await messageRepository.GetBySessionIdAsync(createdSession.Id, includeBinaryData: true);

            // Measure multiple iterations and take average to reduce flakiness
            const int iterations = 3;
            var lazyDurations = new List<TimeSpan>();
            var fullDurations = new List<TimeSpan>();
            List<Message>? lazyMessages = null;
            List<Message>? fullMessages = null;

            for (int i = 0; i < iterations; i++)
            {
                var lr = await MeasurePerformanceAsync(
                    $"Lazy Loading (No Binary Data) - Iter {i + 1}",
                    async () => (await messageRepository.GetBySessionIdAsync(createdSession.Id, includeBinaryData: false)).ToList());

                var fr = await MeasurePerformanceAsync(
                    $"Full Loading (With Binary Data) - Iter {i + 1}",
                    async () => (await messageRepository.GetBySessionIdAsync(createdSession.Id, includeBinaryData: true)).ToList());

                Assert.True(lr.Success);
                Assert.True(fr.Success);

                lazyDurations.Add(lr.Duration);
                fullDurations.Add(fr.Duration);

                // capture last iteration results for further checks
                lazyMessages = (List<Message>)lr.Result!;
                fullMessages = (List<Message>)fr.Result!;
            }

            // Compute average durations
            var avgLazy = TimeSpan.FromMilliseconds(lazyDurations.Average(d => d.TotalMilliseconds));
            var avgFull = TimeSpan.FromMilliseconds(fullDurations.Average(d => d.TotalMilliseconds));

            Assert.Equal(testMessages.Count, lazyMessages!.Count);
            Assert.Equal(testMessages.Count, fullMessages!.Count);

            // Lazy loading should be faster or at least not meaningfully slower.
            // Allow a small tolerance for timing jitter on fast operations.
            var tolerance = TimeSpan.FromMilliseconds(5);
            Assert.True(avgLazy <= avgFull + tolerance,
                $"Lazy loading ({avgLazy.TotalSeconds:F3}s) should be faster than full loading ({avgFull.TotalSeconds:F3}s) within {tolerance.TotalMilliseconds}ms tolerance");

            // Verify binary content handling
            var messagesWithBinary = fullMessages.Where(m => m.BinaryContents?.Count > 0).ToList();
            Assert.True(messagesWithBinary.Count > 0, "Should have messages with binary content");

            Output.WriteLine($"Lazy load (avg): {avgLazy.TotalSeconds:F2}s, " +
                           $"Full load (avg): {avgFull.TotalSeconds:F2}s, " +
                           $"Binary messages: {messagesWithBinary.Count}");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task LargeBinaryDataHandlingTest()
        {
            var messageRepository = await GetMessageRepositoryAsync();
            var sessionRepository = await GetSessionRepositoryAsync();

            // Create a session
            var testSession = TestDataGenerator.GenerateSessions(1)[0];
            var createdSession = await sessionRepository.CreateSessionAsync(testSession);

            // Create messages with very large binary data
            var testMessages = TestDataGenerator.GenerateMessages(30, createdSession.Id);
            foreach (var message in testMessages)
            {
                message.BinaryContents = new List<BinaryData>
                {
                    new BinaryData
                    {
                        Name = $"LargeBinary_{TestDataGenerator.Random.Next(1000, 9999)}",
                        Type = 2, // Application data type
                        Data = TestDataGenerator.GenerateRandomBinaryData(1000000, 5000000) // 1MB-5MB files
                    }
                };
            }

            var result = await MeasurePerformanceAsync(
                "Large Binary Data Creation",
                async () =>
                {
                    var results = new List<Message>();
                    foreach (var message in testMessages)
                    {
                        var created = await messageRepository.CreateMessageAsync(message);
                        results.Add(created);
                    }
                    return results;
                });

            Assert.True(result.Success);
            var createdMessages = (List<Message>)result.Result!;
            Assert.Equal(testMessages.Count, createdMessages.Count);

            // Test retrieval performance with large binary data
            var retrievalResult = await MeasurePerformanceAsync(
                "Large Binary Data Retrieval",
                async () =>
                {
                    var messages = await messageRepository.GetBySessionIdAsync(createdSession.Id, includeBinaryData: true);
                    return messages.ToList();
                });

            Assert.True(retrievalResult.Success);
            var retrievedMessages = (List<Message>)retrievalResult.Result!;
            Assert.Equal(testMessages.Count, retrievedMessages.Count);
            Assert.All(retrievedMessages, m => Assert.True(m.BinaryContents?.Count > 0));
            Assert.All(retrievedMessages, m => Assert.True(m.BinaryContents!.First().Data.Length >= 1000000));

            // Large binary operations should complete in reasonable time
            Assert.True(result.Duration < TimeSpan.FromSeconds(40),
                $"Large binary creation took {result.Duration.TotalSeconds:F2}s, expected < 40s");

            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ThreadedConversationStressTest()
        {
            var messageRepository = await GetMessageRepositoryAsync();
            var sessionRepository = await GetSessionRepositoryAsync();

            // Create parent and child sessions to simulate threaded conversations
            var parentSession = TestDataGenerator.GenerateSessions(1)[0];
            parentSession.ParentSessId = null;
            var createdParent = await sessionRepository.CreateSessionAsync(parentSession);

            var childSessions = TestDataGenerator.GenerateSessions(5);
            var createdChildren = new List<Session>();
            foreach (var child in childSessions)
            {
                child.ParentSessId = createdParent.Id;
                var createdChild = await sessionRepository.CreateSessionAsync(child);
                createdChildren.Add(createdChild);
            }

            // Create messages across all sessions (threaded conversation)
            var allSessions = new List<Session> { createdParent };
            allSessions.AddRange(createdChildren);

            var allMessages = new List<Message>();
            foreach (var session in allSessions)
            {
                var messages = TestDataGenerator.GenerateMessages(100, session.Id);
                
                // Simulate conversation threading - some messages reference others
                for (int i = 1; i < messages.Count; i++)
                {
                    if (TestDataGenerator.Random.NextDouble() < 0.3) // 30% chance of threading
                    {
                        messages[i].Content += $" [Reply to previous message in thread]";
                    }
                }

                foreach (var message in messages)
                {
                    var created = await messageRepository.CreateMessageAsync(message);
                    allMessages.Add(created);
                }
            }

            // Test concurrent threaded queries
            var threadQueryOperations = new List<Func<Task<object>>>();

            // Query by parent session (should include all threads)
            threadQueryOperations.Add(async () => 
            {
                var messages = await messageRepository.GetByParentSessIdAsync(createdParent.Id);
                return (object)messages.ToList();
            });

            // Query individual child sessions
            foreach (var child in createdChildren)
            {
                threadQueryOperations.Add(async () => 
                {
                    var messages = await messageRepository.GetBySessionIdAsync(child.Id);
                    return (object)messages.ToList();
                });
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Threaded Conversation Stress Test",
                threadQueryOperations,
                maxConcurrency: 8);

            // Threaded queries should be efficient
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 20.0, maxFailureRate: 0.02);

            // Verify parent session query includes all related messages
            var parentResult = (List<Message>)result.IndividualResults.First().Result!;
            Assert.True(parentResult.Count >= allMessages.Count, 
                "Parent session query should include messages from all related sessions");

            Output.WriteLine($"Created {allMessages.Count} messages across {allSessions.Count} threaded sessions");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ChronologicalQueryPerformanceTest()
        {
            var messageRepository = await GetMessageRepositoryAsync();
            var sessionRepository = await GetSessionRepositoryAsync();

            // Create sessions
            var testSessions = TestDataGenerator.GenerateSessions(5);
            var createdSessions = new List<Session>();
            foreach (var session in testSessions)
            {
                var created = await sessionRepository.CreateSessionAsync(session);
                createdSessions.Add(created);
            }

            // Create messages with specific timing
            var allMessages = new List<Message>();
            var baseTime = DateTime.UtcNow.AddDays(-30); // Start 30 days ago

            foreach (var session in createdSessions)
            {
                var messages = TestDataGenerator.GenerateMessages(200, session.Id);
                
                // Set chronological timestamps
                for (int i = 0; i < messages.Count; i++)
                {
                    messages[i].CreatedAt = baseTime.AddMinutes(i * 5); // 5 minutes apart
                    messages[i].LastModified = messages[i].CreatedAt;
                }

                foreach (var message in messages)
                {
                    var created = await messageRepository.CreateMessageAsync(message);
                    allMessages.Add(created);
                }
            }

            // Test chronological deletion performance
            var midMessage = allMessages[allMessages.Count / 2];
            
            var deletionResult = await MeasurePerformanceAsync(
                "Chronological Deletion From Specified Message",
                async () =>
                {
                    var deletedCount = await messageRepository.DeleteFromMessageInSessionAsync(
                        midMessage.SessionId, midMessage.Id, includeSpecifiedMessage: true);
                    return deletedCount;
                });

            Assert.True(deletionResult.Success);
            var deletedCount = (int)deletionResult.Result!;
            Assert.True(deletedCount > 0, "Should have deleted some messages");

            // Test session-based deletion performance
            var sessionToDelete = createdSessions.First();
            var sessionDeletionResult = await MeasurePerformanceAsync(
                "Session-Based Bulk Deletion",
                async () =>
                {
                    var deletedCount = await messageRepository.DeleteBySessionIdAsync(sessionToDelete.Id);
                    return deletedCount;
                });

            Assert.True(sessionDeletionResult.Success);
            var sessionDeletedCount = (int)sessionDeletionResult.Result!;
            Assert.True(sessionDeletedCount > 0, "Should have deleted session messages");

            // Chronological operations should be efficient
            Assert.True(deletionResult.Duration < TimeSpan.FromSeconds(5),
                $"Chronological deletion took {deletionResult.Duration.TotalSeconds:F2}s, expected < 5s");

            Output.WriteLine($"Deleted {deletedCount} messages chronologically, {sessionDeletedCount} messages by session");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task BulkGetAllMessagesPerformanceTest()
        {
            var messageRepository = await GetMessageRepositoryAsync();
            var sessionRepository = await GetSessionRepositoryAsync();

            // Create multiple sessions
            var testSessions = TestDataGenerator.GenerateSessions(8);
            var createdSessions = new List<Session>();
            foreach (var session in testSessions)
            {
                var created = await sessionRepository.CreateSessionAsync(session);
                createdSessions.Add(created);
            }

            // Create messages across all sessions
            var totalMessages = 0;
            foreach (var session in createdSessions)
            {
                var messages = TestDataGenerator.GenerateMessages(150, session.Id);
                foreach (var message in messages)
                {
                    await messageRepository.CreateMessageAsync(message);
                    totalMessages++;
                }
            }

            // Test bulk retrieval performance
            var result = await MeasurePerformanceAsync(
                "Bulk Get All Messages",
                async () =>
                {
                    var allMessages = await messageRepository.GetAllAsync();
                    return allMessages.ToList();
                });

            Assert.True(result.Success);
            var allMessages = (List<Message>)result.Result!;
            Assert.True(allMessages.Count >= totalMessages);

            // Should be able to retrieve 1200+ messages quickly
            Assert.True(result.Duration < TimeSpan.FromSeconds(10), 
                $"Bulk message retrieval took {result.Duration.TotalSeconds:F2}s, expected < 10s");

            Output.WriteLine($"Retrieved {allMessages.Count} messages from {createdSessions.Count} sessions");
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task ConcurrentMessageUpdatesStressTest()
        {
            var messageRepository = await GetMessageRepositoryAsync();
            var sessionRepository = await GetSessionRepositoryAsync();

            // Create sessions
            var testSessions = TestDataGenerator.GenerateSessions(3);
            var createdSessions = new List<Session>();
            foreach (var session in testSessions)
            {
                var created = await sessionRepository.CreateSessionAsync(session);
                createdSessions.Add(created);
            }

            // Create messages
            var allMessages = new List<Message>();
            foreach (var session in createdSessions)
            {
                var messages = TestDataGenerator.GenerateMessages(100, session.Id);
                foreach (var message in messages)
                {
                    var created = await messageRepository.CreateMessageAsync(message);
                    allMessages.Add(created);
                }
            }

            // Create concurrent update operations
            var updateOperations = new List<Func<Task<object>>>();

            foreach (var message in allMessages.Take(200)) // Update first 200 messages
            {
                // Content update
                var updatedMessage = message;
                updatedMessage.Content = TestDataGenerator.GenerateRandomString(1000, 5000) + " [UPDATED]";
                updatedMessage.LastModified = DateTime.UtcNow;
                updateOperations.Add(async () => (object)(await messageRepository.SaveMessageAsync(updatedMessage)));
            }

            var result = await MeasureConcurrentPerformanceAsync(
                "Concurrent Message Updates Stress Test",
                updateOperations,
                maxConcurrency: 12);

            // Should handle concurrent updates efficiently
            AssertConcurrentPerformanceRequirements(result, minOperationsPerSecond: 25.0, maxFailureRate: 0.05);
            await VerifyDatabaseIntegrityAsync();
        }

        [Fact]
        public async Task MemoryUsageUnderMessageLoadTest()
        {
            var messageRepository = await GetMessageRepositoryAsync();
            var sessionRepository = await GetSessionRepositoryAsync();

            // Create session
            var testSession = TestDataGenerator.GenerateSessions(1)[0];
            var createdSession = await sessionRepository.CreateSessionAsync(testSession);
            
            // Test with progressively larger datasets
            var smallMessages = TestDataGenerator.GenerateMessages(200, createdSession.Id);
            var mediumMessages = TestDataGenerator.GenerateMessages(300, createdSession.Id);
            var largeMessages = TestDataGenerator.GenerateMessages(150, createdSession.Id);

            // Add binary content to large messages
            foreach (var message in largeMessages)
            {
                message.BinaryContents = new List<BinaryData>
                {
                    new BinaryData
                    {
                        Name = $"Binary_{TestDataGenerator.Random.Next(1000, 9999)}",
                        Type = 3, // Data type
                        Data = TestDataGenerator.GenerateRandomBinaryData(50000, 200000) // Moderate binary data
                    }
                };
            }

            var smallResult = await MeasurePerformanceAsync(
                "Memory Test - Small Messages",
                async () =>
                {
                    foreach (var message in smallMessages)
                    {
                        await messageRepository.CreateMessageAsync(message);
                    }
                    return smallMessages.Count;
                });

            var mediumResult = await MeasurePerformanceAsync(
                "Memory Test - Medium Messages",
                async () =>
                {
                    foreach (var message in mediumMessages)
                    {
                        await messageRepository.CreateMessageAsync(message);
                    }
                    return mediumMessages.Count;
                });

            var largeResult = await MeasurePerformanceAsync(
                "Memory Test - Large Messages",
                async () =>
                {
                    foreach (var message in largeMessages)
                    {
                        await messageRepository.CreateMessageAsync(message);
                    }
                    return largeMessages.Count;
                });

            // Memory usage should scale reasonably
            Assert.True(smallResult.MemoryUsed < 30 * 1024 * 1024, // Less than 30MB
                $"Small messages used {smallResult.MemoryUsed / (1024 * 1024)}MB, expected < 30MB");
            
            Output.WriteLine($"Memory usage - Small: {smallResult.MemoryUsed / 1024}KB, " +
                           $"Medium: {mediumResult.MemoryUsed / 1024}KB, " +
                           $"Large: {largeResult.MemoryUsed / 1024}KB");

            await VerifyDatabaseIntegrityAsync();
        }
    }
}
