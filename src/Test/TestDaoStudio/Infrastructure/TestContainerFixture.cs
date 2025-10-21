using DaoStudio;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Services;
using DaoStudio.Plugins;
using DryIoc;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Moq;
using DaoStudio.Engines.MEAI;
using Serilog;
using Serilog.Extensions.Logging;

namespace TestDaoStudio.Infrastructure;

/// <summary>
/// Test fixture that provides a configured DryIoc container for testing.
/// This fixture sets up all the dependencies that DaoStudio needs but with test-specific configurations.
/// </summary>
public class TestContainerFixture : IDisposable
{
    private Container? _container;
    private ILoggerFactory? _loggerFactory;
    private bool _disposed = false;

    public Container Container => _container ?? throw new InvalidOperationException("Container not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Initializes the test container with all required services.
    /// </summary>
    /// <param name="useInMemoryDatabase">Whether to use in-memory SQLite database</param>
    /// <param name="mockRepositories">Whether to use mock repositories instead of real ones</param>
    /// <param name="databasePath">Optional specific database path to use. If null, generates a default path.</param>
    public async Task InitializeAsync(bool useInMemoryDatabase = true, bool mockRepositories = false, string? databasePath = null)
    {
        _container = new Container();
        
        // Register the container itself so it can be injected
        _container.RegisterInstance(_container);
        
        // Setup logging with Serilog file sink
        var logDirectory = Path.Combine(Path.GetTempPath(), "DaoStudio", "Tests", "Logs");
        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, $"test-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
        
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Infinite)
            .CreateLogger();
        
        _loggerFactory = new SerilogLoggerFactory(serilogLogger, dispose: true);
        
        _container.RegisterInstance(_loggerFactory);

        // Register ILogger<> via Logger<> which uses ILoggerFactory under the hood
        _container.Register(typeof(ILogger<>), typeof(Logger<>), Reuse.Singleton);

        if (mockRepositories)
        {
            RegisterMockRepositories();
        }
        else
        {
            await RegisterRealRepositories(useInMemoryDatabase, databasePath);
        }

        RegisterServices();
    }

    /// <summary>
    /// Creates a container with mock repositories for unit testing.
    /// </summary>
    public static async Task<Container> CreateWithMocksAsync()
    {
        var fixture = new TestContainerFixture();
        await fixture.InitializeAsync(useInMemoryDatabase: true, mockRepositories: true);
        return fixture.Container;
    }

    /// <summary>
    /// Creates a container with real repositories for integration testing.
    /// </summary>
    public static async Task<Container> CreateWithRealRepositoriesAsync(bool useInMemoryDatabase = true, string? databasePath = null)
    {
        var fixture = new TestContainerFixture();
        await fixture.InitializeAsync(useInMemoryDatabase, mockRepositories: false, databasePath);
        return fixture.Container;
    }

    private void RegisterMockRepositories()
    {
        // Register mock repositories
        _container.RegisterInstance(Mock.Of<IAPIProviderRepository>());
        _container.RegisterInstance(Mock.Of<ICachedModelRepository>());
        _container.RegisterInstance(Mock.Of<IMessageRepository>());
        _container.RegisterInstance(Mock.Of<IPersonRepository>());
        _container.RegisterInstance(Mock.Of<ILlmToolRepository>());
        _container.RegisterInstance(Mock.Of<ISettingsRepository>());
        _container.RegisterInstance(Mock.Of<ISessionRepository>());
        
        // Register mock StorageFactory
        var storageFactoryMock = new Mock<StorageFactory>(":memory:");
        storageFactoryMock.Setup(sf => sf.InitializeAsync()).Returns(Task.CompletedTask);
        _container.RegisterInstance(storageFactoryMock.Object);
    }

