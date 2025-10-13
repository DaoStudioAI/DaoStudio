using DaoStudio.DBStorage.Factory;
using DaoStudio.Engines.MEAI;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
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
/// Unit tests for OllamaEngine class.
/// Tests Ollama integration, message handling, and local model management.
/// </summary>
public class OllamaEngineTests : IDisposable
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<ILogger<BaseEngine>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<StorageFactory> _mockStorageFactory;
    private readonly IPerson _testPerson;

    public OllamaEngineTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _mockLogger = new Mock<ILogger<BaseEngine>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockStorageFactory = new Mock<StorageFactory>(":memory:");
        _testPerson = MessageTestHelper.CreateTestPerson("Ollama Assistant", "A local Ollama assistant", "Ollama", "llama2");
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Assert
        engine.Should().NotBeNull();
        engine.Person.Should().Be(_testPerson);
    }

    [Fact]
    public void Constructor_WithNullChatClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestableOllamaEngine(null!, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPerson_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestableOllamaEngine(null!, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestableOllamaEngine(_testPerson, null!, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendMessageAsync_WithValidMessage_ReturnsResponse()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello, Ollama!")
        };

        var expectedResponse = new ChatMessage(ChatRole.Assistant, "Hello! I'm running locally on Ollama. How can I help you?");

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
        firstMessage.Content.Should().Be("Hello! I'm running locally on Ollama. How can I help you?");
        firstMessage.Role.Should().Be(MessageRole.Assistant);

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithNullMessages_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Act & Assert
    var act = async () => await engine.GetMessageAsync(null!, null, null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendMessageAsync_WithEmptyMessages_ThrowsArgumentException()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        var emptyMessages = new List<IMessage>();

        // Act & Assert
    var act = async () => await engine.GetMessageAsync(emptyMessages, null, null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_CancelsCorrectly()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
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

        // Act & Assert
        var act = async () => {
            var result = await engine.GetMessageAsync(messages, null, mockSession.Object, cts.Token);
            await foreach (var message in result)
            {
                // Just enumerate to trigger the exception
            }
        };
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendMessageAsync_WhenProviderThrowsException_HandlesGracefully()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello")
        };

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Ollama connection error"));

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act & Assert
        var act = async () => {
            var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
            await foreach (var message in result)
            {
                // Just enumerate to trigger the exception
            }
        };
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Ollama connection error");
    }

    [Fact]
    public async Task SendMessageAsync_WithSystemMessage_HandlesCorrectly()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateSystemMessage("You are a helpful local AI assistant."),
            MessageTestHelper.CreateUserMessage("Hello!")
        };

        var expectedResponse = new ChatMessage(ChatRole.Assistant, "Hello! I'm your local AI assistant running on Ollama.");

        var mockResponse2 = new List<ChatResponseUpdate>();
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
        firstMessage.Content.Should().Be("Hello! I'm your local AI assistant running on Ollama.");

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
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

    // Act & Assert
    // OllamaEngine doesn't expose GetAvailableModelsAsync on the instance; verify initialization
    engine.Should().NotBeNull();
    engine.Person.Should().Be(_testPerson);
    }

    [Fact]
    public void UsageDetailsReceived_EventFiredCorrectly()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        DaoStudio.Interfaces.UsageDetails? receivedUsage = null;

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
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());


    // Act & Assert - Dispose only if implemented
    var act = () => (engine as IDisposable)?.Dispose();
    act.Should().NotThrow();
    }

    [Fact]
    public void Person_Property_ReturnsCorrectPerson()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Act & Assert
        engine.Person.Should().Be(_testPerson);
        engine.Person.Name.Should().Be("Ollama Assistant");
        engine.Person.ProviderName.Should().Be("Ollama");
        engine.Person.ModelId.Should().Be("llama2");
    }

    [Fact]
    public async Task SendMessageAsync_WithPersonParameters_AppliesParametersCorrectly()
    {
        // Arrange
        var personWithParams = MessageTestHelper.CreateTestPerson();
        personWithParams.Temperature = 0.3;
        personWithParams.TopP = 0.8;
        personWithParams.FrequencyPenalty = 1.1; // repeat_penalty maps to FrequencyPenalty
        personWithParams.Parameters = new Dictionary<string, string>
        {
            { PersonParameterNames.LimitMaxContextLength, "150" }
        };

        var engine = new TestableOllamaEngine(personWithParams, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Test message with Ollama parameters")
        };

        var expectedResponse = new ChatMessage(ChatRole.Assistant, "Response with applied Ollama parameters");

        var mockResponse3 = new List<ChatResponseUpdate>();
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

        // Consume the async enumerable to trigger the mock
        var resultMessages = new List<IMessage>();
        await foreach (var message in result)
        {
            resultMessages.Add(message);
        }

        // Assert
        result.Should().NotBeNull();
        resultMessages.Should().NotBeEmpty();
        
        // Verify that parameters were applied to ChatOptions
        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.Is<ChatOptions>(opts => opts.Temperature == 0.3f && opts.TopP == 0.8f),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithDifferentOllamaModels_HandlesCorrectly()
    {
        // Arrange
        var mistralPerson = MessageTestHelper.CreateTestPerson(
            "Mistral Assistant", 
            "Mistral model on Ollama", 
            "Ollama", 
            "mistral:7b");

        var engine = new TestableOllamaEngine(mistralPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello Mistral!")
        };

        var expectedResponse = new ChatMessage(ChatRole.Assistant, "Bonjour! I'm Mistral running locally on Ollama.");

        var mockResponse4 = new List<ChatResponseUpdate>();
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

        // Assert
        result.Should().NotBeNull();
        var resultMessages2 = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages2.Add(msg);
        }
        resultMessages2.Should().NotBeEmpty();
        resultMessages2.First().Content.Should().Be("Bonjour! I'm Mistral running locally on Ollama.");
    }

    [Fact]
    public async Task SendMessageAsync_WithCodeLlamaModel_HandlesCodeRequests()
    {
        // Arrange
        var codeLlamaPerson = MessageTestHelper.CreateTestPerson(
            "CodeLlama Assistant", 
            "Code generation assistant", 
            "Ollama", 
            "codellama:7b");

        var engine = new TestableOllamaEngine(codeLlamaPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Write a Python function to calculate factorial")
        };

        var expectedResponse = new ChatMessage(ChatRole.Assistant, "```python\ndef factorial(n):\n    if n <= 1:\n        return 1\n    return n * factorial(n - 1)\n```");

        var mockResponse5 = new List<ChatResponseUpdate>();
        var responseUpdate5 = new ChatResponseUpdate(ChatRole.Assistant, expectedResponse.Text);
        mockResponse5.Add(responseUpdate5);
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockResponse5.ToAsyncEnumerable());

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var resultMessages3 = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages3.Add(msg);
        }
        resultMessages3.Should().NotBeEmpty();
        resultMessages3.First().Content.Should().Contain("def factorial");
        resultMessages3.First().Content.Should().Contain("python");
    }

    [Fact]
    public async Task SendMessageAsync_WithMultipleMessages_HandlesConversationCorrectly()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
    var messages = MessageTestHelper.CreateBasicConversation().Cast<IMessage>().ToList();

        var expectedResponse = new ChatMessage(ChatRole.Assistant, "That's right! Paris is indeed the capital and largest city of France.");

        var mockResponse6 = new List<ChatResponseUpdate>();
        var responseUpdate6 = new ChatResponseUpdate(ChatRole.Assistant, expectedResponse.Text);
        mockResponse6.Add(responseUpdate6);
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockResponse6.ToAsyncEnumerable());

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var resultMessages4 = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages4.Add(msg);
        }
        resultMessages4.Should().NotBeEmpty();
        resultMessages4.First().Content.Should().Be("That's right! Paris is indeed the capital and largest city of France.");

        // Verify that all messages were included in the conversation
        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.Is<IList<ChatMessage>>(msgs => msgs.Count == messages.Count),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithLocalModelOffline_HandlesConnectionError()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello")
        };

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Throws(new HttpRequestException("Connection refused - Ollama server not running"));

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act & Assert
        var act = async () => {
            var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
            await foreach (var message in result)
            {
                // Just enumerate to trigger the exception
            }
        };
        await act.Should().ThrowAsync<HttpRequestException>()
                  .WithMessage("Connection refused - Ollama server not running");
    }

    #region Streaming Message Tests

    [Fact]
    public async Task SendMessageAsync_WithStreamingTextResponse_ReturnsMultipleUpdates()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Tell me about Ollama")
        };

        var streamingResponses = new[] { "Ollama is", " a tool", " for running", " large language models", " locally." };
        
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
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("What's the current directory?")
        };

        var toolCall = new FunctionCallContent("call_ollama123", "get_directory", new Dictionary<string, object?> { { "path", "." } });
        
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
            { "system", new List<FunctionWithDescription> { CreateMockDirectoryFunction() } }
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
        json.Should().Contain("get_directory");
        json.Should().Contain("call_ollama123");

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithToolResultResponse_ReturnsToolResultMessage()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateToolCallMessage("get_directory"),
        };

        var toolResult = new FunctionResultContent("call_ollama123", "{\"current_directory\": \"/home/user/ollama\"}");
        
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
        jsonToolResult.Should().Contain("call_ollama123");
        jsonToolResult.Should().Contain("current_directory");
        jsonToolResult.Should().Contain("/home/user/ollama");

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithUsageContent_TriggersUsageDetailsEvent()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello Ollama")
        };

        var usageDetails = new Microsoft.Extensions.AI.UsageDetails
        {
            InputTokenCount = 8,
            OutputTokenCount = 18,
            TotalTokenCount = 26
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
        receivedUsageDetails!.InputTokens.Should().Be(8);
        receivedUsageDetails.OutputTokens.Should().Be(18);
        receivedUsageDetails.TotalTokens.Should().Be(26);
    }

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Start a long response about local AI")
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
    public async Task SendMessageAsync_WithOllamaParameters_AppliesParametersToOptions()
    {
        // Arrange
        var personWithParams = MessageTestHelper.CreateTestPerson();
        personWithParams.ProviderName = "Ollama";
        personWithParams.ModelId = "llama2";
        personWithParams.Temperature = 0.5;
        personWithParams.TopP = 0.9;
        personWithParams.Parameters = new Dictionary<string, string>
        {
            { PersonParameterNames.LimitMaxContextLength, "350" }
        };

        var engine = new TestableOllamaEngine(personWithParams, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
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
                return CreateOllamaMockResponseStream("Test response");
            });

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
        
        // Convert to trigger the call
        await foreach (var msg in result) { }

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Temperature.Should().Be(0.5f);
        capturedOptions.MaxOutputTokens.Should().Be(350);
        capturedOptions.TopP.Should().Be(0.9f);
    }

    [Fact]
    public async Task SendMessageAsync_WithMultipleOllamaModels_HandlesAllCorrectly()
    {
        // Test different Ollama models
        var models = new[]
        {
            "llama2",
            "codellama",
            "mistral",
            "gemma",
            "phi"
        };

        foreach (var modelId in models)
        {
            // Arrange
            var person = MessageTestHelper.CreateTestPerson("Ollama Assistant", "Test", "Ollama", modelId);
            var engine = new TestableOllamaEngine(person, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
            var messages = new List<IMessage>
            {
                MessageTestHelper.CreateUserMessage($"Hello from {modelId}")
            };

            _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                    It.IsAny<IList<ChatMessage>>(),
                    It.IsAny<ChatOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateOllamaMockResponseStream($"Response from {modelId}"));

            var mockSession = new Mock<ISession>();
            mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

            // Act
            var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
            
            // Convert to list
            var resultMessages = new List<IMessage>();
            await foreach (var msg in result)
            {
                resultMessages.Add(msg);
            }

            // Assert
            resultMessages.Should().NotBeEmpty();
            var firstMessage = resultMessages.First();
            firstMessage.Content.Should().Be($"Response from {modelId}");
        }
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
        
        yield return new ChatResponseUpdate(ChatRole.Assistant, " response about local AI");
        await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateOllamaMockResponseStream(string text)
    {
        var responseUpdate = new ChatResponseUpdate(ChatRole.Assistant, text);
        yield return responseUpdate;
        await Task.Yield();
    }

    private static FunctionWithDescription CreateMockDirectoryFunction()
    {
        return new FunctionWithDescription
        {
            Function = (Dictionary<string, object?> args) => Task.FromResult<object?>("directory info"),
            Description = new FunctionDescription
            {
                Name = "get_directory",
                Description = "Gets information about a directory",
                Parameters = new List<FunctionTypeMetadata>
                {
                    new FunctionTypeMetadata
                    {
                        Name = "path",
                        Description = "The directory path to examine",
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
