using DaoStudio.Common.Plugins;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Plugins;
using DaoStudio.Services;
using DryIoc;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Reflection;

namespace DaoStudio;

/// <summary>
/// Main DaoStudio host class that manages AI chat sessions and plugins.
/// 
/// Key Features:
/// - Service composition: Uses dedicated service classes for different responsibilities
/// - Session management: Delegates to SessionService
/// - Plugin management: Delegates to PluginService
/// - Storage abstraction: Provides access to database storage through services
/// - Proper resource disposal: Automatically disposes all services
/// </summary>
public partial class DaoStudioService : IDisposable
{
    private ILogger<DaoStudioService> logger;
    private Container Container;
    private bool _disposed = false;


    public static void RegisterServices(Container container)
    {
        // Register services in container if provided
        if (container != null)
        {
            

            //dryioc open generic
            var loggerFactoryMethod = typeof(LoggerFactoryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == nameof(LoggerFactoryExtensions.CreateLogger)
                                     && m.IsGenericMethod
                                     && m.GetParameters().Length == 1);
            if (loggerFactoryMethod == null)
            {
                throw new InvalidOperationException("LoggerFactoryExtensions.CreateLogger<T> method not found.");
            }
            
            // Register open-generic ILogger<T> to always use the requested generic type argument
            // This ensures that when a class asks for ILogger<HostSessionAdapter> it receives the correctly typed logger
            container.Register(typeof(ILogger<>), made: Made.Of((Request req) =>
            {

                //var targetType = (req.Parent?.ImplementationType) ?? req.ServiceType.GenericTypeArguments[0];
                
                // Use the generic type argument from the requested service type
                var targetType = req.ServiceType.GenericTypeArguments[0];
                return loggerFactoryMethod.MakeGenericMethod(targetType);
            }));

            // Register application paths service first, as it may be needed by other services
            container.Register<IApplicationPathsService, ApplicationPathsService>(Reuse.Singleton);
            
            // Register service instances
            container.RegisterDelegate<StorageFactory>(() => 
            {
                var pathsService = container.Resolve<IApplicationPathsService>();
                return new StorageFactory(pathsService.SettingsDatabasePath);
            }, Reuse.Singleton);
            container.Register<DaoStudioService>(Reuse.Singleton);






            // Register storage repositories
            container.RegisterDelegate<IAPIProviderRepository>(c =>
            {
                var storageFactory = c.Resolve<StorageFactory>();
                return storageFactory.GetApiProviderRepositoryAsync().GetAwaiter().GetResult();
            }, Reuse.Singleton);

            container.RegisterDelegate<ICachedModelRepository>(c =>
            {
                var storageFactory = c.Resolve<StorageFactory>();
                return storageFactory.GetCachedModelRepositoryAsync().GetAwaiter().GetResult();
            }, Reuse.Singleton);

            container.RegisterDelegate<IMessageRepository>(c =>
            {
                var storageFactory = c.Resolve<StorageFactory>();
                return storageFactory.GetMessageRepositoryAsync().GetAwaiter().GetResult();
            }, Reuse.Singleton);

            container.RegisterDelegate<IPersonRepository>(c =>
            {
                var storageFactory = c.Resolve<StorageFactory>();
                return storageFactory.GetPersonRepositoryAsync().GetAwaiter().GetResult();
            }, Reuse.Singleton);

            container.RegisterDelegate<ILlmToolRepository>(c =>
            {
                var storageFactory = c.Resolve<StorageFactory>();
                return storageFactory.GetLlmToolRepositoryAsync().GetAwaiter().GetResult();
            }, Reuse.Singleton);

            container.RegisterDelegate<ISettingsRepository>(c =>
            {
                var storageFactory = c.Resolve<StorageFactory>();
                return storageFactory.GetSettingsRepositoryAsync().GetAwaiter().GetResult();
            }, Reuse.Singleton);
            container.RegisterDelegate<ISessionRepository>(c =>
            {
                var storageFactory = c.Resolve<StorageFactory>();
                return storageFactory.GetSessionRepositoryAsync().GetAwaiter().GetResult();
            }, Reuse.Singleton);



            // Register Settings as singleton implementing ISettings
            container.RegisterDelegate<ISettings>(c =>
            {
                var settingsRepository = c.Resolve<ISettingsRepository>();
                var logger = c.Resolve<ILogger<Settings>>();
                var settings = new Settings(settingsRepository, logger);
                // Initialize settings synchronously (this method will be called during DI setup)
                settings.InitializeAsync().GetAwaiter().GetResult();
                return settings;
            }, Reuse.Singleton);
            
            container.Register<ICachedModelService, CachedModelService>(Reuse.Singleton);
            container.Register<IApiProviderService, ApiProviderService>(Reuse.Singleton);
            container.Register<IToolService, ToolService>(Reuse.Singleton);
            container.Register<IPeopleService, PeopleService>(Reuse.Singleton);
            container.Register<IPluginService, PluginService>(Reuse.Singleton);
            
            // Engine factory service
            container.Register<IEngineService, EngineService>(Reuse.Singleton);
            
            container.Register<ISessionService, SessionService>(Reuse.Singleton);
            container.Register<IMessageService, MessageService>(Reuse.Singleton);

            // Register PlainAIFunction factory
            container.Register<IPlainAIFunctionFactory, PlainAIFunctionFactory>(Reuse.Singleton);

            container.Register<IHost, Host>(Reuse.Singleton);


        }
    }


