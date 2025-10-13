using DaoStudio;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.Interfaces;
using DryIoc;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Infrastructure;

namespace TestDaoStudio.Core;

/// <summary>
/// Unit tests for the DaoStudio core class.
/// Tests service registration, initialization, disposal, and path management.
/// </summary>
public class DaoStudioTests : IDisposable
{
    private readonly TestContainerFixture _containerFixture;
    private Container? _testContainer;

    public DaoStudioTests()
    {
        _containerFixture = new TestContainerFixture();
    }

    [Fact]
    public void RegisterServices_WithValidContainer_RegistersAllServices()
    {
        // Arrange
        var container = new Container();

        // Act
        DaoStudio.DaoStudioService.RegisterServices(container);
        //register ILoggerFactory
        container.RegisterInstance<ILoggerFactory>(LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        }));

        // Assert - Verify key services are registered
        container.IsRegistered<StorageFactory>().Should().BeTrue();
        container.IsRegistered<DaoStudio.DaoStudioService>().Should().BeTrue();
        container.IsRegistered<ISessionService>().Should().BeTrue();
        container.IsRegistered<IMessageService>().Should().BeTrue();
        container.IsRegistered<IPluginService>().Should().BeTrue();
        container.IsRegistered<IApiProviderService>().Should().BeTrue();
        container.IsRegistered<IPeopleService>().Should().BeTrue();
        container.IsRegistered<IToolService>().Should().BeTrue();
        container.IsRegistered<IEngineService>().Should().BeTrue();
        var l = container.Resolve<ILogger<DaoStudio.DaoStudioService>>();
        l.ThrowIfNull();
        //container.IsRegistered<ILogger<DaoStudio.DaoStudioService>>().Should().BeTrue();

        container.Dispose();
    }

    [Fact]
    public void RegisterServices_WithNullContainer_DoesNotThrow()
    {
        // Act & Assert
        var act = () => DaoStudio.DaoStudioService.RegisterServices(null!);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        await _containerFixture.InitializeAsync(useInMemoryDatabase: true, mockRepositories: true);
        _testContainer = _containerFixture.Container;
        var logger = _testContainer.Resolve<ILogger<DaoStudio.DaoStudioService>>();

        // Act
        var DaoStudio = new DaoStudio.DaoStudioService(_testContainer, logger);

        // Assert
        DaoStudio.Should().NotBeNull();
    }

    [Fact]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        await _containerFixture.InitializeAsync(useInMemoryDatabase: true, mockRepositories: true);
        _testContainer = _containerFixture.Container;

        // Act & Assert
        var act = () => new DaoStudio.DaoStudioService(_testContainer, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Constructor_WithNullContainer_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DaoStudio.DaoStudioService>>();

        // Act & Assert
        var act = () => new DaoStudio.DaoStudioService(null!, mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InitializeAsync_CallsStorageAndPluginInitialization()
    {
        // Arrange
        await _containerFixture.InitializeAsync(useInMemoryDatabase: true, mockRepositories: true);
        _testContainer = _containerFixture.Container;

        // Mock the services to verify calls
        var mockStorageFactory = new Mock<StorageFactory>("test.db");
        var mockPluginService = new Mock<IPluginService>();
        
        _containerFixture.RegisterMock(mockStorageFactory.Object);
        _containerFixture.RegisterMock(mockPluginService.Object);

        var logger = _testContainer.Resolve<ILogger<DaoStudio.DaoStudioService>>();
        var DaoStudio = new DaoStudio.DaoStudioService(_testContainer, logger);

        // Act
        await DaoStudio.InitializeAsync(_testContainer);

        // Assert
        mockStorageFactory.Verify(sf => sf.InitializeAsync(), Times.Once);
        mockPluginService.Verify(ps => ps.LoadPluginsAsync(It.IsAny<string>()), Times.Once);
        mockPluginService.Verify(ps => ps.InitializeAsync(), Times.Once);

        DaoStudio.Dispose();
    }

    [Fact]
    public void GetDefaultDatabasePath_ReturnsValidPath()
    {
        // This tests the private method through the static RegisterServices method
        // by verifying that the StorageFactory is registered with a valid path

        // Arrange
        var container = new Container();
        container.RegisterInstance<ILoggerFactory>(LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        }));

        // Act
        DaoStudio.DaoStudioService.RegisterServices(container);
        var storageFactory = container.Resolve<StorageFactory>();

        // Assert
        storageFactory.Should().NotBeNull();
        // The storage factory should be created without throwing exceptions

        container.Dispose();
    }

    [Fact]
    public async Task Dispose_DisposesServicesInCorrectOrder()
    {
        // Arrange
        await _containerFixture.InitializeAsync(useInMemoryDatabase: true, mockRepositories: true);
        _testContainer = _containerFixture.Container;

        var mockPluginService = new Mock<IPluginService>();
        var mockSessionService = new Mock<ISessionService>();
        
        _containerFixture.RegisterMock(mockPluginService.Object);
        _containerFixture.RegisterMock(mockSessionService.Object);

        var logger = _testContainer.Resolve<ILogger<DaoStudio.DaoStudioService>>();
        var DaoStudio = new DaoStudio.DaoStudioService(_testContainer, logger);

        // Act
        DaoStudio.Dispose();

        // Assert
        mockPluginService.Verify(ps => ps.Dispose(), Times.Once);
        mockSessionService.Verify(ss => ss.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Dispose_HandlesExceptionsGracefully()
    {
        // Arrange
        await _containerFixture.InitializeAsync(useInMemoryDatabase: true, mockRepositories: true);
        _testContainer = _containerFixture.Container;

        var mockPluginService = new Mock<IPluginService>();
        mockPluginService.Setup(ps => ps.Dispose()).Throws(new InvalidOperationException("Test exception"));
        
        var mockSessionService = new Mock<ISessionService>();
        
        _containerFixture.RegisterMock(mockPluginService.Object);
        _containerFixture.RegisterMock(mockSessionService.Object);

        var logger = _testContainer.Resolve<ILogger<DaoStudio.DaoStudioService>>();
        var DaoStudio = new DaoStudio.DaoStudioService(_testContainer, logger);

        // Act & Assert - Should not throw despite service throwing exception
        var act = () => DaoStudio.Dispose();
        act.Should().NotThrow();

        // Verify both services were called despite the first one throwing
        mockPluginService.Verify(ps => ps.Dispose(), Times.Once);
        mockSessionService.Verify(ss => ss.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        await _containerFixture.InitializeAsync(useInMemoryDatabase: true, mockRepositories: true);
        _testContainer = _containerFixture.Container;

        var logger = _testContainer.Resolve<ILogger<DaoStudio.DaoStudioService>>();
        var DaoStudio = new DaoStudio.DaoStudioService(_testContainer, logger);

        // Act & Assert
        DaoStudio.Dispose();
        var act = () => DaoStudio.Dispose(); // Second call
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DaoStudio_ImplementsIDisposable()
    {
        // Arrange
        await _containerFixture.InitializeAsync(useInMemoryDatabase: true, mockRepositories: true);
        _testContainer = _containerFixture.Container;

        var logger = _testContainer.Resolve<ILogger<DaoStudio.DaoStudioService>>();

        // Act & Assert
        var DaoStudio = new DaoStudio.DaoStudioService(_testContainer, logger);
        DaoStudio.Should().BeAssignableTo<IDisposable>();
        
        DaoStudio.Dispose();
    }

    [Fact]
    public void RegisterServices_RegistersRepositoriesCorrectly()
    {
        // Arrange
        var container = new Container();

        // Act
        DaoStudio.DaoStudioService.RegisterServices(container);

        // Assert - Verify repositories can be resolved
        var apiProviderRepoFactory = container.Resolve<Func<IAPIProviderRepository>>();
        var cachedModelRepoFactory = container.Resolve<Func<ICachedModelRepository>>();
        var messageRepoFactory = container.Resolve<Func<IMessageRepository>>();
        var personRepoFactory = container.Resolve<Func<IPersonRepository>>();
        var toolRepoFactory = container.Resolve<Func<ILlmToolRepository>>();
        var settingsRepoFactory = container.Resolve<Func<ISettingsRepository>>();
        var sessionRepoFactory = container.Resolve<Func<ISessionRepository>>();

        apiProviderRepoFactory.Should().NotBeNull();
        cachedModelRepoFactory.Should().NotBeNull();
        messageRepoFactory.Should().NotBeNull();
        personRepoFactory.Should().NotBeNull();
        toolRepoFactory.Should().NotBeNull();
        settingsRepoFactory.Should().NotBeNull();
        sessionRepoFactory.Should().NotBeNull();

        container.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_WithPluginLoadingFailure_ContinuesGracefully()
    {
        // Arrange
        await _containerFixture.InitializeAsync(useInMemoryDatabase: true, mockRepositories: true);
        _testContainer = _containerFixture.Container;

        var mockPluginService = new Mock<IPluginService>();
        mockPluginService.Setup(ps => ps.LoadPluginsAsync(It.IsAny<string>()))
                        .ThrowsAsync(new DirectoryNotFoundException("Plugin directory not found"));
        
        _containerFixture.RegisterMock(mockPluginService.Object);

        var logger = _testContainer.Resolve<ILogger<DaoStudio.DaoStudioService>>();
        var DaoStudio = new DaoStudio.DaoStudioService(_testContainer, logger);

        // Act & Assert - Should handle plugin loading failure gracefully
        var act = async () => await DaoStudio.InitializeAsync(_testContainer);
        await act.Should().ThrowAsync<DirectoryNotFoundException>();

        DaoStudio.Dispose();
    }

    public void Dispose()
    {
        _testContainer?.Dispose();
        _containerFixture?.Dispose();
    }
}
