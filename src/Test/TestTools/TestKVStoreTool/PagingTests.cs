using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DaoStudio.Plugins.KVStore;
using DaoStudio.Plugins.KVStore.Repositories;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Common.Plugins;
using Xunit;

namespace TestKVStoreTool
{
    /// <summary>
    /// Comprehensive tests for paging functionality in KVStore components
    /// </summary>
    public class PagingTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly MockHost _mockHost;

        public PagingTests()
        {
            _testConfigPath = Path.Combine(Path.GetTempPath(), "KVStorePagingTest_" + Guid.NewGuid().ToString("N"));
            _mockHost = new MockHost(_testConfigPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testConfigPath))
            {
                Directory.Delete(_testConfigPath, true);
            }
        }

        /// <summary>
        /// Helper method to setup test data in repository
        /// </summary>
        private void SetupTestData(KeyValueRepository repository, string sessionId, int count)
        {
            for (int i = 1; i <= count; i++)
            {
                repository.SetValue(sessionId, $"key{i:D3}", $"value{i}");
            }
        }

        [Fact]
        public void KeyValueRepository_GetKeysWithPaging_BasicFunctionality_ShouldWork()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "paging_basic.db");
            
            using var repository = new KeyValueRepository(dbPath, config);
            const string sessionId = "session1";
            SetupTestData(repository, sessionId, 10);

            // Act & Assert - Get first 5 items
            var page1 = repository.GetKeysWithPaging(sessionId, skip: 0, take: 5);
            Assert.Equal(5, page1.Length);
            Assert.Contains("key001", page1);
            Assert.Contains("key005", page1);

            // Act & Assert - Get next 5 items
            var page2 = repository.GetKeysWithPaging(sessionId, skip: 5, take: 5);
            Assert.Equal(5, page2.Length);
            Assert.Contains("key006", page2);
            Assert.Contains("key010", page2);

            // Act & Assert - Ensure no overlap
            Assert.Empty(page1.Intersect(page2));
        }

        [Fact]
        public void KeyValueRepository_GetKeysWithPaging_DefaultParameters_ShouldWork()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "paging_defaults.db");
            
            using var repository = new KeyValueRepository(dbPath, config);
            const string sessionId = "session1";
            SetupTestData(repository, sessionId, 5);

            // Act - Use default parameters (skip=0, take=null)
            var allKeys = repository.GetKeysWithPaging(sessionId);
            var specificKeys = repository.GetKeysWithPaging(sessionId, skip: 0);

            // Assert
            Assert.Equal(5, allKeys.Length);
            Assert.Equal(5, specificKeys.Length);
            Assert.Equal(allKeys.OrderBy(k => k), specificKeys.OrderBy(k => k));

            // Verify all expected keys are present
            for (int i = 1; i <= 5; i++)
            {
                Assert.Contains($"key{i:D3}", allKeys);
            }
        }

        [Fact]
        public void KeyValueRepository_GetKeysWithPaging_NullTakeParameter_ShouldReturnAllRemaining()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "paging_null_take.db");
            
            using var repository = new KeyValueRepository(dbPath, config);
            const string sessionId = "session1";
            SetupTestData(repository, sessionId, 10);

            // Act - Skip first 3, take all remaining (null take)
            var remainingKeys = repository.GetKeysWithPaging(sessionId, skip: 3, take: null);

            // Assert - Should return 7 remaining keys
            Assert.Equal(7, remainingKeys.Length);
            Assert.DoesNotContain("key001", remainingKeys);
            Assert.DoesNotContain("key002", remainingKeys);
            Assert.DoesNotContain("key003", remainingKeys);
            Assert.Contains("key004", remainingKeys);
            Assert.Contains("key010", remainingKeys);
        }

        [Fact]
        public void KeyValueRepository_GetKeysWithPaging_EdgeCases_ShouldHandleGracefully()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "paging_edge_cases.db");
            
            using var repository = new KeyValueRepository(dbPath, config);
            const string sessionId = "session1";
            SetupTestData(repository, sessionId, 5);

            // Test negative skip - should be treated as 0
            var negativeSkip = repository.GetKeysWithPaging(sessionId, skip: -5, take: 3);
            Assert.Equal(3, negativeSkip.Length);

            // Test skip beyond available data
            var beyondData = repository.GetKeysWithPaging(sessionId, skip: 10, take: 5);
            Assert.Empty(beyondData);

            // Test take more than available
            var takeMoreThanAvailable = repository.GetKeysWithPaging(sessionId, skip: 3, take: 10);
            Assert.Equal(2, takeMoreThanAvailable.Length); // Only 2 items remain after skipping 3

            // Test zero take - should return empty
            var zeroTake = repository.GetKeysWithPaging(sessionId, skip: 0, take: 0);
            Assert.Empty(zeroTake);

            // Test negative take - should return empty
            var negativeTake = repository.GetKeysWithPaging(sessionId, skip: 0, take: -5);
            Assert.Empty(negativeTake);
        }

        [Fact]
        public void KeyValueRepository_GetKeysWithPaging_EmptySession_ShouldReturnEmpty()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "paging_empty.db");
            
            using var repository = new KeyValueRepository(dbPath, config);
            const string sessionId = "emptySession";

            // Act
            var emptyResult = repository.GetKeysWithPaging(sessionId, skip: 0, take: 10);

            // Assert
            Assert.Empty(emptyResult);
        }

        [Fact]
        public void KeyValueRepository_GetKeysWithPaging_NullEmptySessionId_ShouldReturnEmpty()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "paging_null_session.db");
            
            using var repository = new KeyValueRepository(dbPath, config);

            // Act & Assert
            Assert.Empty(repository.GetKeysWithPaging(null!, skip: 0, take: 10));
            Assert.Empty(repository.GetKeysWithPaging("", skip: 0, take: 10));
            Assert.Empty(repository.GetKeysWithPaging("   ", skip: 0, take: 10));
        }

        [Fact]
        public void KeyValueStoreData_GetKeysWithPaging_ShouldWork()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            using var storeData = new KeyValueStoreData(config);
            var dbPath = Path.Combine(_testConfigPath, "store_paging.db");
            const string sessionId = "session1";

            storeData.Initialize(dbPath, sessionId);

            // Setup test data
            for (int i = 1; i <= 8; i++)
            {
                storeData.SetValue(sessionId, $"key{i:D2}", $"value{i}");
            }

            // Act & Assert - Test paging
            var page1 = storeData.GetKeysWithPaging(sessionId, skip: 0, take: 3);
            Assert.Equal(3, page1.Length);

            var page2 = storeData.GetKeysWithPaging(sessionId, skip: 3, take: 3);
            Assert.Equal(3, page2.Length);

            var page3 = storeData.GetKeysWithPaging(sessionId, skip: 6, take: 3);
            Assert.Equal(2, page3.Length); // Only 2 items remain

            // Ensure no overlaps
            Assert.Empty(page1.Intersect(page2));
            Assert.Empty(page1.Intersect(page3));
            Assert.Empty(page2.Intersect(page3));

            // Verify total count
            var allPaged = page1.Concat(page2).Concat(page3).ToArray();
            Assert.Equal(8, allPaged.Length);
        }

        [Fact]
        public void KeyValueStoreData_GetKeysWithPaging_DefaultParameters_ShouldReturnAll()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            using var storeData = new KeyValueStoreData(config);
            var dbPath = Path.Combine(_testConfigPath, "store_paging_defaults.db");
            const string sessionId = "session1";

            storeData.Initialize(dbPath, sessionId);

            // Setup test data
            for (int i = 1; i <= 5; i++)
            {
                storeData.SetValue(sessionId, $"key{i}", $"value{i}");
            }

            // Act
            var allKeysDefault = storeData.GetKeysWithPaging(sessionId);
            var allKeysExplicit = storeData.GetKeys(sessionId);

            // Assert - Should return the same results
            Assert.Equal(5, allKeysDefault.Length);
            Assert.Equal(allKeysExplicit.OrderBy(k => k), allKeysDefault.OrderBy(k => k));
        }

        [Fact]
        public async Task KVStoreHandler_ListKeys_WithPaging_ShouldWork()
        {
            // Arrange
            var pluginInfo = new PlugToolInfo 
            { 
                InstanceId = 1,
                DisplayName = "TestKVStore",
                Config = null
            };

            using var plugin = new KeyValueStorePluginInstance(_mockHost, pluginInfo);
            using var session = new MockHostSession { Id = 12345 };

            // Set up the plugin with a session
            var method = plugin.GetType().GetMethod("RegisterToolFunctionsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var toolFunctions = new List<FunctionWithDescription>();
            await (Task)method!.Invoke(plugin, new object[] { toolFunctions, null!, session })!;
            
            var handler = new KVStoreHandler(plugin);

            // Setup test data - add 15 keys
            for (int i = 1; i <= 15; i++)
            {
                await handler.SetValue($"key{i:D2}", $"value{i}");
            }

            // Act & Assert - Test default paging (page 0, size 50)
            var defaultPage = await handler.ListKeys();
            Assert.Equal(10, defaultPage.Length); // All keys fit in default page size

            // Act & Assert - Test first page of 5 (page 0)
            var page0 = await handler.ListKeys(page: 0, pageSize: 5);
            Assert.Equal(5, page0.Length);

            // Act & Assert - Test second page of 5 (page 1)
            var page1 = await handler.ListKeys(page: 1, pageSize: 5);
            Assert.Equal(5, page1.Length);

            // Act & Assert - Test third page of 5 (page 2)
            var page2 = await handler.ListKeys(page: 2, pageSize: 5);
            Assert.Equal(5, page2.Length);

            // Act & Assert - Test fourth page (page 3, should be empty)
            var page3 = await handler.ListKeys(page: 3, pageSize: 5);
            Assert.Empty(page3);

            // Verify no overlaps between pages
            Assert.Empty(page0.Intersect(page1));
            Assert.Empty(page0.Intersect(page2));
            Assert.Empty(page1.Intersect(page2));

            // Verify all keys are covered
            var allPagedKeys = page0.Concat(page1).Concat(page2).ToArray();
            Assert.Equal(15, allPagedKeys.Length);
        }

        [Fact]
        public async Task KVStoreHandler_ListKeys_EdgeCases_ShouldHandleGracefully()
        {
            // Arrange
            var pluginInfo = new PlugToolInfo 
            { 
                InstanceId = 1,
                DisplayName = "TestKVStore",
                Config = null
            };

            using var plugin = new KeyValueStorePluginInstance(_mockHost, pluginInfo);
            using var session = new MockHostSession { Id = 12345 };

            var method = plugin.GetType().GetMethod("RegisterToolFunctionsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var toolFunctions = new List<FunctionWithDescription>();
            await (Task)method!.Invoke(plugin, new object[] { toolFunctions, null!, session })!;
            
            var handler = new KVStoreHandler(plugin);

            // Setup minimal test data
            await handler.SetValue("testKey", "testValue");

            // Test invalid page numbers (should be corrected to 0)
            var negativePage = await handler.ListKeys(page: -5, pageSize: 10);
            var zeroPage = await handler.ListKeys(page: 0, pageSize: 10);
            var normalPage = await handler.ListKeys(page: 0, pageSize: 10);

            Assert.Single(negativePage);
            Assert.Single(zeroPage);
            Assert.Single(normalPage);
            Assert.Equal(normalPage, negativePage);
            Assert.Equal(normalPage, zeroPage);

            // Test invalid page sizes (should be corrected to 1)
            var negativeSize = await handler.ListKeys(page: 0, pageSize: -10);
            var zeroSize = await handler.ListKeys(page: 0, pageSize: 0);

            Assert.Single(negativeSize);
            Assert.Single(zeroSize);

            // Test page beyond available data
            var beyondData = await handler.ListKeys(page: 100, pageSize: 10);
            Assert.Empty(beyondData);
        }

        [Fact]
        public void KeyValueRepository_GetKeysWithPaging_SessionIsolation_ShouldWork()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "paging_isolation.db");
            
            using var repository = new KeyValueRepository(dbPath, config);
            const string session1 = "session1";
            const string session2 = "session2";

            // Setup different data in each session with different key patterns
            for (int i = 1; i <= 10; i++)
            {
                repository.SetValue(session1, $"s1_key{i:D3}", $"value{i}");
            }
            for (int i = 1; i <= 5; i++)
            {
                repository.SetValue(session2, $"s2_key{i:D3}", $"value{i}");
            }

            // Act & Assert - Session 1 paging
            var session1Page1 = repository.GetKeysWithPaging(session1, skip: 0, take: 5);
            var session1Page2 = repository.GetKeysWithPaging(session1, skip: 5, take: 5);
            Assert.Equal(5, session1Page1.Length);
            Assert.Equal(5, session1Page2.Length);

            // Act & Assert - Session 2 paging
            var session2Page1 = repository.GetKeysWithPaging(session2, skip: 0, take: 3);
            var session2Page2 = repository.GetKeysWithPaging(session2, skip: 3, take: 3);
            Assert.Equal(3, session2Page1.Length);
            Assert.Equal(2, session2Page2.Length); // Only 2 remaining

            // Ensure session isolation - no keys should overlap between sessions
            Assert.Empty(session1Page1.Intersect(session2Page1));
            Assert.Empty(session1Page2.Intersect(session2Page2));
            
            // Verify each session's keys have the correct prefixes
            Assert.All(session1Page1.Concat(session1Page2), key => Assert.StartsWith("s1_", key));
            Assert.All(session2Page1.Concat(session2Page2), key => Assert.StartsWith("s2_", key));
        }

        [Fact]
        public void KeyValueRepository_GetKeysWithPaging_Persistence_ShouldWork()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "paging_persistence.db");
            const string sessionId = "session1";

            // Create repository, add data, and test paging
            using (var repository1 = new KeyValueRepository(dbPath, config))
            {
                SetupTestData(repository1, sessionId, 12);
                
                var page1 = repository1.GetKeysWithPaging(sessionId, skip: 0, take: 4);
                Assert.Equal(4, page1.Length);
            }

            // Create new repository instance and verify paging still works
            using (var repository2 = new KeyValueRepository(dbPath, config))
            {
                var page1 = repository2.GetKeysWithPaging(sessionId, skip: 0, take: 4);
                var page2 = repository2.GetKeysWithPaging(sessionId, skip: 4, take: 4);
                var page3 = repository2.GetKeysWithPaging(sessionId, skip: 8, take: 4);

                Assert.Equal(4, page1.Length);
                Assert.Equal(4, page2.Length);
                Assert.Equal(4, page3.Length);

                // Verify total count is preserved
                var allKeys = page1.Concat(page2).Concat(page3).ToArray();
                Assert.Equal(12, allKeys.Length);
            }
        }

        [Fact]
        public void KeyValueRepository_GetKeysWithPaging_LargeDataset_ShouldPerformWell()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "paging_large.db");
            
            using var repository = new KeyValueRepository(dbPath, config);
            const string sessionId = "session1";

            // Setup larger dataset (1000 keys) with 4-digit padding for proper sorting
            for (int i = 1; i <= 1000; i++)
            {
                repository.SetValue(sessionId, $"key{i:D4}", $"value{i}");
            }

            // Act & Assert - Test various page sizes
            var page1 = repository.GetKeysWithPaging(sessionId, skip: 0, take: 100);     // keys 0001-0100
            var page5 = repository.GetKeysWithPaging(sessionId, skip: 400, take: 100);   // keys 0401-0500
            var page10 = repository.GetKeysWithPaging(sessionId, skip: 900, take: 100);  // keys 0901-1000

            Assert.Equal(100, page1.Length);
            Assert.Equal(100, page5.Length);
            Assert.Equal(100, page10.Length);

            // Verify specific keys are in expected pages
            // Page 1 should contain keys 0001-0100
            Assert.Contains("key0001", page1);
            Assert.Contains("key0100", page1);
            Assert.DoesNotContain("key0101", page1);

            // Page 5 should contain keys 0401-0500
            Assert.Contains("key0401", page5);
            Assert.Contains("key0500", page5);
            Assert.DoesNotContain("key0400", page5);
            Assert.DoesNotContain("key0501", page5);

            // Page 10 should contain keys 0901-1000
            Assert.Contains("key0901", page10);
            Assert.Contains("key1000", page10);
            Assert.DoesNotContain("key0900", page10);
            
            // Verify pages don't overlap
            Assert.Empty(page1.Intersect(page5));
            Assert.Empty(page1.Intersect(page10));
            Assert.Empty(page5.Intersect(page10));
        }
    }
}