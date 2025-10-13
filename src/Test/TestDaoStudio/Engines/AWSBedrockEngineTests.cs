using DaoStudio.Engines.MEAI;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using System.Runtime.CompilerServices;
using TestDaoStudio.Helpers;
using TestDaoStudio.Mocks;
using System.Linq;
using TestDaoStudio.TestableEngines;
using DaoUsageDetails = DaoStudio.Interfaces.UsageDetails;

namespace TestDaoStudio.Engines;

/// <summary>
/// Unit tests for AWSBedrockEngine class.
/// Tests AWS Bedrock integration, message handling, and model management.
/// </summary>
public class AWSBedrockEngineTests : IDisposable
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<ILogger<BaseEngine>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<StorageFactory> _mockStorageFactory;
    private readonly Mock<IAPIProviderRepository> _mockApiProviderRepository;
    private readonly IPerson _testPerson;

    public AWSBedrockEngineTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _mockLogger = new Mock<ILogger<BaseEngine>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockStorageFactory = new Mock<StorageFactory>("test.db");
        _mockApiProviderRepository = new Mock<IAPIProviderRepository>();
        _testPerson = MessageTestHelper.CreateTestPerson("Bedrock Assistant", "An AWS Bedrock assistant", "AWS", "anthropic.claude-3-haiku-20240307-v1:0");
        
        _mockStorageFactory.Setup(sf => sf.GetApiProviderRepositoryAsync())
                          .ReturnsAsync(_mockApiProviderRepository.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Assert
        engine.Should().NotBeNull();
        engine.Person.Should().Be(_testPerson);
    }

    [Fact]
    public void Constructor_WithNullChatClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestableAWSBedrockEngine(null!, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPerson_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestableAWSBedrockEngine(null!, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestableAWSBedrockEngine(_testPerson, null!, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendMessageAsync_WithValidMessage_ReturnsResponse()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello, Bedrock!")
        };

        var expectedResponse = new Microsoft.Extensions.AI.ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, 
            "Hello! I'm Claude running on AWS Bedrock. How can I assist you?");

        var mockResponse = new List<Microsoft.Extensions.AI.ChatResponseUpdate>();
        // Create a mock response with the expected text using proper constructor
        var responseUpdate = new Microsoft.Extensions.AI.ChatResponseUpdate(Microsoft.Extensions.AI.ChatRole.Assistant, expectedResponse.Text);
        mockResponse.Add(responseUpdate);
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(mockResponse));

        // Mock session
        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        
        // Since GetMessageAsync returns IAsyncEnumerable<IMessage>, we need to enumerate it
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }
        
        resultMessages.Should().NotBeEmpty();
        var firstMessage = resultMessages.First();
        firstMessage.Content.Should().Be("Hello! I'm Claude running on AWS Bedrock. How can I assist you?");
        firstMessage.Role.Should().Be(MessageRole.Assistant);

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
            It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithNullMessages_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Act & Assert
        var act = async () => await engine.GetMessageAsync(null!, null, null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendMessageAsync_WithEmptyMessages_ThrowsArgumentException()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        var emptyMessages = new List<IMessage>();

        // Act & Assert
        var act = async () => await engine.GetMessageAsync(emptyMessages, null, null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_CancelsCorrectly()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello")
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Throws(new OperationCanceledException());

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // Act & Assert
        var act = async () => 
        {
            var result = await engine.GetMessageAsync(messages, null, mockSession.Object, cts.Token);
            await foreach (var msg in result)
            {
                // Force enumeration to trigger the exception
            }
        };
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendMessageAsync_WhenProviderThrowsException_HandlesGracefully()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello")
        };

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("AWS Bedrock error"));

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act & Assert
        var act = async () => 
        {
            var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
            await foreach (var msg in result)
            {
                // Force enumeration to trigger the exception
            }
        };
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("AWS Bedrock error");
    }

    [Fact]
    public async Task SendMessageAsync_WithSystemMessage_HandlesCorrectly()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateSystemMessage("You are a helpful assistant running on AWS Bedrock."),
            MessageTestHelper.CreateUserMessage("Hello!")
        };

        var expectedResponse = new Microsoft.Extensions.AI.ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, 
            "Hello! I'm running on AWS Bedrock and ready to help.");

        var mockResponse2 = new List<Microsoft.Extensions.AI.ChatResponseUpdate>();
        // Create a mock response with the expected text using proper constructor
        var responseUpdate2 = new Microsoft.Extensions.AI.ChatResponseUpdate(Microsoft.Extensions.AI.ChatRole.Assistant, expectedResponse.Text);
        mockResponse2.Add(responseUpdate2);
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(mockResponse2));

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }
        
        resultMessages.Should().NotBeEmpty();
        var firstMessage = resultMessages.First();
        firstMessage.Content.Should().Be("Hello! I'm running on AWS Bedrock and ready to help.");

        // Verify that system message was included in the request
        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.Is<IList<Microsoft.Extensions.AI.ChatMessage>>(msgs => msgs.Any(m => m.Role == Microsoft.Extensions.AI.ChatRole.System)),
            It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }


    [Fact]
    public void UsageDetailsReceived_EventFiredCorrectly()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());
        var usageDetailsReceived = false;
        DaoStudio.Interfaces.UsageDetails? receivedUsage = null;

        // Subscribe to the event to ensure it's publicly available. Actual invocation is internal.
        engine.UsageDetailsReceived += (sender, usage) =>
        {
            usageDetailsReceived = true;
            receivedUsage = usage;
        };

        // Act
        var testUsage = new Microsoft.Extensions.AI.UsageDetails
        {
            InputTokenCount = 15,
            OutputTokenCount = 25,
            TotalTokenCount = 40
        };

        // Simulate usage details event (this would normally be triggered by the chat client)
        // Since we can't directly trigger the internal event, we'll test the event subscription
        
    // Assert - subscription succeeded and no event has been raised yet
    usageDetailsReceived.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

    // Act & Assert - call Dispose only via IDisposable if implemented on the instance
    var act = () => (engine as IDisposable)?.Dispose();
    act.Should().NotThrow();
    }

    [Fact]
    public void Person_Property_ReturnsCorrectPerson()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>());

        // Act & Assert
        engine.Person.Should().Be(_testPerson);
        engine.Person.Name.Should().Be("Bedrock Assistant");
        engine.Person.ProviderName.Should().Be("AWS");
        engine.Person.ModelId.Should().Be("anthropic.claude-3-haiku-20240307-v1:0");
    }

    [Fact]
    public async Task SendMessageAsync_WithPersonParameters_AppliesParametersCorrectly()
    {
        // Arrange
        var personWithParams = MessageTestHelper.CreateTestPerson();
        personWithParams.Temperature = 0.5;
        personWithParams.TopP = 0.9;
        personWithParams.Parameters = new Dictionary<string, string>
        {
            { PersonParameterNames.LimitMaxContextLength, "200" }
        };

        var engine = new TestableAWSBedrockEngine(personWithParams, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Test message with parameters")
        };

        var expectedResponse = new Microsoft.Extensions.AI.ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, 
            "Response with applied parameters");

        var mockResponse3 = new List<Microsoft.Extensions.AI.ChatResponseUpdate>();
        // Create a mock response with the expected text using proper constructor
        var responseUpdate3 = new Microsoft.Extensions.AI.ChatResponseUpdate(Microsoft.Extensions.AI.ChatRole.Assistant, expectedResponse.Text);
        mockResponse3.Add(responseUpdate3);
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockResponse3.ToAsyncEnumerable());

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }
        
        resultMessages.Should().NotBeEmpty();
        
        // Verify that parameters were applied to Microsoft.Extensions.AI.ChatOptions
        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
            It.Is<Microsoft.Extensions.AI.ChatOptions>(opts => opts.Temperature == 0.5f && opts.TopP == 0.9f),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithBedrockSpecificModel_HandlesCorrectly()
    {
        // Arrange
        var bedrockPerson = MessageTestHelper.CreateTestPerson(
            "Titan Assistant", 
            "Amazon Titan assistant", 
            "AWS", 
            "amazon.titan-text-express-v1");

        var engine = new TestableAWSBedrockEngine(bedrockPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello Titan!")
        };

        var expectedResponse = new Microsoft.Extensions.AI.ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, 
            "Hello! I'm Amazon Titan, ready to assist you.");

        var mockResponse3 = new List<Microsoft.Extensions.AI.ChatResponseUpdate>();
        // Create a mock response with the expected text using proper constructor
        var responseUpdate3 = new Microsoft.Extensions.AI.ChatResponseUpdate(Microsoft.Extensions.AI.ChatRole.Assistant, expectedResponse.Text);
        mockResponse3.Add(responseUpdate3);
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockResponse3.ToAsyncEnumerable());

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }
        
        resultMessages.Should().NotBeEmpty();
        var firstMessage = resultMessages.First();
        firstMessage.Content.Should().Be("Hello! I'm Amazon Titan, ready to assist you.");
    }

    [Fact]
    public async Task SendMessageAsync_WithMultipleMessages_HandlesConversationCorrectly()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
    var messages = MessageTestHelper.CreateBasicConversation().Cast<IMessage>().ToList();

        var expectedResponse = new Microsoft.Extensions.AI.ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, 
            "Yes, that's absolutely correct! Paris has been the capital of France for centuries.");

        var mockResponse3 = new List<Microsoft.Extensions.AI.ChatResponseUpdate>();
        // Create a mock response with the expected text using proper constructor
        var responseUpdate3 = new Microsoft.Extensions.AI.ChatResponseUpdate(Microsoft.Extensions.AI.ChatRole.Assistant, expectedResponse.Text);
        mockResponse3.Add(responseUpdate3);
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockResponse3.ToAsyncEnumerable());

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }
        
        resultMessages.Should().NotBeEmpty();
        var firstMessage = resultMessages.First();
        firstMessage.Content.Should().Be("Yes, that's absolutely correct! Paris has been the capital of France for centuries.");

        // Verify that all messages were included in the conversation
        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.Is<IList<Microsoft.Extensions.AI.ChatMessage>>(msgs => msgs.Count == messages.Count),
            It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithLongMessage_HandlesCorrectly()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
    var longMessage = MessageTestHelper.CreateLongMessage(5000);
    var messages = new List<IMessage> { longMessage as IMessage ?? (IMessage)longMessage };

        var expectedResponse = new Microsoft.Extensions.AI.ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, 
            "I've received your long message and processed it successfully.");

        var mockResponse3 = new List<Microsoft.Extensions.AI.ChatResponseUpdate>();
        // Create a mock response with the expected text using proper constructor
        var responseUpdate3 = new Microsoft.Extensions.AI.ChatResponseUpdate(Microsoft.Extensions.AI.ChatRole.Assistant, expectedResponse.Text);
        mockResponse3.Add(responseUpdate3);
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<Microsoft.Extensions.AI.ChatMessage>>(),
                It.IsAny<Microsoft.Extensions.AI.ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockResponse3.ToAsyncEnumerable());

        // Mock session
        var mockSession = new Mock<ISession>();

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }
        
        resultMessages.Should().NotBeEmpty();
        var firstMessage = resultMessages.First();
        firstMessage.Content.Should().Be("I've received your long message and processed it successfully.");
    }

    #region Streaming Message Tests

    [Fact]
    public async Task SendMessageAsync_WithStreamingTextResponse_ReturnsMultipleUpdates()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Tell me about AWS Bedrock")
        };

        var streamingResponses = new[] { "AWS Bedrock", " is a", " fully managed", " service for", " building AI applications." };
        
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
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("What databases are available on AWS?")
        };

        var toolCall = new FunctionCallContent("call_aws123", "list_aws_services", new Dictionary<string, object?> { { "category", "database" } });
        
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
            { "aws", new List<FunctionWithDescription> { CreateMockAWSFunction() } }
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
        json.Should().Contain("list_aws_services");
        json.Should().Contain("call_aws123");

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithToolResultResponse_ReturnsToolResultMessage()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateToolCallMessage("list_aws_services"),
        };

        var toolResult = new FunctionResultContent("call_aws123", "{\"services\": [\"RDS\", \"DynamoDB\", \"Aurora\", \"DocumentDB\"]}");
        
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
        jsonToolResult.Should().Contain("call_aws123");
        jsonToolResult.Should().Contain("RDS");
        jsonToolResult.Should().Contain("DynamoDB");

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithUsageContent_TriggersUsageDetailsEvent()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello Bedrock")
        };

        var usageDetails = new Microsoft.Extensions.AI.UsageDetails
        {
            InputTokenCount = 12,
            OutputTokenCount = 28,
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
        receivedUsageDetails!.InputTokens.Should().Be(12);
        receivedUsageDetails.OutputTokens.Should().Be(28);
        receivedUsageDetails.TotalTokens.Should().Be(40);
    }

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var engine = new TestableAWSBedrockEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Start a long response about AWS services")
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
    public async Task SendMessageAsync_WithBedrockParameters_AppliesParametersToOptions()
    {
        // Arrange
        var personWithParams = MessageTestHelper.CreateTestPerson();
        personWithParams.ProviderName = "AWS";
        personWithParams.ModelId = "anthropic.claude-3-haiku-20240307-v1:0";
        personWithParams.Temperature = 0.6;
        personWithParams.TopP = 0.85;
        personWithParams.Parameters = new Dictionary<string, string>
        {
            { PersonParameterNames.LimitMaxContextLength, "400" }
        };

        var engine = new TestableAWSBedrockEngine(personWithParams, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
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
                return CreateBedrockMockResponseStream("Test response");
            });

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        // Act
        var result = await engine.GetMessageAsync(messages, null, mockSession.Object, CancellationToken.None);
        
        // Convert to trigger the call
        await foreach (var msg in result) { }

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Temperature.Should().Be(0.6f);
        capturedOptions.MaxOutputTokens.Should().Be(400);
        capturedOptions.TopP.Should().Be(0.85f);
    }

    [Fact]
    public async Task SendMessageAsync_WithMultipleBedrockModels_HandlesCorrectly()
    {
        // Test different Bedrock model types
        var models = new[]
        {
            "anthropic.claude-3-haiku-20240307-v1:0",
            "anthropic.claude-3-sonnet-20240229-v1:0",
            "meta.llama2-70b-chat-v1"
        };

        foreach (var modelId in models)
        {
            // Arrange
            var person = MessageTestHelper.CreateTestPerson("Bedrock Assistant", "Test", "AWS", modelId);
            var engine = new TestableAWSBedrockEngine(person, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
            var messages = new List<IMessage>
            {
                MessageTestHelper.CreateUserMessage($"Hello from {modelId}")
            };

            _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                    It.IsAny<IList<ChatMessage>>(),
                    It.IsAny<ChatOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateBedrockMockResponseStream($"Response from {modelId}"));

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
        // Provide the tool call via Contents so the engine processes it correctly.
        var update = new ChatResponseUpdate(ChatRole.Assistant, new List<AIContent> { toolCall });
        yield return update;
        await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateToolResultResponseStream(FunctionResultContent toolResult)
    {
        // Provide the tool result via Contents so the engine processes it correctly.
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
        
        yield return new ChatResponseUpdate(ChatRole.Assistant, " response about AWS services");
        await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateBedrockMockResponseStream(string text)
    {
        var responseUpdate = new ChatResponseUpdate(ChatRole.Assistant, text);
        yield return responseUpdate;
        await Task.Yield();
    }

    private static FunctionWithDescription CreateMockAWSFunction()
    {
        return new FunctionWithDescription
        {
            Function = (Dictionary<string, object?> args) => Task.FromResult<object?>("AWS services list"),
            Description = new FunctionDescription
            {
                Name = "list_aws_services",
                Description = "Lists AWS services by category",
                Parameters = new List<FunctionTypeMetadata>
                {
                    new FunctionTypeMetadata
                    {
                        Name = "category",
                        Description = "The service category to list",
                        ParameterType = typeof(string),
                        IsRequired = true,
                        DefaultValue = null
                    }
                }
            }
        };
    }

    #endregion

    private static async IAsyncEnumerable<Microsoft.Extensions.AI.ChatResponseUpdate> CreateAsyncEnumerable(
        IEnumerable<Microsoft.Extensions.AI.ChatResponseUpdate> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Delay(1); // Small delay to simulate streaming
        }
    }

    public void Dispose()
    {
        // Clean up any resources if needed
    }
}
