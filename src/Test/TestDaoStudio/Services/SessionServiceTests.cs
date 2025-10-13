using DaoStudio.Interfaces;
using DaoStudio.Services;
using DaoStudio.DBStorage.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Helpers;
using TestDaoStudio.Mocks;

namespace TestDaoStudio.Services;

/// <summary>
/// Unit tests for SessionService class.
/// Tests CRUD operations and session management functionality.
/// </summary>
public class SessionServiceTests : IDisposable
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

    public SessionServiceTests()
    {
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockMessageService = new Mock<IMessageService>();
        _mockToolService = new Mock<IToolService>();
        _mockPersonRepository = new Mock<IPersonRepository>();
        _mockPeopleService = new Mock<IPeopleService>();
        // Ensure peopleService returns a valid person when requested to avoid null lookups in SessionService.OpenSession
        _mockPeopleService.Setup(ps => ps.GetPersonAsync(It.IsAny<string>()))
                          .ReturnsAsync((string name) => MessageTestHelper.CreateTestPerson(name));
        _mockPluginService = new Mock<IPluginService>();
        _mockEngineService = new Mock<IEngineService>();
        _mockLogger = new Mock<ILogger<SessionService>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();

        // Ensure logger factory creates a valid logger for any category to avoid null loggers
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
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act & Assert
        _sessionService.Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(IMessageService))]
    [InlineData(nameof(ISessionRepository))]
    [InlineData(nameof(IToolService))]
    [InlineData(nameof(IPersonRepository))]
    [InlineData(nameof(IPeopleService))]
    [InlineData(nameof(IPluginService))]
    [InlineData(nameof(IEngineService))]
    [InlineData(nameof(ILogger<SessionService>))]
    public void Constructor_WithNullParameters_ThrowsArgumentNullException(string parameterName)
    {
        // Arrange & Act & Assert
        Action act = parameterName switch
        {
            nameof(IMessageService) => () => new SessionService(null!, _mockSessionRepository.Object, _mockToolService.Object, _mockPersonRepository.Object, _mockPeopleService.Object, _mockPluginService.Object, _mockEngineService.Object, _mockLogger.Object, _mockLoggerFactory.Object),
            nameof(ISessionRepository) => () => new SessionService(_mockMessageService.Object, null!, _mockToolService.Object, _mockPersonRepository.Object, _mockPeopleService.Object, _mockPluginService.Object, _mockEngineService.Object, _mockLogger.Object, _mockLoggerFactory.Object),
            nameof(IToolService) => () => new SessionService(_mockMessageService.Object, _mockSessionRepository.Object, null!, _mockPersonRepository.Object, _mockPeopleService.Object, _mockPluginService.Object, _mockEngineService.Object, _mockLogger.Object, _mockLoggerFactory.Object),
            nameof(IPersonRepository) => () => new SessionService(_mockMessageService.Object, _mockSessionRepository.Object, _mockToolService.Object, null!, _mockPeopleService.Object, _mockPluginService.Object, _mockEngineService.Object, _mockLogger.Object, _mockLoggerFactory.Object),
            nameof(IPeopleService) => () => new SessionService(_mockMessageService.Object, _mockSessionRepository.Object, _mockToolService.Object, _mockPersonRepository.Object, null!, _mockPluginService.Object, _mockEngineService.Object, _mockLogger.Object, _mockLoggerFactory.Object),
            nameof(IPluginService) => () => new SessionService(_mockMessageService.Object, _mockSessionRepository.Object, _mockToolService.Object, _mockPersonRepository.Object, _mockPeopleService.Object, null!, _mockEngineService.Object, _mockLogger.Object, _mockLoggerFactory.Object),
            nameof(IEngineService) => () => new SessionService(_mockMessageService.Object, _mockSessionRepository.Object, _mockToolService.Object, _mockPersonRepository.Object, _mockPeopleService.Object, _mockPluginService.Object, null!, _mockLogger.Object, _mockLoggerFactory.Object),
            nameof(ILogger<SessionService>) => () => new SessionService(_mockMessageService.Object, _mockSessionRepository.Object, _mockToolService.Object, _mockPersonRepository.Object, _mockPeopleService.Object, _mockPluginService.Object, _mockEngineService.Object, null!, _mockLoggerFactory.Object),
            _ => throw new ArgumentException("Invalid parameter name", nameof(parameterName))
        };

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateSession_CreatesNewSessionWithCorrectProperties()
    {
        // Arrange
        var person = MessageTestHelper.CreateTestPerson();

        var expectedDbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = 1,
            Title = person.Name,
            Description = string.Empty,
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _mockSessionRepository.Setup(sr => sr.CreateSessionAsync(It.IsAny<DaoStudio.DBStorage.Models.Session>()))
                     .ReturnsAsync(expectedDbSession);

        // Act
        var session = await _sessionService.CreateSession(person);

        // Assert
        session.Should().NotBeNull();
        session.Title.Should().Be(person.Name);
        session.Description.Should().Be(string.Empty);
        session.CurrentPerson.Should().Be(person);

        _mockSessionRepository.Verify(sr => sr.CreateSessionAsync(It.Is<DaoStudio.DBStorage.Models.Session>(s =>
                s.Title == person.Name &&
                s.Description == string.Empty &&
                s.PersonNames.Contains(person.Name)
            )), Times.Once);
    }

    [Fact]
    public async Task GetSessionAsync_WithValidId_ReturnsExistingSession()
    {
        // Arrange
        var sessionId = 123L;
        var person = MessageTestHelper.CreateTestPerson();

        var dbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = sessionId,
            Title = "Existing Session",
            Description = "An existing session",
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _mockSessionRepository.Setup(sr => sr.GetSessionAsync(sessionId))
                         .ReturnsAsync(dbSession);

        // Act
        var session = await _sessionService.OpenSession(sessionId);

        // Assert
        session.Should().NotBeNull();
        session!.Id.Should().Be(sessionId);
        session.Title.Should().Be("Existing Session");

        _mockSessionRepository.Verify(sr => sr.GetSessionAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task GetSessionAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        var invalidSessionId = 999L;

        _mockSessionRepository.Setup(sr => sr.GetSessionAsync(invalidSessionId))
                             .ReturnsAsync((DaoStudio.DBStorage.Models.Session?)null);

        // Act
        var act = async () => await _sessionService.OpenSession(invalidSessionId);

        // Assert
        await act.Should().ThrowAsync<Exception>()
                  .WithMessage($"Session with ID {invalidSessionId} not found");
        _mockSessionRepository.Verify(sr => sr.GetSessionAsync(invalidSessionId), Times.Once);
    }

    [Fact]
    public async Task GetAllSessionsAsync_ReturnsAllSessions()
    {
        // Arrange
        var person1 = MessageTestHelper.CreateTestPerson("Person 1");
        var person2 = MessageTestHelper.CreateTestPerson("Person 2");

        var dbSessions = new List<DaoStudio.DBStorage.Models.Session>
        {
            new() { Id = 1, Title = "Session 1", PersonNames = new List<string> { person1.Name } },
            new() { Id = 2, Title = "Session 2", PersonNames = new List<string> { person2.Name } }
        };

        _mockSessionRepository.Setup(sr => sr.GetAllSessionsAsync(It.IsAny<DaoStudio.DBStorage.Models.SessionInclusionOptions>()))
                     .ReturnsAsync(dbSessions);

        // Act
        var sessions = await _sessionService.GetAllSessionsAsync();

        // Assert
        sessions.Should().HaveCount(2);
        sessions.Should().Contain(s => s.Id == 1 && s.Title == "Session 1");
        sessions.Should().Contain(s => s.Id == 2 && s.Title == "Session 2");

        _mockSessionRepository.Verify(sr => sr.GetAllSessionsAsync(It.IsAny<DaoStudio.DBStorage.Models.SessionInclusionOptions>()), Times.Once);
    }

    [Fact]
    public async Task DeleteSessionAsync_WithValidId_RemovesSessionFromCache()
    {
        // Arrange
        var sessionId = 123L;
        var person = MessageTestHelper.CreateTestPerson();

        var dbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = sessionId,
            Title = "Session to Delete",
            PersonNames = new List<string> { person.Name }
        };

        _mockSessionRepository.Setup(sr => sr.GetSessionAsync(sessionId))
                     .ReturnsAsync(dbSession);

        // First get the session to populate cache
        await _sessionService.OpenSession(sessionId);

        // Act
        await _sessionService.DeleteSessionAsync(sessionId);

        // Assert
        _mockSessionRepository.Verify(sr => sr.DeleteSessionAsync(sessionId), Times.Once);
        _mockMessageService.Verify(ms => ms.DeleteMessagesBySessionIdAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task CreateSession_WithNullPerson_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _sessionService.CreateSession(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("person");
    }

    [Fact]
    public async Task CreateSession_WithValidPerson_SetsPropertiesCorrectly()
    {
        // Arrange
        var person = MessageTestHelper.CreateTestPerson();

        var expectedDbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = 1,
            Title = person.Name,
            Description = string.Empty,
            PersonNames = new List<string> { person.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _mockSessionRepository.Setup(sr => sr.CreateSessionAsync(It.IsAny<DaoStudio.DBStorage.Models.Session>()))
                     .ReturnsAsync(expectedDbSession);

        // Act
        var session = await _sessionService.CreateSession(person);

        // Assert
        session.Should().NotBeNull();
        session.Title.Should().Be(person.Name);
    }

    [Fact]
    public async Task CreateSession_SetsTimestampsCorrectly()
    {
        // Arrange
        var person = MessageTestHelper.CreateTestPerson();
        var beforeCreation = DateTime.UtcNow;

        var capturedDbSession = new DaoStudio.DBStorage.Models.Session();
        _mockSessionRepository.Setup(sr => sr.CreateSessionAsync(It.IsAny<DaoStudio.DBStorage.Models.Session>()))
                     .Callback<DaoStudio.DBStorage.Models.Session>(s =>
                                 {
                                     capturedDbSession = s;
                                     s.Id = 1; // Simulate database assigning an ID
                                 })
                                 .ReturnsAsync((DaoStudio.DBStorage.Models.Session s) => s);

        // Act
        await _sessionService.CreateSession(person);
        var afterCreation = DateTime.UtcNow;

        // Assert
        capturedDbSession.CreatedAt.Should().BeOnOrAfter(beforeCreation);
        capturedDbSession.CreatedAt.Should().BeOnOrBefore(afterCreation);
        capturedDbSession.LastModified.Should().BeOnOrAfter(beforeCreation);
        capturedDbSession.LastModified.Should().BeOnOrBefore(afterCreation);
    }


    [Fact]
    public void Dispose_DisposesResourcesProperly()
    {
        // Act & Assert
        var act = () => _sessionService.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SessionService_HandlesRepositoryExceptions_Gracefully()
    {
        // Arrange
        var sessionId = 123L;
        _mockSessionRepository.Setup(sr => sr.GetSessionAsync(sessionId))
                     .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        var act = async () => await _sessionService.OpenSession(sessionId);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Database error");
    }

    public void Dispose()
    {
        _sessionService?.Dispose();
    }
}
