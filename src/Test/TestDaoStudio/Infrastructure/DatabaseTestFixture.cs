using DaoStudio.DBStorage.Factory;
using Microsoft.Extensions.Logging;
using System.Data.SQLite;
using Serilog;
using Serilog.Extensions.Logging;

namespace TestDaoStudio.Infrastructure;

/// <summary>
/// Test fixture that manages SQLite database lifecycle for integration tests.
/// Provides database creation, migration execution, and cleanup functionality.
/// </summary>
public class DatabaseTestFixture : IDisposable, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly string? _databasePath;
    private readonly ILogger<DatabaseTestFixture> _logger;
    private StorageFactory? _storageFactory;
    private bool _disposed = false;

    public string ConnectionString => _connectionString;
    public string? DatabasePath => _databasePath;
    public StorageFactory StorageFactory => _storageFactory ?? throw new InvalidOperationException("Database not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Creates a new database test fixture.
    /// </summary>
    /// <param name="useInMemoryDatabase">If true, uses in-memory SQLite database. If false, uses file-based database.</param>
    /// <param name="testName">Optional test name to create unique database file</param>
    public DatabaseTestFixture(bool useInMemoryDatabase = true, string? testName = null)
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), "DaoStudio", "Tests", "Logs");
        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, $"database-test-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
        
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Error()
            //.WriteTo.File(logFilePath, rollingInterval: RollingInterval.Infinite)
            .CreateLogger();
        
        var loggerFactory = new SerilogLoggerFactory(serilogLogger, dispose: true);
        _logger = loggerFactory.CreateLogger<DatabaseTestFixture>();

        if (useInMemoryDatabase)
        {
            // Use a named shared in-memory database so that **all connections** opened with the same
            // connection string can see the same schema. We build two related strings:
            // 1. _databasePath  : the SQLite URI representing the shared in-memory DB ("file:memdb...?")
            // 2. _connectionString : the full connection string used by SQLiteConnection ("Data Source=file:memdb...?...")
            //
            // Down-stream components such as StorageFactory expect a plain "file:..." URI (they will wrap it
            // in their own connection string builders), so we keep that value in _databasePath.  This avoids
            // accidentally ending up with a doubled "Data Source=" prefix and the resulting
            // "SQLite Error 14: unable to open database file" exception.
            var memName = $"memdb_{Guid.NewGuid():N}";
            _databasePath = $"file:{memName}?mode=memory&cache=shared";              // plain URI
            _connectionString = $"Data Source={_databasePath}";                     // full connection string
        }
        else
        {
            _databasePath = GetTestDatabasePath(testName);
            _connectionString = $"Data Source={_databasePath}";
        }
    }

    /// <summary>
    /// Initializes the test database and runs migrations.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Clean up any existing file database
            if (_databasePath != null && File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
                _logger.LogDebug("Deleted existing test database: {DatabasePath}", _databasePath);
            }

            // Ensure directory exists for file databases
            if (_databasePath != null)
            {
                var directory = Path.GetDirectoryName(_databasePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            // Create and initialize storage factory
            // If using shared in-memory DB (_databasePath == null), use the same connection string so that
            // StorageFactory and all its repositories use the shared database.
            var databasePath = _databasePath ?? _connectionString;
            _storageFactory = new StorageFactory(databasePath);
            await _storageFactory.InitializeAsync();

            _logger.LogDebug("Initialized test database: {DatabasePath}", databasePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize test database");
            throw;
        }
    }

    /// <summary>
    /// Creates a new connection to the test database.
    /// </summary>
    public SQLiteConnection CreateConnection()
    {
        return new SQLiteConnection(_connectionString);
    }

    /// <summary>
    /// Executes a SQL command against the test database.
    /// </summary>
    public async Task<int> ExecuteNonQueryAsync(string sql, object? parameters = null)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();
        
        using var command = new SQLiteCommand(sql, connection);
        
        if (parameters != null)
        {
            AddParameters(command, parameters);
        }
        
        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Executes a SQL query and returns the first result.
    /// </summary>
    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();
        
        using var command = new SQLiteCommand(sql, connection);
        
        if (parameters != null)
        {
            AddParameters(command, parameters);
        }
        
        var result = await command.ExecuteScalarAsync();
        return result is T typedResult ? typedResult : default(T);
    }

    /// <summary>
    /// Verifies that a table exists in the database.
    /// </summary>
    public async Task<bool> TableExistsAsync(string tableName)
    {
        const string sql = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName";
        var count = await ExecuteScalarAsync<int>(sql, new { tableName });
        return count > 0;
    }

    /// <summary>
    /// Gets the count of rows in a table.
    /// </summary>
    public async Task<int> GetRowCountAsync(string tableName)
    {
        var sql = $"SELECT COUNT(*) FROM {tableName}";
        var result = await ExecuteScalarAsync<int?>(sql);
        return result ?? 0;
    }

    /// <summary>
    /// Clears all data from a table.
    /// </summary>
    public async Task ClearTableAsync(string tableName)
    {
        var sql = $"DELETE FROM {tableName}";
        await ExecuteNonQueryAsync(sql);
    }

    /// <summary>
    /// Clears all data from all tables (but keeps schema).
    /// </summary>
    public async Task ClearAllDataAsync()
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();

        // Get all table names
        const string getTablesSql = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        using var command = new SQLiteCommand(getTablesSql, connection);
        using var reader = await command.ExecuteReaderAsync();

        var tableNames = new List<string>();
        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        // Clear each table
        foreach (var tableName in tableNames)
        {
            using var deleteCommand = new SQLiteCommand($"DELETE FROM {tableName}", connection);
            await deleteCommand.ExecuteNonQueryAsync();
        }

        _logger.LogDebug("Cleared all data from {TableCount} tables", tableNames.Count);
    }

    /// <summary>
    /// Creates a collection fixture for xUnit test collections.
    /// </summary>
    public static DatabaseTestFixture CreateCollectionFixture(string collectionName)
    {
        return new DatabaseTestFixture(useInMemoryDatabase: false, testName: collectionName);
    }

    /// <summary>
    /// Creates an in-memory fixture for unit tests.
    /// </summary>
    public static DatabaseTestFixture CreateInMemoryFixture()
    {
        return new DatabaseTestFixture(useInMemoryDatabase: true);
    }

    private void AddParameters(SQLiteCommand command, object parameters)
    {
        var properties = parameters.GetType().GetProperties();
        foreach (var property in properties)
        {
            var value = property.GetValue(parameters);
            command.Parameters.AddWithValue($"@{property.Name}", value ?? DBNull.Value);
        }
    }

    private static string GetTestDatabasePath(string? testName = null)
    {
        var testDatabasePath = Environment.GetEnvironmentVariable("TEST_DATABASE_PATH");
        if (!string.IsNullOrEmpty(testDatabasePath))
        {
            return testDatabasePath;
        }

        // Create unique database file for this test
        var tempPath = Path.GetTempPath();
        var fileName = testName != null ? $"test_{testName}_{Guid.NewGuid():N}.db" : $"test_{Guid.NewGuid():N}.db";
        var testDbPath = Path.Combine(tempPath, "DaoStudio", "Tests", fileName);

        return testDbPath;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                // Clean up file database if it exists
                if (_databasePath != null && File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                    _logger.LogDebug("Cleaned up test database: {DatabasePath}", _databasePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up test database: {DatabasePath}", _databasePath);
            }

            _disposed = true;
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_storageFactory != null)
        {
            // StorageFactory doesn't implement IAsyncDisposable, so just dispose synchronously
            _storageFactory.Dispose();
        }

        // Perform any async cleanup if needed
        await Task.CompletedTask;
    }
}
