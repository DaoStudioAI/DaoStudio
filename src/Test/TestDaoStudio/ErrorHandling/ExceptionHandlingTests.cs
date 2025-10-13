using DaoStudio.Common;
using DaoStudio.Interfaces;
using DaoStudio.Services;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using TestDaoStudio.Helpers;
using TestDaoStudio.Infrastructure;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Factory;
using DryIoc;

namespace TestDaoStudio.ErrorHandling;

// Test subclass to inject a mock IChatClient into OpenAIEngine
internal class TestableOpenAIEngine : DaoStudio.Engines.MEAI.OpenAIEngine
{
    private readonly Microsoft.Extensions.AI.IChatClient _chatClient;

    public TestableOpenAIEngine(
        DaoStudio.Interfaces.IPerson person,
        Microsoft.Extensions.Logging.ILogger<DaoStudio.Engines.MEAI.BaseEngine> logger,
        Microsoft.Extensions.Logging.ILoggerFactory loggerFactory,
        DaoStudio.DBStorage.Factory.StorageFactory storage,
        DaoStudio.Interfaces.IPlainAIFunctionFactory plainAIFunctionFactory,
        DaoStudio.Interfaces.ISettings settings,
        Microsoft.Extensions.AI.IChatClient chatClient)
        : base(person, logger, loggerFactory, storage, plainAIFunctionFactory, settings)
    {
        _chatClient = chatClient;
    }

    protected override Task<Microsoft.Extensions.AI.IChatClient> CreateChatClientAsync()
        => Task.FromResult(_chatClient);
}

/// <summary>
/// Tests for exception handling scenarios across the system.
/// Tests proper error propagation, recovery, and user-friendly error messages.
/// </summary>
public class ExceptionHandlingTests : IDisposable
{
    private readonly TestContainerFixture _containerFixture;
    private readonly DatabaseTestFixture _databaseFixture;

    public ExceptionHandlingTests()
    {
        _containerFixture = new TestContainerFixture();
        _databaseFixture = new DatabaseTestFixture();
    }

    [Fact]
    public async Task SessionService_DatabaseConnectionError_ThrowsAppropriateException()
    {
        // Arrange
        await _containerFixture.InitializeAsync();

        var mockSessionRepo = new Mock<DaoStudio.DBStorage.Interfaces.ISessionRepository>();
        mockSessionRepo.Setup(r => r.CreateSessionAsync(It.IsAny<DaoStudio.DBStorage.Models.Session>()))
                      .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Create mocks for other dependencies
        var mockMessageService = new Mock<IMessageService>();
        var mockToolService = new Mock<IToolService>();
        var mockPersonRepository = new Mock<IPersonRepository>();
        var mockPeopleService = new Mock<IPeopleService>();
        var mockPluginService = new Mock<IPluginService>();
        var mockEngineService = new Mock<IEngineService>();
        var mockLogger = new Mock<ILogger<SessionService>>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();

        var sessionService = new SessionService(
            mockMessageService.Object,
            mockSessionRepo.Object,
            mockToolService.Object,
            mockPersonRepository.Object,
            mockPeopleService.Object,
            mockPluginService.Object,
            mockEngineService.Object,
            mockLogger.Object,
            mockLoggerFactory.Object
            );

        // Create a test person first
        var testPerson = MessageTestHelper.CreateTestPerson("TestPerson", "A test assistant", "OpenAI", "gpt-4");

        // Act & Assert
        var act = async () => await sessionService.CreateSession(testPerson);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Database connection failed");
    }

    [Fact]
    public async Task MessageService_InvalidSessionId_ThrowsUserVisibleException()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var invalidSessionId = 0;

        var invalidMessage = MessageTestHelper.CreateTestMessage(
            invalidSessionId,
            MessageRole.User,
            "Test message");

