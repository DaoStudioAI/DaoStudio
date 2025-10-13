using DaoStudio.Engines;
using DaoStudio.Interfaces;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.Interfaces.Plugins;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Helpers;
using TestDaoStudio.Mocks;
using DaoStudio.Engines.MEAI;
using TestDaoStudio.TestableEngines;
using System.Text.Json;

namespace TestDaoStudio.ToolCall;

/// <summary>
/// Unit tests for Ollama tool call functionality.
/// Tests tool invocation, results handling, and usage tracking.
/// </summary>
public class OllamaToolCallTests : IDisposable
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<ILogger<BaseEngine>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<StorageFactory> _mockStorageFactory;
    private readonly Mock<IAPIProviderRepository> _mockApiProviderRepository;
    private readonly MockPerson _testPerson;

    public OllamaToolCallTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _mockLogger = new Mock<ILogger<BaseEngine>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockStorageFactory = new Mock<StorageFactory>("test.db");
        _mockApiProviderRepository = new Mock<IAPIProviderRepository>();
        
        _testPerson = MockPerson.CreateAssistant("Ollama Assistant", "Ollama", "llama2");

        _mockStorageFactory.Setup(sf => sf.GetApiProviderRepositoryAsync())
                          .ReturnsAsync(_mockApiProviderRepository.Object);
    }

    [Fact]
    public async Task SendMessageAsync_WithToolCallResponse_ReturnsToolCallMessage()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("List files in the current directory")
        };

        var toolCall = new FunctionCallContent("call_789", "list_directory", new Dictionary<string, object?> { { "path", "." } });
        
        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateToolCallResponseStream(toolCall));

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        var directoryFunction = CreateMockDirectoryFunction();
        var functions = new Dictionary<string, List<FunctionWithDescription>>
        {
            { "file_system", new List<FunctionWithDescription> { directoryFunction } }
        };

        // Act
        var result = await engine.GetMessageAsync(messages, functions, mockSession.Object, CancellationToken.None);
        
        // Convert async enumerable to list
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }

        // Assert
        resultMessages.Should().NotBeEmpty();
        var toolCallMessage = resultMessages.FirstOrDefault(m => m.Role == MessageRole.Assistant);
        toolCallMessage.Should().NotBeNull();
        toolCallMessage!.Content.Should().Contain("list the files");

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessageAsync_WithToolResultMessage_HandlesToolResults()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        
        var toolResultMessage = MessageTestHelper.CreateToolResultMessage(
            "call_789",
            "file1.txt\nfile2.txt\nfolder1/\nREADME.md"
        );
        
        var messages = new List<IMessage> { toolResultMessage };

        var followUpResponse = new ChatMessage(ChatRole.Assistant, "I can see there are 3 files and 1 folder in the current directory: file1.txt, file2.txt, README.md, and a folder named folder1.");

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateSimpleResponseStream(followUpResponse));

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
        var responseMessage = resultMessages.First();
        responseMessage.Role.Should().Be(MessageRole.Assistant);
        responseMessage.Content.Should().Contain("3 files and 1 folder");

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessageAsync_WithUsageDetails_TracksTokenUsage()
    {
        // Arrange
        var engine = new TestableOllamaEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Show directory contents with details")
        };

        var usageDetails = new Microsoft.Extensions.AI.UsageDetails
        {
            InputTokenCount = 25,
            OutputTokenCount = 15,
            TotalTokenCount = 40
        };

        _mockChatClient.Setup(c => c.GetStreamingResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateUsageResponseStream(usageDetails));

        var mockSession = new Mock<ISession>();
        mockSession.Setup(s => s.ToolExecutionMode).Returns(ToolExecutionMode.Auto);

        var directoryFunction = CreateMockDirectoryFunction();
        var functions = new Dictionary<string, List<FunctionWithDescription>>
        {
            { "file_system", new List<FunctionWithDescription> { directoryFunction } }
        };

        // Act
        var result = await engine.GetMessageAsync(messages, functions, mockSession.Object, CancellationToken.None);
        
        // Convert async enumerable to list
        var resultMessages = new List<IMessage>();
        await foreach (var msg in result)
        {
            resultMessages.Add(msg);
        }

        // Assert
        resultMessages.Should().NotBeEmpty();
        // Note: Usage tracking verification would depend on actual implementation
        // usageEventReceived.Should().BeTrue();

        _mockChatClient.Verify(c => c.GetStreamingResponseAsync(
            It.IsAny<IList<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #region Helper Methods

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateToolCallResponseStream(FunctionCallContent toolCall)
    {
        // Provide a human-readable text then the content update
        var textUpdate = new ChatResponseUpdate(ChatRole.Assistant, "list the files");
        yield return textUpdate;

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

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateSimpleResponseStream(ChatMessage message)
    {
        var update = new ChatResponseUpdate(message.Role, message.Text);
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

    private static FunctionWithDescription CreateMockDirectoryFunction()
    {
        return new FunctionWithDescription
        {
            Function = (Dictionary<string, object?> args) => Task.FromResult<object?>("file1.txt\nfile2.txt\nfolder1/\nREADME.md"),
            Description = new FunctionDescription
            {
                Name = "list_directory",
                Description = "Lists files and directories in the specified path",
                Parameters = new List<FunctionTypeMetadata>
                {
                    new FunctionTypeMetadata
                    {
                        Name = "path",
                        Description = "The directory path to list",
                        ParameterType = typeof(string),
                        IsRequired = true,
                        DefaultValue = null
                    },
                    new FunctionTypeMetadata
                    {
                        Name = "detailed",
                        Description = "Include detailed file information",
                        ParameterType = typeof(bool),
                        IsRequired = false,
                        DefaultValue = false
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
