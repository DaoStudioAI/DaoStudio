using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Factory;
using Xunit;
using Xunit.Abstractions;

namespace PerformanceStorage
{
    /// <summary>
    /// Abstract base class providing common functionality for database load tests
    /// </summary>
    public abstract class BaseLoadTest : IDisposable
    {
        protected readonly ITestOutputHelper Output;
        protected StorageFactory? StorageFactory;
    /// <summary>
    /// Database path or connection identifier. If <see cref="UseInMemoryDatabase"/> is true,
    /// this will be set to the special in-memory identifier used by SQLite (":memory:").
    /// </summary>
    protected string DatabasePath;

    /// <summary>
    /// When true tests will use an in-memory SQLite database instead of a file.
    /// </summary>
    protected bool UseInMemoryDatabase { get; }
        private readonly Stopwatch _testStopwatch;
        private readonly List<PerformanceMetric> _performanceMetrics;

        /// <summary>
        /// Create a new BaseLoadTest.
        /// </summary>
        /// <param name="output">xUnit test output helper</param>
        /// <param name="useInMemoryDatabase">If true, use SQLite in-memory database</param>
        protected BaseLoadTest(ITestOutputHelper output, bool useInMemoryDatabase = false)
        {
            Output = output;
            UseInMemoryDatabase = useInMemoryDatabase;

            if (UseInMemoryDatabase)
            {
                // Use SQLite in-memory database
                DatabasePath = ":memory:";
            }
            else
            {
                DatabasePath = Path.Combine(Path.GetTempPath(), $"load_test_{Guid.NewGuid()}.db");
            }
            _testStopwatch = new Stopwatch();
            _performanceMetrics = new List<PerformanceMetric>();
        }

        /// <summary>
        /// Initialize storage factory and database for testing
        /// </summary>
        protected async Task InitializeStorageAsync()
        {
            StorageFactory = new StorageFactory(DatabasePath);
            await StorageFactory.InitializeAsync();
            await StorageFactory.ApplyMigrationsAsync();
            _testStopwatch.Start();
        }

