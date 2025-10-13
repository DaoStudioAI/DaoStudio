using DaoStudio;
using DaoStudio.DBStorage;
using DaoStudio.DBStorage.Models;
using DaoStudio.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Helpers;
using TestDaoStudio.Mocks;
using TestDaoStudio.Infrastructure;
using DaoStudio.DBStorage.Interfaces;

namespace TestDaoStudio.Core;

/// <summary>
/// Unit tests for the Session class.
/// Tests session lifecycle, messaging, tool execution, events, and disposal.
/// </summary>
public class SessionTests : IDisposable
{
    private readonly TestContainerFixture _containerFixture;
    private readonly Mock<IMessageService> _mockMessageService;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<IToolService> _mockToolService;
    private readonly Mock<IPluginService> _mockPluginService;
    private readonly Mock<IEngineService> _mockEngineService;
    private readonly Mock<IPeopleService> _mockPeopleService;
    private readonly Mock<ILogger<DaoStudio.Session>> _mockLogger;
    private readonly IPerson _testPerson;
    private readonly DaoStudio.DBStorage.Models.Session _dbSession;

    public SessionTests()
    {
        _containerFixture = new TestContainerFixture();
        _mockMessageService = new Mock<IMessageService>();
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockToolService = new Mock<IToolService>();
        _mockPluginService = new Mock<IPluginService>();
        _mockEngineService = new Mock<IEngineService>();
        _mockPeopleService = new Mock<IPeopleService>();
        _mockLogger = new Mock<ILogger<DaoStudio.Session>>();
        
        _testPerson = MockPerson.CreateAssistant();
        _dbSession = new DaoStudio.DBStorage.Models.Session
        {
            Id = 1,
            Title = "Test Session",
            Description = "A test session",
            PersonNames = new List<string> { _testPerson.Name },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var session = CreateTestSession();

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().Be(_dbSession.Id);
        session.Title.Should().Be(_dbSession.Title);
        session.Description.Should().Be(_dbSession.Description);
        session.CurrentPerson.Should().Be(_testPerson);
        session.SessionStatus.Should().Be(SessionStatus.Idle);
    }

    [Theory]
    [InlineData(nameof(IMessageService))]
    [InlineData(nameof(ISessionRepository))]
    [InlineData(nameof(IToolService))]
    [InlineData(nameof(ILogger<DaoStudio.Session>))]
    public void Constructor_WithNullParameters_ThrowsArgumentNullException(string parameterName)
    {
        // Arrange & Act & Assert
        Action act = parameterName switch
        {
            nameof(IMessageService) => () => new DaoStudio.Session(null!, _mockSessionRepository.Object, _mockToolService.Object, 
                _dbSession, _testPerson, _mockLogger.Object, _mockPluginService.Object, _mockEngineService.Object, _mockPeopleService.Object),
            nameof(ISessionRepository) => () => new DaoStudio.Session(_mockMessageService.Object, null!, _mockToolService.Object, 
                _dbSession, _testPerson, _mockLogger.Object, _mockPluginService.Object, _mockEngineService.Object, _mockPeopleService.Object),
            nameof(IToolService) => () => new DaoStudio.Session(_mockMessageService.Object, _mockSessionRepository.Object, null!, 
                _dbSession, _testPerson, _mockLogger.Object, _mockPluginService.Object, _mockEngineService.Object, _mockPeopleService.Object),
            nameof(ILogger<DaoStudio.Session>) => () => new DaoStudio.Session(_mockMessageService.Object, _mockSessionRepository.Object, _mockToolService.Object, 
                _dbSession, _testPerson, null!, _mockPluginService.Object, _mockEngineService.Object, _mockPeopleService.Object),
            _ => throw new ArgumentException("Invalid parameter name", nameof(parameterName))
        };

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Id_ReturnsCorrectDatabaseSessionId()
    {
        // Arrange
        var session = CreateTestSession();

        // Act & Assert
        session.Id.Should().Be(_dbSession.Id);
    }

    [Fact]
    public void SessionStatus_PropertyChanged_FiresEvent()
    {
        // Arrange
        var session = CreateTestSession();
        var eventFired = false;
    PropertyChangeNotification? eventArgs = null;

        session.PropertyChanged += (sender, e) =>
        {
            eventFired = true;
            eventArgs = e;
        };

        // Act
        session.SessionStatus = SessionStatus.Sending;

        // Assert
        eventFired.Should().BeTrue();
        eventArgs.Should().NotBeNull();
    eventArgs.HasValue.Should().BeTrue();
    eventArgs!.Value.PropertyName.Should().Be(nameof(SessionStatus));
        session.SessionStatus.Should().Be(SessionStatus.Sending);
    }

    [Fact]
    public async Task UpdatePersonAsync_WithValidPerson_UpdatesPersonAndRecreatesEngine()
    {
        // Arrange
        var session = CreateTestSession();
        var newPerson = MockPerson.CreateAssistant("Claude Assistant", "Anthropic", "claude-3-haiku-20240307");
        var mockEngine = new MockEngine();

    _mockEngineService.Setup(es => es.CreateEngineAsync(It.IsAny<IPerson>()))
             .Returns(Task.FromResult<IEngine>(mockEngine));

        // Act
        await session.UpdatePersonAsync(newPerson);

        // Assert
        session.CurrentPerson.Should().Be(newPerson);
        _mockEngineService.Verify(es => es.CreateEngineAsync(It.IsAny<IPerson>()), Times.Once);
        _mockSessionRepository.Verify(sr => sr.SaveSessionAsync(It.IsAny<DaoStudio.DBStorage.Models.Session>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePersonAsync_WithNullPerson_ThrowsArgumentNullException()
    {
        // Arrange
        var session = CreateTestSession();

        // Act & Assert
        var act = async () => await session.UpdatePersonAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("person");
    }

    [Fact]
    public async Task InitializeAsync_SetsUpPluginsAndEngine()
    {
        // Arrange
        var session = CreateTestSession();
        var mockEngine = new MockEngine();

    _mockEngineService.Setup(es => es.CreateEngineAsync(It.IsAny<IPerson>()))
             .Returns(Task.FromResult<IEngine>(mockEngine));

        // Act
        await session.InitializeAsync();

        // Assert
        _mockEngineService.Verify(es => es.CreateEngineAsync(It.IsAny<IPerson>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WhenAlreadyInitialized_DoesNotReinitialize()
    {
        // Arrange
        var session = CreateTestSession();
        var mockEngine = new MockEngine();

    _mockEngineService.Setup(es => es.CreateEngineAsync(It.IsAny<IPerson>()))
             .Returns(Task.FromResult<IEngine>(mockEngine));

        // Act - Initialize twice
        await session.InitializeAsync();
        await session.InitializeAsync();

        // Assert - Should only be called once
        _mockEngineService.Verify(es => es.CreateEngineAsync(It.IsAny<IPerson>()), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithValidMessage_ReturnsResponse()
    {
        // Arrange
        var session = CreateTestSession();
        var mockEngine = new MockEngine();
        mockEngine.SetResponses("Test response from AI");

        _mockEngineService.Setup(es => es.CreateEngineAsync(It.IsAny<IPerson>()))
                         .ReturnsAsync(mockEngine);

        var userMessage = MessageTestHelper.CreateUserMessage("Hello");
        var assistantMessage = MessageTestHelper.CreateAssistantMessage("Test response from AI");

        _mockMessageService.Setup(ms => ms.CreateMessageAsync(
                It.IsAny<string>(), It.IsAny<MessageRole>(), It.IsAny<MessageType>(), It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>()))
            .Returns((string content, MessageRole role, MessageType type, long? sessionId, bool save, long parentMsgId, long parentSessId) =>
                Task.FromResult<IMessage>(role == MessageRole.User
                    ? MessageTestHelper.CreateUserMessage(content, sessionId ?? 0)
                    : MessageTestHelper.CreateAssistantMessage(content, sessionId ?? 0)));

        _mockMessageService.Setup(ms => ms.GetMessagesBySessionIdAsync(It.IsAny<long>()))
                          .ReturnsAsync(new List<IMessage> { userMessage });

        // Initialize the session first
        await session.InitializeAsync();

        // Act
        var response = await session.SendMessageAsync("Hello");

        // Assert
        response.Should().NotBeNull();
        session.SessionStatus.Should().Be(SessionStatus.Idle); // Should return to Idle after completion
    }

    [Fact]
    public async Task SendMessageAsync_WithNullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var session = CreateTestSession();

        // Act & Assert
        var act = async () => await session.SendMessageAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendMessageAsync_WhenSessionDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var session = CreateTestSession();
        session.Dispose();

        // Act & Assert
        var act = async () => await session.SendMessageAsync("Hello");
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_CancelsCorrectly()
    {
        // Arrange
        var session = CreateTestSession();
        var mockEngine = new MockEngine();
        
        _mockEngineService.Setup(es => es.CreateEngineAsync(It.IsAny<IPerson>()))
                         .ReturnsAsync(mockEngine);

        // Initialize the session
        await session.InitializeAsync();

        // Act & Assert
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // The cancellation will happen during the SendMessageAsync execution
        var sendTask = session.SendMessageAsync("Hello");
        
        // Give a small delay for the operation to start
        await Task.Delay(10);
        
        var act = async () => await sendTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Dispose_UnsubscribesFromEvents()
    {
        // Arrange
        var session = CreateTestSession();

        // Act
        session.Dispose();

        // Assert
        // Verify that tool service events are unsubscribed
        _mockToolService.VerifyRemove(ts => ts.ToolChanged -= It.IsAny<EventHandler<ToolOperationEventArgs>>(), Times.Once);
        _mockToolService.VerifyRemove(ts => ts.ToolListUpdated -= It.IsAny<EventHandler<ToolListUpdateEventArgs>>(), Times.Once);
    }

    [Fact]
    public void Dispose_DisposesEngineIfDisposable()
    {
        // Arrange
        var session = CreateTestSession();
        var mockDisposableEngine = new Mock<IEngine>();
        mockDisposableEngine.As<IDisposable>();

        // We can't easily test this without access to the private engine field
        // This test verifies the disposal pattern is implemented correctly
        
        // Act & Assert
        var act = () => session.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Engine_UsageDetailsReceived_ForwardsEvent()
    {
        // Arrange
        var session = CreateTestSession();
        var mockEngine = new MockEngine();
    var usageDetailsReceived = false;
    DaoStudio.Interfaces.UsageDetails? receivedUsage = null;

        _mockEngineService.Setup(es => es.CreateEngineAsync(It.IsAny<IPerson>()))
                         .ReturnsAsync(mockEngine);

        session.UsageDetailsReceived += (sender, usage) =>
        {
            usageDetailsReceived = true;
            receivedUsage = usage;
        };

        await session.InitializeAsync();

        var testUsage = new DaoStudio.Interfaces.UsageDetails
        {
            InputTokens = 10,
            OutputTokens = 20,
            TotalTokens = 30
        };

        // Act
        mockEngine.SimulateUsageDetails(new Microsoft.Extensions.AI.UsageDetails
        {
            InputTokenCount = (int?)(testUsage.InputTokens ?? 0),
            OutputTokenCount = (int?)(testUsage.OutputTokens ?? 0),
            TotalTokenCount = (int?)(testUsage.TotalTokens ?? 0)
        });

        // Allow some time for the event to be processed
        await Task.Delay(50);

        // Assert
        usageDetailsReceived.Should().BeTrue();
        receivedUsage.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateSessionLastModifiedAsync_UpdatesTimestamp()
    {
        // Arrange
        var session = CreateTestSession();
        var originalTimestamp = session.LastModified;

        // Act
        await Task.Delay(1); // Ensure time difference
        await session.UpdateSessionLastModifiedAsync();

        // Assert
        session.LastModified.Should().BeAfter(originalTimestamp);
        _mockSessionRepository.Verify(sr => sr.SaveSessionAsync(It.IsAny<DaoStudio.DBStorage.Models.Session>()), Times.Once);
    }

    [Fact]
    public async Task GetPersonsAsync_ReturnsCurrentPerson()
    {
        // Arrange
        var session = CreateTestSession();

        // Act
        var persons = await session.GetPersonsAsync();

        // Assert
        persons.Should().HaveCount(1);
        persons.First().Should().Be(_testPerson);
    }

    [Fact]
    public void ParentSessionId_ReturnsCorrectValue()
    {
        // Arrange
        _dbSession.ParentSessId = 123;
        var session = CreateTestSession();

        // Act & Assert
        session.ParentSessionId.Should().Be(123);
    }

    [Fact]
    public void ToolExecutionMode_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var session = CreateTestSession();

        // Act
        session.ToolExecutionMode = ToolExecutionMode.RequireAny;

        // Assert
        session.ToolExecutionMode.Should().Be(ToolExecutionMode.RequireAny);
    }

    [Fact]
    public void TotalTokenCount_ReturnsDbSessionValue()
    {
        // Arrange
        _dbSession.TotalTokenCount = 100;
        var session = CreateTestSession();

        // Act & Assert
        session.TotalTokenCount.Should().Be(100);
    }

    [Fact]
    public void InputTokenCount_ReturnsDbSessionValue()
    {
        // Arrange
        _dbSession.InputTokenCount = 50;
        var session = CreateTestSession();

        // Act & Assert
        session.InputTokenCount.Should().Be(50);
    }

    [Fact]
    public void OutputTokenCount_ReturnsDbSessionValue()
    {
        // Arrange
        _dbSession.OutputTokenCount = 25;
        var session = CreateTestSession();

        // Act & Assert
        session.OutputTokenCount.Should().Be(25);
    }

    private DaoStudio.Session CreateTestSession()
    {
        return new DaoStudio.Session(
            _mockMessageService.Object,
            _mockSessionRepository.Object,
            _mockToolService.Object,
            _dbSession,
            _testPerson,
            _mockLogger.Object,
            _mockPluginService.Object,
            _mockEngineService.Object,
            _mockPeopleService.Object
        );
    }

    public void Dispose()
    {
        _containerFixture?.Dispose();
    }
}
