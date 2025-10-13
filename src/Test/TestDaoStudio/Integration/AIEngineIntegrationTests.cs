
using DaoStudio.Engines.MEAI;
using DaoStudio.Interfaces;
using DaoStudio.DBStorage.Factory;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Helpers;
using TestDaoStudio.Infrastructure;
using TestDaoStudio.ErrorHandling;
using System.Text;

namespace TestDaoStudio.Integration;

/// <summary>
/// Integration tests for AI engine functionality.
/// Tests engine initialization, message processing, model interactions, and error handling.
/// </summary>
public class AIEngineIntegrationTests : IDisposable
{
    private readonly TestContainerFixture _containerFixture;

    // Testable subclasses for engines to inject mocked IChatClient
    internal class TestableGoogleEngine : GoogleEngine
    {
        private readonly IChatClient _chatClient;
        public TestableGoogleEngine(IPerson person, ILogger<BaseEngine> logger, StorageFactory storage, IPlainAIFunctionFactory plainAIFunctionFactory, ISettings settingsService, IChatClient chatClient)
            : base(person, logger, storage, plainAIFunctionFactory, settingsService) => _chatClient = chatClient;
        protected override Task<IChatClient> CreateChatClientAsync() => Task.FromResult(_chatClient);
    }

    internal class TestableAWSBedrockEngine : AWSBedrockEngine
    {
        private readonly IChatClient _chatClient;
        public TestableAWSBedrockEngine(IPerson person, ILogger<BaseEngine> logger, ILoggerFactory loggerFactory, StorageFactory storage, IPlainAIFunctionFactory plainAIFunctionFactory, ISettings settingsService, IChatClient chatClient)
            : base(person, logger, loggerFactory, storage, plainAIFunctionFactory, settingsService) => _chatClient = chatClient;
        protected override Task<IChatClient> CreateChatClientAsync() => Task.FromResult(_chatClient);
    }

    internal class TestableOllamaEngine : OllamaEngine
    {
        private readonly IChatClient _chatClient;
        public TestableOllamaEngine(IPerson person, ILogger<BaseEngine> logger, ILoggerFactory loggerFactory, StorageFactory storage, IPlainAIFunctionFactory plainAIFunctionFactory, ISettings settingsService, IChatClient chatClient)
            : base(person, logger, loggerFactory, storage, plainAIFunctionFactory, settingsService) => _chatClient = chatClient;
        protected override Task<IChatClient> CreateChatClientAsync() => Task.FromResult(_chatClient);
    }

    internal class TestableOpenAIEngine : OpenAIEngine
    {
        private readonly IChatClient _chatClient;
        public TestableOpenAIEngine(IPerson person, ILogger<BaseEngine> logger, ILoggerFactory loggerFactory, StorageFactory storage, IPlainAIFunctionFactory plainAIFunctionFactory, ISettings settingsService, IChatClient chatClient)
            : base(person, logger, loggerFactory, storage, plainAIFunctionFactory, settingsService) => _chatClient = chatClient;
        protected override Task<IChatClient> CreateChatClientAsync() => Task.FromResult(_chatClient);
    }

    public AIEngineIntegrationTests()
    {
        _containerFixture = new TestContainerFixture();
    }

