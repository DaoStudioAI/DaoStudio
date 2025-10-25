using DaoStudio.Interfaces;
using DaoStudio.Services;
using DaoStudio.DBStorage.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Helpers;

namespace TestDaoStudio.Services;

/// <summary>
/// Unit tests for SessionService message forwarding functionality.
/// Tests that the session infrastructure properly handles multiple session instances and message notifications.
/// </summary>
public class SessionServiceMessageForwardingTests : IDisposable
{
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<IMessageService> _mockMessageService;
    private readonly Mock<IToolService> _mockToolService;
    private readonly Mock<IPersonRepository> _mockPersonRepository;
    private readonly Mock<IPeopleService> _mockPeopleService;
    private readonly Mock<IPluginService> _mockPluginService;
    private readonly Mock<IEngineService> _mockEngineService;
    private readonly Mock<ILogger<SessionService>> _mockLogger;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly SessionService _sessionService;

    public SessionServiceMessageForwardingTests()
    {
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockMessageService = new Mock<IMessageService>();
        _mockToolService = new Mock<IToolService>();
        _mockPersonRepository = new Mock<IPersonRepository>();
        _mockPeopleService = new Mock<IPeopleService>();
        _mockPeopleService.Setup(ps => ps.GetPersonAsync(It.IsAny<string>()))
                          .ReturnsAsync((string name) => MessageTestHelper.CreateTestPerson(name));
        _mockPluginService = new Mock<IPluginService>();
        _mockEngineService = new Mock<IEngineService>();
        _mockLogger = new Mock<ILogger<SessionService>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();

        _mockLoggerFactory
            .Setup(lf => lf.CreateLogger(It.IsAny<string>()))
            .Returns(_mockLogger.Object);

        _sessionService = new SessionService(
            _mockMessageService.Object,
            _mockSessionRepository.Object,
            _mockToolService.Object,
            _mockPersonRepository.Object,
            _mockPeopleService.Object,
            _mockPluginService.Object,
            _mockEngineService.Object,
            _mockLogger.Object,
            _mockLoggerFactory.Object
        );
    }

    [Fact]
    public async Task CreateSession_WithParentSessionId_CreatesSubsessionMessageCorrectly()
    {
        // Arrange
        var parentSessionId = 1L;
        var person = MessageTestHelper.CreateTestPerson();
        
        var parentDbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = parentSessionId,
            Title = "Parent Session",
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        var childDbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = 2,
            Title = person.Name,
            Description = string.Empty,
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            ParentSessId = parentSessionId
        };

        _mockSessionRepository.Setup(sr => sr.GetSessionAsync(parentSessionId))
            .ReturnsAsync(parentDbSession);
        
        _mockSessionRepository.Setup(sr => sr.CreateSessionAsync(It.IsAny<DaoStudio.DBStorage.Models.Session>()))
            .ReturnsAsync(childDbSession);

        var subsessionMessage = MessageTestHelper.CreateTestMessage(
            sessionId: parentSessionId,
            role: MessageRole.User,
            type: MessageType.Information);

        _mockMessageService.Setup(ms => ms.CreateMessageAsync(
            It.IsAny<string>(),
            It.Is<MessageRole>(r => r == MessageRole.User),
            It.Is<MessageType>(t => t == MessageType.Information),
            It.Is<long?>(id => id == parentSessionId),
            It.Is<bool>(b => b == false),
            It.IsAny<long>(),
            It.IsAny<long>()))
            .ReturnsAsync(subsessionMessage);

        _mockMessageService.Setup(ms => ms.SaveMessageAsync(It.IsAny<IMessage>(), It.Is<bool>(b => b == true)))
            .ReturnsAsync(true);

        // Act
        var session = await _sessionService.CreateSession(person, parentSessionId);

        // Assert
        session.Should().NotBeNull();
        session.ParentSessionId.Should().Be(parentSessionId);
        
        // Verify that the subsession message was created and saved
        _mockMessageService.Verify(ms => ms.CreateMessageAsync(
            It.IsAny<string>(),
            It.Is<MessageRole>(r => r == MessageRole.User),
            It.Is<MessageType>(t => t == MessageType.Information),
            It.Is<long?>(id => id == parentSessionId),
            It.Is<bool>(b => b == false),
            It.IsAny<long>(),
            It.IsAny<long>()), Times.Once, "Subsession information message should be created");
        