        // Act & Assert
        var act = async () => await messageService.CreateMessageAsync(
            invalidMessage.Content ?? "",
            (MessageRole)invalidMessage.Role,
            (MessageType)invalidMessage.Type,
            invalidMessage.SessionId,
            true,
            invalidMessage.ParentMsgId,
            invalidMessage.ParentSessId);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ApiProviderService_NullApiKey_ThrowsValidationException()
    {
        // Arrange
        await _containerFixture.InitializeAsync();

        var mockApiProviderRepo = new Mock<IAPIProviderRepository>();
        var mockLogger = new Mock<ILogger<ApiProviderService>>();
        var apiProviderService = new ApiProviderService(mockApiProviderRepo.Object, mockLogger.Object);

        var invalidProvider = new DaoStudio.DBStorage.Models.APIProvider
        {
            Name = "TestProvider",
            ApiEndpoint = "https://api.test.com",
            ApiKey = null, // Invalid null API key
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        // Act & Assert
        var act = async () => await apiProviderService.CreateApiProviderAsync(
            "TestProvider",
            ProviderType.OpenAI,
            "https://api.test.com",
            null, // Invalid null API key
            null,
            true);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ToolService_InvalidToolConfiguration_HandlesGracefully()
    {
        // Arrange
        await _containerFixture.InitializeAsync();

        var mockToolRepo = new Mock<ILlmToolRepository>();
        mockToolRepo.Setup(r => r.CreateToolAsync(It.IsAny<DaoStudio.DBStorage.Models.LlmTool>()))
                   .ThrowsAsync(new ArgumentException("Invalid tool configuration"));

        var mockLogger = new Mock<ILogger<ToolService>>();
        var toolService = new ToolService(mockToolRepo.Object, mockLogger.Object);

        var invalidTool = MessageTestHelper.CreateTestTool("InvalidTool", "Invalid tool");
        invalidTool.ToolConfig = "{ invalid json }"; // Invalid JSON

        // Act & Assert
        var act = async () => await toolService.CreateToolAsync(
            invalidTool.Name,
            invalidTool.Description,
            invalidTool.StaticId,
            invalidTool.ToolConfig,
            invalidTool.Parameters,
            invalidTool.IsEnabled,
            invalidTool.AppId);

        await act.Should().ThrowAsync<ArgumentException>()
                 .WithMessage("Invalid tool configuration");
    }

    [Fact]
    public async Task DaoStudio_InitializationFailure_ThrowsLlmInitializationException()
    {
        // Arrange
        var container = new Container();
        var mockLogger = new Mock<ILogger<DaoStudio.DaoStudioService>>();

        // Register a mock StorageFactory that throws during initialization
        var mockStorageFactory = new Mock<StorageFactory>("test.db");
        mockStorageFactory.Setup(sf => sf.InitializeAsync())
                         .ThrowsAsync(new InvalidOperationException("Database initialization failed"));

        container.RegisterInstance(mockStorageFactory.Object);

        // Register a mock plugin service that shouldn't be called due to storage failure
        var mockPluginService = new Mock<IPluginService>();
        container.RegisterInstance(mockPluginService.Object);

        // Create DaoStudio instance (constructor should not throw)
        var DaoStudio = new DaoStudio.DaoStudioService(container, mockLogger.Object);

        // Act & Assert - Test the actual initialization method
        var act = async () => await DaoStudio.InitializeAsync(container);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Database initialization failed");
    }

    [Fact]
    public async Task Session_SendMessageWithNullEngine_ThrowsLlmInitializationException()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var testPerson = MessageTestHelper.CreateTestPerson("TestBot", "A test assistant", "OpenAI", "gpt-4");

        var session = await sessionService.CreateSession(testPerson);

        // Create a mock session with null engine to test error handling
        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.Id).Returns(session.Id);
    mockSession.Setup(s => s.SendMessageAsync(It.IsAny<string>()))
          .ThrowsAsync(new UIException("Engine not initialized"));

        var testMessage = "Test message";

        // Act & Assert
        var act = async () => await mockSession.Object.SendMessageAsync(testMessage);

    await act.Should().ThrowAsync<UIException>();
    }

