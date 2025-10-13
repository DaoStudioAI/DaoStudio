using System;
using System.IO;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Repositories;
using DaoStudio.DBStorage.Migrations;
using System.Reflection;

namespace DaoStudio.DBStorage.Factory
{
    /// <summary>
    /// Factory for creating and accessing settings repositories
    /// </summary>
    public class StorageFactory : IDisposable
    {
        /// <summary>
        /// The database path for this instance
        /// </summary>
        public string DatabasePath { get; }
        
        private ISettingsRepository? _repository;
        private IPersonRepository? _personRepository;
        private ILlmToolRepository? _llmToolRepository;
        private ILlmPromptRepository? _llmPromptRepository;
        private IAPIProviderRepository? _apiProviderRepository;
        private ISessionRepository? _sessionRepository;
        private IMessageRepository? _messageRepository;
        private ICachedModelRepository? _cachedModelRepository;
        private IApplicationRepository? _applicationRepository;
        private IMigrationManager? _migrationManager;
        private bool _isInitialized = false;
        private readonly object _initLock = new object();
        private bool _disposed = false;

        /// <summary>
        /// Constructor with custom database path
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public StorageFactory(string databasePath)
        {
            DatabasePath = databasePath;
        }
        
        /// <summary>
        /// Initializes the storage factory by applying all pending migrations
        /// </summary>
        /// <returns>A task that represents the asynchronous initialization operation</returns>
        public virtual async Task InitializeAsync()
        {
            if (_isInitialized)
                return;
            
            lock (_initLock)
            {
                if (_isInitialized)
                    return;
                
                // Create the migration manager
                _migrationManager = new SqliteMigrationManager(DatabasePath);
            }
            
            _migrationManager ??= new SqliteMigrationManager(DatabasePath);

            // Register migrations in order
            //_migrationManager.RegisterMigration(new Migration_001_AddSettingsIndex());

            // Apply migrations
            await _migrationManager.MigrateToLatestAsync();
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// Ensures the storage factory is initialized
        /// </summary>
        private async Task EnsureInitializedAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }
        }

        /// <summary>
        /// Get the migration manager
        /// </summary>
        /// <returns>The migration manager instance</returns>
        public async Task<IMigrationManager> GetMigrationManagerAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            _migrationManager ??= new SqliteMigrationManager(DatabasePath);
            return _migrationManager;
        }
        
        /// <summary>
        /// Apply all pending migrations to the latest version
        /// </summary>
        /// <returns>True if migrations were applied, false if already at latest version</returns>
        public async Task<bool> ApplyMigrationsAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            var manager = await GetMigrationManagerAsync();
            return await manager.MigrateToLatestAsync();
        }
        
        /// <summary>
        /// Get the current database version
        /// </summary>
        /// <returns>Current database version</returns>
        public async Task<int> GetDatabaseVersionAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            var manager = await GetMigrationManagerAsync();
            return await manager.GetCurrentVersionAsync();
        }

        /// <summary>
        /// Get the settings repository
        /// </summary>
        /// <returns>The settings repository instance</returns>
        public async Task<ISettingsRepository> GetSettingsRepositoryAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            _repository ??= new SqliteSettingsRepository(DatabasePath);
            return _repository;
        }

        public async Task<IPersonRepository> GetPersonRepositoryAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            _personRepository ??= new SqlitePersonRepository(DatabasePath);
            return _personRepository;
        }

        /// <summary>
        /// Get the LLM tool repository
        /// </summary>
        /// <returns>The LLM tool repository instance</returns>
        public async Task<ILlmToolRepository> GetLlmToolRepositoryAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            _llmToolRepository ??= new SqliteLlmToolRepository(DatabasePath);
            return _llmToolRepository;
        }

        /// <summary>
        /// Get the LLM prompt repository
        /// </summary>
        /// <returns>The LLM prompt repository instance</returns>
        public async Task<ILlmPromptRepository> GetLlmPromptRepositoryAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            _llmPromptRepository ??= new SqliteLlmPromptRepository(DatabasePath);
            return _llmPromptRepository;
        }

        /// <summary>
        /// Get the LLM provider repository
        /// </summary>
        /// <returns>The LLM provider repository instance</returns>
        public virtual async Task<IAPIProviderRepository> GetApiProviderRepositoryAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            _apiProviderRepository ??= new SqliteAPIProviderRepository(DatabasePath);
            return _apiProviderRepository;
        }

        /// <summary>
        /// Get the Session repository
        /// </summary>
        /// <returns>The Session repository instance</returns>
        public async Task<ISessionRepository> GetSessionRepositoryAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            _sessionRepository ??= new SqliteSessionRepository(DatabasePath);
            return _sessionRepository;
        }

        /// <summary>
        /// Get the Message repository
        /// </summary>
        /// <returns>The Message repository instance</returns>
        public async Task<IMessageRepository> GetMessageRepositoryAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            _messageRepository ??= new SqliteMessageRepository(DatabasePath);
            return _messageRepository;
        }

        /// <summary>
        /// Get the cached model repository
        /// </summary>
        /// <returns>The cached model repository instance</returns>
        public async Task<ICachedModelRepository> GetCachedModelRepositoryAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            _cachedModelRepository ??= new SqliteCachedModelRepository(DatabasePath);
            return _cachedModelRepository;
        }
        
        /// <summary>
        /// Get the Applications repository
        /// </summary>
        /// <returns>The Applications repository instance</returns>
        public async Task<IApplicationRepository> GetApplicationRepositoryAsync()
        {
            ThrowIfDisposed();
            await EnsureInitializedAsync();
            _applicationRepository ??= new SqliteApplicationRepository(DatabasePath);
            return _applicationRepository;
        }
        
        /// <summary>
        /// Check if the object has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(StorageFactory));
            }
        }
        
        /// <summary>
        /// Disposes of the resources used by the StorageFactory
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Disposes of the resources used by the StorageFactory
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    (_repository as IDisposable)?.Dispose();
                    (_personRepository as IDisposable)?.Dispose();
                    (_llmToolRepository as IDisposable)?.Dispose();
                    (_llmPromptRepository as IDisposable)?.Dispose();
                    (_apiProviderRepository as IDisposable)?.Dispose();
                    (_sessionRepository as IDisposable)?.Dispose();
                    (_messageRepository as IDisposable)?.Dispose();
                    (_migrationManager as IDisposable)?.Dispose();
                    (_cachedModelRepository as IDisposable)?.Dispose();
                    (_applicationRepository as IDisposable)?.Dispose();

                }

                _disposed = true;
            }
        }
        
        /// <summary>
        /// Finalizer
        /// </summary>
        ~StorageFactory()
        {
            Dispose(false);
        }
    }
} 