    public DaoStudioService(Container container, ILogger<DaoStudioService> logger)
    {
        if (container == null) throw new ArgumentNullException(nameof(container));
        if (logger == null) throw new ArgumentNullException(nameof(logger));

        this.logger = logger;
        Container = container;
    }

    public async Task InitializeAsync(Container container)
    {
        if (container == null) throw new ArgumentNullException(nameof(container));
        // Run storage initialization and plugin loading concurrently
        var storage = container.Resolve<StorageFactory>();
        var storageTask = storage.InitializeAsync();

        var pluginsPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "Plugins");
        var pluginService = container.Resolve<IPluginService>();
        var pluginsTask = pluginService.LoadPluginsAsync(pluginsPath);

        await Task.WhenAll(storageTask, pluginsTask);

        // pluginService depends on storage
        await pluginService.InitializeAsync();
    }


    /// <summary>
    /// Determines the default database path by finding the best location for the database file
    /// </summary>
    /// <returns>The default database path</returns>
    private static string GetDefaultDatabasePath()
    {
        string databasePath = string.Empty;
        bool useExeFolder = false;
        bool needCleanup = false;

        try
        {
            // Try exe folder first, now with Config subfolder
            var exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var configPath = Path.Combine(exePath ?? "", "Config");
            databasePath = Path.Combine(configPath, "settings.db");

            if (File.Exists(databasePath))
            {
                // Test write access to existing file
                try
                {
                    using (var stream = File.OpenWrite(databasePath))
                    {
                        useExeFolder = true;
                    }
                }
                catch
                {
                    useExeFolder = false;
                }
            }
                else
                {
                    // Try to create the Config directory and test file
                    try
                    {
                        // Ensure Config directory exists
                        Directory.CreateDirectory(configPath);
                        
                        using (var testFile = File.Create(databasePath))
                        {
                            testFile.Close();
                        }
                        useExeFolder = true;
                        needCleanup = true;
                    }
                    catch
                    {
                        useExeFolder = false;
                        needCleanup = true;
                    }
                }            // Clean up test file if we created it
            if (needCleanup && File.Exists(databasePath))
            {
                try
                {
                    File.Delete(databasePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        catch
        {
            useExeFolder = false;
        }

        if (!useExeFolder)
        {
            // Fall back to AppData if exe folder is not writable, also with Config subfolder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var DaoStudioPath = Path.Combine(appDataPath, DaoStudio.Common.Constants.AppName);
            var configPath = Path.Combine(DaoStudioPath, "Config");

            // Ensure Config directory exists
            if (!Directory.Exists(configPath))
            {
                Directory.CreateDirectory(configPath);
            }

            databasePath = Path.Combine(configPath, "settings.db");
        }

        return databasePath;
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
                // Dispose services in reverse order of dependencies
                try
                {
                    var PluginService = Container.Resolve<IPluginService>();
                    PluginService?.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error disposing PluginService");
                }

                try
                {
                    var SessionService = Container.Resolve<ISessionService>();
                    SessionService?.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error disposing SessionService");
                }

                // Note: Other services don't implement IDisposable but could in the future
            }
            _disposed = true;
        }
    }

    ~DaoStudioService()
    {
        Dispose(false);
    }

}
