
using DaoStudio.Interfaces;
using DaoStudio.Services;
using DryIoc;
using FluentAssertions;
using System.Diagnostics;
using TestDaoStudio.Helpers;
using TestDaoStudio.Infrastructure;

namespace TestDaoStudio.Performance;

/// <summary>
/// Performance tests for load testing scenarios.
/// Tests system behavior under various load conditions.
/// </summary>
public class LoadTests : IDisposable
{
    private readonly TestContainerFixture _containerFixture;
    private readonly DatabaseTestFixture _databaseFixture;

    public LoadTests()
    {
        _containerFixture = new TestContainerFixture();
        _databaseFixture = new DatabaseTestFixture();
    }

    [Fact]
    public async Task HighVolumeSessionCreation_PerformsWithinLimits()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var testPerson = MessageTestHelper.CreateTestPerson("LoadTestBot", "A test assistant", "OpenAI", "gpt-4");

        const int sessionCount = 100;
        const int maxExecutionTimeMs = 10000; // 10 seconds

        // Act
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task<DaoStudio.Interfaces.ISession>>();

        for (int i = 0; i < sessionCount; i++)
        {
            var task = sessionService.CreateSession(testPerson);
            tasks.Add(task);
        }

        var sessions = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        sessions.Should().HaveCount(sessionCount);
        sessions.Should().OnlyContain(s => s.Id != 0);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxExecutionTimeMs);

        // Verify average creation time per session
        var averageTimePerSession = stopwatch.ElapsedMilliseconds / (double)sessionCount;
        averageTimePerSession.Should().BeLessThan(100); // Less than 100ms per session
    }

    [Fact]
    public async Task HighVolumeMessageCreation_PerformsWithinLimits()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var testPerson = MessageTestHelper.CreateTestPerson("MessageLoadBot", "A test assistant", "OpenAI", "gpt-4");

        var session = await sessionService.CreateSession(testPerson);

        const int messageCount = 500;
        const int maxExecutionTimeMs = 15000; // 15 seconds

        // Act
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task<DaoStudio.Interfaces.IMessage>>();

        for (int i = 0; i < messageCount; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            var content = $"Load test message #{i + 1} - {(role == MessageRole.User ? "User" : "Assistant")}";
            tasks.Add(messageService.CreateMessageAsync(content, role, MessageType.Normal, session.Id));
        }

        var messages = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        messages.Should().HaveCount(messageCount);
        messages.Should().OnlyContain(m => m.Id != 0);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxExecutionTimeMs);

        // Verify average creation time per message
        var averageTimePerMessage = stopwatch.ElapsedMilliseconds / (double)messageCount;
        averageTimePerMessage.Should().BeLessThan(30); // Less than 30ms per message
    }

    [Fact]
    public async Task ConcurrentSessionAccess_HandlesLoad()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var peopleService = _containerFixture.Container.Resolve<IPeopleService>();

        // Create and save person to database first
        var testPerson = await peopleService.CreatePersonAsync(
            "ConcurrentBot",
            "A test assistant",
            null,
            true,
            "OpenAI",
            "gpt-4",
            "You are a helpful assistant for testing purposes.",
            null,
            null);
        testPerson.Should().NotBeNull();

        // Create base sessions
        const int sessionCount = 20;
        var sessions = new List<DaoStudio.Interfaces.ISession>();

        for (int i = 0; i < sessionCount; i++)
        {
            var session = await sessionService.CreateSession(testPerson!);
            sessions.Add(session);
        }

        const int concurrentOperations = 100;
        const int maxExecutionTimeMs = 20000; // 20 seconds

        // Act - Perform concurrent read/write operations
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();

        for (int i = 0; i < concurrentOperations; i++)
        {
            var sessionIndex = i % sessionCount;
            var session = sessions[sessionIndex];

            if (i % 3 == 0)
            {
                // Read operation - open session
                tasks.Add(sessionService.OpenSession(session.Id));
            }
            else if (i % 3 == 1)
            {
                // Write operation - add message
                var content = $"Concurrent message {i}";
                var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
                tasks.Add(messageService.CreateMessageAsync(content, role, MessageType.Normal, session.Id));
            }
            else
            {
                // Read operation - get messages
                tasks.Add(messageService.GetMessagesBySessionIdAsync(session.Id));
            }
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxExecutionTimeMs);

        // Verify data integrity after concurrent operations
        foreach (var session in sessions)
        {
            var retrievedSession = await sessionService.OpenSession(session.Id);
            retrievedSession.Should().NotBeNull();
            retrievedSession!.Id.Should().Be(session.Id);

            var messages = await messageService.GetMessagesBySessionIdAsync(session.Id);
            messages.Should().OnlyContain(m => m.SessionId == session.Id);
        }
    }

    [Fact]
    public async Task DatabaseQueryPerformance_UnderLoad()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var peopleService = _containerFixture.Container.Resolve<IPeopleService>();

        // Create and save person to database first
        var testPerson = await peopleService.CreatePersonAsync(
            "QueryBot",
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
        const int sessionCount = 50;
        const int messagesPerSession = 20;
        var sessions = new List<DaoStudio.Interfaces.ISession>();

        for (int i = 0; i < sessionCount; i++)
        {
            var session = await sessionService.CreateSession(testPerson!);
            sessions.Add(session);

            // Add messages to each session
            for (int j = 0; j < messagesPerSession; j++)
            {
                var role = j % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
                var content = $"Message {j + 1} in session {i + 1}";
                await messageService.CreateMessageAsync(content, role, MessageType.Normal, session.Id);
            }
        }

        const int queryCount = 200;
        const int maxQueryTimeMs = 10000; // 10 seconds

        // Act - Perform various queries
        var stopwatch = Stopwatch.StartNew();
        var queryTasks = new List<Task>();

        for (int i = 0; i < queryCount; i++)
        {
            var sessionIndex = i % sessionCount;
            var session = sessions[sessionIndex];

            if (i % 4 == 0)
            {
                queryTasks.Add(sessionService.GetAllSessionsAsync());
            }
            else if (i % 4 == 1)
            {
                queryTasks.Add(sessionService.OpenSession(session.Id));
            }
            else if (i % 4 == 2)
            {
                queryTasks.Add(messageService.GetMessagesBySessionIdAsync(session.Id));
            }
            else
            {
                queryTasks.Add(messageService.GetAllMessagesAsync());
            }
        }

        await Task.WhenAll(queryTasks);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxQueryTimeMs);

        var averageQueryTime = stopwatch.ElapsedMilliseconds / (double)queryCount;
        averageQueryTime.Should().BeLessThan(50); // Less than 50ms per query on average
    }

    [Fact]
    public async Task LargeMessageContent_HandlesEfficiently()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var testPerson = MessageTestHelper.CreateTestPerson("LargeContentBot", "A test assistant", "OpenAI", "gpt-4");

        var session = await sessionService.CreateSession(testPerson);

        // Create large content messages
        const int messageCount = 20;
        const int contentSizeKB = 10; // 10KB per message
        const int maxExecutionTimeMs = 15000; // 15 seconds

        var largeContent = new string('A', contentSizeKB * 1024);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task<DaoStudio.Interfaces.IMessage>>();

        for (int i = 0; i < messageCount; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            var content = $"Large message {i + 1}: {largeContent}";
            tasks.Add(messageService.CreateMessageAsync(content, role, MessageType.Normal, session.Id));
        }

        var messages = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        messages.Should().HaveCount(messageCount);
        messages.Should().OnlyContain(m => (m.Content != null ? m.Content.Length : 0) > contentSizeKB * 1024);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxExecutionTimeMs);

        // Test retrieval performance
        var retrievalStopwatch = Stopwatch.StartNew();
        var retrievedMessages = await messageService.GetMessagesBySessionIdAsync(session.Id);
        retrievalStopwatch.Stop();

        retrievedMessages.Should().HaveCount(messageCount);
        retrievalStopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // 5 seconds for retrieval
    }

    [Fact]
    public async Task MemoryUsage_StaysWithinLimits()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var testPerson = MessageTestHelper.CreateTestPerson("MemoryBot", "A test assistant", "OpenAI", "gpt-4");

        // Measure initial memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        const int operationCount = 1000;
        const long maxMemoryIncreaseMB = 100; // 100MB max increase

        // Act - Perform memory-intensive operations
        for (int i = 0; i < operationCount; i++)
        {
            var session = await sessionService.CreateSession(testPerson);

            var messageContent = $"Memory test message {i}";
            await messageService.CreateMessageAsync(messageContent, MessageRole.User, MessageType.Normal, session.Id);

            // Periodically force garbage collection
            if (i % 100 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        // Measure final memory
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
    public async Task StressTest_MultipleOperationsSimultaneously()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var testPerson = MessageTestHelper.CreateTestPerson("StressBot", "A test assistant", "OpenAI", "gpt-4");

        const int simultaneousOperations = 50;
        const int operationsPerType = 20;
        const int maxExecutionTimeMs = 30000; // 30 seconds

        // Act - Run multiple types of operations simultaneously
        var stopwatch = Stopwatch.StartNew();
        var allTasks = new List<Task>();

        // Session creation tasks
        var sessionTasks = Enumerable.Range(0, simultaneousOperations).Select(i =>
            Task.Run(async () =>
            {
                for (int j = 0; j < operationsPerType; j++)
                {
                    await sessionService.CreateSession(testPerson);
                }
            }));
        allTasks.AddRange(sessionTasks);

        // Create a base session for message operations
        var baseSession = await sessionService.CreateSession(testPerson);

        // Message creation tasks
        var messageTasks = Enumerable.Range(0, simultaneousOperations).Select(i =>
            Task.Run(async () =>
            {
                for (int j = 0; j < operationsPerType; j++)
                {
                    var role = (i + j) % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
                    var content = $"Stress message {i}-{j}";
                    await messageService.CreateMessageAsync(content, role, MessageType.Normal, baseSession.Id);
                }
            }));
        allTasks.AddRange(messageTasks);


        // Query tasks
        var queryTasks = Enumerable.Range(0, simultaneousOperations / 2).Select(i =>
            Task.Run(async () =>
            {
                for (int j = 0; j < operationsPerType; j++)
                {
                    await sessionService.GetAllSessionsAsync();
                    await messageService.GetMessagesBySessionIdAsync(baseSession.Id);
                }
            }));
        allTasks.AddRange(queryTasks);

        await Task.WhenAll(allTasks);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(maxExecutionTimeMs);

        // Verify system is still responsive
        var finalSession = await sessionService.CreateSession(testPerson);
        finalSession.Should().NotBeNull();
        finalSession.Id.Should().NotBe(0);
    }

    [Fact]
    public async Task ResponseTime_UnderNormalLoad()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var testPerson = MessageTestHelper.CreateTestPerson("ResponseBot", "A test assistant", "OpenAI", "gpt-4");

        const int testIterations = 100;
        const int maxResponseTimeMs = 1000; // 1 second per operation
        var responseTimes = new List<long>();

        // Act - Measure response times for typical operations
        for (int i = 0; i < testIterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            // Typical user workflow: create session, add messages, retrieve messages
            var session = await sessionService.CreateSession(testPerson);

            await messageService.CreateMessageAsync("Hello", MessageRole.User, MessageType.Normal, session.Id);
            await messageService.CreateMessageAsync("Hello there!", MessageRole.Assistant, MessageType.Normal, session.Id);

            var messages = await messageService.GetMessagesBySessionIdAsync(session.Id);

            stopwatch.Stop();
            responseTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert
        var averageResponseTime = responseTimes.Average();
        var maxResponseTime = responseTimes.Max();
        var p95ResponseTime = responseTimes.OrderBy(x => x).Skip((int)(testIterations * 0.95)).First();

        averageResponseTime.Should().BeLessThan(maxResponseTimeMs / 2); // Average should be well below max
        maxResponseTime.Should().BeLessThan(maxResponseTimeMs);
        p95ResponseTime.Should().BeLessThan((long)(maxResponseTimeMs * 0.8)); // 95th percentile should be reasonable
    }

    public void Dispose()
    {
        _databaseFixture?.Dispose();
        _containerFixture?.Dispose();
    }
}
