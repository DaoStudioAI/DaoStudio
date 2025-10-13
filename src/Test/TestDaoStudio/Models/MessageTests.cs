using DaoStudio;
using DBStorage = DaoStudio.DBStorage;
using DaoStudio.Interfaces;
using FluentAssertions;
using TestDaoStudio.Helpers;

namespace TestDaoStudio.Models;

/// <summary>
/// Unit tests for the Message model class.
/// Tests property getters/setters, validation logic, and serialization/deserialization.
/// </summary>
public class MessageTests
{
    [Fact]
    public void Message_DefaultConstructor_CreatesInstanceWithDefaultValues()
    {
    // Act
    var message = new Message { Role = (int)MessageRole.User };

        // Assert
        message.Id.Should().Be(0);
        message.SessionId.Should().Be(0);
        message.Content.Should().BeNull();
        message.BinaryContents.Should().BeNull();
        message.BinaryVersion.Should().Be(0);
        message.ParentMsgId.Should().Be(0);
        message.ParentSessId.Should().Be(0);
    }

    [Fact]
    public void Message_PropertySettersAndGetters_WorkCorrectly()
    {
    // Arrange
    var message = new Message { Content = string.Empty, Role = (int)MessageRole.User };
        var now = DateTime.UtcNow;

        // Act
        message.Id = 123;
        message.SessionId = 456;
        message.Content = "Test content";
        ((IMessage)message).Role = MessageRole.User;
        ((IMessage)message).Type = MessageType.Normal;
        message.BinaryVersion = 1;
        message.ParentMsgId = 789;
        message.ParentSessId = 101112;
        message.CreatedAt = now;
        message.LastModified = now;

        // Assert
        message.Id.Should().Be(123);
        message.SessionId.Should().Be(456);
        message.Content.Should().Be("Test content");
        ((IMessage)message).Role.Should().Be(MessageRole.User);
        ((IMessage)message).Type.Should().Be(MessageType.Normal);
        message.BinaryVersion.Should().Be(1);
        message.ParentMsgId.Should().Be(789);
        message.ParentSessId.Should().Be(101112);
        message.CreatedAt.Should().Be(now);
        message.LastModified.Should().Be(now);
    }

    [Theory]
    [InlineData(MessageRole.User)]
    [InlineData(MessageRole.Assistant)]
    [InlineData(MessageRole.System)]
    [InlineData(MessageRole.Developer)]
    public void Message_RoleProperty_HandlesAllValidRoles(MessageRole role)
    {
    // Arrange
    var message = new Message { Content = string.Empty, Role = (int)MessageRole.User };

        // Act
        ((IMessage)message).Role = role;

        // Assert
        ((IMessage)message).Role.Should().Be(role);
    }

    [Theory]
    [InlineData(MessageType.Normal)]
    [InlineData(MessageType.Information)]
    public void Message_TypeProperty_HandlesAllValidTypes(MessageType type)
    {
    // Arrange
    var message = new Message { Content = string.Empty, Role = (int)MessageRole.User };

        // Act
        ((IMessage)message).Type = type;

        // Assert
        ((IMessage)message).Type.Should().Be(type);
    }

    [Fact]
    public void AddBinaryData_WithValidData_AddsBinaryDataToMessage()
    {
    // Arrange
    var message = new Message { Content = string.Empty, Role = (int)MessageRole.User };
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        message.AddBinaryData("test-file.png", MsgBinaryDataType.Image, testData);

        // Assert
        message.BinaryContents.Should().NotBeNull();
        message.BinaryContents.Should().HaveCount(1);
        
        var binaryData = message.BinaryContents!.First();
        binaryData.Name.Should().Be("test-file.png");
        binaryData.Type.Should().Be(MsgBinaryDataType.Image);
        binaryData.Data.Should().BeEquivalentTo(testData);
    }

    [Fact]
    public void AddBinaryData_MultipleCalls_AddsAllBinaryData()
    {
        // Arrange
    var message = new Message { Content = string.Empty, Role = (int)MessageRole.User };
        var imageData = new byte[] { 1, 2, 3 };
        var audioData = new byte[] { 4, 5, 6 };

        // Act
        message.AddBinaryData("image.png", MsgBinaryDataType.Image, imageData);
        message.AddBinaryData("audio.wav", MsgBinaryDataType.Audio, audioData);

        // Assert
        message.BinaryContents.Should().HaveCount(2);
        message.BinaryContents!.Should().Contain(bd => bd.Name == "image.png" && bd.Type == MsgBinaryDataType.Image);
        message.BinaryContents!.Should().Contain(bd => bd.Name == "audio.wav" && bd.Type == MsgBinaryDataType.Audio);
    }

    [Fact]
    public void FromDBMessage_WithValidDBMessage_ConvertsCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dbMessage = new DBStorage.Models.Message
        {
            Id = 123,
            SessionId = 456,
            Content = "Test content",
            Role = (int)MessageRole.Assistant,
            Type = (int)MessageType.Normal,
            BinaryVersion = 1,
            ParentMsgId = 789,
            ParentSessId = 101112,
            CreatedAt = now
        };

        // Act
        var message = Message.FromDBMessage(dbMessage);

