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
    public class TestLlmRepositories : IDisposable
    {
        private readonly string _testDbPath;
        private readonly IPersonRepository _modelRepository;

        public TestLlmRepositories()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_llm_model_repo_{Guid.NewGuid()}.db");
            
            // Initialize repository directly with SqlitePersonRepository
            _modelRepository = new SqlitePersonRepository(_testDbPath);
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
        public async Task GetModelReturnsNullForNonExistentModel()
        {
            // Arrange - nothing to arrange

            // Act
            var model = await _modelRepository.GetPersonAsync(999);

            // Assert
            Assert.Null(model);
        }

        [Fact]
        public async Task SaveAndGetModelWorks()
        {
            // Arrange
            var newModel = new Person
            {
                Name = "Test Model",
                ProviderName = "TestProvider", // Renamed from ProviderId
                ModelId = "test-model-v1", // Added
                Image = new byte[] { 1, 2, 3 }, // Added
                ToolNames = new string[] { "Tool1", "Tool2" }, // Renamed from ToolIds
                Parameters = new Dictionary<string, string>
                {
                    { "Temperature", "0.7" },
                    { "MaxTokens", "1000" }
                },
                IsEnabled = true,
                DeveloperMessage = "Test developer message"
            };

            // Act - Create
            var createdModel = await _modelRepository.CreatePersonAsync(newModel);
            var retrievedModel = await _modelRepository.GetPersonAsync(createdModel.Id);

            // Assert - Create
            Assert.NotNull(retrievedModel);
            Assert.Equal(createdModel.Id, retrievedModel.Id);
            Assert.Equal("Test Model", retrievedModel.Name);
            Assert.Equal("TestProvider", retrievedModel.ProviderName); // Updated assertion
            Assert.Equal("test-model-v1", retrievedModel.ModelId); // Added
            Assert.Equal(new byte[] { 1, 2, 3 }, retrievedModel.Image); // Added
            Assert.Equal(new string[] { "Tool1", "Tool2" }, retrievedModel.ToolNames); // Updated assertion
            Assert.Equal("0.7", retrievedModel.Parameters["Temperature"]);
            Assert.Equal("1000", retrievedModel.Parameters["MaxTokens"]);
            Assert.True(retrievedModel.IsEnabled);
            Assert.Equal("Test developer message", retrievedModel.DeveloperMessage);
            Assert.NotEqual(default, retrievedModel.CreatedAt);
            Assert.NotEqual(default, retrievedModel.LastModified);

            // Act - Update
            retrievedModel.Name = "Updated Test Model";
            retrievedModel.ModelId = "test-model-v2"; // Added
            retrievedModel.Image = new byte[] { 4, 5, 6 }; // Added
            retrievedModel.ToolNames = new string[] { "Tool3" }; // Renamed from ToolIds
            var t = retrievedModel.LastModified;
            var updateResult = await _modelRepository.SavePersonAsync(retrievedModel);
            var updatedModel = await _modelRepository.GetPersonAsync(retrievedModel.Id);

            // Assert - Update
            Assert.True(updateResult);
            Assert.Equal("Updated Test Model", updatedModel!.Name);
            Assert.Equal("test-model-v2", updatedModel.ModelId); // Added
            Assert.Equal(new byte[] { 4, 5, 6 }, updatedModel.Image); // Added
            Assert.Equal(new string[] { "Tool3" }, updatedModel.ToolNames); // Updated assertion
            Assert.Equal(retrievedModel.CreatedAt, updatedModel.CreatedAt);
            Assert.NotEqual(t, updatedModel.LastModified);
        }

        [Fact]
        public async Task GetAllModelsReturnsAllModels()
        {
            // Arrange
            var model1 = new Person
            {
                Name = "Model 1",
                ProviderName = "Provider1", // Renamed from ProviderId
                ModelId = "model-1-id", // Added
                Image = null, // Added
                ToolNames = Array.Empty<string>(), // Renamed from ToolIds
                Parameters = new Dictionary<string, string> { { "Temperature", "0.7" } },
                IsEnabled = true
            };
            var model2 = new Person
            {
                Name = "Model 2",
                ProviderName = "Provider2", // Renamed from ProviderId
                ModelId = "model-2-id", // Added
                Image = new byte[] { 7 }, // Added
                ToolNames = new string[] { "ToolA" }, // Renamed from ToolIds
                Parameters = new Dictionary<string, string> { { "Temperature", "0.9" } },
                IsEnabled = false
            };
            await _modelRepository.CreatePersonAsync(model1);
            await _modelRepository.CreatePersonAsync(model2);

            // Act
            var allModels = await _modelRepository.GetAllPersonsAsync();

            // Assert
            Assert.Equal(2, allModels.Count());
            Assert.Contains(allModels, m => m.Name == "Model 1");
            Assert.Contains(allModels, m => m.Name == "Model 2");
        }

        [Fact]
        public async Task DeleteModelWorks()
        {
            // Arrange
            var model = new Person
            {
                Name = "Model to delete",
                ProviderName = "Provider1", // Renamed from ProviderId
                ModelId = "delete-model-id", // Added
                Image = null, // Added
                ToolNames = Array.Empty<string>(), // Renamed from ToolIds
                Parameters = new Dictionary<string, string> { { "Temperature", "0.7" } },
                IsEnabled = true
            };
            var createdModel = await _modelRepository.CreatePersonAsync(model);

            // Act
            var deleteResult = await _modelRepository.DeletePersonAsync(createdModel.Id);
            var retrievedModel = await _modelRepository.GetPersonAsync(createdModel.Id);

            // Assert
            Assert.True(deleteResult);
            Assert.Null(retrievedModel);
        }

        [Fact]
        public async Task UpdateModelWorks()
        {
            // Arrange
            var model = new Person
            {
                Name = "Original Model",
                ProviderName = "Provider1", // Renamed from ProviderId
                ModelId = "original-model-id", // Added
                Image = new byte[] { 10 }, // Added
                ToolNames = new string[] { "ToolX" }, // Renamed from ToolIds
                Parameters = new Dictionary<string, string> { { "Temperature", "0.7" } },
                IsEnabled = true
            };
            var createdModel = await _modelRepository.CreatePersonAsync(model);
            
            // Update the model
            createdModel.Name = "Updated Model";
            createdModel.ProviderName = "Provider2"; // Renamed from ProviderId
            createdModel.ModelId = "updated-model-id"; // Added
            createdModel.Image = new byte[] { 11, 12 }; // Added
            createdModel.ToolNames = new string[] { "ToolY", "ToolZ" }; // Renamed from ToolIds
            createdModel.Parameters = new Dictionary<string, string> { { "Temperature", "0.9" } };
            createdModel.IsEnabled = false;
            createdModel.DeveloperMessage = "Updated message";

            // Act
            var t= createdModel.LastModified;
            var updateResult = await _modelRepository.SavePersonAsync(createdModel); // Corrected: Use SaveModelAsync for update
            var retrievedModel = await _modelRepository.GetPersonAsync(createdModel.Id);

            // Assert
            Assert.True(updateResult); // Corrected: SaveModelAsync returns bool
            Assert.NotNull(retrievedModel);
            Assert.Equal("Updated Model", retrievedModel.Name);
            Assert.Equal("Provider2", retrievedModel.ProviderName); // Updated assertion
            Assert.Equal("updated-model-id", retrievedModel.ModelId); // Added
            Assert.Equal(new byte[] { 11, 12 }, retrievedModel.Image); // Added
            Assert.Equal(new string[] { "ToolY", "ToolZ" }, retrievedModel.ToolNames); // Updated assertion
            Assert.Equal("0.9", retrievedModel.Parameters["Temperature"]);
            Assert.False(retrievedModel.IsEnabled);
            Assert.Equal("Updated message", retrievedModel.DeveloperMessage);
            Assert.Equal(createdModel.CreatedAt.ToLocalTime(), retrievedModel.CreatedAt.ToLocalTime());
            Assert.NotEqual(t, retrievedModel.LastModified);
        }

        [Fact]
        public async Task GetModelsByProviderNameWorks() // Renamed from GetModelsByProviderIdWorks
        {
            // Arrange
            var providerName1 = "Provider1"; // Renamed variable
            var providerName2 = "Provider2"; // Renamed variable
            
            var model1 = new Person
            {
                Name = "Provider 1 Model 1",
                ProviderName = providerName1, // Updated property and variable
                ModelId = "p1m1", // Added
                Image = null, // Added
                ToolNames = Array.Empty<string>(), // Renamed from ToolIds
                Parameters = new Dictionary<string, string> { { "Key", "Value" } },
                IsEnabled = true
            };
            var model2 = new Person
            {
                Name = "Provider 1 Model 2",
                ProviderName = providerName1, // Updated property and variable
                ModelId = "p1m2", // Added
                Image = null, // Added
                ToolNames = Array.Empty<string>(), // Renamed from ToolIds
                Parameters = new Dictionary<string, string> { { "Key", "Value" } },
                IsEnabled = true
            };
            var model3 = new Person
            {
                Name = "Provider 2 Model",
                ProviderName = providerName2, // Updated property and variable
                ModelId = "p2m1", // Added
                Image = null, // Added
                ToolNames = Array.Empty<string>(), // Renamed from ToolIds
                Parameters = new Dictionary<string, string> { { "Key", "Value" } },
                IsEnabled = true
            };
            
            await _modelRepository.CreatePersonAsync(model1);
            await _modelRepository.CreatePersonAsync(model2);
            await _modelRepository.CreatePersonAsync(model3);

            // Act
            var provider1Models = await _modelRepository.GetPersonsByProviderNameAsync(providerName1); // Renamed method
            var provider2Models = await _modelRepository.GetPersonsByProviderNameAsync(providerName2); // Renamed method

            // Assert
            Assert.Equal(2, provider1Models.Count());
            Assert.Single(provider2Models);
            Assert.All(provider1Models, m => Assert.Equal(providerName1, m.ProviderName)); // Updated assertion
            Assert.All(provider2Models, m => Assert.Equal(providerName2, m.ProviderName)); // Updated assertion
        }

        [Fact]
        public async Task CreateModelWithExistingIdThrowsException()
        {
            // Arrange
            var model = new Person
            {
                Id = 1, // Set an existing ID
                Name = "Test Model",
                ProviderName = "Provider1", // Renamed from ProviderId
                ModelId = "test-id", // Added
                ToolNames = Array.Empty<string>() // Renamed from ToolIds
            };

            // Act & Assert
            var ret = await _modelRepository.CreatePersonAsync(model);
            Assert.NotNull(ret);
            Assert.NotEqual(1, ret.Id);
            Assert.NotEqual(0, ret.Id);
        }

        [Fact]
        public async Task SaveModelWithoutIdThrowsException()
        {
            // Arrange
            var model = new Person
            {
                Id = 0, // Invalid ID for save
                Name = "Test Model",
                ProviderName = "Provider1", // Renamed from ProviderId
                ModelId = "test-id", // Added
                ToolNames = Array.Empty<string>() // Renamed from ToolIds
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _modelRepository.SavePersonAsync(model));
        }

        [Fact]
        public async Task GetEnabledModelsWorks()
        {
            // Arrange
            var model1 = new Person
            {
                Name = "Enabled Model",
                ProviderName = "Provider1", // Renamed from ProviderId
                ModelId = "enabled-model-id", // Added
                Image = null, // Added
                ToolNames = Array.Empty<string>(), // Renamed from ToolIds
                Parameters = new Dictionary<string, string> { { "Temperature", "0.7" } },
                IsEnabled = true
            };
            var model2 = new Person
            {
                Name = "Disabled Model",
                ProviderName = "Provider2", // Renamed from ProviderId
                ModelId = "disabled-model-id", // Added
                Image = null, // Added
                ToolNames = Array.Empty<string>(), // Renamed from ToolIds
                Parameters = new Dictionary<string, string> { { "Temperature", "0.8" } },
                IsEnabled = false
            };
            await _modelRepository.CreatePersonAsync(model1);
            await _modelRepository.CreatePersonAsync(model2);

            // Act
            var enabledModels = await _modelRepository.GetEnabledPersonsAsync();

            // Assert
            Assert.Single(enabledModels);
            Assert.Equal("Enabled Model", enabledModels.First().Name);
        }

        [Fact]
        public async Task GetModelByNameAsyncWorks()
        {
            // Arrange
            var model = new Person
            {
                Name = "Unique Model Name",
                ProviderName = "Provider1",
                ModelId = "unique-model-id",
                ToolNames = new string[] { "Tool1" },
                Parameters = new Dictionary<string, string> { { "Temperature", "0.7" } },
                IsEnabled = true
            };
            await _modelRepository.CreatePersonAsync(model);

            // Act
            var retrievedModel = await _modelRepository.GetPersonByNameAsync("Unique Model Name");
            var nonExistentModel = await _modelRepository.GetPersonByNameAsync("Non-existent Model");

            // Assert
            Assert.NotNull(retrievedModel);
            Assert.Equal("Unique Model Name", retrievedModel.Name);
            Assert.Equal("Provider1", retrievedModel.ProviderName);
            Assert.Equal("unique-model-id", retrievedModel.ModelId);
            Assert.Null(nonExistentModel);
        }
    }
} 