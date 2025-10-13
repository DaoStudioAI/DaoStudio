using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using DaoStudio.Plugins.KVStore;
using DaoStudio.Plugins.KVStore.Repositories;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Common.Plugins;
using Xunit;

namespace TestKVStoreTool
{
    /// <summary>
    /// Mock implementation of IHost for testing
    /// </summary>
    public class MockHost : IHost
    {
        private readonly string _configPath;

        public MockHost(string configPath)
        {
            _configPath = configPath;
        }

        public string GetPluginConfigureFolderPath(string pluginId)
        {
            var path = Path.Combine(_configPath, pluginId);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        public Task<List<IPerson>> GetPersonsAsync(string? name) => throw new NotImplementedException();
        public Task<ISession> StartNewSession(long? parentSessId, string? personName = null) => throw new NotImplementedException();
        public Task<ISession> OpenHostSession(long sessionId) => throw new NotImplementedException();
        public Task<IHostSession> StartNewHostSessionAsync(IHostSession? parent, string? personName = null) => throw new NotImplementedException();
        public Task<List<IHostPerson>> GetHostPersonsAsync(string? name) => throw new NotImplementedException();
    }

    /// <summary>
    /// Mock implementation of IHostSession for testing
    /// </summary>
    public class MockHostSession : IHostSession
    {
        public long Id { get; set; } = 12345;
        public long? ParentSessionId { get; set; }
        public CancellationTokenSource? CurrentCancellationToken { get; set; } = new CancellationTokenSource();
        public ToolExecutionMode ToolExecutionMode { get; set; } = ToolExecutionMode.Auto;
        
        public List<(HostSessMsgType msgType, string message)> SentMessages { get; } = new();

        public Task SendMessageAsync(HostSessMsgType msgType, string message)
        {
            SentMessages.Add((msgType, message));
            return Task.CompletedTask;
        }

        public Dictionary<string, List<FunctionWithDescription>>? GetTools() => null;
        public void SetTools(Dictionary<string, List<FunctionWithDescription>> tools) { }
        public Task<List<IHostPerson>?> GetPersonsAsync() => Task.FromResult<List<IHostPerson>?>(null);
    // GetUnderlyingSession removed from IHostSession - not required for these tests.

        public void Dispose()
        {
            CurrentCancellationToken?.Dispose();
        }
    }

    /// <summary>
    /// Core tests for KVStore functionality with LiteDB
    /// </summary>
    public class KVStoreTests : IDisposable
    {
        private readonly string _testConfigPath;
        private readonly MockHost _mockHost;

        public KVStoreTests()
        {
            _testConfigPath = Path.Combine(Path.GetTempPath(), "KVStoreTest_" + Guid.NewGuid().ToString("N"));
            _mockHost = new MockHost(_testConfigPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testConfigPath))
            {
                Directory.Delete(_testConfigPath, true);
            }
        }

        [Fact]
        public void KeyValueRepository_BasicOperations_ShouldWork()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "basic_test.db");
            
            using var repository = new KeyValueRepository(dbPath, config);
            const string sessionId = "session1";
            const string key = "testKey";
            const string value = "testValue";

            // Act & Assert - Set value
            var setResult = repository.SetValue(sessionId, key, value);
            Assert.True(setResult);

            // Act & Assert - Get value
            var getResult = repository.TryGetValue(sessionId, key, out var retrievedValue);
            Assert.True(getResult);
            Assert.Equal(value, retrievedValue);

            // Act & Assert - Key exists
            var keyExists = repository.KeyExists(sessionId, key);
            Assert.True(keyExists);

            // Act & Assert - Get keys
            var keys = repository.GetKeys(sessionId);
            Assert.Single(keys);
            Assert.Equal(key, keys[0]);

            // Act & Assert - Delete key
            var deleteResult = repository.DeleteKey(sessionId, key);
            Assert.True(deleteResult);

