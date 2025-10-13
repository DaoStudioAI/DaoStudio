using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Repositories;
using Xunit;

namespace Test.TestStorage
{
    public class TestLlmToolRepository : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ILlmToolRepository _toolRepository;

        public TestLlmToolRepository()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_tool_repo_{Guid.NewGuid()}.db");
            
            // Initialize repository directly with SqliteLlmToolRepository
            _toolRepository = new SqliteLlmToolRepository(_testDbPath);
        }

        public void Dispose()
        {
            // Clean up the test database after tests
            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                }
                catch
                {
                    // Ignore deletion errors during cleanup
                }
            }
        }

        [Fact]
        public async Task GetToolReturnsNullForNonExistentTool()
        {
            // Arrange - nothing to arrange

            // Act
            var tool = await _toolRepository.GetToolAsync(999);

            // Assert
            Assert.Null(tool);
        }

        [Fact]
        public async Task SaveAndGetToolWorks()
        {
            // Arrange
            var newTool = new LlmTool
            {
                Name = "Web Search Tool",
                Description = "Search the web for information",
                ToolConfig = "{ \"endpoint\": \"https://api.search.com\" }",
                ToolType = 0, // 0=Normal // Assuming 1 = WebSearch
                Parameters = new Dictionary<string, string> 
                { 
                    { "Param1", "Value1" },
                    { "Param2", "Value2" }
                },
                IsEnabled = true,
                LastModified = DateTime.UtcNow,
                State = 0, // 0=Stateless
                StateData = null,
                AppId = 12345 // Test the new AppId field
            };

            // Act - Create
            var createdTool = await _toolRepository.CreateToolAsync(newTool);
            var retrievedTool = await _toolRepository.GetToolAsync(createdTool.Id);

            // Assert - Create
             Assert.NotNull(createdTool);
            Assert.NotNull(retrievedTool);
            Assert.Equal(newTool.Id, retrievedTool.Id);
            Assert.Equal("Web Search Tool", retrievedTool.Name);
            Assert.Equal("Search the web for information", retrievedTool.Description);
            Assert.Equal("{ \"endpoint\": \"https://api.search.com\" }", retrievedTool.ToolConfig);
            Assert.Equal(0, retrievedTool.ToolType); // 0=Normal
            Assert.Equal("Value1", retrievedTool.Parameters["Param1"]);
            Assert.Equal("Value2", retrievedTool.Parameters["Param2"]);
            Assert.True(retrievedTool.IsEnabled);
            Assert.Equal(0, retrievedTool.State); // 0=Stateless
            Assert.Null(retrievedTool.StateData);
            Assert.Equal(12345, retrievedTool.AppId); // Test the new AppId field

            // Act - Update
            retrievedTool.Name = "Updated Test Tool";
            var t = retrievedTool.LastModified;
            var updateResult = await _toolRepository.SaveToolAsync(retrievedTool);
            var updatedTool = await _toolRepository.GetToolAsync(retrievedTool.Id);            // Assert - Update
            Assert.True(updateResult);
            Assert.Equal("Updated Test Tool", updatedTool!.Name);
            Assert.Equal(retrievedTool.Id, updatedTool.Id);
            Assert.Equal(retrievedTool.StaticId, updatedTool.StaticId);
            Assert.Equal(retrievedTool.Description, updatedTool.Description);
            Assert.Equal(retrievedTool.ToolConfig, updatedTool.ToolConfig);
            Assert.Equal(retrievedTool.ToolType, updatedTool.ToolType);
            Assert.Equal(retrievedTool.Parameters, updatedTool.Parameters);
            Assert.Equal(retrievedTool.IsEnabled, updatedTool.IsEnabled);
            Assert.Equal(retrievedTool.LastModified.ToLocalTime(), updatedTool.LastModified.ToLocalTime());
            Assert.Equal(retrievedTool.State, updatedTool.State);
            Assert.Equal(retrievedTool.StateData, updatedTool.StateData);

            Assert.Equal(retrievedTool.CreatedAt, updatedTool.CreatedAt);
            Assert.NotEqual(t, updatedTool.LastModified);
        }

        [Fact]
        public async Task GetAllToolsReturnsAllTools()
        {
            // Arrange
            var tool1 = new LlmTool
            {
                Name = "Tool 1",
                Description = "First tool",
                ToolConfig = "{ \"config\": \"value\" }",
                ToolType = 0, // 0=Normal
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true,
                LastModified = DateTime.UtcNow,
                State = 0 // 0=Stateless
            };
            
            var tool2 = new LlmTool
            {
                Name = "Tool 2",
                Description = "Second tool",
                ToolConfig = "{ \"config\": \"other\" }",
                ToolType = 0, // 0=Normal
                Parameters = new Dictionary<string, string> { { "Param", "Other" } },
                IsEnabled = false,
                LastModified = DateTime.UtcNow,
                State = 0 // 0=Stateless
            };
            
            await _toolRepository.CreateToolAsync(tool1);
            await _toolRepository.CreateToolAsync(tool2);

            // Act
            var allTools = await _toolRepository.GetAllToolsAsync();

            // Assert
            Assert.Equal(2, allTools.Count());
            Assert.Contains(allTools, t => t.Name == "Tool 1");
            Assert.Contains(allTools, t => t.Name == "Tool 2");
        }

        [Fact]
        public async Task DeleteToolWorks()
        {
            // Arrange
            var tool = new LlmTool
            {
                Name = "Tool to delete",
                Description = "Will be deleted",
                ToolConfig = "{ \"config\": \"value\" }",
                ToolType = 0, // 0=Normal
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true,
                LastModified = DateTime.UtcNow,
                State = 0 // 0=Stateless
            };
            
            var createdTool = await _toolRepository.CreateToolAsync(tool);

            // Act
            var deleteResult = await _toolRepository.DeleteToolAsync(createdTool.Id);
            var retrievedTool = await _toolRepository.GetToolAsync(createdTool.Id);

            // Assert
            Assert.True(deleteResult);
            Assert.Null(retrievedTool);
        }

        [Fact]
        public async Task UpdateToolWorks()
        {
            // Arrange
            var tool = new LlmTool
            {
                Name = "Original Tool",
                Description = "Original description",
                ToolConfig = "{ \"config\": \"original\" }",
                ToolType = 0, // 0=Normal
                Parameters = new Dictionary<string, string> { { "Param", "Original" } },
                IsEnabled = true,
                LastModified = DateTime.UtcNow,
                State = 0 // 0=Stateless
            };
            
            var createdTool = await _toolRepository.CreateToolAsync(tool);
            
            // Update the tool
            createdTool.Name = "Updated Tool";
            createdTool.Description = "Updated description";
            createdTool.ToolConfig = "{ \"config\": \"updated\" }";
            createdTool.ToolType = 0; // 0=Normal
            createdTool.Parameters = new Dictionary<string, string> { { "Param", "Updated" }, { "NewParam", 42.ToString() } };
            createdTool.IsEnabled = false;
            createdTool.LastModified = DateTime.UtcNow;
            createdTool.State = 1; // 1=Stateful
            createdTool.StateData = new byte[] { 1, 2, 3, 4 };

            // Act
            var updateResult = await _toolRepository.SaveToolAsync(createdTool);
            var retrievedTool = await _toolRepository.GetToolAsync(createdTool.Id);

            // Assert
            Assert.True(updateResult);
            Assert.NotNull(retrievedTool);
            Assert.Equal("Updated Tool", retrievedTool.Name);
            Assert.Equal("Updated description", retrievedTool.Description);
            Assert.Equal("{ \"config\": \"updated\" }", retrievedTool.ToolConfig);
            Assert.Equal(0, retrievedTool.ToolType); // 0=Normal
            Assert.Equal("Updated", retrievedTool.Parameters["Param"]);
            Assert.Equal(42.ToString(), retrievedTool.Parameters["NewParam"]);
            Assert.False(retrievedTool.IsEnabled);
            Assert.Equal(1, retrievedTool.State); // 1=Stateful
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, retrievedTool.StateData);
        }

        [Fact]
        public async Task CreateToolWithExistingIdThrowsException()
        {
            // Arrange
            var tool = new LlmTool
            {
                Id = 1, // Set an existing ID
                Name = "Test Tool",
            };

            // Act & Assert
            var ret = await _toolRepository.CreateToolAsync(tool);
            Assert.NotNull(ret);
            Assert.NotEqual(1, ret.Id);
            Assert.NotEqual(0, ret.Id);
            Assert.Equal("Test Tool", ret.Name);
        }

        [Fact]
        public async Task SaveToolWithoutIdThrowsException()
        {
            // Arrange
            var tool = new LlmTool
            {
                Id = 0, // Invalid ID for save
                Name = "Test Tool",
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _toolRepository.SaveToolAsync(tool));
        }



        [Fact]
        public async Task StaticIdFieldIsPersisted()
        {
            // Arrange
            var staticId = "com.agent.test-tool";
            
            var tool = new LlmTool
            {
                Name = "Static ID Tool",
                Description = "Tool with static ID",
                StaticId = staticId,
                ToolConfig = "{}",
                ToolType = 0, // 0=Normal
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true,
                LastModified = DateTime.UtcNow,
                State = 0 // 0=Stateless
            };
            
            // Act
            var createdTool = await _toolRepository.CreateToolAsync(tool);
            var retrievedTool = await _toolRepository.GetToolAsync(createdTool.Id);

            // Assert
            Assert.NotNull(retrievedTool);
            Assert.Equal(staticId, retrievedTool.StaticId);
            Assert.Equal("Static ID Tool", retrievedTool.Name);
        }        [Fact]
        public async Task ToolStateDataPersistsCorrectly()
        {
            // Arrange
            var stateData = new byte[] { 1, 2, 3, 4, 5 };
            var tool = new LlmTool
            {
                Name = "StatefulTool",
                Description = "Tool with state",
                StaticId = "com.test.stateful",
                ToolConfig = "{}",
                ToolType = 0, // 0=Normal
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                State = 1, // 1=Stateful
                StateData = stateData
            };

            // Act
            var createdTool = await _toolRepository.CreateToolAsync(tool);
            var retrievedTool = await _toolRepository.GetToolAsync(createdTool.Id);

            // Assert
            Assert.NotNull(retrievedTool);
            Assert.Equal(1, retrievedTool.State); // 1=Stateful
            Assert.Equal(stateData, retrievedTool.StateData);

            // Act - update with new state
            var newStateData = new byte[] { 6, 7, 8, 9, 10 };
            retrievedTool.StateData = newStateData;
            await _toolRepository.SaveToolAsync(retrievedTool);
            var updatedTool = await _toolRepository.GetToolAsync(retrievedTool.Id);

            // Assert - updated state
            Assert.Equal(newStateData, updatedTool?.StateData);
        }
          [Fact]
        public async Task GetToolsByStaticIdAsyncWorks()
        {
            // Arrange
            var tool = new LlmTool
            {
                Name = "Tool With Static ID",
                Description = "Test tool with a static ID",
                StaticId = "com.test.uniquestatic",
                ToolConfig = "{ \"config\": \"value\" }",
                ToolType = 0, // 0=Normal
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true,
                State = 0 // 0=Stateless
            };
            
            await _toolRepository.CreateToolAsync(tool);

            // Act
            var retrievedTools = await _toolRepository.GetToolsByStaticIdAsync("com.test.uniquestatic");
            var nonExistentTools = await _toolRepository.GetToolsByStaticIdAsync("com.test.nonexistent");

            // Assert
            Assert.Single(retrievedTools);
            var retrievedTool = retrievedTools.First();
            Assert.Equal("Tool With Static ID", retrievedTool.Name);
            Assert.Equal("com.test.uniquestatic", retrievedTool.StaticId);
            Assert.Empty(nonExistentTools);
        }

        [Fact]
        public async Task AppIdFieldIsPersisted()
        {
            // Arrange
            var appId = 98765L;
            
            var tool = new LlmTool
            {
                Name = "AppId Tool",
                Description = "Tool with AppId",
                StaticId = "com.test.appid-tool",
                ToolConfig = "{}",
                ToolType = 0, // 0=Normal
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                LastModified = DateTime.UtcNow,
                State = 0, // 0=Stateless
                AppId = appId
            };
            
            // Act
            var createdTool = await _toolRepository.CreateToolAsync(tool);
            var retrievedTool = await _toolRepository.GetToolAsync(createdTool.Id);

            // Assert
            Assert.NotNull(retrievedTool);
            Assert.Equal(appId, retrievedTool.AppId);
            Assert.Equal("AppId Tool", retrievedTool.Name);

            // Test updating AppId
            retrievedTool.AppId = 54321L;
            await _toolRepository.SaveToolAsync(retrievedTool);
            var updatedTool = await _toolRepository.GetToolAsync(retrievedTool.Id);

            Assert.Equal(54321L, updatedTool?.AppId);
        }

        [Fact]
        public async Task CreateToolWithDuplicateNameThrowsException()
        {
            // Arrange
            var tool1 = new LlmTool
            {
                Name = "Duplicate Tool Name",
                Description = "First tool with this name",
                StaticId = "com.test.duplicate1",
                ToolConfig = "{}",
                ToolType = 0,
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                State = 0
            };

            var tool2 = new LlmTool
            {
                Name = "Duplicate Tool Name", // Same name as tool1
                Description = "Second tool with same name",
                StaticId = "com.test.duplicate2",
                ToolConfig = "{}",
                ToolType = 0,
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                State = 0
            };

            // Act & Assert
            await _toolRepository.CreateToolAsync(tool1);
            
            // Creating second tool with same name should throw exception due to unique constraint
            await Assert.ThrowsAnyAsync<Exception>(() => _toolRepository.CreateToolAsync(tool2));
        }

        [Fact]
        public async Task UpdateToolToDuplicateNameThrowsException()
        {
            // Arrange
            var tool1 = new LlmTool
            {
                Name = "Original Tool 1",
                Description = "First tool",
                StaticId = "com.test.original1",
                ToolConfig = "{}",
                ToolType = 0,
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                State = 0
            };

            var tool2 = new LlmTool
            {
                Name = "Original Tool 2",
                Description = "Second tool",
                StaticId = "com.test.original2",
                ToolConfig = "{}",
                ToolType = 0,
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                State = 0
            };

            var createdTool1 = await _toolRepository.CreateToolAsync(tool1);
            var createdTool2 = await _toolRepository.CreateToolAsync(tool2);

            // Act & Assert
            // Try to update tool2 to have the same name as tool1
            createdTool2.Name = "Original Tool 1";
            
            // This should throw an exception due to unique constraint violation
            await Assert.ThrowsAnyAsync<Exception>(() => _toolRepository.SaveToolAsync(createdTool2));
        }
    }
}