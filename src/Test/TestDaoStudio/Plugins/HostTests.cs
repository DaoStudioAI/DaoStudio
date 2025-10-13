using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Plugins;
using DryIoc;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Mocks;
using TestDaoStudio.Helpers;

namespace TestDaoStudio.Plugins;

/// <summary>
/// Unit tests for the Host class.
/// Tests plugin host functionality including session creation and management.
/// </summary>
public class HostTests : IDisposable
{
    private readonly Mock<IPeopleService> _mockPeopleService;
    private readonly Lazy<ISessionService> _mockSessionServiceLazy;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Mock<IMessageService> _mockMessageService;
    private readonly Mock<ILogger<Host>> _mockLogger;
    private readonly Mock<IApplicationPathsService> _mockApplicationPathsService;
    private readonly Container _container;
    private readonly Host _host;

    public HostTests()
    {
        _mockPeopleService = new Mock<IPeopleService>();
        _mockSessionService = new Mock<ISessionService>();
        _mockSessionServiceLazy = new Lazy<ISessionService>(() => _mockSessionService.Object);
        _mockMessageService = new Mock<IMessageService>();
        _mockLogger = new Mock<ILogger<Host>>();
        _mockApplicationPathsService = new Mock<IApplicationPathsService>();
        _container = new Container();

        // Setup container to resolve logger for HostSessionAdapter
        _container.RegisterDelegate<ILogger<HostSessionAdapter>>(
            r => Mock.Of<ILogger<HostSessionAdapter>>(), 
            Reuse.Transient);

        _host = new Host(
            _mockPeopleService.Object,
            _mockSessionServiceLazy,
            _mockMessageService.Object,
            _mockLogger.Object,
            _container,
            _mockApplicationPathsService.Object);
    }

    public void Dispose()
    {
        _container?.Dispose();
    }

    /// <summary>
    /// Test that demonstrates the bug: when StartNewHostSessionAsync is called with a parent session,
    /// it ignores the parent parameter and passes null to StartNewSession, which prevents the
    /// creation of a SubsessionId message that would provide navigation to the child session.
    /// 
    /// Expected behavior: The parent session ID should be extracted from the parent parameter
    /// and passed to StartNewSession so a SubsessionId message is created.
    /// 
    /// Actual behavior: The parent parameter is ignored, null is passed to StartNewSession,
    /// and no SubsessionId message is created.
    /// </summary>
    [Fact]
    public async Task StartNewHostSessionAsync_WithParentSession_PassesParentIdToStartNewSession()
    {
        // Arrange
        const long parentSessionId = 123;
        const string personName = "TestPerson";

        // Create a mock parent session
        var mockParentSession = new Mock<IHostSession>();
        mockParentSession.Setup(s => s.Id).Returns(parentSessionId);

        // Create a mock child session that would be returned by StartNewSession
        var mockChildSession = new Mock<ISession>();
        mockChildSession.Setup(s => s.Id).Returns(456);

        // Setup the session service to return our mock child session
        // Note: The bug is that null is passed instead of parentSessionId
        _mockSessionService
            .Setup(s => s.StartNewSession(null, personName)) // This shows the bug - null instead of parentSessionId
            .ReturnsAsync(mockChildSession.Object);

        // Track calls to verify the incorrect behavior
        var startNewSessionCalls = new List<(long? parentSessId, string? personName)>();
        _mockSessionService
            .Setup(s => s.StartNewSession(It.IsAny<long?>(), It.IsAny<string?>()))
            .Callback<long?, string?>((parentSessId, personName) => 
                startNewSessionCalls.Add((parentSessId, personName)))
            .ReturnsAsync(mockChildSession.Object);

        // Act
        var result = await _host.StartNewHostSessionAsync(mockParentSession.Object, personName);

        // Assert
        result.Should().NotBeNull();
        
    // Verify that StartNewSession was called with the parent session ID (bug fixed)
    startNewSessionCalls.Should().HaveCount(1);
    startNewSessionCalls[0].parentSessId.Should().Be(parentSessionId);
    startNewSessionCalls[0].personName.Should().Be(personName);
    }

    /// <summary>
    /// Test that demonstrates what the correct behavior should be.
    /// This test would pass if the bug were fixed.
    /// </summary>
    [Fact]
    public async Task StartNewHostSessionAsync_WithParentSession_CreatesSubsessionIdMessagePath()
    {
        // Arrange
        const long parentSessionId = 123;
        const string personName = "TestPerson";

        // Create a mock parent session
        var mockParentSession = new Mock<IHostSession>();
        mockParentSession.Setup(s => s.Id).Returns(parentSessionId);

        // Create a mock child session that would be returned by StartNewSession
        var mockChildSession = new Mock<ISession>();
        mockChildSession.Setup(s => s.Id).Returns(456);

        // Track calls to verify what should be the correct behavior
        var startNewSessionCalls = new List<(long? parentSessId, string? personName)>();
        _mockSessionService
            .Setup(s => s.StartNewSession(It.IsAny<long?>(), It.IsAny<string?>()))
            .Callback<long?, string?>((parentSessId, personName) => 
                startNewSessionCalls.Add((parentSessId, personName)))
            .ReturnsAsync(mockChildSession.Object);

        // Act
        var result = await _host.StartNewHostSessionAsync(mockParentSession.Object, personName);

        // Assert
        result.Should().NotBeNull();
        
        // Now that bug is fixed, ensure parent ID passed
        startNewSessionCalls.Should().HaveCount(1);
        startNewSessionCalls[0].parentSessId.Should().Be(parentSessionId);
        startNewSessionCalls[0].personName.Should().Be(personName);
    }

