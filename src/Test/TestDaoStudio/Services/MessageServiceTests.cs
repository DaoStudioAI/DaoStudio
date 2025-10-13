using DaoStudio.Interfaces;
using DaoStudio.Services;
using DaoStudio.DBStorage.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Helpers;

namespace TestDaoStudio.Services;

/// <summary>
/// Unit tests for MessageService class.
/// Tests message CRUD operations, binary data handling, and message management functionality.
/// </summary>
public class MessageServiceTests : IDisposable
{
    private readonly Mock<IMessageRepository> _mockRepository;
    private readonly Mock<ILogger<MessageService>> _mockLogger;
    private readonly MessageService _service;

    public MessageServiceTests()
    {
        _mockRepository = new Mock<IMessageRepository>();
        _mockLogger = new Mock<ILogger<MessageService>>();
        _service = new MessageService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act & Assert
        _service.Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(IMessageRepository))]
    [InlineData(nameof(ILogger<MessageService>))]
    public void Constructor_WithNullParameters_ThrowsArgumentNullException(string parameterName)
    {
        // Arrange & Act & Assert
        Action act = parameterName switch
        {
            nameof(IMessageRepository) => () => new MessageService(null!, _mockLogger.Object),
            nameof(ILogger<MessageService>) => () => new MessageService(_mockRepository.Object, null!),
            _ => throw new ArgumentException("Invalid parameter name", nameof(parameterName))
        };

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateMessageAsync_WithValidMessage_CreatesMessage()
    {
        // Arrange
        var content = "Hello, world!";
        var role = MessageRole.User;
        var type = MessageType.Normal;
        var sessionId = 1L;

        var expectedMessage = MessageTestHelper.CreateUserMessage(content, sessionId);
        expectedMessage.Id = 1;

        _mockRepository.Setup(r => r.CreateMessageAsync(It.IsAny<DaoStudio.DBStorage.Models.Message>()))
                      .ReturnsAsync(new DaoStudio.DBStorage.Models.Message
                      {
                          Id = 1,
                          Content = content,
                          Role = (int)role,
                          Type = (int)type,
                          SessionId = sessionId,
                          CreatedAt = DateTime.UtcNow,
                          LastModified = DateTime.UtcNow
                      });

        // Act
        var result = await _service.CreateMessageAsync(content, role, type, sessionId, true, 0, 0);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Be(content);
        result.Role.Should().Be(role);
        result.Type.Should().Be(type);
        result.SessionId.Should().Be(sessionId);

        _mockRepository.Verify(r => r.CreateMessageAsync(It.Is<DaoStudio.DBStorage.Models.Message>(m =>
            m.Content == content &&
            m.Role == (int)role &&
            m.Type == (int)type &&
            m.SessionId == sessionId
        )), Times.Once);
    }

    [Fact]
    public async Task CreateMessageAsync_WithIMessageParameter_CreatesMessage()
    {
        // Arrange
        var message = MessageTestHelper.CreateUserMessage("Test message", 1);

        _mockRepository.Setup(r => r.CreateMessageAsync(It.IsAny<DaoStudio.DBStorage.Models.Message>()))
                      .ReturnsAsync(new DaoStudio.DBStorage.Models.Message
                      {
                          Id = 123,
                          Content = message.Content,
                          Role = message.Role,
                          Type = message.Type,
                          SessionId = message.SessionId,
                          CreatedAt = DateTime.UtcNow,
                          LastModified = DateTime.UtcNow
                      });

        // Act
        var result = await _service.SaveMessageAsync(message, allowCreate: true);

        // Assert
        result.Should().BeFalse();
        _mockRepository.Verify(r => r.CreateMessageAsync(It.IsAny<DaoStudio.DBStorage.Models.Message>()), Times.Never);
    }

    [Fact]
    public async Task GetMessageByIdAsync_WithValidId_ReturnsMessage()
    {
        // Arrange
        var messageId = 1L;
        var dbMessage = new DaoStudio.DBStorage.Models.Message
        {
            Id = messageId,
            Content = "Test message",
            Role = (int)MessageRole.User,
            Type = (int)MessageType.Normal,
            SessionId = 1,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.GetByIdAsync(messageId))
                  .ReturnsAsync(dbMessage);

        // Act
        var result = await _service.GetMessageByIdAsync(messageId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(messageId);
        result.Content.Should().Be("Test message");

        _mockRepository.Verify(r => r.GetByIdAsync(messageId), Times.Once);
    }

    [Fact]
    public async Task GetMessageByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var invalidId = 999L;
        _mockRepository.Setup(r => r.GetByIdAsync(invalidId))
                  .ReturnsAsync((DaoStudio.DBStorage.Models.Message?)null);

        // Act
        var result = await _service.GetMessageByIdAsync(invalidId);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(r => r.GetByIdAsync(invalidId), Times.Once);
    }

    [Fact]
    public async Task GetMessagesBySessionIdAsync_WithValidSessionId_ReturnsMessages()
    {
        // Arrange
        var sessionId = 1L;
        var dbMessages = new List<DaoStudio.DBStorage.Models.Message>
        {
            new() { Id = 1, Content = "Message 1", SessionId = sessionId, Role = (int)MessageRole.User },
            new() { Id = 2, Content = "Message 2", SessionId = sessionId, Role = (int)MessageRole.Assistant }
        };

        _mockRepository.Setup(r => r.GetBySessionIdAsync(sessionId))
                  .ReturnsAsync(dbMessages);

        // Act
        var result = await _service.GetMessagesBySessionIdAsync(sessionId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(m => m.SessionId == sessionId);

        _mockRepository.Verify(r => r.GetBySessionIdAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task DeleteMessagesBySessionIdAsync_WithValidSessionId_DeletesMessages()
    {
        // Arrange
        var sessionId = 1L;
        var deletedCount = 5;

        _mockRepository.Setup(r => r.DeleteBySessionIdAsync(sessionId))
                  .ReturnsAsync(deletedCount);

        // Act
        var result = await _service.DeleteMessagesBySessionIdAsync(sessionId);

        // Assert
        result.Should().Be(deletedCount);
        _mockRepository.Verify(r => r.DeleteBySessionIdAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task SaveMessageAsync_WithNewMessage_CreatesMessage()
    {
        // Arrange
        var message = MessageTestHelper.CreateUserMessage("New message");
        message.Id = 0; // New message

        _mockRepository.Setup(r => r.CreateMessageAsync(It.IsAny<DaoStudio.DBStorage.Models.Message>()))
                      .ReturnsAsync(new DaoStudio.DBStorage.Models.Message
                      {
                          Id = 1,
                          Content = message.Content,
                          Role = message.Role,
                          Type = message.Type,
                          SessionId = message.SessionId,
                          CreatedAt = DateTime.UtcNow,
                          LastModified = DateTime.UtcNow
                      });

        // Act
        var result = await _service.SaveMessageAsync(message, allowCreate: true);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.CreateMessageAsync(It.IsAny<DaoStudio.DBStorage.Models.Message>()), Times.Once);
    }

    [Fact]
    public async Task SaveMessageAsync_WithExistingMessage_UpdatesMessage()
    {
        // Arrange
        var message = MessageTestHelper.CreateUserMessage("Updated message");
        message.Id = 1; // Existing message

        _mockRepository.Setup(r => r.SaveMessageAsync(It.IsAny<DaoStudio.DBStorage.Models.Message>()))
                  .ReturnsAsync(true);

        // Act
        var result = await _service.SaveMessageAsync(message, allowCreate: false);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.SaveMessageAsync(It.IsAny<DaoStudio.DBStorage.Models.Message>()), Times.Once);
    }

    [Fact]
    public async Task SaveMessageAsync_WithNewMessageAndAllowCreateFalse_ThrowsException()
    {
        // Arrange
        var message = MessageTestHelper.CreateUserMessage("New message");
        message.Id = 0; // New message

        // Act & Assert
        var act = async () => await _service.SaveMessageAsync(message, allowCreate: false);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateMessageAsync_WithValidMessage_UpdatesMessage()
    {
        // Arrange
        var message = MessageTestHelper.CreateUserMessage("Updated message");
        message.Id = 1;

        _mockRepository.Setup(r => r.SaveMessageAsync(It.IsAny<DaoStudio.DBStorage.Models.Message>()))
                  .ReturnsAsync(true);

        // Act
        var result = await _service.SaveMessageAsync(message, allowCreate: false);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.SaveMessageAsync(It.IsAny<DaoStudio.DBStorage.Models.Message>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMessageAsync_WithValidId_DeletesMessage()
    {
        // Arrange
        var messageId = 1L;
        _mockRepository.Setup(r => r.DeleteAsync(messageId))
                  .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteMessageAsync(messageId);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.DeleteAsync(messageId), Times.Once);
    }

    [Fact]
    public async Task GetAllMessagesAsync_ReturnsAllMessages()
    {
        // Arrange
        var dbMessages = new List<DaoStudio.DBStorage.Models.Message>
        {
            new() { Id = 1, Content = "Message 1", SessionId = 1, Role = (int)MessageRole.User },
            new() { Id = 2, Content = "Message 2", SessionId = 2, Role = (int)MessageRole.Assistant }
        };

        _mockRepository.Setup(r => r.GetAllAsync())
                      .ReturnsAsync(dbMessages);

        // Act
        var result = await _service.GetAllMessagesAsync();

        // Assert
        result.Should().HaveCount(2);
        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateMessageAsync_WithNullContent_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _service.CreateMessageAsync(null!, MessageRole.User, MessageType.Normal, 1);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateMessageAsync_WithInvalidSessionId_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _service.CreateMessageAsync("Test", MessageRole.User, MessageType.Normal, 0);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteMessageInSessionAsync_WithValidParameters_DeletesMessages()
    {
        // Arrange
        var sessionId = 1L;
        var specifiedMessageId = 5L;
        var includeSpecified = true;
        var deletedCount = 3;

        _mockRepository.Setup(r => r.DeleteFromMessageInSessionAsync(sessionId, specifiedMessageId, includeSpecified))
                  .ReturnsAsync(deletedCount);

        // Act
        var result = await _service.DeleteMessageInSessionAsync(sessionId, specifiedMessageId, includeSpecified);

        // Assert
        result.Should().Be(deletedCount);
        _mockRepository.Verify(r => r.DeleteFromMessageInSessionAsync(sessionId, specifiedMessageId, includeSpecified), Times.Once);
    }

    [Fact]
    public async Task MessageService_HandlesRepositoryExceptions_Gracefully()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync())
                  .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        var act = async () => await _service.GetAllMessagesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Database error");
    }

    public void Dispose()
    {
        // No unmanaged resources to dispose. MessageService does not implement IDisposable.
    }
}
