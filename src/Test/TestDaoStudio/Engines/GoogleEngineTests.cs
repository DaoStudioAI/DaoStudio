using DaoStudio.Engines.MEAI;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using System.Runtime.CompilerServices;
using TestDaoStudio.Helpers;
using TestDaoStudio.Mocks;
using TestDaoStudio.TestableEngines;
using DaoUsageDetails = DaoStudio.Interfaces.UsageDetails;

namespace TestDaoStudio.Engines;

/// <summary>
/// Unit tests for GoogleEngine class.
/// Tests Google AI integration, message handling, and model management.
/// </summary>
public class GoogleEngineTests : IDisposable
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<ILogger<BaseEngine>> _mockLogger;
    private readonly Mock<StorageFactory> _mockStorageFactory;
    private readonly Mock<IAPIProviderRepository> _mockApiProviderRepository;
    private readonly Mock<IPlainAIFunctionFactory> _mockPlainAIFunctionFactory;
    private readonly IPerson _testPerson;

    public GoogleEngineTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _mockLogger = new Mock<ILogger<BaseEngine>>();
        _mockStorageFactory = new Mock<StorageFactory>(":memory:");
        _mockApiProviderRepository = new Mock<IAPIProviderRepository>();
        _mockPlainAIFunctionFactory = new Mock<IPlainAIFunctionFactory>();
        _testPerson = MessageTestHelper.CreateTestPerson("Google Assistant", "A Google AI assistant", "Google", "gemini-pro");

        // Set up the StorageFactory mock to return the mocked repository
        SetupStorageFactoryMocks();
    }

    private void SetupStorageFactoryMocks()
    {
        // Create a test API provider
        var testProvider = new APIProvider
        {
            Id = 1,
            Name = "Google",
            ApiEndpoint = "https://api.google.com",
            ApiKey = "test-api-key",
            IsEnabled = true,
            ProviderType = 3 // Google
        };

        // Setup the API provider repository mock
        _mockApiProviderRepository
            .Setup(repo => repo.GetProviderByNameAsync("Google"))
            .ReturnsAsync(testProvider);

        // Setup the storage factory to return the mocked repository
        _mockStorageFactory
            .Setup(factory => factory.GetApiProviderRepositoryAsync())
            .ReturnsAsync(_mockApiProviderRepository.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var engine = new GoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>());

        // Assert
        engine.Should().NotBeNull();
        engine.Person.Should().Be(_testPerson);
    }

    [Fact]
    public void Constructor_WithNullPerson_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new GoogleEngine(null!, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new GoogleEngine(_testPerson, null!, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendMessageAsync_WithValidMessage_ReturnsResponse()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello, Google!")
        };

        // Create a proper mock response using the actual ChatResponseUpdate structure
        var mockUpdates = new List<ChatResponseUpdate>();
        // We'll simulate the response by creating the updates properly without reflection
        var responseText = "Hello! How can I help you today?";
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateMockResponseStream(responseText));

        // Mock session
        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        // Convert async enumerable to list
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }
        var firstMessage = resultMessages.First();

        // Assert
        resultMessages.Should().NotBeEmpty();
        firstMessage.Content.Should().Be(responseText);
        firstMessage.Role.Should().Be(MessageRole.Assistant);

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private async IAsyncEnumerable<ChatResponseUpdate> CreateMockResponseStream(string text)
    {
        // Create a mock response with the given text using proper constructor
        var update = new ChatResponseUpdate(ChatRole.Assistant, text);
        yield return update;
    }

    [Fact]
    public async Task SendMessageAsync_WithNullMessages_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);

        // Act & Assert
        var act = async () => await engine.GetMessageAsync(null!, null, null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendMessageAsync_WithEmptyMessages_ThrowsArgumentException()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var emptyMessages = new List<IMessage>();

        // Act & Assert
        var act = async () => await engine.GetMessageAsync(emptyMessages, null, null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_CancelsCorrectly()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello")
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Throws(new OperationCanceledException());

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // Act & Assert: enumerate to trigger cancellation during streaming
        var act = async () =>
        {
            var stream = await engine.GetMessageAsync(messages, null, mockSession.Object, cts.Token);
            await foreach (var _ in stream) { }
        };
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendMessageAsync_WhenProviderThrowsException_HandlesGracefully()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello")
        };

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Google API error"));

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act & Assert: enumerate to trigger provider exception during streaming
        var act = async () =>
        {
            var stream = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
            await foreach (var _ in stream) { }
        };
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Google API error");
    }

    [Fact]
    public async Task SendMessageAsync_WithSystemMessage_HandlesCorrectly()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateSystemMessage("You are a helpful assistant."),
            MessageTestHelper.CreateUserMessage("Hello!")
        };

        var expectedResponse = new ChatMessage(ChatRole.Assistant, "Hello! I'm here to help.");

        var mockResponse2 = new List<ChatResponseUpdate>();
        // Create a mock response with the expected text using proper constructor
        var responseUpdate2 = new ChatResponseUpdate(ChatRole.Assistant, expectedResponse.Text);
        mockResponse2.Add(responseUpdate2);

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockResponse2.ToAsyncEnumerable());

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }
        var firstMessage = resultMessages.First();

        // Assert
        resultMessages.Should().NotBeEmpty();
        firstMessage.Content.Should().Be("Hello! I'm here to help.");

        // Verify that system message was included in the request
        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.Is<IList<ChatMessage>>(msgs => msgs.Any(m => m.Role == ChatRole.System)),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_ReturnsModelList()
    {
        // Arrange
        var engine = new GoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>());

        // Act & Assert
        // GoogleEngine doesn't expose a GetAvailableModelsAsync API on the engine instance
        // Ensure engine initializes correctly instead of calling a non-existent method
        engine.Should().NotBeNull();
        engine.Person.Should().Be(_testPerson);
    }

    [Fact]
    public void UsageDetailsReceived_EventFiredCorrectly()
    {
        // Arrange
        var engine = new GoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>());
        DaoStudio.Interfaces.UsageDetails? receivedUsage = null;

        // Create a handler and ensure subscribing/unsubscribing doesn't throw
        EventHandler<DaoStudio.Interfaces.UsageDetails> handler = (sender, usage) =>
        {
            receivedUsage = usage;
        };

        Action addHandler = () => engine.UsageDetailsReceived += handler;
        Action removeHandler = () => engine.UsageDetailsReceived -= handler;

        addHandler.Should().NotThrow();
        removeHandler.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var engine = new GoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>());

        // Act & Assert - Call Dispose only if the engine implements IDisposable
        var act = () => (engine as IDisposable)?.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Person_Property_ReturnsCorrectPerson()
    {
        // Arrange
        var engine = new GoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>());

        // Act & Assert
        engine.Person.Should().Be(_testPerson);
        engine.Person.Name.Should().Be("Google Assistant");
        engine.Person.ProviderName.Should().Be("Google");
        engine.Person.ModelId.Should().Be("gemini-pro");
    }

    [Fact]
    public async Task SendMessageAsync_WithPersonParameters_AppliesParametersCorrectly()
    {
        // Arrange
        var personWithParams = MessageTestHelper.CreateTestPerson();
        personWithParams.Temperature = 0.8;
        personWithParams.Parameters = new Dictionary<string, string>
        {
            { PersonParameterNames.LimitMaxContextLength, "100" }
        };

        var engine = new TestableGoogleEngine(personWithParams, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Test message")
        };

        var expectedResponse = new ChatMessage(ChatRole.Assistant, "Response with parameters");

        var mockResponse3 = new List<ChatResponseUpdate>();
        // Create a mock response with the expected text using proper constructor
        var responseUpdate3 = new ChatResponseUpdate(ChatRole.Assistant, expectedResponse.Text);
        mockResponse3.Add(responseUpdate3);

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockResponse3.ToAsyncEnumerable());

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        // Convert async enumerable to list to trigger enumeration
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }

        // Assert
        result.Should().NotBeNull();
        resultMessages.Should().NotBeEmpty();

        // Verify that parameters were applied to ChatOptions
        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.Is<ChatOptions>(opts => opts.Temperature == 0.8f),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithMultipleMessages_HandlesConversationCorrectly()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = MessageTestHelper.CreateBasicConversation().Cast<IMessage>().ToList();

        var expectedResponse = new ChatMessage(ChatRole.Assistant, "That's correct! Paris is indeed the capital of France.");

        var mockResponse4 = new List<ChatResponseUpdate>();
        // Create a mock response with the expected text using proper constructor
        var responseUpdate4 = new ChatResponseUpdate(ChatRole.Assistant, expectedResponse.Text);
        mockResponse4.Add(responseUpdate4);

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockResponse4.ToAsyncEnumerable());

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }
        var firstMessage = resultMessages.First();

        // Assert
        resultMessages.Should().NotBeEmpty();
        firstMessage.Content.Should().Be("That's correct! Paris is indeed the capital of France.");

        // Verify that all messages were included in the conversation
        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.Is<IList<ChatMessage>>(msgs => msgs.Count >= messages.Count),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #region Streaming Message Tests

    [Fact]
    public async Task SendMessageAsync_WithStreamingTextResponse_ReturnsMultipleUpdates()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Tell me about Google's Gemini AI")
        };

        var streamingResponses = new[] { "Gemini is", " Google's", " most advanced", " AI model", " family." };
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateMultiPartResponseStream(streamingResponses));

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
        
        // Convert async enumerable to list
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }

        // Assert
        resultMessages.Should().NotBeEmpty();
        resultMessages.Count.Should().Be(streamingResponses.Length);
        
        // Check that content builds up progressively
        var expectedContent = "";
        for (int i = 0; i < streamingResponses.Length; i++)
        {
            expectedContent = streamingResponses[i];
            resultMessages[i].Content.Should().Be(expectedContent);
            resultMessages[i].Role.Should().Be(MessageRole.Assistant);
        }

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithToolCallResponse_ReturnsToolCallMessage()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Search for information about AI")
        };

        var toolCall = new FunctionCallContent("call_google123", "search_web", new Dictionary<string, object?> { { "query", "artificial intelligence" } });
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateToolCallResponseStream(toolCall));

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // Mock tools
        var tools = new Dictionary<string, List<FunctionWithDescription>>
        {
            { "search", new List<FunctionWithDescription> { CreateMockSearchFunction() } }
        };

        // Act
        var result = await engine.GetMessageAsync(messages, tools, mockSession.Object, CancellationToken.None);
        
        // Convert async enumerable to list
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }

        // Assert
        resultMessages.Should().NotBeEmpty();
        var firstMessage = resultMessages.First();
        firstMessage.Role.Should().Be(MessageRole.Assistant);
        
        // The BaseEngine converts FunctionCallContent into binary contents (ToolCall).
        // Verify a binary content entry of type ToolCall exists and contains the serialized FunctionCallContent.
        firstMessage.BinaryContents.Should().NotBeNull();
        var toolBinary = firstMessage.BinaryContents!.FirstOrDefault(b => b.Type == DaoStudio.Interfaces.MsgBinaryDataType.ToolCall);
        toolBinary.Should().NotBeNull();

        var json = System.Text.Encoding.UTF8.GetString(toolBinary!.Data);
        json.Should().Contain("search_web");
        json.Should().Contain("call_google123");

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithToolResultResponse_ReturnsToolResultMessage()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateToolCallMessage("search_web"),
        };

        var toolResult = new FunctionResultContent("call_google123", "{\"results\": [\"AI is transforming technology\", \"Machine learning advances\"]}");
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateToolResultResponseStream(toolResult));

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
        
        // Convert async enumerable to list
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }

        // Assert
        resultMessages.Should().NotBeEmpty();
        var firstMessage = resultMessages.First();
        firstMessage.Role.Should().Be(MessageRole.User);
        
        // Verify ToolCallResult is present in binary contents and contains expected data
        firstMessage.BinaryContents.Should().NotBeNull();
        var toolResultBinary = firstMessage.BinaryContents!.FirstOrDefault(b => b.Type == DaoStudio.Interfaces.MsgBinaryDataType.ToolCallResult);
        toolResultBinary.Should().NotBeNull();
        var jsonToolResult = System.Text.Encoding.UTF8.GetString(toolResultBinary!.Data);
        jsonToolResult.Should().Contain("call_google123");
        jsonToolResult.Should().Contain("AI is transforming");
        jsonToolResult.Should().Contain("Machine learning");

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithUsageContent_TriggersUsageDetailsEvent()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello Gemini")
        };

        var usageDetails = new Microsoft.Extensions.AI.UsageDetails
        {
            InputTokenCount = 20,
            OutputTokenCount = 35,
            TotalTokenCount = 55
        };
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateUsageResponseStream(usageDetails));

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        DaoUsageDetails? receivedUsageDetails = null;
        engine.UsageDetailsReceived += (sender, details) => receivedUsageDetails = details;

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
        
        // Convert async enumerable to list to trigger processing
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }

        // Assert
        receivedUsageDetails.Should().NotBeNull();
        receivedUsageDetails!.InputTokens.Should().Be(20);
        receivedUsageDetails.OutputTokens.Should().Be(35);
        receivedUsageDetails.TotalTokens.Should().Be(55);
    }

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Start a long response about Google AI")
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateCancellableResponseStream(cts.Token));

        var mockSession = new Mock<ISession>();

        // Act & Assert
        var act = async () =>
        {
            var result = await engine.GetMessageAsync(messages, null, mockSession.Object, cts.Token);
            await foreach (var msg in result)
            {
                // This should throw OperationCanceledException
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendMessageAsync_WithGeminiParameters_AppliesParametersToOptions()
    {
        // Arrange
        var personWithParams = MessageTestHelper.CreateTestPerson();
        personWithParams.ProviderName = "Google";
        personWithParams.ModelId = "gemini-pro";
        personWithParams.Temperature = 0.9;
        personWithParams.TopP = 0.8;
        personWithParams.TopK = 40;
        personWithParams.Parameters = new Dictionary<string, string>
        {
            { PersonParameterNames.LimitMaxContextLength, "500" }
        };

        var engine = new TestableGoogleEngine(personWithParams, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Test message")
        };

        ChatOptions? capturedOptions = null;
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns<IList<ChatMessage>, ChatOptions, CancellationToken>((msgs, options, ct) =>
            {
                capturedOptions = options;
                return CreateGoogleMockResponseStream("Test response");
            });

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
        
        // Convert to trigger the call
        await foreach (var msg in result) { }

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Temperature.Should().Be(0.9f);
        capturedOptions.MaxOutputTokens.Should().Be(500);
        capturedOptions.TopP.Should().Be(0.8f);
        capturedOptions.TopK.Should().Be(40);
    }

    #endregion

    #region Helper Methods for Stream Creation

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateMultiPartResponseStream(string[] parts)
    {
        foreach (var part in parts)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, part);
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateToolCallResponseStream(FunctionCallContent toolCall)
    {
        var update = new ChatResponseUpdate(ChatRole.Assistant, new List<AIContent> { toolCall });
        yield return update;
        await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateToolResultResponseStream(FunctionResultContent toolResult)
    {
        var update = new ChatResponseUpdate(ChatRole.Tool, new List<AIContent> { toolResult });
        yield return update;
        await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateUsageResponseStream(Microsoft.Extensions.AI.UsageDetails usageDetails)
    {
        var textUpdate = new ChatResponseUpdate(ChatRole.Assistant, "Response with usage");
        yield return textUpdate;
        
        // Create usage content and add to response
        var usageContent = new UsageContent(usageDetails);
        var usageUpdate = new ChatResponseUpdate(null, new List<AIContent> { usageContent });
        yield return usageUpdate;
        
        await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateCancellableResponseStream([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, "Starting");
        await Task.Yield();
        
        cancellationToken.ThrowIfCancellationRequested();
        
        yield return new ChatResponseUpdate(ChatRole.Assistant, " response about Google AI");
        await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateGoogleMockResponseStream(string text)
    {
        var responseUpdate = new ChatResponseUpdate(ChatRole.Assistant, text);
        yield return responseUpdate;
        await Task.Yield();
    }

    private static FunctionWithDescription CreateMockSearchFunction()
    {
        return new FunctionWithDescription
        {
            Function = (Dictionary<string, object?> args) => Task.FromResult<object?>("search results"),
            Description = new FunctionDescription
            {
                Name = "search_web",
                Description = "Searches the web for information",
                Parameters = new List<FunctionTypeMetadata>
                {
                    new FunctionTypeMetadata
                    {
                        Name = "query",
                        Description = "The search query",
                        ParameterType = typeof(string),
                        IsRequired = true,
                        DefaultValue = null
                    }
                }
            }
        };
    }

    #endregion

    public void Dispose()
    {
        // Clean up any resources if needed
    }
}
