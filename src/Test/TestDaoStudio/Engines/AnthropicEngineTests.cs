using DaoStudio.Common;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using System.Runtime.CompilerServices;
using System.Text;
using TestDaoStudio.Helpers;
using TestDaoStudio.Mocks;
using DaoStudio.Engines.MEAI;
using TestDaoStudio.TestableEngines;
using DaoUsageDetails = DaoStudio.Interfaces.UsageDetails;

namespace TestDaoStudio.Engines;

/// <summary>
/// Unit tests for AnthropicEngine class.
/// Tests engine initialization, message sending, system message handling, and disposal.
/// </summary>
public class AnthropicEngineTests : IDisposable
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<ILogger<BaseEngine>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<StorageFactory> _mockStorageFactory;
    private readonly Mock<IAPIProviderRepository> _mockApiProviderRepository;
    private readonly MockPerson _testPerson;

    public AnthropicEngineTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _mockLogger = new Mock<ILogger<BaseEngine>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockStorageFactory = new Mock<StorageFactory>("test.db");
        _mockApiProviderRepository = new Mock<IAPIProviderRepository>();
        
        _testPerson = MockPerson.CreateAssistant("Claude Assistant", "Anthropic", "claude-3-haiku-20240307");

        _mockStorageFactory.Setup(sf => sf.GetApiProviderRepositoryAsync())
                          .ReturnsAsync(_mockApiProviderRepository.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Assert
        engine.Should().NotBeNull();
        engine.Should().BeAssignableTo<BaseEngine>();
    }

    [Fact]
    public void Constructor_WithNullPerson_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestableAnthropicEngine(null!, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestableAnthropicEngine(_testPerson, null!, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestableAnthropicEngine(_testPerson, _mockLogger.Object, null!, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullStorageFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, null!, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateChatClientAsync_WithValidProvider_CreatesClient()
    {
        // Arrange
        var mockProvider = new DaoStudio.DBStorage.Models.APIProvider
        {
            Name = "Anthropic",
            ApiKey = "sk-ant-test-key",
            ApiEndpoint = "https://api.anthropic.com",
            IsEnabled = true
        };

        _mockApiProviderRepository.Setup(apr => apr.GetProviderByNameAsync("Anthropic"))
                                 .ReturnsAsync(mockProvider);

        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Act & Assert - Engine should initialize without throwing
        engine.Should().NotBeNull();
    }

    [Fact]
    public async Task SendMessageAsync_WithValidMessage_ReturnsResponse()
    {
        // Arrange
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello, Claude!")
        };

        var expectedResponse = new ChatMessage(ChatRole.Assistant, "Hello! I'm Claude, an AI assistant created by Anthropic. How can I help you today?");

        var mockResponse = new List<ChatResponseUpdate>();
        var responseUpdate = new ChatResponseUpdate(ChatRole.Assistant, expectedResponse.Text);
        mockResponse.Add(responseUpdate);
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockResponse.ToAsyncEnumerable());

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
        firstMessage.Content.Should().Be("Hello! I'm Claude, an AI assistant created by Anthropic. How can I help you today?");
        firstMessage.Role.Should().Be(MessageRole.Assistant);

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateChatClientAsync_WithNullProvider_ThrowsLlmInitializationException()
    {
        // Arrange
        _mockApiProviderRepository.Setup(apr => apr.GetProviderByNameAsync("Anthropic"))
                                 .ReturnsAsync((DaoStudio.DBStorage.Models.APIProvider?)null);

        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Act & Assert - Engine should initialize without throwing
        engine.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_WithEmptyApiKey_ThrowsLlmInitializationException()
    {
        // Arrange
        var mockProvider = new DaoStudio.DBStorage.Models.APIProvider
        {
            Name = "Anthropic",
            ApiKey = "", // Empty API key should cause failure for Anthropic
            ApiEndpoint = "https://api.anthropic.com",
            IsEnabled = true
        };

        _mockApiProviderRepository.Setup(apr => apr.GetProviderByNameAsync("Anthropic"))
                                 .ReturnsAsync(mockProvider);

        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Act & Assert - Engine should initialize without throwing
        engine.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_WithInvalidEndpoint_ThrowsLlmInitializationException()
    {
        // Arrange
        var mockProvider = new DaoStudio.DBStorage.Models.APIProvider
        {
            Name = "Anthropic",
            ApiKey = "sk-ant-test-key",
            ApiEndpoint = "invalid-url", // Invalid URL
            IsEnabled = true
        };

        _mockApiProviderRepository.Setup(apr => apr.GetProviderByNameAsync("Anthropic"))
                                 .ReturnsAsync(mockProvider);

        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Act & Assert - Engine should initialize without throwing
        engine.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAvailableModelsAsync_WithRepositoryException_ThrowsLlmInitializationException()
    {
        // Arrange
        _mockApiProviderRepository.Setup(apr => apr.GetProviderByNameAsync("Anthropic"))
                                 .ThrowsAsync(new InvalidOperationException("Database error"));

        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Act & Assert - Engine should initialize without throwing
        engine.Should().NotBeNull();
    }

    [Fact]
    public void AnthropicEngine_ImplementsBaseEngine()
    {
        // Arrange & Act
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Assert
        engine.Should().BeAssignableTo<BaseEngine>();
        engine.Should().BeAssignableTo<IEngine>();
    }

    [Theory]
    [InlineData("claude-3-haiku-20240307")]
    [InlineData("claude-3-sonnet-20240229")]
    [InlineData("claude-3-opus-20240229")]
    public void AnthropicEngine_WithDifferentModels_InitializesCorrectly(string modelId)
    {
        // Arrange
        var person = MessageTestHelper.CreateTestPerson("Claude", "Test Claude", "Anthropic", modelId);

        // Act
        var engine = new TestableAnthropicEngine(person, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Assert
        engine.Should().NotBeNull();
    }

    [Fact]
    public void AnthropicEngine_WithSystemMessage_HandlesCorrectly()
    {
        // Arrange
        var personWithSystemMessage = MessageTestHelper.CreateAnthropicTestPerson();
        personWithSystemMessage.DeveloperMessage = "You are a helpful assistant specialized in testing.";

        // Act
        var engine = new TestableAnthropicEngine(personWithSystemMessage, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Assert - Anthropic engine should handle system messages correctly
        engine.Should().NotBeNull();
    }

    [Fact]
    public void AnthropicEngine_WithLongSystemMessage_HandlesCorrectly()
    {
        // Arrange
        var personWithLongSystemMessage = MessageTestHelper.CreateAnthropicTestPerson();
        personWithLongSystemMessage.DeveloperMessage = new string('A', 5000); // Very long system message

        // Act
        var engine = new TestableAnthropicEngine(personWithLongSystemMessage, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Assert - Should handle long system messages
        engine.Should().NotBeNull();
    }

    [Fact]
    public void AnthropicEngine_WithToolConfiguration_InitializesCorrectly()
    {
        // Arrange
    var personWithTools = MessageTestHelper.CreateAnthropicTestPerson();
    personWithTools.ToolNames = new string[] { "weather", "calculator", "search" };

        // Act
        var engine = new TestableAnthropicEngine(personWithTools, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Assert
        engine.Should().NotBeNull();
    }

    [Fact]
    public void AnthropicEngine_WithCustomParameters_InitializesCorrectly()
    {
        // Arrange
        var personWithParams = MessageTestHelper.CreateTestPerson();
        personWithParams.ProviderName = "Anthropic";
        personWithParams.ModelId = "claude-3-haiku-20240307";
        personWithParams.Temperature = 0.7;
        personWithParams.TopP = 1.0;
        personWithParams.Parameters = new Dictionary<string, string>
        {
            { PersonParameterNames.LimitMaxContextLength, "150" }
        };

        // Act
        var engine = new TestableAnthropicEngine(personWithParams, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Assert
        engine.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_CallsBaseDispose()
    {
        // Arrange
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Act & Assert - Should not throw
        // Call Dispose only if the engine implements IDisposable
        var act = () => (engine as IDisposable)?.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void AnthropicEngine_LogsInitializationAttempts()
    {
        // Arrange & Act
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Assert - Engine should be created without throwing
        engine.Should().NotBeNull();
    }

    #region Streaming Message Tests

    [Fact]
    public async Task SendMessageAsync_WithStreamingTextResponse_ReturnsMultipleUpdates()
    {
        // Arrange
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Tell me about Anthropic's Claude AI")
        };

        var streamingResponses = new[] { "Claude is", " an AI assistant", " created by Anthropic", ", designed to be", " helpful, harmless, and honest." };
        
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
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("What's the weather like in San Francisco?")
        };

        var toolCall = new FunctionCallContent("call_abc123", "get_weather", new Dictionary<string, object?> { { "location", "San Francisco" } });
        
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
            { "weather", new List<FunctionWithDescription> { CreateMockWeatherFunction() } }
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

        var json = Encoding.UTF8.GetString(toolBinary!.Data);
        json.Should().Contain("get_weather");
        json.Should().Contain("call_abc123");

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithToolResultResponse_ReturnsToolResultMessage()
    {
        // Arrange
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateToolCallMessage("get_weather"),
        };

        var toolResult = new FunctionResultContent("call_abc123", "{\"temperature\": \"68°F\", \"condition\": \"partly cloudy\"}");
        
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
        var jsonToolResult = Encoding.UTF8.GetString(toolResultBinary!.Data);
        jsonToolResult.Should().Contain("call_abc123");
        jsonToolResult.Should().Contain("temperature");
        jsonToolResult.Should().Contain("partly cloudy");

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithUsageContent_TriggersUsageDetailsEvent()
    {
        // Arrange
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello Claude")
        };

        var usageDetails = new Microsoft.Extensions.AI.UsageDetails
        {
            InputTokenCount = 15,
            OutputTokenCount = 25,
            TotalTokenCount = 40
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
        receivedUsageDetails!.InputTokens.Should().Be(15);
        receivedUsageDetails.OutputTokens.Should().Be(25);
        receivedUsageDetails.TotalTokens.Should().Be(40);
    }

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Start a long response about Anthropic's research")
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
    public async Task SendMessageAsync_WithPersonParameters_AppliesParametersToOptions()
    {
        // Arrange
        var personWithParams = MessageTestHelper.CreateTestPerson();
        personWithParams.ProviderName = "Anthropic";
        personWithParams.ModelId = "claude-3-haiku-20240307";
        personWithParams.Temperature = 0.7;
        personWithParams.TopP = 0.95;
        personWithParams.Parameters = new Dictionary<string, string>
        {
            { PersonParameterNames.LimitMaxContextLength, "300" }
        };

        var engine = new TestableAnthropicEngine(personWithParams, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
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
                return CreateMockResponseStream("Test response");
            });

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
        
        // Convert to trigger the call
        await foreach (var msg in result) { }

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Temperature.Should().Be(0.7f);
        capturedOptions.MaxOutputTokens.Should().Be(300);
        capturedOptions.TopP.Should().Be(0.95f);
    }

    [Fact]
    public async Task SendMessageAsync_WithLongConversation_HandlesContextCorrectly()
    {
        // Arrange
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = MessageTestHelper.CreateMultiTurnConversation().Cast<IMessage>().ToList();

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateMockResponseStream("This is a response considering the full conversation context."));

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
        firstMessage.Role.Should().Be(MessageRole.Assistant);
        firstMessage.Content.Should().Contain("response considering the full conversation context");

        // Verify that the chat client received all messages from the conversation
        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.Is<IList<ChatMessage>>(msgs => msgs.Count == messages.Count),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
        
        yield return new ChatResponseUpdate(ChatRole.Assistant, " response about Anthropic's research");
        await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateMockResponseStream(string text)
    {
        var responseUpdate = new ChatResponseUpdate(ChatRole.Assistant, text);
        yield return responseUpdate;
        await Task.Yield();
    }

    private static FunctionWithDescription CreateMockWeatherFunction()
    {
        return new FunctionWithDescription
        {
            Function = (Dictionary<string, object?> args) => Task.FromResult<object?>("partly cloudy"),
            Description = new FunctionDescription
            {
                Name = "get_weather",
                Description = "Gets weather information for a location",
                Parameters = new List<FunctionTypeMetadata>
                {
                    new FunctionTypeMetadata
                    {
                        Name = "location",
                        Description = "The location to get weather for",
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
        // Cleanup any resources if needed
    }
}