    /// <summary>
    /// Test that verifies behavior when no parent session is provided.
    /// This should work correctly (and currently does).
    /// </summary>
    [Fact]
    public async Task StartNewHostSessionAsync_WithNullParent_PassesNullToStartNewSession()
    {
        // Arrange
        const string personName = "TestPerson";

        // Create a mock child session that would be returned by StartNewSession
        var mockChildSession = new Mock<ISession>();
        mockChildSession.Setup(s => s.Id).Returns(456);

        // Track calls to verify the behavior
        var startNewSessionCalls = new List<(long? parentSessId, string? personName)>();
        _mockSessionService
            .Setup(s => s.StartNewSession(It.IsAny<long?>(), It.IsAny<string?>()))
            .Callback<long?, string?>((parentSessId, personName) => 
                startNewSessionCalls.Add((parentSessId, personName)))
            .ReturnsAsync(mockChildSession.Object);

        // Act
        var result = await _host.StartNewHostSessionAsync(null, personName);

        // Assert
        result.Should().NotBeNull();
        
        // When no parent is provided, passing null to StartNewSession is correct
        startNewSessionCalls.Should().HaveCount(1);
        startNewSessionCalls[0].parentSessId.Should().BeNull(); // This is correct for null parent
        startNewSessionCalls[0].personName.Should().Be(personName);
    }

    /// <summary>
    /// Verifies that when a parent session is provided to Host.StartNewHostSessionAsync, the current buggy
    /// implementation does NOT create a SubsessionId message on the parent session because it incorrectly
    /// passes null to ISessionService.StartNewSession. This documents the regression so that when the
    /// implementation is fixed, this test can be updated to assert the opposite (that the message IS created).
    /// </summary>
    [Fact]
    public async Task StartNewHostSessionAsync_WithParentSession_PassesParentId_AndSubsessionMessageWouldBeCreatedInRealService()
    {
        // Arrange
        const long parentSessionId = 555;
        const string personName = "AnotherPerson";

        var mockParentHostSession = new Mock<IHostSession>();
        mockParentHostSession.Setup(s => s.Id).Returns(parentSessionId);

        // Child session returned
        var mockChildSession = new Mock<ISession>();
        mockChildSession.Setup(s => s.Id).Returns(777);

        // Capture StartNewSession calls
        var startNewSessionCalls = new List<(long? parentSessId, string? personName)>();
        _mockSessionService
            .Setup(s => s.StartNewSession(It.IsAny<long?>(), It.IsAny<string?>()))
            .Callback<long?, string?>((pid, pname) => startNewSessionCalls.Add((pid, pname)))
            .ReturnsAsync(mockChildSession.Object);

        // We also want to ensure IMessageService.CreateMessageAsync is NOT invoked with parentSessionId
        // to create the SubsessionId info message. Since SessionService is responsible for that and is not used
        // (because parentSessId becomes null), CreateMessageAsync should never be called with sessionId = parentSessionId
        var createMessageCalls = new List<long?>();
        _mockMessageService
            .Setup(m => m.CreateMessageAsync(It.IsAny<string>(), It.IsAny<MessageRole>(), It.IsAny<MessageType>(),
                It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<long>()))
            .Callback<string, MessageRole, MessageType, long?, bool, long, long>((c, r, t, sid, save, pmid, psid) =>
            {
                createMessageCalls.Add(sid);
            })
            .ReturnsAsync(Mock.Of<IMessage>());

        // Act
        var childHostSession = await _host.StartNewHostSessionAsync(mockParentHostSession.Object, personName);

        // Assert
        childHostSession.Should().NotBeNull();
        startNewSessionCalls.Should().HaveCount(1);
    // Expect parentSessId now set
    startNewSessionCalls[0].parentSessId.Should().Be(parentSessionId);
    startNewSessionCalls[0].personName.Should().Be(personName);

    // NOTE: We mocked ISessionService.StartNewSession directly, so the real SessionService.CreateSession logic
    // (which would call IMessageService.CreateMessageAsync to create a SubsessionId message) is NOT executed here.
    // Therefore we cannot observe a CreateMessageAsync call. This test confines itself to verifying that
    // the parent session id was propagated correctly, which is the prerequisite for the SubsessionId message.
    }
}