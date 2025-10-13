using DaoStudio.Engines;
using DaoStudio.Interfaces;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using System.Runtime.CompilerServices;
using TestDaoStudio.Helpers;
using TestDaoStudio.Mocks;
using DaoStudio.Engines.MEAI;
using TestDaoStudio.TestableEngines;

namespace TestDaoStudio.Engines.Streaming;

/// <summary>
/// Unit tests for OpenAI streaming functionality.
/// Tests streaming message responses and multi-part content delivery.
/// </summary>
public class OpenAIStreamingTests : IDisposable
{
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<ILogger<BaseEngine>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<StorageFactory> _mockStorageFactory;
    private readonly Mock<IAPIProviderRepository> _mockApiProviderRepository;
    private readonly MockPerson _testPerson;

    public OpenAIStreamingTests()
    {
        _mockChatClient = new Mock<IChatClient>();
        _mockLogger = new Mock<ILogger<BaseEngine>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockStorageFactory = new Mock<StorageFactory>("test.db");
        _mockApiProviderRepository = new Mock<IAPIProviderRepository>();
        
        _testPerson = MockPerson.CreateAssistant("OpenAI Assistant", "OpenAI", "gpt-4");

        _mockStorageFactory.Setup(sf => sf.GetApiProviderRepositoryAsync())
                          .ReturnsAsync(_mockApiProviderRepository.Object);
    }

    [Fact]
    public async Task SendMessageAsync_WithStreamingTextResponse_ReturnsMultipleUpdates()
    {
        // Arrange
        var engine = new TestableOpenAIEngine(_testPerson, _mockLogger.Object, _mockLoggerFactory.Object, _mockStorageFactory.Object, Mock.Of<IPlainAIFunctionFactory>(), Mock.Of<ISettings>(), _mockChatClient.Object);
        var messages = new List<IMessage>
        {
            MessageTestHelper.CreateUserMessage("Tell me a story in parts")
        };

        var streamingResponses = new[] { "Once upon", " a time", " there was", " a brave", " knight." };
        
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

    #region Helper Methods for Stream Creation

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateMultiPartResponseStream(string[] parts)
    {
        foreach (var part in parts)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, part);
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> CreateCancellableResponseStream([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, "Starting");
        await Task.Yield();
        
        cancellationToken.ThrowIfCancellationRequested();
        
        yield return new ChatResponseUpdate(ChatRole.Assistant, " response");
        await Task.Yield();
    }

    #endregion

    public void Dispose()
    {
        // Cleanup any resources if needed
    }
}