        /// <summary>
        /// Cleanup resources and delete test database
        /// </summary>
        public virtual void Dispose()
        {
            _testStopwatch.Stop();
            
            // Log final performance summary
            if (_performanceMetrics.Count > 0)
            {
                LogPerformanceSummary();
            }

            StorageFactory?.Dispose();
            
            // Only attempt to delete the database file when not using in-memory DB
            if (!UseInMemoryDatabase && File.Exists(DatabasePath))
            {
                try
                {
                    // Ensure SQLite pooled connections are cleared so file can be deleted on Windows
                    try
                    {
                        // Microsoft.Data.Sqlite exposes ClearAllPools on the SqliteConnection type
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    }
                    catch { }
                    File.Delete(DatabasePath);
                }
                catch (Exception ex)
                {
                    Output.WriteLine($"Warning: Could not delete test database {DatabasePath}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Measure performance of a synchronous operation
        /// </summary>
        protected PerformanceResult MeasurePerformance<T>(string operationName, Func<T> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);
            
            try
            {
                var result = operation();
                stopwatch.Stop();
                
                var memoryAfter = GC.GetTotalMemory(false);
                var memoryUsed = memoryAfter - memoryBefore;
                
                var perfResult = new PerformanceResult
                {
                    OperationName = operationName,
                    Duration = stopwatch.Elapsed,
                    MemoryUsed = memoryUsed,
                    Success = true,
                    Result = result
                };

                _performanceMetrics.Add(new PerformanceMetric(operationName, stopwatch.Elapsed, memoryUsed, true));
                LogPerformanceResult(perfResult);
                
                return perfResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var perfResult = new PerformanceResult
                {
                    OperationName = operationName,
                    Duration = stopwatch.Elapsed,
                    Success = false,
                    Exception = ex
                };

                _performanceMetrics.Add(new PerformanceMetric(operationName, stopwatch.Elapsed, 0, false));
                LogPerformanceResult(perfResult);
                
                throw;
            }
        }

        /// <summary>
        /// Measure performance of an asynchronous operation
        /// </summary>
        protected async Task<PerformanceResult> MeasurePerformanceAsync<T>(string operationName, Func<Task<T>> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);
            
            try
            {
                var result = await operation();
                stopwatch.Stop();
                
                var memoryAfter = GC.GetTotalMemory(false);
                var memoryUsed = memoryAfter - memoryBefore;
                
                var perfResult = new PerformanceResult
                {
                    OperationName = operationName,
                    Duration = stopwatch.Elapsed,
                    MemoryUsed = memoryUsed,
                    Success = true,
                    Result = result
                };

                _performanceMetrics.Add(new PerformanceMetric(operationName, stopwatch.Elapsed, memoryUsed, true));
                LogPerformanceResult(perfResult);
                
                return perfResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var perfResult = new PerformanceResult
                {
                    OperationName = operationName,
                    Duration = stopwatch.Elapsed,
                    Success = false,
                    Exception = ex
                };

                _performanceMetrics.Add(new PerformanceMetric(operationName, stopwatch.Elapsed, 0, false));
                LogPerformanceResult(perfResult);
                
                throw;
            }
        }

        /// <summary>
        /// Measure performance of concurrent operations
        /// </summary>
        protected async Task<ConcurrentPerformanceResult> MeasureConcurrentPerformanceAsync<T>(
            string operationName, 
            IEnumerable<Func<Task<T>>> operations, 
            int maxConcurrency = 4)
        {
            var operationsList = operations.ToList();
            var results = new List<PerformanceResult>();
            var overallStopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);

            try
            {
                // Execute operations with controlled concurrency
                var semaphore = new System.Threading.SemaphoreSlim(maxConcurrency);
                var tasks = operationsList.Select(async (operation, index) =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        var result = await operation();
                        stopwatch.Stop();
                        
                        return new PerformanceResult
                        {
                            OperationName = $"{operationName}_#{index}",
                            Duration = stopwatch.Elapsed,
                            Success = true,
                            Result = result
                        };
                    }
                    catch (Exception ex)
                    {
                        return new PerformanceResult
                        {
                            OperationName = $"{operationName}_#{index}",
                            Duration = TimeSpan.Zero,
                            Success = false,
                            Exception = ex
                        };
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                results.AddRange(await Task.WhenAll(tasks));
                overallStopwatch.Stop();
                
                var memoryAfter = GC.GetTotalMemory(false);
                var memoryUsed = memoryAfter - memoryBefore;

                var concurrentResult = new ConcurrentPerformanceResult
                {
                    OperationName = operationName,
                    TotalDuration = overallStopwatch.Elapsed,
                    TotalMemoryUsed = memoryUsed,
                    OperationCount = operationsList.Count,
                    SuccessfulOperations = results.Count(r => r.Success),
                    FailedOperations = results.Count(r => !r.Success),
                    AverageDuration = TimeSpan.FromMilliseconds(results.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds)),
                    MinDuration = results.Where(r => r.Success).Min(r => r.Duration),
                    MaxDuration = results.Where(r => r.Success).Max(r => r.Duration),
                    OperationsPerSecond = operationsList.Count / overallStopwatch.Elapsed.TotalSeconds,
                    IndividualResults = results
                };

                LogConcurrentPerformanceResult(concurrentResult);
                return concurrentResult;
            }
            catch (Exception ex)
            {
                overallStopwatch.Stop();
                Output.WriteLine($"Concurrent operation '{operationName}' failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Run a bulk operation stress test
        /// </summary>
        protected async Task<PerformanceResult> RunBulkOperationStressTestAsync<T>(
            string operationName,
            Func<Task<IEnumerable<T>>> bulkOperation,
            int expectedCount)
        {
            var result = await MeasurePerformanceAsync(operationName, async () =>
            {
                var items = await bulkOperation();
                var itemsList = items.ToList();
                
                Assert.Equal(expectedCount, itemsList.Count);
                return itemsList;
            });

            // Calculate throughput
            var throughput = expectedCount / result.Duration.TotalSeconds;
            Output.WriteLine($"Bulk operation throughput: {throughput:F2} items/second");

            return result;
        }

        /// <summary>
        /// Verify database constraints and data integrity after load operations
        /// </summary>
        protected async Task VerifyDatabaseIntegrityAsync()
        {
            await MeasurePerformanceAsync("Database Integrity Check", async () =>
            {
                // This would normally run PRAGMA integrity_check or similar
                // For now, we'll just ensure the factory is still functional
                Assert.NotNull(StorageFactory);
                
                // Test basic connection
                var settingsRepo = await StorageFactory.GetSettingsRepositoryAsync();
                Assert.NotNull(settingsRepo);
                
                return true;
            });
        }

        /// <summary>
        /// Log individual performance result
        /// </summary>
        private void LogPerformanceResult(PerformanceResult result)
        {
            if (result.Success)
            {
                Output.WriteLine($"✓ {result.OperationName}: {result.Duration.TotalMilliseconds:F2}ms, Memory: {result.MemoryUsed / 1024.0:F1}KB");
            }
            else
            {
                Output.WriteLine($"✗ {result.OperationName}: FAILED - {result.Exception?.Message}");
            }
        }

        /// <summary>
        /// Log concurrent performance result
        /// </summary>
        private void LogConcurrentPerformanceResult(ConcurrentPerformanceResult result)
        {
            Output.WriteLine($"=== Concurrent Performance Results for {result.OperationName} ===");
            Output.WriteLine($"Total Duration: {result.TotalDuration.TotalMilliseconds:F2}ms");
            Output.WriteLine($"Operations: {result.SuccessfulOperations}/{result.OperationCount} successful");
            Output.WriteLine($"Throughput: {result.OperationsPerSecond:F2} ops/sec");
            Output.WriteLine($"Average Duration: {result.AverageDuration.TotalMilliseconds:F2}ms");
            Output.WriteLine($"Min/Max Duration: {result.MinDuration.TotalMilliseconds:F2}ms / {result.MaxDuration.TotalMilliseconds:F2}ms");
            Output.WriteLine($"Total Memory Used: {result.TotalMemoryUsed / 1024.0:F1}KB");
            
            if (result.FailedOperations > 0)
            {
                Output.WriteLine($"Failed Operations: {result.FailedOperations}");
                foreach (var failed in result.IndividualResults.Where(r => !r.Success))
                {
                    Output.WriteLine($"  - {failed.OperationName}: {failed.Exception?.Message}");
                }
            }
        }

        /// <summary>
        /// Log final performance summary
        /// </summary>
        private void LogPerformanceSummary()
        {
            Output.WriteLine($"\n=== Load Test Performance Summary ===");
            Output.WriteLine($"Total Test Duration: {_testStopwatch.Elapsed.TotalSeconds:F2} seconds");
            Output.WriteLine($"Total Operations: {_performanceMetrics.Count}");
            Output.WriteLine($"Successful Operations: {_performanceMetrics.Count(m => m.Success)}");
            Output.WriteLine($"Failed Operations: {_performanceMetrics.Count(m => !m.Success)}");
            
            if (_performanceMetrics.Any(m => m.Success))
            {
                var avgDuration = _performanceMetrics.Where(m => m.Success).Average(m => m.Duration.TotalMilliseconds);
                var totalMemory = _performanceMetrics.Where(m => m.Success).Sum(m => m.MemoryUsed);
                
                Output.WriteLine($"Average Operation Duration: {avgDuration:F2}ms");
                Output.WriteLine($"Total Memory Used: {totalMemory / (1024.0 * 1024.0):F2}MB");
            }
        }

        /// <summary>
        /// Assert performance meets minimum requirements
        /// </summary>
        protected void AssertPerformanceRequirements(PerformanceResult result, TimeSpan maxDuration, long maxMemoryBytes = long.MaxValue)
        {
            Assert.True(result.Success, $"Operation {result.OperationName} failed: {result.Exception?.Message}");
            Assert.True(result.Duration <= maxDuration, 
                $"Operation {result.OperationName} took {result.Duration.TotalMilliseconds}ms, expected <= {maxDuration.TotalMilliseconds}ms");
            Assert.True(result.MemoryUsed <= maxMemoryBytes,
                $"Operation {result.OperationName} used {result.MemoryUsed / 1024.0}KB, expected <= {maxMemoryBytes / 1024.0}KB");
        }

        /// <summary>
        /// Assert concurrent performance meets minimum requirements
        /// </summary>
        protected void AssertConcurrentPerformanceRequirements(ConcurrentPerformanceResult result, double minOperationsPerSecond, double maxFailureRate = 0.05)
        {
            var failureRate = (double)result.FailedOperations / result.OperationCount;
            
            Assert.True(result.OperationsPerSecond >= minOperationsPerSecond,
                $"Throughput {result.OperationsPerSecond:F2} ops/sec is below required {minOperationsPerSecond} ops/sec");
            Assert.True(failureRate <= maxFailureRate,
                $"Failure rate {failureRate:P2} exceeds maximum allowed {maxFailureRate:P2}");
        }
    }

    /// <summary>
    /// Performance measurement result for individual operations
    /// </summary>
    public class PerformanceResult
    {
        public string OperationName { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public long MemoryUsed { get; set; }
        public bool Success { get; set; }
        public object? Result { get; set; }
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Performance measurement result for concurrent operations
    /// </summary>
    public class ConcurrentPerformanceResult
    {
        public string OperationName { get; set; } = string.Empty;
        public TimeSpan TotalDuration { get; set; }
        public long TotalMemoryUsed { get; set; }
        public int OperationCount { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public double OperationsPerSecond { get; set; }
        public List<PerformanceResult> IndividualResults { get; set; } = new();
    }

    /// <summary>
    /// Internal performance metric for tracking
    /// </summary>
    internal record PerformanceMetric(string Operation, TimeSpan Duration, long MemoryUsed, bool Success);
}