    private async Task RegisterRealRepositories(bool useInMemoryDatabase, string? databasePath = null)
    {
        // Register StorageFactory with test database path
        // Use a temporary *file-based* SQLite database even when useInMemoryDatabase is true so that
        // each new connection references the same database and sees the existing schema.
        var dbPath = databasePath ?? GetTestDatabasePath();
        _container.RegisterDelegate<StorageFactory>(() => new StorageFactory(dbPath), Reuse.Singleton);

        // Initialize storage and register repositories
        var storageFactory = _container.Resolve<StorageFactory>();
        await storageFactory.InitializeAsync();

        // Register storage repositories
        _container.RegisterDelegate<IAPIProviderRepository>(c =>
        {
            var storage = c.Resolve<StorageFactory>();
            return storage.GetApiProviderRepositoryAsync().Result;
        }, Reuse.Singleton);

        _container.RegisterDelegate<ICachedModelRepository>(c =>
        {
            var storage = c.Resolve<StorageFactory>();
            return storage.GetCachedModelRepositoryAsync().Result;
        }, Reuse.Singleton);

        _container.RegisterDelegate<IMessageRepository>(c =>
        {
            var storage = c.Resolve<StorageFactory>();
            return storage.GetMessageRepositoryAsync().Result;
        }, Reuse.Singleton);

        _container.RegisterDelegate<IPersonRepository>(c =>
        {
            var storage = c.Resolve<StorageFactory>();
            return storage.GetPersonRepositoryAsync().Result;
        }, Reuse.Singleton);

        _container.RegisterDelegate<ILlmToolRepository>(c =>
        {
            var storage = c.Resolve<StorageFactory>();
            return storage.GetLlmToolRepositoryAsync().Result;
        }, Reuse.Singleton);

        _container.RegisterDelegate<ISettingsRepository>(c =>
        {
            var storage = c.Resolve<StorageFactory>();
            return storage.GetSettingsRepositoryAsync().Result;
        }, Reuse.Singleton);

        _container.RegisterDelegate<ISessionRepository>(c =>
        {
            var storage = c.Resolve<StorageFactory>();
            return storage.GetSessionRepositoryAsync().Result;
        }, Reuse.Singleton);
    }

    private void RegisterServices()
    {
        // Register all services (same as DaoStudio)
        // Register a mock Settings for tests
        _container.RegisterDelegate<ISettings>(() => Mock.Of<ISettings>(), Reuse.Singleton);
        _container.Register<ICachedModelService, CachedModelService>(Reuse.Singleton);
        _container.Register<IApiProviderService, ApiProviderService>(Reuse.Singleton);
        _container.Register<IToolService, ToolService>(Reuse.Singleton);
        _container.Register<IPeopleService, PeopleService>(Reuse.Singleton);
        _container.Register<IApplicationPathsService, ApplicationPathsService>(Reuse.Singleton);
        _container.Register<IPluginService, PluginService>(Reuse.Singleton);
        _container.Register<IEngineService, EngineService>(Reuse.Singleton);
        _container.Register<ISessionService, SessionService>(Reuse.Singleton);
        _container.Register<IMessageService, MessageService>(Reuse.Singleton);
        _container.Register<IHost, Host>(Reuse.Singleton);
        
        // Register the PlainAIFunctionFactory
        _container.Register<IPlainAIFunctionFactory, DaoStudio.Plugins.PlainAIFunctionFactory>(Reuse.Singleton);
        
        // Register all engine types for DryIoc dependency injection
        _container.Register<OpenAIEngine>(Reuse.Transient);
        _container.Register<GoogleEngine>(Reuse.Transient);
        _container.Register<AnthropicEngine>(Reuse.Transient);
        _container.Register<OllamaEngine>(Reuse.Transient);
        _container.Register<AWSBedrockEngine>(Reuse.Transient);
        
        // Register DaoStudio itself
        _container.Register<DaoStudio.DaoStudioService>(Reuse.Singleton);
    }

    /// <summary>
    /// Allows overriding specific services for testing.
    /// </summary>
    public void RegisterMock<TService>(TService mockInstance) where TService : class
    {
        if (_container == null)
            throw new InvalidOperationException("Container not initialized");
            
        // Prevent DryIoc from disposing externally provided mock instances
        _container.RegisterInstance(
            mockInstance,
            ifAlreadyRegistered: IfAlreadyRegistered.Replace,
            setup: Setup.With(preventDisposal: true));
    }

    /// <summary>
    /// Allows overriding specific services with Moq mocks for testing.
    /// </summary>
    public Mock<TService> RegisterMock<TService>() where TService : class
    {
        if (_container == null)
            throw new InvalidOperationException("Container not initialized");

        var mock = new Mock<TService>();
        // Prevent DryIoc from disposing mocks it does not own
        _container.RegisterInstance(
            mock.Object,
            ifAlreadyRegistered: IfAlreadyRegistered.Replace,
            setup: Setup.With(preventDisposal: true));
        return mock;
    }

    private static string GetTestDatabasePath()
    {
        var testDatabasePath = Environment.GetEnvironmentVariable("TEST_DATABASE_PATH");
        if (!string.IsNullOrEmpty(testDatabasePath))
        {
            return testDatabasePath;
        }

        // Create unique database file for each test instance to prevent cross-test contamination
        var tempPath = Path.GetTempPath();
        var uniqueDbName = $"test_{Guid.NewGuid():N}.db";
        var testDbPath = Path.Combine(tempPath, "DaoStudio", "Tests", uniqueDbName);
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(testDbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return testDbPath;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _loggerFactory?.Dispose();
                try
                {
                    _container?.Dispose();
                }
                catch
                {
                    // Swallow disposal exceptions from mocks registered to throw on Dispose
                }
            }
            _disposed = true;
        }
    }
}
