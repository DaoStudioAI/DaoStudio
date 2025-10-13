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
/// Unit tests for Google tool call functionality.
/// Tests tool calls, tool results, and usage tracking.
/// </summary>
public class GoogleToolCallTests : IDisposable
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<ILogger<BaseEngine>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<StorageFactory> _mockStorageFactory;
    private readonly Mock<IAPIProviderRepository> _mockApiProviderRepository;
    private readonly Mock<IPlainAIFunctionFactory> _mockPlainAIFunctionFactory;
    private readonly MockPerson _testPerson;

    public GoogleToolCallTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _mockLogger = new Mock<ILogger<BaseEngine>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockStorageFactory = new Mock<StorageFactory>("test.db");
        _mockApiProviderRepository = new Mock<IAPIProviderRepository>();
        _mockPlainAIFunctionFactory = new Mock<IPlainAIFunctionFactory>();
        
        _testPerson = MockPerson.CreateAssistant("Google Assistant", "Google", "gemini-pro");

        _mockStorageFactory.Setup(sf => sf.GetApiProviderRepositoryAsync())
                          .ReturnsAsync(_mockApiProviderRepository.Object);
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

        var toolCall = new FunctionCallContent("call_789", "search", new Dictionary<string, object?> { { "query", "artificial intelligence" } });
        
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
        if (!string.IsNullOrEmpty(firstMessage.Content))
        {
            firstMessage.Content.Should().Contain("Tool Call: search");
            firstMessage.Content.Should().Contain("call_789");
        }
        else
        {
            firstMessage.BinaryContents.Should().NotBeNull();
            var toolBinary = firstMessage.BinaryContents!.FirstOrDefault(b => b.Type == DaoStudio.Interfaces.MsgBinaryDataType.ToolCall);
            toolBinary.Should().NotBeNull();
            var json = Encoding.UTF8.GetString(toolBinary!.Data);
            json.Should().Contain("search");
            json.Should().Contain("call_789");
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
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateToolCallMessage("search"),
        };

        var toolResult = new FunctionResultContent("call_789", "{\"results\": [\"AI is transforming technology\", \"Machine learning advances\"]}");
        
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
            firstMessage.Content.Should().Contain("call_789");
        }
        else
        {
            firstMessage.BinaryContents.Should().NotBeNull();
            var toolResultBinary = firstMessage.BinaryContents!.FirstOrDefault(b => b.Type == DaoStudio.Interfaces.MsgBinaryDataType.ToolCallResult);
            toolResultBinary.Should().NotBeNull();
            var json = Encoding.UTF8.GetString(toolResultBinary!.Data);
            json.Should().Contain("call_789");
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
        var engine = new TestableGoogleEngine(_testPerson, _mockLogger.Object, _mockStorageFactory.Object, _mockPlainAIFunctionFactory.Object, Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Hello Gemini")
        };

        var usageDetails = new Microsoft.Extensions.AI.UsageDetails
        {
            InputTokenCount = 12,
            OutputTokenCount = 18,
            TotalTokenCount = 30
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
        receivedUsageDetails.OutputTokens.Should().Be(18);
        receivedUsageDetails.TotalTokens.Should().Be(30);
    }

    #region Helper Methods for Tool Call Testing

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateToolCallResponseStream(FunctionCallContent toolCall)
    {
    var update = new ChatResponseUpdate(ChatRole.Assistant, "");
    update.Contents = new List<AIContent> { toolCall };
    yield return update;
    await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateToolResultResponseStream(FunctionResultContent toolResult)
    {
    var update = new ChatResponseUpdate(ChatRole.Tool, toolResult.Result?.ToString());
    update.Contents = new List<AIContent> { toolResult };
    yield return update;
    await Task.Yield();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateUsageResponseStream(Microsoft.Extensions.AI.UsageDetails usageDetails)
    {
        var textUpdate = new ChatResponseUpdate(ChatRole.Assistant, "Response with usage");
        yield return textUpdate;
        
        // Create usage content and add to response
        var usageContent = new UsageContent(usageDetails);
    var usageUpdate = new ChatResponseUpdate(ChatRole.Assistant, string.Empty);
    usageUpdate.Contents = new List<AIContent> { usageContent };
    yield return usageUpdate;
        
        await Task.Yield();
    }

    private static FunctionWithDescription CreateMockSearchFunction()
    {
        return new FunctionWithDescription
        {
            Function = (Dictionary<string, object?> args) => Task.FromResult<object?>("search results"),
            Description = new FunctionDescription
            {
                Name = "search",
                Description = "Searches for information on the web",
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
        // Cleanup any resources if needed
    }
}