        // Assert
        message.Id.Should().Be(123);
        message.SessionId.Should().Be(456);
        message.Content.Should().Be("Test content");
        ((IMessage)message).Role.Should().Be(MessageRole.Assistant);
        ((IMessage)message).Type.Should().Be(MessageType.Normal);
        message.BinaryVersion.Should().Be(1);
        message.ParentMsgId.Should().Be(789);
        message.ParentSessId.Should().Be(101112);
        message.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void FromDBMessage_WithNullDBMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => Message.FromDBMessage(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("dbMessage");
    }

    [Fact]
    public void FromDBMessage_WithBinaryContents_ConvertsBinaryDataCorrectly()
    {
        // Arrange
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        var dbMessage = new DBStorage.Models.Message
        {
            Id = 1,
            SessionId = 1,
            Content = "Test",
            Role = (int)MessageRole.User,
            BinaryContents = new List<DBStorage.Models.BinaryData>
            {
                new() { Name = "test.png", Type = (int)MsgBinaryDataType.Image, Data = testData }
            }
        };

        // Act
        var message = Message.FromDBMessage(dbMessage);

        // Assert
        message.BinaryContents.Should().NotBeNull();
        message.BinaryContents.Should().HaveCount(1);
        var binaryData = message.BinaryContents!.First();
        binaryData.Name.Should().Be("test.png");
        binaryData.Type.Should().Be(MsgBinaryDataType.Image);
        binaryData.Data.Should().BeEquivalentTo(testData);
    }

    [Fact]
    public void ToDBMessage_WithValidMessage_ConvertsCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var message = new Message
        {
            Id = 123,
            SessionId = 456,
            Content = "Test content",
            Role = (int)MessageRole.User,
            BinaryVersion = 1,
            ParentMsgId = 789,
            ParentSessId = 101112,
            CreatedAt = now
        };
        ((IMessage)message).Role = MessageRole.User;
        ((IMessage)message).Type = MessageType.Information;

        // Act
        var dbMessage = message.ToDBMessage();

        // Assert
        dbMessage.Id.Should().Be(123);
        dbMessage.SessionId.Should().Be(456);
        dbMessage.Content.Should().Be("Test content");
        dbMessage.Role.Should().Be((int)MessageRole.User);
        dbMessage.Type.Should().Be((int)MessageType.Information);
        dbMessage.BinaryVersion.Should().Be(1);
        dbMessage.ParentMsgId.Should().Be(789);
        dbMessage.ParentSessId.Should().Be(101112);
        dbMessage.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void ToDBMessage_WithBinaryContents_ConvertsBinaryDataCorrectly()
    {
        // Arrange
        var testData = new byte[] { 1, 2, 3, 4, 5 };
    var message = new Message { Content = string.Empty, Role = (int)MessageRole.User };
        message.AddBinaryData("test.png", MsgBinaryDataType.Image, testData);

        // Act
        var dbMessage = message.ToDBMessage();

        // Assert
        dbMessage.BinaryContents.Should().NotBeNull();
        dbMessage.BinaryContents.Should().HaveCount(1);
        var binaryData = dbMessage.BinaryContents!.First();
        binaryData.Name.Should().Be("test.png");
        binaryData.Type.Should().Be((int)MsgBinaryDataType.Image);
        binaryData.Data.Should().BeEquivalentTo(testData);
    }

    [Fact]
    public void MessageTestHelper_CreateTestMessage_CreatesValidMessage()
    {
        // Act
        var message = MessageTestHelper.CreateTestMessage();

        // Assert
        MessageTestHelper.IsValidMessage(message).Should().BeTrue();
        message.Content.Should().NotBeEmpty();
        ((IMessage)message).Role.Should().Be(MessageRole.User);
        ((IMessage)message).Type.Should().Be(MessageType.Normal);
    }

    [Fact]
    public void MessageTestHelper_CreateUserMessage_CreatesUserMessage()
    {
        // Act
        var message = MessageTestHelper.CreateUserMessage("Hello");

        // Assert
        message.Content.Should().Be("Hello");
        ((IMessage)message).Role.Should().Be(MessageRole.User);
    }

    [Fact]
    public void MessageTestHelper_CreateAssistantMessage_CreatesAssistantMessage()
    {
        // Act
        var message = MessageTestHelper.CreateAssistantMessage("Hi there!");

        // Assert
        message.Content.Should().Be("Hi there!");
        ((IMessage)message).Role.Should().Be(MessageRole.Assistant);
    }

    [Fact]
    public void MessageTestHelper_CreateSystemMessage_CreatesSystemMessage()
    {
        // Act
        var message = MessageTestHelper.CreateSystemMessage("You are helpful");

        // Assert
        message.Content.Should().Be("You are helpful");
        ((IMessage)message).Role.Should().Be(MessageRole.System);
    }

    [Fact]
    public void MessageTestHelper_CreateLongMessage_CreatesMessageOfRequestedLength()
    {
        // Act
        var message = MessageTestHelper.CreateLongMessage(5000);

        // Assert
        message.Content.Should().NotBeNull();
        message.Content!.Length.Should().Be(5000);
        MessageTestHelper.IsValidMessage(message).Should().BeTrue();
    }

    [Fact]
    public void MessageTestHelper_CreateEmptyMessage_CreatesEmptyMessage()
    {
        // Act
        var message = MessageTestHelper.CreateEmptyMessage();

        // Assert
        message.Content.Should().BeEmpty();
    }
}
