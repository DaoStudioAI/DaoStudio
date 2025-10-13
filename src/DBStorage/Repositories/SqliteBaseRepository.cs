using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// Base class for SQLite repositories with connection management and pooling
    /// </summary>
    public abstract class SqliteBaseRepository : IDisposable
    {
        private readonly string _connectionString;
        private SqliteConnection? _connection;
        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
        private DateTime _lastConnectionUse = DateTime.MinValue;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the SqliteBaseRepository class
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        protected SqliteBaseRepository(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentNullException(nameof(databasePath));

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = true
            }.ToString();
        }

        /// <summary>
        /// Gets a shared, reusable SQLite connection
        /// </summary>
        /// <returns>An open SQLite connection that can be reused</returns>
        protected async Task<SqliteConnection> GetConnectionAsync()
        {
            // Always provide a fresh connection to guarantee thread-safety. The underlying
            // Microsoft.Data.Sqlite connection pool ensures this is inexpensive.
            ThrowIfDisposed();

            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        /// <summary>
        /// Gets a shared, reusable SQLite connection synchronously
        /// </summary>
        /// <returns>An open SQLite connection that can be reused</returns>
        protected SqliteConnection GetConnection()
        {
            // Synchronous variant – returns a fresh, open connection every time.
            ThrowIfDisposed();

            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Executes an operation with a transaction for better performance and consistency
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <returns>The result of the operation</returns>
        protected async Task<T> ExecuteInTransactionAsync<T>(Func<SqliteConnection, DbTransaction, Task<T>> operation)
        {
            ThrowIfDisposed();

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using DbTransaction transaction = await connection.BeginTransactionAsync();
            try
            {
                var result = await operation(connection, transaction);
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Executes an operation with a transaction for better performance and consistency
        /// </summary>
        /// <param name="operation">The operation to execute</param>
    protected async Task ExecuteInTransactionAsync(Func<SqliteConnection, DbTransaction, Task> operation)
        {
            await ExecuteInTransactionAsync(async (conn, trans) =>
            {
                await operation(conn, trans);
                return true; // dummy return value
            });
        }

        /// <summary>
        /// Forces a connection reset (useful for error recovery)
        /// </summary>
        protected async Task ResetConnectionAsync()
        {
            await _connectionSemaphore.WaitAsync();
            try
            {
                _connection?.Dispose();
                _connection = null;
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Checks if the object has been disposed
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// Abstract method for repository-specific initialization
        /// </summary>
        protected abstract void Initialize();

        /// <summary>
        /// Disposes of the repository resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the repository resources
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _connection?.Dispose();
                    _connectionSemaphore?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~SqliteBaseRepository()
        {
            Dispose(false);
        }
    }
}
