using DaoStudio.Engines;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Helpers;
using TestDaoStudio.Mocks;
using DaoStudio.Engines.MEAI;
using TestDaoStudio.TestableEngines;
using DaoUsageDetails = DaoStudio.Interfaces.UsageDetails;
using System.Text;

namespace TestDaoStudio.Engines.ToolCall;

/// <summary>
/// Unit tests for Anthropic tool call functionality.
/// Tests tool calls, tool results, and usage tracking.
/// </summary>
public class AnthropicToolCallTests : IDisposable
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<ILogger<BaseEngine>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<StorageFactory> _mockStorageFactory;
    private readonly Mock<IAPIProviderRepository> _mockApiProviderRepository;
    private readonly MockPerson _testPerson;

    public AnthropicToolCallTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _mockLogger = new Mock<ILogger<BaseEngine>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockStorageFactory = new Mock<StorageFactory>("test.db");
        _mockApiProviderRepository = new Mock<IAPIProviderRepository>();
        
        _testPerson = MockPerson.CreateAssistant("Anthropic Assistant", "Anthropic", "claude-3-sonnet");

        _mockStorageFactory.Setup(sf => sf.GetApiProviderRepositoryAsync())
                          .ReturnsAsync(_mockApiProviderRepository.Object);
    }

    [Fact]
    public async Task SendMessageAsync_WithToolCallResponse_ReturnsToolCallMessage()
    {
        // Arrange
        var engine = new TestableAnthropicEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("What's the weather like in Paris?")
        };

        var toolCall = new FunctionCallContent("call_456", "get_weather", new Dictionary<string, object?> { { "location", "Paris" } });
        
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
        if (!string.IsNullOrEmpty(firstMessage.Content))
        {
            firstMessage.Content.Should().Contain("Tool Call: get_weather");
        }
        else
        {
            firstMessage.BinaryContents.Should().NotBeNull();
            var toolBinary = firstMessage.BinaryContents!.FirstOrDefault(b => b.Type == DaoStudio.Interfaces.MsgBinaryDataType.ToolCall);
            toolBinary.Should().NotBeNull();
            var json = Encoding.UTF8.GetString(toolBinary!.Data);
            json.Should().Contain("get_weather");
        }
        if (!string.IsNullOrEmpty(firstMessage.Content))
        {
            if (!string.IsNullOrEmpty(firstMessage.Content))
            {
                firstMessage.Content.Should().Contain("call_456");
            }
            else
            {
                firstMessage.BinaryContents.Should().NotBeNull();
                var toolBinary = firstMessage.BinaryContents!.FirstOrDefault(b => b.Type == DaoStudio.Interfaces.MsgBinaryDataType.ToolCall);
                toolBinary.Should().NotBeNull();
                var json = Encoding.UTF8.GetString(toolBinary!.Data);
                json.Should().Contain("call_456");
            }
        }
        else
        {
            firstMessage.BinaryContents.Should().NotBeNull();
            var toolBinary = firstMessage.BinaryContents!.FirstOrDefault(b => b.Type == DaoStudio.Interfaces.MsgBinaryDataType.ToolCall);
            toolBinary.Should().NotBeNull();
            var json = Encoding.UTF8.GetString(toolBinary!.Data);
            json.Should().Contain("call_456");
        }

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

        var toolResult = new FunctionResultContent("call_456", "{\"temperature\": \"68°F\", \"condition\": \"partly cloudy\"}");
        
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
        if (!string.IsNullOrEmpty(firstMessage.Content))
        {
            firstMessage.Content.Should().Contain("Tool Result:");
        }
        else
        {
            firstMessage.BinaryContents.Should().NotBeNull();
            var toolResultBinary = firstMessage.BinaryContents!.FirstOrDefault(b => b.Type == DaoStudio.Interfaces.MsgBinaryDataType.ToolCallResult);
            toolResultBinary.Should().NotBeNull();
            var json = Encoding.UTF8.GetString(toolResultBinary!.Data);
            json.Should().Contain("call_456");
        }
        if (!string.IsNullOrEmpty(firstMessage.Content))
        {
            if (!string.IsNullOrEmpty(firstMessage.Content))
            {
                firstMessage.Content.Should().Contain("call_456");
            }
            else
            {
                firstMessage.BinaryContents.Should().NotBeNull();
                var toolResultBinary = firstMessage.BinaryContents!.FirstOrDefault(b => b.Type == DaoStudio.Interfaces.MsgBinaryDataType.ToolCallResult);
                toolResultBinary.Should().NotBeNull();
                var json = Encoding.UTF8.GetString(toolResultBinary!.Data);
                json.Should().Contain("call_456");
            }
        }
        else
        {
            firstMessage.BinaryContents.Should().NotBeNull();
            var toolResultBinary = firstMessage.BinaryContents!.FirstOrDefault(b => b.Type == DaoStudio.Interfaces.MsgBinaryDataType.ToolCallResult);
            toolResultBinary.Should().NotBeNull();
            var json = Encoding.UTF8.GetString(toolResultBinary!.Data);
            json.Should().Contain("call_456");
        }

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

    #region Helper Methods for Tool Call Testing

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateToolCallResponseStream(FunctionCallContent toolCall)
    {
    var update = new ChatResponseUpdate(ChatRole.Assistant, new List<AIContent> { toolCall });
    yield return update;
    await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateToolResultResponseStream(FunctionResultContent toolResult)
    {
    // yield the tool result as content
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
    var usageUpdate = new ChatResponseUpdate(ChatRole.Assistant, new List<AIContent> { usageContent });
    yield return usageUpdate;
        
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