    [Fact]
    public async Task OpenAIEngine_SendMessage_ProcessesCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetStreamingResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { MakeUpdate("Hello! How can I help you today?") }));

        var person = MessageTestHelper.CreateTestPerson("OpenAI Assistant", "Test", "OpenAI", "gpt-4");
        var engine = new TestableOpenAIEngine(
            person,
            Mock.Of<ILogger<BaseEngine>>(),
            Mock.Of<ILoggerFactory>(),
            new Mock<StorageFactory>("test.db").Object, 
            Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), mockChatClient.Object);

        // Act
        var messages = new List<IMessage> { CreateTestMessage("Hello, AI!", MessageRole.User) };
        var responseStream = await engine.GetMessageAsync(messages, null, CreateMockSession(), CancellationToken.None);

        // Assert
        responseStream.Should().NotBeNull();
        var responseList = new List<IMessage>();
        await foreach (var msg in responseStream)
        {
            responseList.Add(msg);
        }
        responseList.Should().NotBeEmpty();
        responseList.Last().Content.Should().Be("Hello! How can I help you today?");
        responseList.Last().Role.Should().Be(MessageRole.Assistant);

        mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GoogleEngine_SendMessage_ProcessesCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetStreamingResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { MakeUpdate("I'm Gemini, Google's AI assistant. How can I help?") }));

        var person = MessageTestHelper.CreateTestPerson("Gemini", "Test", "Google", "gemini-pro");
        var engine = new TestableGoogleEngine(
            person,
            Mock.Of<ILogger<BaseEngine>>(),
            new Mock<StorageFactory>("test.db").Object,
            Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), mockChatClient.Object);

        // Act
        var messages = new List<IMessage> { CreateTestMessage("What are you?", MessageRole.User) };
        var responseStream = await engine.GetMessageAsync(messages, null, CreateMockSession(), CancellationToken.None);

        // Assert
        responseStream.Should().NotBeNull();
        var responseList = new List<IMessage>();
        await foreach (var msg in responseStream)
        {
            responseList.Add(msg);
        }
        responseList.Should().NotBeEmpty();
        responseList.Last().Content.Should().Be("I'm Gemini, Google's AI assistant. How can I help?");
        responseList.Last().Role.Should().Be(MessageRole.Assistant);
    }

    [Fact]
    public async Task AWSBedrockEngine_SendMessage_ProcessesCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetStreamingResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { MakeUpdate("I'm Claude, an AI assistant created by Anthropic.") }));

        var person = MessageTestHelper.CreateTestPerson("Claude", "Test", "AWS", "anthropic.claude-3-haiku");
        var engine = new TestableAWSBedrockEngine(
            person,
            Mock.Of<ILogger<BaseEngine>>(),
            Mock.Of<ILoggerFactory>(),
            new Mock<StorageFactory>("test.db").Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), mockChatClient.Object);

        // Act
        var messages = new List<IMessage> { CreateTestMessage("Who are you?", MessageRole.User) };
        var responseStream = await engine.GetMessageAsync(messages, null, CreateMockSession(), CancellationToken.None);

        // Assert
        responseStream.Should().NotBeNull();
        var responseList = new List<IMessage>();
        await foreach (var msg in responseStream)
        {
            responseList.Add(msg);
        }
        responseList.Should().NotBeEmpty();
        responseList.Last().Content.Should().Be("I'm Claude, an AI assistant created by Anthropic.");
        responseList.Last().Role.Should().Be(MessageRole.Assistant);
    }

    [Fact]
    public async Task OllamaEngine_SendMessage_ProcessesCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetStreamingResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { MakeUpdate("Hello! I'm running locally via Ollama.") }));

        var person = MessageTestHelper.CreateTestPerson("Ollama", "Test", "Ollama", "llama3");
        var engine = new TestableOllamaEngine(
            person,
            Mock.Of<ILogger<BaseEngine>>(),
            Mock.Of<ILoggerFactory>(),
            new Mock<StorageFactory>("test.db").Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), mockChatClient.Object);

        // Act
        var messages = new List<IMessage> { CreateTestMessage("Hello Ollama!", MessageRole.User) };
        var responseStream = await engine.GetMessageAsync(messages, null, CreateMockSession(), CancellationToken.None);

        // Assert
        responseStream.Should().NotBeNull();
        var responseList = new List<IMessage>();
        await foreach (var msg in responseStream)
        {
            responseList.Add(msg);
        }
        responseList.Should().NotBeEmpty();
        responseList.Last().Content.Should().Be("Hello! I'm running locally via Ollama.");
        responseList.Last().Role.Should().Be(MessageRole.Assistant);
    }

    [Fact]
    public async Task Engine_WithSystemMessage_IncludesInContext()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        var mockChatClient = new Mock<IChatClient>();
        var capturedMessages = new List<ChatMessage>();
        mockChatClient.Setup(c => c.GetStreamingResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions, CancellationToken>((messages, options, token) =>
            {
                capturedMessages.AddRange(messages);
            })
            .Returns(CreateAsyncEnumerable(new[] { MakeUpdate("Understood, I'll be helpful.") }));

        var engine = new TestableOpenAIEngine(
            MessageTestHelper.CreateTestPerson("SystemTest", "Test", "OpenAI", "gpt-4"),
            Mock.Of<ILogger<BaseEngine>>(),
            Mock.Of<ILoggerFactory>(),
            new Mock<StorageFactory>("test.db").Object,
            Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), mockChatClient.Object);


        // Act
        var messages = new List<IMessage>
        {
            CreateTestMessage("You are a helpful assistant.", MessageRole.System),
            CreateTestMessage("Hello!", MessageRole.User)
        };
        var responseStream = await engine.GetMessageAsync(messages, null, CreateMockSession(), CancellationToken.None);

        // Enumerate the stream to trigger the execution
        await foreach (var msg in responseStream)
        {
            // We don't need to do anything with the messages, just enumerate to trigger the mock
        }

        // Assert
        capturedMessages.Should().HaveCount(2);
        capturedMessages[0].Role.Should().Be(ChatRole.System);
        capturedMessages[0].Text.Should().Be("You are a helpful assistant.");
        capturedMessages[1].Role.Should().Be(ChatRole.User);
        capturedMessages[1].Text.Should().Be("Hello!");
    }

    [Fact]
    public async Task Engine_WithConversationHistory_MaintainsContext()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        var mockChatClient = new Mock<IChatClient>();
        var capturedMessages = new List<ChatMessage>();
        mockChatClient.Setup(c => c.GetStreamingResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions, CancellationToken>((messages, options, token) =>
            {
                capturedMessages.Clear();
                capturedMessages.AddRange(messages);
            })
            .Returns(CreateAsyncEnumerable(new[] { MakeUpdate("Yes, I remember our previous conversation.") }));

        var engine = new TestableOpenAIEngine(
            MessageTestHelper.CreateTestPerson("HistoryBot", "Test", "OpenAI", "gpt-4"),
            Mock.Of<ILogger<BaseEngine>>(),
            Mock.Of<ILoggerFactory>(),
            new Mock<StorageFactory>("test.db").Object,
            Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), mockChatClient.Object);

        var message1 = MessageTestHelper.CreateUserMessage("My name is John.");
        var message2 = MessageTestHelper.CreateUserMessage("What's my name?");

        // Act: simulate two-turn conversation by passing history on second call
        var stream1 = await engine.GetMessageAsync(new List<IMessage> { message1 }, null, CreateMockSession(), CancellationToken.None);
        await foreach (var _ in stream1) { }
        var stream2 = await engine.GetMessageAsync(new List<IMessage> { message1, message2 }, null, CreateMockSession(), CancellationToken.None);
        await foreach (var _ in stream2) { }

        // Assert
        capturedMessages.Should().HaveCount(2);
        capturedMessages.Should().Contain(m => m.Text == "My name is John.");
        capturedMessages.Should().Contain(m => m.Text == "What's my name?");
    }

    [Fact]
    public async Task Engine_WithCancellation_StopsProcessing()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetStreamingResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .Throws(new OperationCanceledException());

        var engine = new TestableOpenAIEngine(
            MessageTestHelper.CreateTestPerson("CancelBot", "Test", "OpenAI", "gpt-4"),
            Mock.Of<ILogger<BaseEngine>>(),
            Mock.Of<ILoggerFactory>(),
            new Mock<StorageFactory>("test.db").Object,
            Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), mockChatClient.Object);

        var testMessage = MessageTestHelper.CreateUserMessage("This should be cancelled");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert: enumerate to trigger cancellation
        var act = async () =>
        {
            var stream = await engine.GetMessageAsync(new List<IMessage> { testMessage }, null, CreateMockSession(), cts.Token);
            await foreach (var _ in stream) { }
        };
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Engine_WithApiError_ThrowsAppropriateException()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        var mockChatClient = new Mock<IChatClient>();
        mockChatClient.Setup(c => c.GetStreamingResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .Throws(new HttpRequestException("Unauthorized: Invalid API key"));

        var engine = new TestableOpenAIEngine(
            MessageTestHelper.CreateTestPerson("ErrorBot", "Test", "OpenAI", "gpt-4"),
            Mock.Of<ILogger<BaseEngine>>(),
            Mock.Of<ILoggerFactory>(),
            new Mock<StorageFactory>("test.db").Object,
            Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), mockChatClient.Object);

        var testMessage = MessageTestHelper.CreateUserMessage("This should fail");

        // Act & Assert: exception thrown on enumeration
        var act = async () =>
        {
            var stream = await engine.GetMessageAsync(new List<IMessage> { testMessage }, null, CreateMockSession(), CancellationToken.None);
            await foreach (var _ in stream) { }
        };
        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*Invalid API key*");
    }



    [Fact]
    public async Task Engine_WithStreaming_ProcessesStreamCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        var mockChatClient = new Mock<IChatClient>();

        var streamingUpdates = new[]
        {
            MakeUpdate("Hello"),
            MakeUpdate(" there"),
            MakeUpdate("!")
        };

        mockChatClient.Setup(c => c.GetStreamingResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(streamingUpdates));

        var engine = new TestableOpenAIEngine(
            MessageTestHelper.CreateTestPerson("StreamBot", "Test", "OpenAI", "gpt-4"),
            Mock.Of<ILogger<BaseEngine>>(),
            Mock.Of<ILoggerFactory>(),
            new Mock<StorageFactory>("test.db").Object,
            Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), mockChatClient.Object);

        var testMessage = MessageTestHelper.CreateUserMessage("Say hello");

        // Act
        var stream = await engine.GetMessageAsync(new List<IMessage> { testMessage }, null, CreateMockSession(), CancellationToken.None);
        StringBuilder stringBuilder = new ();
        await foreach (var msg in stream)
        {
            stringBuilder.Append( msg.Content);
        }

        // Assert: concatenated content equals combined chunks
        stringBuilder.Should().NotBeNull();
        stringBuilder.ToString().Should().Be("Hello there!");
    }

    [Fact]
    public async Task MultipleEngines_ConcurrentRequests_HandleCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync();
        var mockOpenAiClient = new Mock<IChatClient>();
        var mockGoogleClient = new Mock<IChatClient>();

        mockOpenAiClient.Setup(c => c.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { MakeUpdate("OpenAI response") }));

        mockGoogleClient.Setup(c => c.GetStreamingResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { MakeUpdate("Google response") }));

        var openAiEngine = new TestableOpenAIEngine(
            MessageTestHelper.CreateTestPerson("OpenAI Bot", "Test", "OpenAI", "gpt-4"),
            Mock.Of<ILogger<BaseEngine>>(),
            Mock.Of<ILoggerFactory>(),
            new Mock<StorageFactory>("test.db").Object,
            Mock.Of<IPlainAIFunctionFactory>(),
            Mock.Of<ISettings>(),
            mockOpenAiClient.Object);
        var googleEngine = new TestableGoogleEngine(
            MessageTestHelper.CreateTestPerson("Google Bot", "Test", "Google", "gemini-pro"),
            Mock.Of<ILogger<BaseEngine>>(),
            new Mock<StorageFactory>("test.db").Object,
            Mock.Of<IPlainAIFunctionFactory>(),
            Mock.Of<ISettings>(),
            mockGoogleClient.Object);

        var testMessage = MessageTestHelper.CreateUserMessage("Hello from both engines");

        // Act
        // RunAsync removed — tests use explicit Task.Run blocks below which call GetMessageAsync directly.

        // Helper base type for type inference
        using var _ = new System.IO.MemoryStream(); // no-op to keep usings

        var openAiTask = Task.Run(async () =>
        {
            var s = await openAiEngine.GetMessageAsync(new List<IMessage> { testMessage }, null, CreateMockSession(), CancellationToken.None);
            IMessage? last = null; await foreach (var m in s) { last = m; }
            return last?.Content ?? string.Empty;
        });
        var googleTask = Task.Run(async () =>
        {
            var s = await googleEngine.GetMessageAsync(new List<IMessage> { testMessage }, null, CreateMockSession(), CancellationToken.None);
            IMessage? last = null; await foreach (var m in s) { last = m; }
            return last?.Content ?? string.Empty;
        });

        var responses = await Task.WhenAll(openAiTask, googleTask);

        // Assert
        responses.Should().HaveCount(2);
        responses[0].Should().Be("OpenAI response");
        responses[1].Should().Be("Google response");
    }

    // No longer needed: engines are constructed with IPerson + logger + storage and we inject IChatClient via testable subclasses

    private static ChatResponseUpdate MakeUpdate(string text)
    {
        var update = new ChatResponseUpdate(ChatRole.Assistant, text);
        return update;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateAsyncEnumerable(
        IEnumerable<ChatResponseUpdate> items)
    {
        foreach (var item in items)
        {
            await Task.Delay(1); // Simulate streaming delay
            yield return item;
        }
    }

    private IMessage CreateTestMessage(string content, MessageRole role)
    {
        var mock = new Mock<IMessage>();
        mock.Setup(m => m.Content).Returns(content);
        mock.Setup(m => m.Role).Returns(role);
        mock.Setup(m => m.CreatedAt).Returns(DateTime.UtcNow);
        return mock.Object;
    }

    private ISession CreateMockSession()
    {
        var mock = new Mock<ISession>();
        mock.Setup(s => s.Id).Returns(1);
        return mock.Object;
    }

    public void Dispose()
    {
        _containerFixture?.Dispose();
    }
}
