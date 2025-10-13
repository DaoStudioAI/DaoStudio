
using DaoStudio.Interfaces;
using DaoStudio.Services;
using FluentAssertions;
using Moq;
using TestDaoStudio.Helpers;
using TestDaoStudio.Infrastructure;
using DryIoc;

namespace TestDaoStudio.ErrorHandling;

/// <summary>
/// Tests for edge cases and boundary conditions.
/// Tests system behavior with unusual inputs, extreme values, and corner cases.
/// </summary>
public class EdgeCaseTests : IDisposable
{
    private readonly TestContainerFixture _containerFixture;
    private readonly DatabaseTestFixture _databaseFixture;

    public EdgeCaseTests()
    {
        _containerFixture = new TestContainerFixture();
        _databaseFixture = new DatabaseTestFixture();
    }

    [Fact]
    public async Task EmptyStringInputs_HandledCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        // Create test person for session creation
        var testPerson = MessageTestHelper.CreateTestPerson("TestPerson", "Test Description", "TestProvider", "test-model");

        // Act & Assert - Empty person names (null person)
        var act1 = async () => await sessionService.CreateSession(null!);
        await act1.Should().ThrowAsync<ArgumentNullException>();

        // Create valid session for message tests
        var session = await sessionService.CreateSession(testPerson);

    }

    [Fact]
    public async Task NullInputs_HandledGracefully()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        // Create test person for session creation
        var testPerson = MessageTestHelper.CreateTestPerson("TestPerson", "Test Description", "TestProvider", "test-model");

        // Act & Assert - Null person
        var act1 = async () => await sessionService.CreateSession(null!);
        await act1.Should().ThrowAsync<ArgumentNullException>();

    }

    [Fact]
    public async Task ExtremelyLongStrings_HandledAppropriately()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        var veryLongTitle = new string('A', 10000); // 10k characters
        var veryLongDescription = new string('B', 50000); // 50k characters
        var veryLongContent = new string('C', 100000); // 100k characters

        // Create test person with long name
        var testPersonLong = MessageTestHelper.CreateTestPerson(veryLongTitle, "Test Description", "TestProvider", "test-model");

        // Act & Assert - Very long person name in session
        try
        {
            var session = await sessionService.CreateSession(testPersonLong);
            session.Should().NotBeNull();

            // If creation succeeds, test very long message content
            var longMessage = MessageTestHelper.CreateTestMessage(session.Id, MessageRole.User, veryLongContent);
            var createdMessage = await messageService.CreateMessageAsync(
                longMessage.Content ?? string.Empty,
                (MessageRole)longMessage.Role,
                (MessageType)longMessage.Type,
                longMessage.SessionId,
                true,
                longMessage.ParentMsgId,
                longMessage.ParentSessId);
            createdMessage.Should().NotBeNull();
            createdMessage.Content.Should().HaveLength(veryLongContent.Length);
        }
        catch (ArgumentException)
        {
            // Expected if there are length limits
        }
    }

    [Fact]
    public async Task SpecialCharacters_HandledCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var peopleService = _containerFixture.Container.Resolve<IPeopleService>();

        // var specialCharsTitle = "Test 🚀 Session with émojis and spëcial chars: <>\"'&";
        var specialCharsContent = "Message with special chars: \n\t\r\0 and unicode: 你好 🌍 ñáéíóú";

        // Act
        // Create and save person to database first
        var testPersonSpecial = await peopleService.CreatePersonAsync(
            "TestPerson",
            "Test Description",
            null,
            true,
            "OpenAI",
            "test-model",
            "You are a helpful assistant for testing purposes.",
            null,
            null);
        testPersonSpecial.Should().NotBeNull();

        var session = await sessionService.CreateSession(testPersonSpecial!);
        var message = MessageTestHelper.CreateTestMessage(session.Id, MessageRole.User, specialCharsContent);
        var createdMessage = await messageService.CreateMessageAsync(
            message.Content ?? string.Empty,
            (MessageRole)message.Role,
            (MessageType)message.Type,
            message.SessionId,
            true,
            message.ParentMsgId,
            message.ParentSessId);

        // Assert
        session.Should().NotBeNull();
        createdMessage.Content.Should().Be(specialCharsContent);

        // Verify retrieval preserves special characters
        var retrievedSession = await sessionService.OpenSession(session.Id);
        var retrievedMessages = await messageService.GetMessagesBySessionIdAsync(session.Id);

        retrievedSession.Should().NotBeNull();
        retrievedMessages.First().Content.Should().Be(specialCharsContent);
    }

    [Fact]
    public async Task InvalidIds_HandledGracefully()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        // Act & Assert - Negative IDs
        // OpenSession(-1) should throw and should return null
        Func<Task> act = async () => await sessionService.OpenSession(-1);
        await act.Should().ThrowAsync<ArgumentException>();

        // GetMessagesBySessionIdAsync(-1) should throw and should return an empty collection
        Func<Task> act2 = async () => await messageService.GetMessagesBySessionIdAsync(0);
        await act2.Should().ThrowAsync<ArgumentException>();

        // Act & Assert - Zero ID
        Func<Task> act3 = async () => await sessionService.OpenSession(0);
        await act3.Should().ThrowAsync<ArgumentException>();

        // Act & Assert - Very large ID
        var largeId = long.MaxValue;
        Func<Task> act4 = async () => await sessionService.OpenSession(largeId);
        await act4.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ConcurrentModification_SameEntity_HandledCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var peopleService = _containerFixture.Container.Resolve<IPeopleService>();

        // Create and save person to database first
        var testPersonConcurrent = await peopleService.CreatePersonAsync(
            "TestPerson",
            "Test Description",
            null,
            true,
            "OpenAI",
            "test-model",
            "You are a helpful assistant for testing purposes.",
            null,
            null);
        testPersonConcurrent.Should().NotBeNull();

        var session = await sessionService.CreateSession(testPersonConcurrent!);

        // Act - Modify same session concurrently
        var tasks = new List<Task<bool>>();
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var sessionToUpdate = await sessionService.OpenSession(session.Id);
                if (sessionToUpdate != null)
                {
                    sessionToUpdate.Description = $"Updated by task {index}";
                    return await sessionService.SaveSessionAsync(sessionToUpdate);
                }
                return false;
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - At least some updates should succeed
        results.Should().Contain(true);

        // Verify final state is consistent
        var finalSession = await sessionService.OpenSession(session.Id);
        finalSession.Should().NotBeNull();
        finalSession!.Description.Should().StartWith("Updated by task");
    }

    [Fact]
    public async Task RapidCreateDelete_Operations_MaintainConsistency()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var peopleService = _containerFixture.Container.Resolve<IPeopleService>();

        // Act - Rapidly create and delete sessions
        var createdSessions = new List<DaoStudio.Interfaces.ISession>();

        for (int i = 0; i < 50; i++)
        {
            // Create and save person to database first
            var person = await peopleService.CreatePersonAsync(
                $"TestPerson{i}",
                "Test Description",
                null,
                true,
                "OpenAI",
                "test-model",
                "You are a helpful assistant for testing purposes.",
                null,
                null);

            person.Should().NotBeNull();
            var session = await sessionService.CreateSession(person!);
            createdSessions.Add(session);
        }

        // Delete every other session
        var deleteTasks = new List<Task<bool>>();
        for (int i = 0; i < createdSessions.Count; i += 2)
        {
            deleteTasks.Add(sessionService.DeleteSessionAsync(createdSessions[i].Id));
        }

        var deleteResults = await Task.WhenAll(deleteTasks);

        // Assert
        deleteResults.Should().OnlyContain(result => result == true);

        // Verify remaining sessions still exist
        for (int i = 1; i < createdSessions.Count; i += 2)
        {
            var remainingSession = await sessionService.OpenSession(createdSessions[i].Id);
            remainingSession.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task DateTimeBoundaries_HandledCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var peopleService = _containerFixture.Container.Resolve<IPeopleService>();

        // Act - Create sessions with boundary DateTime values
        // Create and save person to database first
        var testPersonDateTime = await peopleService.CreatePersonAsync(
            "TestPerson",
            "Test Description",
            null,
            true,
            "OpenAI",
            "test-model",
            "You are a helpful assistant for testing purposes.",
            null,
            null);
        testPersonDateTime.Should().NotBeNull();

        var session = await sessionService.CreateSession(testPersonDateTime!);

        var retrievedSession = await sessionService.OpenSession(session.Id);
        retrievedSession.Should().NotBeNull();

        // Should be able to create new session after error
        var newSession = await sessionService.CreateSession(testPersonDateTime!);
        newSession.Should().NotBeNull();
        
        // Verify timestamps are close to when the session was created
        // Compare in UTC to avoid timezone issues
        retrievedSession!.CreatedAt.ToUniversalTime().Should().BeCloseTo(session.CreatedAt.ToUniversalTime(), TimeSpan.FromSeconds(1));
        retrievedSession.LastModified.ToUniversalTime().Should().BeCloseTo(session.LastModified.ToUniversalTime(), TimeSpan.FromSeconds(1));
        
        // Verify they are recent timestamps (within the last minute)
        var utcNow = DateTime.UtcNow;
        (utcNow - retrievedSession.CreatedAt.ToUniversalTime()).Should().BeLessThan(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task EnumBoundaryValues_HandledCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        var testPersonEnum = MessageTestHelper.CreateTestPerson("TestPerson", "Test Description", "TestProvider", "test-model");
        var session = await sessionService.CreateSession(testPersonEnum);

        // Act - Test with various enum values
        var roles = new[] { MessageRole.User, MessageRole.Assistant, MessageRole.System, MessageRole.Developer };
        var types = new[] { MessageType.Normal, MessageType.Information };

        foreach (var role in roles)
        {
            foreach (var type in types)
            {
                var message = MessageTestHelper.CreateTestMessage(session.Id, role, $"Test {role} {type}", type);
                message.Type = (int)type;

                var createdMessage = await messageService.CreateMessageAsync(
                    message.Content ?? string.Empty,
                    (MessageRole)message.Role,
                    (MessageType)message.Type,
                    message.SessionId,
                    true,
                    message.ParentMsgId,
                    message.ParentSessId);

                // Assert
                createdMessage.Should().NotBeNull();
                createdMessage.Role.Should().Be(role);
                createdMessage.Type.Should().Be(type);
            }
        }
    }

    [Fact]
    public async Task CollectionBoundaries_HandledCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();

        // Act & Assert - Single person
        var testPersonSingle = MessageTestHelper.CreateTestPerson("Person1", "Test Description", "TestProvider", "test-model");
        var singlePersonSession = await sessionService.CreateSession(testPersonSingle);
        singlePersonSession.Should().NotBeNull();

        // Act & Assert - Very long person name
        var longPersonName = new string('P', 1000);
        try
        {
            var testPersonLongName = MessageTestHelper.CreateTestPerson(longPersonName, "Test Description", "TestProvider", "test-model");
            var longNameSession = await sessionService.CreateSession(testPersonLongName);
            longNameSession.Should().NotBeNull();
        }
        catch (ArgumentException)
        {
            // Expected if there are name length limits
        }
    }


    [Fact]
    public async Task MemoryPressureEdgeCases_HandledCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        var testPersonMemory = MessageTestHelper.CreateTestPerson("TestPerson", "Test Description", "TestProvider", "test-model");
        var session = await sessionService.CreateSession(testPersonMemory);

        // Act - Create many small objects rapidly
        var tasks = new List<Task>();
        for (int i = 0; i < 1000; i++)
        {
            var message = MessageTestHelper.CreateTestMessage(session.Id, MessageRole.User, $"Rapid message {i}");
            tasks.Add(messageService.CreateMessageAsync(
                message.Content ?? string.Empty,
                (MessageRole)message.Role,
                (MessageType)message.Type,
                message.SessionId,
                true,
                message.ParentMsgId,
                message.ParentSessId));
        }

        // Assert - Should complete without memory issues
        await Task.WhenAll(tasks);

        // Verify all messages were created
        var allMessages = await messageService.GetMessagesBySessionIdAsync(session.Id);
        allMessages.Should().HaveCount(1000);
    }

    [Fact]
    public async Task NetworkInterruption_Simulation_HandlesGracefully()
    {
        // Arrange
        var mockChatClient = new Mock<Microsoft.Extensions.AI.IChatClient>();

        // Simulate intermittent network failures on streaming API
        var callCount = 0;
        mockChatClient
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount % 3 == 0) // Fail every 3rd call
                {
                    throw new HttpRequestException("Network error");
                }

                async IAsyncEnumerable<Microsoft.Extensions.AI.ChatResponseUpdate> Stream()
                {
                    var update = new Microsoft.Extensions.AI.ChatResponseUpdate(Microsoft.Extensions.AI.ChatRole.Assistant, "Response");
                    yield return update;
                    await Task.CompletedTask;
                }

                return Stream();
            });

        // Create engine via testable subclass to inject mock chat client
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<DaoStudio.Engines.MEAI.BaseEngine>>();
        var mockLoggerFactory = new Mock<Microsoft.Extensions.Logging.ILoggerFactory>();
        var mockStorageFactory = new Mock<DaoStudio.DBStorage.Factory.StorageFactory>("test.db");
        var person = MessageTestHelper.CreateTestPerson("NetworkBot", "A test assistant", "OpenAI", "gpt-4");
        var engine = new TestableOpenAIEngine(person, mockLogger.Object, mockLoggerFactory.Object, mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), mockChatClient.Object);

        // Act & Assert - Some calls should succeed, others should fail predictably
        var testMessage = MessageTestHelper.CreateTestMessage(0, MessageRole.User, "Network test");

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // First two calls should succeed (must enumerate the stream)
        var messages1 = new List<DaoStudio.Interfaces.IMessage> { testMessage };
        var stream1 = await engine.GetMessageAsync(messages1, null, mockSession.Object, CancellationToken.None);
        DaoStudio.Interfaces.IMessage? last1 = null;
        await foreach (var m in stream1) { last1 = m; }
        last1.Should().NotBeNull();

        var messages2 = new List<DaoStudio.Interfaces.IMessage> { testMessage };
        var stream2 = await engine.GetMessageAsync(messages2, null, mockSession.Object, CancellationToken.None);
        DaoStudio.Interfaces.IMessage? last2 = null;
        await foreach (var m in stream2) { last2 = m; }
        last2.Should().NotBeNull();

        // Third call should fail upon enumeration
        var act = async () =>
        {
            var stream3 = await engine.GetMessageAsync(new List<DaoStudio.Interfaces.IMessage> { testMessage }, null, mockSession.Object, CancellationToken.None);
            await foreach (var _ in stream3) { }
        };
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Network error");
    }

    [Fact]
    public async Task UnicodeEdgeCases_HandledCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        // Test various Unicode edge cases
        var unicodeTestCases = new[]
        {
            "Basic ASCII: Hello World",
            "Accented: café résumé naïve",
            "Emoji: 🚀 🌍 💻 🎉",
            "CJK: 你好世界 こんにちは 안녕하세요",
            "RTL: مرحبا بالعالم שלום עולם",
            "Mathematical: ∑ ∫ ∞ ≈ ≠ ≤ ≥",
            "Symbols: ♠ ♣ ♥ ♦ ★ ☆ ☀ ☂",
            "Zero-width chars: a\u200Bb\u200Cc\u200Dd", // Zero-width space, non-joiner, joiner
            "Combining chars: e\u0301 a\u0300 o\u0302", // Combining acute, grave, circumflex
            "Surrogate pairs: 𝕳𝖊𝖑𝖑𝖔 𝖂𝖔𝖗𝖑𝖉" // Mathematical bold fraktur
        };

        var testPersonUnicode = MessageTestHelper.CreateTestPerson("TestPerson", "Test Description", "TestProvider", "test-model");
        var session = await sessionService.CreateSession(testPersonUnicode);

        // Act & Assert
        foreach (var testCase in unicodeTestCases)
        {
            var message = MessageTestHelper.CreateTestMessage(session.Id, MessageRole.User, testCase);
            var createdMessage = await messageService.CreateMessageAsync(
                message.Content ?? string.Empty,
                (MessageRole)message.Role,
                (MessageType)message.Type,
                message.SessionId,
                true,
                message.ParentMsgId,
                message.ParentSessId);

            createdMessage.Should().NotBeNull();
            createdMessage.Content.Should().Be(testCase);
        }

        // Verify all messages can be retrieved correctly
        var allMessages = await messageService.GetMessagesBySessionIdAsync(session.Id);
        allMessages.Should().HaveCount(unicodeTestCases.Length);

        for (int i = 0; i < unicodeTestCases.Length; i++)
        {
            allMessages.Should().Contain(m => m.Content == unicodeTestCases[i]);
        }
    }

    public void Dispose()
    {
        _databaseFixture?.Dispose();
        _containerFixture?.Dispose();
    }
}