        _mockMessageService.Verify(ms => ms.SaveMessageAsync(
            It.IsAny<IMessage>(), 
            It.Is<bool>(b => b == true)), Times.Once, "Subsession information message should be saved");
    }

    [Fact]
    public async Task MultipleSessionInstances_CanBeCreatedWithSameId()
    {
        // Arrange
        var sessionId = 1L;
        var person = MessageTestHelper.CreateTestPerson();
        
        var dbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = sessionId,
            Title = "Test Session",
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _mockSessionRepository.Setup(sr => sr.GetSessionAsync(sessionId))
            .ReturnsAsync(dbSession);

        // Act - Open the same session twice to create two separate instances
        var session1 = await _sessionService.OpenSession(sessionId);
        var session2 = await _sessionService.OpenSession(sessionId);

        // Assert
        session1.Should().NotBeNull();
        session2.Should().NotBeNull();
        session1.Should().NotBeSameAs(session2, "Should create different session instances");
        session1.Id.Should().Be(session2.Id, "Both instances should have the same session ID");
        session1.Id.Should().Be(sessionId);
    }

    [Fact]
    public async Task CreateSession_WithParentSession_FiresSubsessionCreatedEvent()
    {
        // Arrange
        var parentSessionId = 1L;
        var person = MessageTestHelper.CreateTestPerson();
        
        var parentDbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = parentSessionId,
            Title = "Parent Session",
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        var childDbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = 2,
            Title = person.Name,
            Description = string.Empty,
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            ParentSessId = parentSessionId
        };

        _mockSessionRepository.Setup(sr => sr.GetSessionAsync(parentSessionId))
            .ReturnsAsync(parentDbSession);
        
        _mockSessionRepository.Setup(sr => sr.CreateSessionAsync(It.IsAny<DaoStudio.DBStorage.Models.Session>()))
            .ReturnsAsync(childDbSession);

        var subsessionMessage = MessageTestHelper.CreateTestMessage(
            sessionId: parentSessionId,
            role: MessageRole.User,
            type: MessageType.Information);

        _mockMessageService.Setup(ms => ms.CreateMessageAsync(
            It.IsAny<string>(),
            It.IsAny<MessageRole>(),
            It.IsAny<MessageType>(),
            It.IsAny<long?>(),
            It.IsAny<bool>(),
            It.IsAny<long>(),
            It.IsAny<long>()))
            .ReturnsAsync(subsessionMessage);

        _mockMessageService.Setup(ms => ms.SaveMessageAsync(It.IsAny<IMessage>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

        ISession? capturedParentSession = null;
        ISession? capturedChildSession = null;

        _sessionService.SubsessionCreated += (sender, childSession) =>
        {
            capturedParentSession = sender as ISession;
            capturedChildSession = childSession;
        };

        // Act
        var session = await _sessionService.CreateSession(person, parentSessionId);

        // Assert
        capturedParentSession.Should().NotBeNull("SubsessionCreated event should fire");
        capturedChildSession.Should().NotBeNull("Child session should be passed in event");
        capturedChildSession.Should().Be(session);
        capturedParentSession!.Id.Should().Be(parentSessionId);
    }

    [Fact]
    public async Task SessionDisposal_WorksCorrectly()
    {
        // Arrange
        var sessionId = 1L;
        var person = MessageTestHelper.CreateTestPerson();
        
        var dbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = sessionId,
            Title = "Test Session",
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _mockSessionRepository.Setup(sr => sr.GetSessionAsync(sessionId))
            .ReturnsAsync(dbSession);

        var session = await _sessionService.OpenSession(sessionId);

        // Act & Assert - Should dispose without throwing
        var act = () => session.Dispose();
        act.Should().NotThrow("Session should dispose cleanly");
    }

    [Fact]
    public async Task CreateSession_WithoutParentSession_DoesNotCreateSubsessionMessage()
    {
        // Arrange
        var person = MessageTestHelper.CreateTestPerson();

        var dbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = 1,
            Title = person.Name,
            Description = string.Empty,
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            ParentSessId = null
        };

        _mockSessionRepository.Setup(sr => sr.CreateSessionAsync(It.IsAny<DaoStudio.DBStorage.Models.Session>()))
            .ReturnsAsync(dbSession);

        // Act
        var session = await _sessionService.CreateSession(person, parentSessionId: null);

        // Assert
        session.Should().NotBeNull();
        session.ParentSessionId.Should().BeNull();
        
        // Verify that no information message was created (no parent session)
        _mockMessageService.Verify(ms => ms.CreateMessageAsync(
            It.IsAny<string>(),
            It.IsAny<MessageRole>(),
            It.Is<MessageType>(t => t == MessageType.Information),
            It.IsAny<long?>(),
            It.IsAny<bool>(),
            It.IsAny<long>(),
            It.IsAny<long>()), Times.Never, "No subsession message should be created for non-child sessions");
    }

    [Fact]
    public async Task Session_SupportsOnMessageChangedEventSubscription()
    {
        // Arrange
        var sessionId = 1L;
        var person = MessageTestHelper.CreateTestPerson();
        
        var dbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = sessionId,
            Title = "Test Session",
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _mockSessionRepository.Setup(sr => sr.GetSessionAsync(sessionId))
            .ReturnsAsync(dbSession);

        var session = await _sessionService.OpenSession(sessionId);
        var eventFired = false;

        // Act - Subscribe to the event
        var act = () =>
        {
            session.OnMessageChanged += (sender, args) =>
            {
                eventFired = true;
            };
        };

        // Assert - Event subscription should work without throwing
        act.Should().NotThrow("OnMessageChanged event should be subscribable");
        eventFired.Should().BeFalse("Event should not fire without trigger");
    }

    [Fact]
    public async Task SessionService_CreatesSessionsWithCorrectConfiguration()
    {
        // Arrange
        var person = MessageTestHelper.CreateTestPerson();

        var dbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = 1,
            Title = person.Name,
            Description = string.Empty,
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _mockSessionRepository.Setup(sr => sr.CreateSessionAsync(It.IsAny<DaoStudio.DBStorage.Models.Session>()))
            .ReturnsAsync(dbSession);

        // Act
        var session = await _sessionService.CreateSession(person);

        // Assert
        session.Should().NotBeNull();
        session.CurrentPerson.Should().Be(person);
        session.Id.Should().Be(1);
        session.Title.Should().Be(person.Name);
        
        // Verify the session was created in the repository
        _mockSessionRepository.Verify(sr => sr.CreateSessionAsync(
            It.Is<DaoStudio.DBStorage.Models.Session>(s => 
                s.Title == person.Name &&
                s.PersonNames.Contains(person.Name))), Times.Once);
    }

    [Fact]
    public async Task CreateSubsession_FiresOnMessageChangedEventOnParentSession()
    {
        // Arrange
        var parentSessionId = 1L;
        var person = MessageTestHelper.CreateTestPerson();
        
        var parentDbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = parentSessionId,
            Title = "Parent Session",
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        var childDbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = 2,
            Title = person.Name,
            Description = string.Empty,
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            ParentSessId = parentSessionId
        };

        _mockSessionRepository.Setup(sr => sr.GetSessionAsync(parentSessionId))
            .ReturnsAsync(parentDbSession);
        
        _mockSessionRepository.Setup(sr => sr.CreateSessionAsync(It.IsAny<DaoStudio.DBStorage.Models.Session>()))
            .ReturnsAsync(childDbSession);

        var subsessionMessage = MessageTestHelper.CreateTestMessage(
            sessionId: parentSessionId,
            role: MessageRole.User,
            type: MessageType.Information);

        _mockMessageService.Setup(ms => ms.CreateMessageAsync(
            It.IsAny<string>(),
            It.IsAny<MessageRole>(),
            It.IsAny<MessageType>(),
            It.IsAny<long?>(),
            It.IsAny<bool>(),
            It.IsAny<long>(),
            It.IsAny<long>()))
            .ReturnsAsync(subsessionMessage);

        _mockMessageService.Setup(ms => ms.SaveMessageAsync(It.IsAny<IMessage>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

        // Open parent session BEFORE creating child session
        var parentSession = await _sessionService.OpenSession(parentSessionId);

        var messageChangedFired = false;
        IMessage? capturedMessage = null;
        MessageChangeType? capturedChangeType = null;

        parentSession.OnMessageChanged += (sender, args) =>
        {
            messageChangedFired = true;
            capturedMessage = args.Message;
            capturedChangeType = args.Change;
        };

        // Act - Create child session
        var childSession = await _sessionService.CreateSession(person, parentSessionId);

        // Give a small delay for async event handlers to fire
        await Task.Delay(100);

        // Assert
        childSession.Should().NotBeNull();
        childSession.ParentSessionId.Should().Be(parentSessionId);
        
        messageChangedFired.Should().BeTrue("OnMessageChanged should be fired on parent session when subsession is created");
        capturedMessage.Should().NotBeNull("Message should be captured");
        capturedMessage!.SessionId.Should().Be(parentSessionId, "Message should belong to parent session");
        capturedMessage.Type.Should().Be(MessageType.Information, "Message should be of type Information");
        capturedChangeType.Should().Be(MessageChangeType.New, "Change type should be New");
    }

    public void Dispose()
    {
        _sessionService?.Dispose();
    }
}
