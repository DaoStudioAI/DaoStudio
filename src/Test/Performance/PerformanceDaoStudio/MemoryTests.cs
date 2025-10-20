
using DaoStudio.Interfaces;
using DaoStudio.Services;
using FluentAssertions;
using System.Diagnostics;
using TestDaoStudio.Helpers;
using TestDaoStudio.Infrastructure;
using DryIoc;

namespace PerformanceDaoStudio;

/// <summary>
/// Performance tests focused on memory usage and garbage collection.
/// Tests memory efficiency and leak detection scenarios.
/// </summary>
public class MemoryTests : IDisposable
{
    private readonly TestContainerFixture _containerFixture;
    private readonly DatabaseTestFixture _databaseFixture;

    public MemoryTests()
    {
        _containerFixture = new TestContainerFixture();
        _databaseFixture = new DatabaseTestFixture();
    }

    [Fact]
    public async Task SessionCreation_DoesNotLeakMemory()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var testPerson = MessageTestHelper.CreateTestPerson("MemoryTestBot", "A test assistant", "OpenAI", "gpt-4");

        // Force initial garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        const int iterations = 1000;
        const long maxMemoryIncreaseMB = 50; // 50MB max increase

        // Act - Create and dispose many sessions
        for (int i = 0; i < iterations; i++)
        {
            var session = await sessionService.CreateSession(testPerson);

            // Simulate session usage
            session.Should().NotBeNull();
            session.Id.Should().NotBe(0);

            // Force garbage collection every 100 iterations
            if (i % 100 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        // Force final garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseMB = memoryIncrease / (1024 * 1024);
        
        memoryIncreaseMB.Should().BeLessThan(maxMemoryIncreaseMB);
    }

    [Fact]
    public async Task MessageCreation_DoesNotLeakMemory()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var testPerson = MessageTestHelper.CreateTestPerson("MessageMemoryBot", "A test assistant", "OpenAI", "gpt-4");

    var session = await sessionService.CreateSession(testPerson);

        // Force initial garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        const int messageCount = 2000;
        const long maxMemoryIncreaseMB = 100; // 100MB max increase

        // Act - Create many messages
        for (int i = 0; i < messageCount; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            var content = $"Memory test message {i} with some content to test memory usage patterns";
            var message = MessageTestHelper.CreateTestMessage(session.Id, role, content);

            var createdMessage = await messageService.CreateMessageAsync(
                message.Content ?? string.Empty,
                (MessageRole)message.Role,
                (MessageType)message.Type,
                message.SessionId,
                true,
                message.ParentMsgId,
                message.ParentSessId);
            createdMessage.Should().NotBeNull();

            // Force garbage collection every 200 messages
            if (i % 200 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        // Force final garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseMB = memoryIncrease / (1024 * 1024);
        
        memoryIncreaseMB.Should().BeLessThan(maxMemoryIncreaseMB);
    }

    [Fact]
    public async Task LargeObjectHandling_ManagesMemoryEfficiently()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var testPerson = MessageTestHelper.CreateTestPerson("LargeObjectBot", "A test assistant", "OpenAI", "gpt-4");

    var session = await sessionService.CreateSession(testPerson);

        // Force initial garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        const int largeObjectCount = 50;
        const int objectSizeKB = 100; // 100KB per object
        const long maxMemoryIncreaseMB = 200; // 200MB max increase

        // Act - Create and process large objects
        for (int i = 0; i < largeObjectCount; i++)
        {
            var largeContent = new string('X', objectSizeKB * 1024);
            var message = MessageTestHelper.CreateTestMessage(
                session.Id,
                MessageRole.User,
                $"Large object {i}: {largeContent}");

            var createdMessage = await messageService.CreateMessageAsync(
                message.Content ?? string.Empty,
                (MessageRole)message.Role,
                (MessageType)message.Type,
                message.SessionId,
                true,
                message.ParentMsgId,
                message.ParentSessId);
            createdMessage.Should().NotBeNull();

            // Retrieve and verify the message
            var retrievedMessages = await messageService.GetMessagesBySessionIdAsync(session.Id);
            retrievedMessages.Should().Contain(m => m.Id == createdMessage.Id);

            // Force garbage collection every 10 large objects
            if (i % 10 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        // Force final garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseMB = memoryIncrease / (1024 * 1024);
        
        memoryIncreaseMB.Should().BeLessThan(maxMemoryIncreaseMB);
    }

    [Fact]
    public async Task ConcurrentOperations_DoNotCauseMemoryLeaks()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var testPerson = MessageTestHelper.CreateTestPerson("ConcurrentMemoryBot", "A test assistant", "OpenAI", "gpt-4");

        // Force initial garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        const int concurrentTasks = 20;
        const int operationsPerTask = 50;
        const long maxMemoryIncreaseMB = 150; // 150MB max increase

        // Act - Run concurrent operations
        var tasks = Enumerable.Range(0, concurrentTasks).Select(taskId =>
            Task.Run(async () =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    var session = await sessionService.CreateSession(testPerson);

                    var message = MessageTestHelper.CreateTestMessage(
                        session.Id,
                        MessageRole.User,
                        $"Concurrent message {taskId}-{i}");

                    await messageService.CreateMessageAsync(
                        message.Content ?? string.Empty,
                        (MessageRole)message.Role,
                        (MessageType)message.Type,
                        message.SessionId,
                        true,
                        message.ParentMsgId,
                        message.ParentSessId);
                    
                    var messages = await messageService.GetMessagesBySessionIdAsync(session.Id);
                    messages.Should().NotBeEmpty();
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Force final garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseMB = memoryIncrease / (1024 * 1024);
        
        memoryIncreaseMB.Should().BeLessThan(maxMemoryIncreaseMB);
    }

    [Fact]
    public async Task RepeatedQueryOperations_DoNotAccumulateMemory()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var peopleService = _containerFixture.Container.Resolve<IPeopleService>();

        // Create and save person to database first
        var testPerson = await peopleService.CreatePersonAsync(
            "QueryMemoryBot",
            "A test assistant",
            null,
            true,
            "OpenAI",
            "gpt-4",
            "You are a helpful assistant for testing purposes.",
            null,
            null);
        testPerson.Should().NotBeNull();

        // Create test data
        var sessions = new List<ISession>();
        for (int i = 0; i < 10; i++)
        {
            var session = await sessionService.CreateSession(testPerson!);
            sessions.Add(session);

            // Add messages to each session
            for (int j = 0; j < 10; j++)
            {
                var message = MessageTestHelper.CreateTestMessage(
                    session.Id,
                    j % 2 == 0 ? MessageRole.User : MessageRole.Assistant,
                    $"Query test message {j}");
                await messageService.CreateMessageAsync(
                    message.Content ?? string.Empty,
                    (MessageRole)message.Role,
                    (MessageType)message.Type,
                    message.SessionId,
                    true,
                    message.ParentMsgId,
                    message.ParentSessId);
            }
        }

        // Force initial garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        const int queryIterations = 1000;
        const long maxMemoryIncreaseMB = 30; // 30MB max increase

        // Act - Perform repeated queries
        for (int i = 0; i < queryIterations; i++)
        {
            var sessionIndex = i % sessions.Count;
            var session = sessions[sessionIndex];

            // Alternate between different query types
            if (i % 4 == 0)
            {
                var allSessions = await sessionService.GetAllSessionsAsync();
                allSessions.Should().NotBeEmpty();
            }
            else if (i % 4 == 1)
            {
                var retrievedSession = await sessionService.OpenSession(session.Id);
                retrievedSession.Should().NotBeNull();
            }
            else if (i % 4 == 2)
            {
                var messages = await messageService.GetMessagesBySessionIdAsync(session.Id);
                messages.Should().NotBeEmpty();
            }
            else
            {
                var allMessages = await messageService.GetAllMessagesAsync();
                allMessages.Should().NotBeEmpty();
            }

            // Force garbage collection every 100 queries
            if (i % 100 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        // Force final garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseMB = memoryIncrease / (1024 * 1024);
        
        memoryIncreaseMB.Should().BeLessThan(maxMemoryIncreaseMB);
    }

    [Fact]
    public async Task ServiceDisposal_ReleasesMemoryCorrectly()
    {
        // Arrange
        var localContainerFixture = new TestContainerFixture();
        var localDatabaseFixture = new DatabaseTestFixture();

        await localContainerFixture.InitializeAsync();
        await localDatabaseFixture.InitializeAsync();

        // Force initial garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        const long maxMemoryIncreaseMB = 20; // 20MB max increase after disposal

        // Act - Use services and then dispose
        {
            var sessionService = localContainerFixture.Container.Resolve<ISessionService>();
            var messageService = localContainerFixture.Container.Resolve<IMessageService>();
            var testPerson = MessageTestHelper.CreateTestPerson("DisposalBot", "A test assistant", "OpenAI", "gpt-4");

            // Create some data
            for (int i = 0; i < 100; i++)
            {
                var session = await sessionService.CreateSession(testPerson);

                var message = MessageTestHelper.CreateTestMessage(
                    session.Id,
                    MessageRole.User,
                    $"Disposal message {i}");
                await messageService.CreateMessageAsync(
                    message.Content ?? string.Empty,
                    (MessageRole)message.Role,
                    (MessageType)message.Type,
                    message.SessionId,
                    true,
                    message.ParentMsgId,
                    message.ParentSessId);
            }
        } // Services go out of scope

        // Dispose fixtures
        localContainerFixture.Dispose();
        localDatabaseFixture.Dispose();

        // Force garbage collection after disposal
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseMB = memoryIncrease / (1024 * 1024);
        
        memoryIncreaseMB.Should().BeLessThan(maxMemoryIncreaseMB);
    }

    [Fact]
    public async Task MemoryPressure_HandledGracefully()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var testPerson = MessageTestHelper.CreateTestPerson("PressureBot", "A test assistant", "OpenAI", "gpt-4");

    var session = await sessionService.CreateSession(testPerson);

        // Force initial garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        const int pressureIterations = 100;
        const int largeSizeKB = 500; // 500KB per iteration
        const long maxMemoryIncreaseMB = 300; // 300MB max increase

        // Act - Create memory pressure and test system behavior
        var createdObjects = new List<string>();
        
        for (int i = 0; i < pressureIterations; i++)
        {
            // Create memory pressure with large objects
            var largeObject = new string('M', largeSizeKB * 1024);
            createdObjects.Add(largeObject);

            // Perform normal operations under pressure
            var message = MessageTestHelper.CreateTestMessage(
                session.Id,
                MessageRole.User,
                $"Pressure test message {i}");

            var createdMessage = await messageService.CreateMessageAsync(
                message.Content ?? string.Empty,
                (MessageRole)message.Role,
                (MessageType)message.Type,
                message.SessionId,
                true,
                message.ParentMsgId,
                message.ParentSessId);
            createdMessage.Should().NotBeNull();

            // Periodically release some pressure
            if (i % 20 == 0 && createdObjects.Count > 10)
            {
                createdObjects.RemoveRange(0, 10);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        // Clear pressure objects
        createdObjects.Clear();

        // Force final garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseMB = memoryIncrease / (1024 * 1024);
        
        memoryIncreaseMB.Should().BeLessThan(maxMemoryIncreaseMB);

        // Verify system is still functional
        var testMessage = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.User,
            "Final test message");
        var finalTestMessage = await messageService.CreateMessageAsync(
            testMessage.Content ?? string.Empty,
            (MessageRole)testMessage.Role,
            (MessageType)testMessage.Type,
            testMessage.SessionId,
            true,
            testMessage.ParentMsgId,
            testMessage.ParentSessId);
        finalTestMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task GarbageCollectionEfficiency_MeetsExpectations()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var testPerson = MessageTestHelper.CreateTestPerson("GCBot", "A test assistant", "OpenAI", "gpt-4");

        const int testCycles = 10;
        const int objectsPerCycle = 200;
        var memoryReadings = new List<long>();

        // Act - Test garbage collection efficiency
        for (int cycle = 0; cycle < testCycles; cycle++)
        {
            // Create objects
            var sessions = new List<ISession>();
            for (int i = 0; i < objectsPerCycle; i++)
            {
                var session = await sessionService.CreateSession(testPerson);
                sessions.Add(session);

                var message = MessageTestHelper.CreateTestMessage(
                    session.Id,
                    MessageRole.User,
                    $"GC message {cycle}-{i}");
                await messageService.CreateMessageAsync(
                    message.Content ?? string.Empty,
                    (MessageRole)message.Role,
                    (MessageType)message.Type,
                    message.SessionId,
                    true,
                    message.ParentMsgId,
                    message.ParentSessId);
            }

            // Measure memory before GC
            var beforeGC = GC.GetTotalMemory(false);

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure memory after GC
            var afterGC = GC.GetTotalMemory(false);
            memoryReadings.Add(afterGC);

            // Clear references to allow collection in next cycle
            sessions.Clear();
        }

        // Assert - Memory should stabilize after initial cycles
        var lastThreeReadings = memoryReadings.TakeLast(3).ToList();
        var memoryVariance = lastThreeReadings.Max() - lastThreeReadings.Min();
        var maxVarianceMB = 50; // 50MB max variance in stable state

        (memoryVariance / (1024 * 1024)).Should().BeLessThan(maxVarianceMB);
    }

    public void Dispose()
    {
        _databaseFixture?.Dispose();
        _containerFixture?.Dispose();
    }
}