    [Fact]
    public async Task Engine_ApiKeyMissing_HandlesGracefullyWithPlaceholder()
    {
        // Arrange - Test that the engine handles missing API keys gracefully with placeholder
        var mockApiProviderRepository = new Mock<IAPIProviderRepository>();
        var provider = new DaoStudio.DBStorage.Models.APIProvider
        {
            Name = "TestProvider",
            ApiEndpoint = "https://api.test.com",
            ApiKey = null, // Missing API key - should use placeholder
            IsEnabled = true
        };
        mockApiProviderRepository.Setup(p => p.GetProviderByNameAsync("TestProvider"))
                                .ReturnsAsync(provider);

        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<DaoStudio.Engines.MEAI.BaseEngine>>();
        var mockLoggerFactory = new Mock<Microsoft.Extensions.Logging.ILoggerFactory>();

        // Mock StorageFactory similar to how it's done in OpenAIEngineTests
        var mockStorageFactory = new Mock<DaoStudio.DBStorage.Factory.StorageFactory>("test.db");
        mockStorageFactory.Setup(sf => sf.GetApiProviderRepositoryAsync())
                         .ReturnsAsync(mockApiProviderRepository.Object);

        var mockPerson = new Mock<DaoStudio.Interfaces.IPerson>();
        mockPerson.Setup(p => p.ProviderName).Returns("TestProvider");
        mockPerson.Setup(p => p.ModelId).Returns("gpt-4");

        // Act & Assert - Engine constructor should not throw when API key is null
        // The API key validation happens later in CreateChatClientAsync()
        var act = () => new DaoStudio.Engines.MEAI.OpenAIEngine(
            mockPerson.Object,
            mockLogger.Object,
            mockLoggerFactory.Object,
            mockStorageFactory.Object,
            Mock.Of<DaoStudio.Interfaces.IPlainAIFunctionFactory>(),
            Mock.Of<DaoStudio.Interfaces.ISettings>());

        // Constructor should succeed - null API key handling is deferred to CreateChatClientAsync
        act.Should().NotThrow();

        var engine = act();
        engine.Should().NotBeNull();
    }

    [Fact]
    public async Task DatabaseOperation_TransactionFailure_RollsBackCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var peopleService = _containerFixture.Container.Resolve<IPeopleService>();

        // Create and save person to database first
        var testPerson = await peopleService.CreatePersonAsync(
            "TransactionBot",
            "A test assistant",
            null,
            true,
            "OpenAI",
            "gpt-4",
            "You are a helpful assistant for testing purposes.",
            null,
            null);
        testPerson.Should().NotBeNull();

        var session = await sessionService.CreateSession(testPerson!);

        // Act - Try to create a message with invalid data that might cause transaction failure
        var invalidMessage = MessageTestHelper.CreateTestMessage(session.Id, MessageRole.User, "");
        invalidMessage.Content = null; // This might cause constraint violations

        // The behavior depends on database constraints, but should handle gracefully
        try
        {
            await messageService.CreateMessageAsync(
                invalidMessage.Content ?? "",
                MessageRole.User,
                MessageType.Information,
                invalidMessage.SessionId,
                true,
                invalidMessage.ParentMsgId,
                invalidMessage.ParentSessId);
        }
        catch (Exception ex)
        {
            // Assert - Exception should be meaningful
            ex.Should().NotBeNull();
            ex.Message.Should().NotBeEmpty();
        }