            // Act & Assert - Verify deletion
            var getAfterDelete = repository.TryGetValue(sessionId, key, out _);
            Assert.False(getAfterDelete);
        }

        [Fact]
        public void KeyValueRepository_SessionIsolation_ShouldWork()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "isolation_test.db");
            
            using var repository = new KeyValueRepository(dbPath, config);
            const string session1 = "session1";
            const string session2 = "session2";
            const string key = "sharedKey";
            const string value1 = "value1";
            const string value2 = "value2";

            // Act - Set values in different sessions
            repository.SetValue(session1, key, value1);
            repository.SetValue(session2, key, value2);

            // Assert - Values should be isolated by session
            repository.TryGetValue(session1, key, out var retrievedValue1);
            repository.TryGetValue(session2, key, out var retrievedValue2);

            Assert.Equal(value1, retrievedValue1);
            Assert.Equal(value2, retrievedValue2);

            // Assert - Key counts should be independent
            Assert.Equal(1, repository.GetKeyCount(session1));
            Assert.Equal(1, repository.GetKeyCount(session2));
        }

        [Fact]
        public void KeyValueRepository_Persistence_ShouldWork()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "persistence.db");
            const string sessionId = "session1";
            const string key = "persistentKey";
            const string value = "persistentValue";

            // Act - Create repository, set value, and dispose
            using (var repository1 = new KeyValueRepository(dbPath, config))
            {
                repository1.SetValue(sessionId, key, value);
            }

            // Act - Create new repository instance and try to retrieve the value
            using (var repository2 = new KeyValueRepository(dbPath, config))
            {
                var getResult = repository2.TryGetValue(sessionId, key, out var retrievedValue);
                
                // Assert - Value should be persisted
                Assert.True(getResult);
                Assert.Equal(value, retrievedValue);
            }
        }

        [Fact]
        public void KeyValueStoreData_WithRepository_ShouldWork()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            using var storeData = new KeyValueStoreData(config);
            var dbPath = Path.Combine(_testConfigPath, "data_integration.db");
            const string sessionId = "session1";

            // Act - Initialize with database path
            storeData.Initialize(dbPath, sessionId);

            // Act & Assert - Basic operations
            var setResult = storeData.SetValue(sessionId, "key1", "value1");
            Assert.True(setResult);

            var getResult = storeData.TryGetValue(sessionId, "key1", out var value);
            Assert.True(getResult);
            Assert.Equal("value1", value);

            var keys = storeData.GetKeys(sessionId);
            Assert.Single(keys);
            Assert.Equal("key1", keys[0]);

            var deleteResult = storeData.DeleteKey(sessionId, "key1");
            Assert.True(deleteResult);

            var keysAfterDelete = storeData.GetKeys(sessionId);
            Assert.Empty(keysAfterDelete);
        }

        [Fact]
        public async Task KVStoreHandler_BasicOperations_ShouldWork()
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

            // Act & Assert - Set a value
            var setResult = await handler.SetValue("testKey", "testValue");
            Assert.True(setResult);

            // Get the value
            var getValue = await handler.GetValue("testKey");
            Assert.Equal("testValue", getValue);

            // List keys
            var keys = await handler.ListKeys();
            Assert.Single(keys);
            Assert.Equal("testKey", keys[0]);

            // Check if key exists
            var keyExists = await handler.IsKeyExist("testKey");
            Assert.True(keyExists);

            // Delete the key
            var deleteResult = await handler.DeleteKey("testKey");
            Assert.True(deleteResult);

            // Verify deletion
            var keyExistsAfterDelete = await handler.IsKeyExist("testKey");
            Assert.False(keyExistsAfterDelete);
        }

        [Fact]
        public void KeyValueRepository_EdgeCases_ShouldHandleGracefully()
        {
            // Arrange
            var config = new KeyValueStoreConfig { IsCaseSensitive = true };
            var dbPath = Path.Combine(_testConfigPath, "edge_cases.db");
            
            using var repository = new KeyValueRepository(dbPath, config);

            // Test null/empty parameters
            Assert.False(repository.SetValue("", "key", "value"));
            Assert.False(repository.SetValue("session", "", "value"));
            Assert.False(repository.SetValue(null!, "key", "value"));
            Assert.False(repository.SetValue("session", null!, "value"));

            Assert.False(repository.TryGetValue("", "key", out _));
            Assert.False(repository.TryGetValue("session", "", out _));
            Assert.False(repository.TryGetValue(null!, "key", out _));
            Assert.False(repository.TryGetValue("session", null!, out _));

            Assert.Empty(repository.GetKeys(""));
            Assert.Empty(repository.GetKeys(null!));

            Assert.False(repository.DeleteKey("", "key"));
            Assert.False(repository.DeleteKey("session", ""));
            Assert.False(repository.DeleteKey(null!, "key"));
            Assert.False(repository.DeleteKey("session", null!));

            // Test non-existent operations
            Assert.False(repository.DeleteKey("session", "nonExistentKey"));
            Assert.False(repository.TryGetValue("session", "nonExistentKey", out _));
        }

        [Fact]
        public async Task KeyValueStorePluginInstance_WithInstanceName_ShouldPrefixFunctionNames()
        {
            // Arrange
            var config = new KeyValueStoreConfig
            {
                IsCaseSensitive = true,
                InstanceName = "testInstance"
            };

            var plugToolInfo = new PlugToolInfo
            {
                Config = System.Text.Json.JsonSerializer.Serialize(config),
                DisplayName = "Test KV Store"
            };

            var host = new MockHost(_testConfigPath);
            var pluginInstance = new KeyValueStorePluginInstance(host, plugToolInfo);
            var functions = new List<FunctionWithDescription>();

            // Act
            await pluginInstance.GetSessionFunctionsAsync(functions, null, new MockHostSession());

            // Assert
            Assert.NotEmpty(functions);
            
            // All function names should be prefixed with "testInstance_"
            foreach (var function in functions)
            {
                Assert.StartsWith("testInstance_", function.Description.Name);
                Assert.Contains("[testInstance]", function.Description.Description);
            }
            
            // Check specific functions exist with correct prefixes
            Assert.Contains(functions, f => f.Description.Name == "testInstance_kv_get_value");
            Assert.Contains(functions, f => f.Description.Name == "testInstance_kv_set_value");
            Assert.Contains(functions, f => f.Description.Name == "testInstance_kv_list_keys");
            Assert.Contains(functions, f => f.Description.Name == "testInstance_kv_delete_key");
            Assert.Contains(functions, f => f.Description.Name == "testInstance_kv_is_key_exist");
        }

        [Fact]
        public async Task KeyValueStorePluginInstance_WithoutInstanceName_ShouldNotPrefixFunctionNames()
        {
            // Arrange
            var config = new KeyValueStoreConfig
            {
                IsCaseSensitive = true,
                InstanceName = null // No instance name
            };

            var plugToolInfo = new PlugToolInfo
            {
                Config = System.Text.Json.JsonSerializer.Serialize(config),
                DisplayName = "Test KV Store"
            };

            var host = new MockHost(_testConfigPath);
            var pluginInstance = new KeyValueStorePluginInstance(host, plugToolInfo);
            var functions = new List<FunctionWithDescription>();

            // Act
            await pluginInstance.GetSessionFunctionsAsync(functions, null, new MockHostSession());

            // Assert
            Assert.NotEmpty(functions);
            
            // Function names should NOT be prefixed
            foreach (var function in functions)
            {
                Assert.DoesNotContain("_kv_", function.Description.Name);
                Assert.DoesNotContain("[", function.Description.Description); // No instance name in brackets
            }
            
            // Check specific functions exist without prefixes
            Assert.Contains(functions, f => f.Description.Name == "kv_get_value");
            Assert.Contains(functions, f => f.Description.Name == "kv_set_value");
            Assert.Contains(functions, f => f.Description.Name == "kv_list_keys");
            Assert.Contains(functions, f => f.Description.Name == "kv_delete_key");
            Assert.Contains(functions, f => f.Description.Name == "kv_is_key_exist");
        }
    }
}