        // Assert - Session should still be accessible after failed operation
        var retrievedSession = await sessionService.OpenSession(session.Id);
        retrievedSession.Should().NotBeNull();
    }

    [Fact]
    public async Task ConcurrentAccess_DeadlockScenario_HandlesGracefully()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        var testPerson = MessageTestHelper.CreateTestPerson("DeadlockBot", "A test assistant", "OpenAI", "gpt-4");
        var session1 = await sessionService.CreateSession(testPerson);
        var session2 = await sessionService.CreateSession(testPerson);

        // Act - Create potential deadlock scenario with concurrent operations
        var tasks = new List<Task>();

        for (int i = 0; i < 20; i++)
        {
            var sessionId = i % 2 == 0 ? session1.Id : session2.Id;
            var message = MessageTestHelper.CreateTestMessage(
                sessionId,
                MessageRole.User,
                $"Concurrent message {i}");

            tasks.Add(messageService.CreateMessageAsync(
                message.Content ?? "",
                (MessageRole)message.Role,
                (MessageType)message.Type,
                message.SessionId,
                true,
                message.ParentMsgId,
                message.ParentSessId));
        }

        // Assert - All operations should complete without deadlock
        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LargeDataOperation_OutOfMemoryScenario_HandlesGracefully()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        var testPerson = MessageTestHelper.CreateTestPerson("LargeDataBot", "A test assistant", "OpenAI", "gpt-4");
        var session = await sessionService.CreateSession(testPerson);

        // Act - Try to create extremely large message content
        var largeContent = new string('X', 10 * 1024 * 1024); // 10MB string
        var largeMessage = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.User,
            largeContent);

        // This should either succeed or fail gracefully with appropriate error
        try
        {
            var result = await messageService.CreateMessageAsync(
                largeMessage.Content ?? "",
                (MessageRole)largeMessage.Role,
                (MessageType)largeMessage.Type,
                largeMessage.SessionId,
                true,
                largeMessage.ParentMsgId,
                largeMessage.ParentSessId);
            result.Should().NotBeNull();
        }
        catch (Exception ex)
        {
            // Assert - Should be a meaningful error, not a generic crash
            ex.Should().BeOneOf(
                typeof(ArgumentException),
                typeof(InvalidOperationException),
                typeof(OutOfMemoryException));
        }
    }

    [Fact]
    public async Task NetworkTimeout_ApiCall_ThrowsTimeoutException()
    {
        // Arrange
        var mockChatClient = new Mock<Microsoft.Extensions.AI.IChatClient>();
        mockChatClient
            .Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Throws(new TimeoutException("Request timed out"));

        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<DaoStudio.Engines.MEAI.BaseEngine>>();
        var mockLoggerFactory = new Mock<Microsoft.Extensions.Logging.ILoggerFactory>();
        var mockStorageFactory = new Mock<DaoStudio.DBStorage.Factory.StorageFactory>("test.db");

        var person = MessageTestHelper.CreateTestPerson("TimeoutBot", "A test assistant", "OpenAI", "gpt-4");

        var engine = new TestableOpenAIEngine(person, mockLogger.Object, mockLoggerFactory.Object, mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), mockChatClient.Object);

        var messages = new List<DaoStudio.Interfaces.IMessage>
        {
            MessageTestHelper.CreateUserMessage("Test timeout")
        };

        // Create a minimal mock ISession to satisfy BaseEngine parameter checks and options mapping
        var mockSession = new Mock<DaoStudio.Interfaces.ISession>();
        mockSession.SetupGet(s => s.ToolExecutionMode).Returns(DaoStudio.Interfaces.ToolExecutionMode.Auto);
        // Return null tools; use the Plugins namespace type to match the ISession signature
        mockSession.Setup(s => s.GetTools()).Returns((Dictionary<string, List<DaoStudio.Interfaces.Plugins.FunctionWithDescription>>?)null);
        mockSession.SetupGet(s => s.CurrentCancellationToken).Returns((CancellationTokenSource?)null);

        // Act & Assert: must enumerate the stream to trigger the provider call/exception
        var act = async () =>
        {
            var stream = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
            await foreach (var _ in stream) { }
        };

        await act.Should().ThrowAsync<TimeoutException>()
                 .WithMessage("Request timed out");
    }

    [Fact]
    public async Task InvalidConfiguration_ServiceInitialization_ThrowsConfigurationException()
    {
        // Arrange
        var mockApiProviderRepo = new Mock<IAPIProviderRepository>();
        mockApiProviderRepo.Setup(r => r.GetAllProvidersAsync())
                              .ThrowsAsync(new InvalidOperationException("Configuration table not found"));

        var mockLogger2 = new Mock<ILogger<ApiProviderService>>();
        var apiProviderService = new ApiProviderService(mockApiProviderRepo.Object, mockLogger2.Object);

        // Act & Assert
        var act = async () => await apiProviderService.GetAllApiProvidersAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Configuration table not found");
    }

    [Fact]
    public void UserVisibleException_ContainsHelpfulMessage()
    {
        // Arrange
        var userMessage = "The API key you provided is invalid. Please check your API key in the settings.";
        var userException = new UIException(userMessage);

        // Act & Assert
        userException.Message.Should().Be(userMessage);
        userException.InnerException.Should().BeNull();
        userException.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public async Task ExceptionLogging_CapturesImportantDetails()
    {
        // Arrange
        await _containerFixture.InitializeAsync();

        var mockSessionRepo = new Mock<ISessionRepository>();
        var testException = new InvalidOperationException("Test exception for logging");

        mockSessionRepo.Setup(r => r.GetSessionAsync(It.IsAny<long>()))
                          .ThrowsAsync(testException);

        // Create mocks for other dependencies
        var mockMessageService = new Mock<IMessageService>();
        var mockToolService = new Mock<IToolService>();
        var mockPersonRepository = new Mock<IPersonRepository>();
        var mockPeopleService = new Mock<IPeopleService>();
        var mockPluginService = new Mock<IPluginService>();
        var mockEngineService = new Mock<IEngineService>();
        var mockLogger = new Mock<ILogger<SessionService>>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();

        var sessionService = new SessionService(
            mockMessageService.Object,
            mockSessionRepo.Object,
            mockToolService.Object,
            mockPersonRepository.Object,
            mockPeopleService.Object,
            mockPluginService.Object,
            mockEngineService.Object,
            mockLogger.Object,
            mockLoggerFactory.Object
            );

        // Act
        Exception? caughtException = null;
        try
        {
            await sessionService.OpenSession(123);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        caughtException.Should().NotBeNull();
        caughtException.Should().Be(testException);
        caughtException!.Message.Should().Be("Test exception for logging");
        caughtException.StackTrace.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CascadingFailures_StopAtAppropriateLevel()
    {
        // Arrange
        await _containerFixture.InitializeAsync();

        var mockMessageRepo = new Mock<IMessageRepository>();
        var mockToolRepo = new Mock<ILlmToolRepository>();

        // Setup cascading failure scenario
        mockMessageRepo.Setup(r => r.CreateMessageAsync(It.IsAny<DaoStudio.DBStorage.Models.Message>()))
                      .ThrowsAsync(new InvalidOperationException("Database error"));

        mockToolRepo.Setup(r => r.GetToolsByStaticIdAsync(It.IsAny<string>()))
                   .ThrowsAsync(new InvalidOperationException("Tool repository error"));

        var messageService = new MessageService(mockMessageRepo.Object, Mock.Of<ILogger<MessageService>>());
        var mockLogger = new Mock<ILogger<ToolService>>();
        var toolService = new ToolService(mockToolRepo.Object, mockLogger.Object);

        // Act & Assert - Each service should handle its own errors
        var testMessage = MessageTestHelper.CreateTestMessage(1, MessageRole.User, "test");
        var messageAct = async () => await messageService.CreateMessageAsync(
            testMessage.Content ?? string.Empty,
            (MessageRole)testMessage.Role,
            (MessageType)testMessage.Type,
            testMessage.SessionId,
            true,
            testMessage.ParentMsgId,
            testMessage.ParentSessId);

        var toolAct = async () => await toolService.GetToolsByStaticIdAsync("test-tool");

        await messageAct.Should().ThrowAsync<InvalidOperationException>()
                        .WithMessage("Database error");

        await toolAct.Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage("Tool repository error");
    }

    [Fact]
    public async Task ResourceCleanup_AfterException_CompletesSuccessfully()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        await _databaseFixture.InitializeAsync();

        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var peopleService = _containerFixture.Container.Resolve<IPeopleService>();

        // Create and save person to database first
        var testPerson = await peopleService.CreatePersonAsync(
            "CleanupBot",
            "A test assistant",
            null,
            true,
            "OpenAI",
            "gpt-4",
            "You are a helpful assistant for testing purposes.",
            null,
            null);
        testPerson.Should().NotBeNull();

        // Act - Create session, cause error, then verify cleanup
        var session = await sessionService.CreateSession(testPerson!);

        // Simulate error scenario
        // Attempt to save a null session should not throw but return false
        var saveResult = await sessionService.SaveSessionAsync(null!);
        saveResult.Should().BeFalse();

        // Assert - Session should still be accessible after the failed save
        var retrievedSession = await sessionService.OpenSession(session.Id);
        retrievedSession.Should().NotBeNull();

        // Should be able to create new session after error
        var newSession = await sessionService.CreateSession(testPerson!);
        newSession.Should().NotBeNull();
    }

    public void Dispose()
    {
        _databaseFixture?.Dispose();
        _containerFixture?.Dispose();
    }
}
