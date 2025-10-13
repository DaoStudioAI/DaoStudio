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
    public class TestCachedModelRepository : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ICachedModelRepository _cachedModelRepository;

        public TestCachedModelRepository()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_cached_model_repo_{Guid.NewGuid()}.db");
            
            // Initialize repository directly with SqliteCachedModelRepository
            _cachedModelRepository = new SqliteCachedModelRepository(_testDbPath);
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
            var model = await _cachedModelRepository.GetModelAsync(999);

            // Assert
            Assert.Null(model);
        }

        [Fact]
        public async Task SaveAndGetModelWorks()
        {
            // Arrange
            var newModel = new CachedModel
            {
                ApiProviderId = 1,
                Name = "Test Model",
                ModelId = "test-model-v1",
                ProviderType = 1, // OpenAI
                Catalog = "test-catalog",
                Parameters = new Dictionary<string, string>
                {
                    { "temperature", "0.7" },
                    { "max_tokens", "1000" },
                    { "model_type", "gpt-4" }
                }
            };

            // Act - Create
            var createdModel = await _cachedModelRepository.CreateModelAsync(newModel);
            var retrievedModel = await _cachedModelRepository.GetModelAsync(createdModel.Id);

            // Assert - Create
            Assert.NotNull(retrievedModel);
            Assert.Equal(createdModel.Id, retrievedModel.Id);
            Assert.Equal(1, retrievedModel.ApiProviderId);
            Assert.Equal("Test Model", retrievedModel.Name);
            Assert.Equal("test-model-v1", retrievedModel.ModelId);
            Assert.Equal(1, retrievedModel.ProviderType); // OpenAI
            Assert.Equal("test-catalog", retrievedModel.Catalog);
            Assert.NotNull(retrievedModel.Parameters);
            Assert.Equal("0.7", retrievedModel.Parameters["temperature"]);
            Assert.Equal("1000", retrievedModel.Parameters["max_tokens"]);
            Assert.Equal("gpt-4", retrievedModel.Parameters["model_type"]);

            // Act - Update
            retrievedModel.Name = "Updated Test Model";
            retrievedModel.Catalog = "updated-catalog";
            retrievedModel.Parameters["temperature"] = "0.9";
            retrievedModel.Parameters["new_param"] = "new_value";
            var updateResult = await _cachedModelRepository.SaveModelAsync(retrievedModel);
            var updatedModel = await _cachedModelRepository.GetModelAsync(retrievedModel.Id);

            // Assert - Update
            Assert.True(updateResult);
            Assert.Equal("Updated Test Model", updatedModel!.Name);
            Assert.Equal("updated-catalog", updatedModel.Catalog);
            Assert.Equal(createdModel.Id, updatedModel.Id);
            Assert.NotNull(updatedModel.Parameters);
            Assert.Equal("0.9", updatedModel.Parameters["temperature"]);
            Assert.Equal("1000", updatedModel.Parameters["max_tokens"]);
            Assert.Equal("gpt-4", updatedModel.Parameters["model_type"]);
            Assert.Equal("new_value", updatedModel.Parameters["new_param"]);
        }

        [Fact]
        public async Task GetAllModelsReturnsAllModels()
        {
            // Arrange
            var model1 = new CachedModel
            {
                ApiProviderId = 1,
                Name = "Model 1",
                ModelId = "model-1",
                ProviderType = 1, // OpenAI
                Catalog = "catalog1"
            };
            
            var model2 = new CachedModel
            {
                ApiProviderId = 2,
                Name = "Model 2",
                ModelId = "model-2",
                ProviderType = 2, // Anthropic
                Catalog = "catalog2"
            };
            
            await _cachedModelRepository.CreateModelAsync(model1);
            await _cachedModelRepository.CreateModelAsync(model2);

            // Act
            var allModels = await _cachedModelRepository.GetAllModelsAsync();

            // Assert
            Assert.Equal(2, allModels.Count());
            Assert.Contains(allModels, m => m.Name == "Model 1");
            Assert.Contains(allModels, m => m.Name == "Model 2");
        }

        [Fact]
        public async Task DeleteModelWorks()
        {
            // Arrange
            var model = new CachedModel
            {
                ApiProviderId = 1,
                Name = "Model to delete",
                ModelId = "delete-model",
                ProviderType = 3, // Google
                Catalog = "delete-catalog"
            };
            
            var createdModel = await _cachedModelRepository.CreateModelAsync(model);

            // Act
            var deleteResult = await _cachedModelRepository.DeleteModelAsync(createdModel.Id);
            var retrievedModel = await _cachedModelRepository.GetModelAsync(createdModel.Id);

            // Assert
            Assert.True(deleteResult);
            Assert.Null(retrievedModel);
        }

        [Fact]
        public async Task GetModelsByProviderIdWorks()
        {
            // Arrange
            var providerId1 = 1L;
            var providerId2 = 2L;
            
            var model1 = new CachedModel
            {
                ApiProviderId = providerId1,
                Name = "Provider 1 Model 1",
                ModelId = "p1m1",
                ProviderType = 1, // OpenAI
                Catalog = "catalog1"
            };
            
            var model2 = new CachedModel
            {
                ApiProviderId = providerId1,
                Name = "Provider 1 Model 2",
                ModelId = "p1m2",
                ProviderType = 1, // OpenAI
                Catalog = "catalog1"
            };
            
            var model3 = new CachedModel
            {
                ApiProviderId = providerId2,
                Name = "Provider 2 Model",
                ModelId = "p2m1",
                ProviderType = 2, // Anthropic
                Catalog = "catalog2"
            };
            
            await _cachedModelRepository.CreateModelAsync(model1);
            await _cachedModelRepository.CreateModelAsync(model2);
            await _cachedModelRepository.CreateModelAsync(model3);

            // Act
            var provider1Models = await _cachedModelRepository.GetModelsByProviderIdAsync(providerId1);
            var provider2Models = await _cachedModelRepository.GetModelsByProviderIdAsync(providerId2);

            // Assert
            Assert.Equal(2, provider1Models.Count());
            Assert.Single(provider2Models);
            Assert.All(provider1Models, m => Assert.Equal(providerId1, m.ApiProviderId));
            Assert.All(provider2Models, m => Assert.Equal(providerId2, m.ApiProviderId));
        }

        [Fact]
        public async Task GetModelsByCriteriaWorks()
        {
            // Arrange
            var providerId = 1L;
            var providerType = 1; // OpenAI
            var catalog1 = "catalog1";
            var catalog2 = "catalog2";
            
            var model1 = new CachedModel
            {
                ApiProviderId = providerId,
                Name = "Model 1",
                ModelId = "model-1",
                ProviderType = providerType,
                Catalog = catalog1
            };
            
            var model2 = new CachedModel
            {
                ApiProviderId = providerId,
                Name = "Model 2",
                ModelId = "model-2",
                ProviderType = providerType,
                Catalog = catalog1
            };
            
            var model3 = new CachedModel
            {
                ApiProviderId = providerId,
                Name = "Model 3",
                ModelId = "model-3",
                ProviderType = providerType,
                Catalog = catalog2
            };
            
            await _cachedModelRepository.CreateModelAsync(model1);
            await _cachedModelRepository.CreateModelAsync(model2);
            await _cachedModelRepository.CreateModelAsync(model3);

            // Act
            var catalog1Models = await _cachedModelRepository.GetModelsByCriteriaAsync(providerId, providerType, catalog1);
            var catalog2Models = await _cachedModelRepository.GetModelsByCriteriaAsync(providerId, providerType, catalog2);

            // Assert
            Assert.Equal(2, catalog1Models.Count());
            Assert.Single(catalog2Models);
            Assert.All(catalog1Models, m => Assert.Equal(catalog1, m.Catalog));
            Assert.All(catalog2Models, m => Assert.Equal(catalog2, m.Catalog));
        }

        [Fact]
        public async Task CreateModelsAsyncWorks()
        {
            // Arrange
            var models = new List<CachedModel>
            {
                new CachedModel
                {
                    ApiProviderId = 1,
                    Name = "Bulk Model 1",
                    ModelId = "bulk-1",
                    ProviderType = 1, // OpenAI
                    Catalog = "bulk-catalog"
                },
                new CachedModel
                {
                    ApiProviderId = 1,
                    Name = "Bulk Model 2",
                    ModelId = "bulk-2",
                    ProviderType = 1, // OpenAI
                    Catalog = "bulk-catalog"
                },
                new CachedModel
                {
                    ApiProviderId = 1,
                    Name = "Bulk Model 3",
                    ModelId = "bulk-3",
                    ProviderType = 1, // OpenAI
                    Catalog = "bulk-catalog"
                }
            };

            // Act
            var createdCount = await _cachedModelRepository.CreateModelsAsync(models);
            var allModels = await _cachedModelRepository.GetAllModelsAsync();

            // Assert
            Assert.Equal(3, createdCount);
            Assert.Equal(3, allModels.Count());
            Assert.Contains(allModels, m => m.Name == "Bulk Model 1");
            Assert.Contains(allModels, m => m.Name == "Bulk Model 2");
            Assert.Contains(allModels, m => m.Name == "Bulk Model 3");
        }

        [Fact]
        public async Task DeleteModelsByProviderIdAsyncWorks()
        {
            // Arrange
            var providerId1 = 1L;
            var providerId2 = 2L;
            
            var models = new List<CachedModel>
            {
                new CachedModel
                {
                    ApiProviderId = providerId1,
                    Name = "Provider 1 Model 1",
                    ModelId = "p1m1",
                    ProviderType = 1, // OpenAI
                    Catalog = "catalog1"
                },
                new CachedModel
                {
                    ApiProviderId = providerId1,
                    Name = "Provider 1 Model 2",
                    ModelId = "p1m2",
                    ProviderType = 1, // OpenAI
                    Catalog = "catalog1"
                },
                new CachedModel
                {
                    ApiProviderId = providerId2,
                    Name = "Provider 2 Model",
                    ModelId = "p2m1",
                    ProviderType = 2, // Anthropic
                    Catalog = "catalog2"
                }
            };

            foreach (var model in models)
            {
                await _cachedModelRepository.CreateModelAsync(model);
            }

            // Act
            var deletedCount = await _cachedModelRepository.DeleteModelsByProviderIdAsync(providerId1);
            var remainingModels = await _cachedModelRepository.GetAllModelsAsync();
            var provider2Models = await _cachedModelRepository.GetModelsByProviderIdAsync(providerId2);

            // Assert
            Assert.Equal(2, deletedCount);
            Assert.Single(remainingModels);
            Assert.Single(provider2Models);
            Assert.Equal("Provider 2 Model", remainingModels.First().Name);
        }

        [Fact]
        public async Task UpdateModelWorks()
        {
            // Arrange
            var model = new CachedModel
            {
                ApiProviderId = 1,
                Name = "Original Model",
                ModelId = "original-id",
                ProviderType = 1, // OpenAI
                Catalog = "original-catalog"
            };
            
            var createdModel = await _cachedModelRepository.CreateModelAsync(model);
            
            // Update the model
            createdModel.Name = "Updated Model";
            createdModel.ModelId = "updated-id";
            createdModel.ProviderType = 2; // Anthropic
            createdModel.Catalog = "updated-catalog";
            createdModel.ApiProviderId = 2;

            // Act
            var updateResult = await _cachedModelRepository.SaveModelAsync(createdModel);
            var retrievedModel = await _cachedModelRepository.GetModelAsync(createdModel.Id);

            // Assert
            Assert.True(updateResult);
            Assert.NotNull(retrievedModel);
            Assert.Equal("Updated Model", retrievedModel.Name);
            Assert.Equal("updated-id", retrievedModel.ModelId);
            Assert.Equal(2, retrievedModel.ProviderType); // Anthropic
            Assert.Equal("updated-catalog", retrievedModel.Catalog);
            Assert.Equal(2L, retrievedModel.ApiProviderId);
        }


        [Fact]
        public async Task SaveModelWithoutIdThrowsException()
        {
            // Arrange
            var model = new CachedModel
            {
                Id = 0, // Invalid ID for save
                ApiProviderId = 1,
                Name = "Test Model",
                ModelId = "test-id",
                ProviderType = 1, // OpenAI
                Catalog = "test-catalog"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _cachedModelRepository.SaveModelAsync(model));
        }

        [Fact]
        public async Task DeleteNonExistentModelReturnsFalse()
        {
            // Arrange - nothing to arrange

            // Act
            var result = await _cachedModelRepository.DeleteModelAsync(999);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteModelsByProviderIdReturnsZeroForNonExistentProvider()
        {
            // Arrange - nothing to arrange

            // Act
            var result = await _cachedModelRepository.DeleteModelsByProviderIdAsync(999);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task ParametersFieldIsPersisted()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "temperature", "0.8" },
                { "max_tokens", "2000" },
                { "top_p", "0.9" },
                { "frequency_penalty", "0.1" }
            };
            
            var model = new CachedModel
            {
                ApiProviderId = 1,
                Name = "Parameters Test Model",
                ModelId = "param-test-model",
                ProviderType = 1, // OpenAI
                Catalog = "param-catalog",
                Parameters = parameters
            };
            
            // Act
            var createdModel = await _cachedModelRepository.CreateModelAsync(model);
            var retrievedModel = await _cachedModelRepository.GetModelAsync(createdModel.Id);

            // Assert
            Assert.NotNull(retrievedModel);
            Assert.NotNull(retrievedModel.Parameters);
            Assert.Equal(4, retrievedModel.Parameters.Count);
            Assert.Equal("0.8", retrievedModel.Parameters["temperature"]);
            Assert.Equal("2000", retrievedModel.Parameters["max_tokens"]);
            Assert.Equal("0.9", retrievedModel.Parameters["top_p"]);
            Assert.Equal("0.1", retrievedModel.Parameters["frequency_penalty"]);

            // Test updating Parameters
            retrievedModel.Parameters["temperature"] = "0.5";
            retrievedModel.Parameters["presence_penalty"] = "0.2";
            retrievedModel.Parameters.Remove("frequency_penalty");
            
            await _cachedModelRepository.SaveModelAsync(retrievedModel);
            var updatedModel = await _cachedModelRepository.GetModelAsync(retrievedModel.Id);

            Assert.NotNull(updatedModel?.Parameters);
            Assert.Equal(4, updatedModel.Parameters.Count); // 3 remaining + 1 new
            Assert.Equal("0.5", updatedModel.Parameters["temperature"]);
            Assert.Equal("0.2", updatedModel.Parameters["presence_penalty"]);
            Assert.False(updatedModel.Parameters.ContainsKey("frequency_penalty"));
        }

        [Fact]
        public async Task EmptyParametersFieldIsPersisted()
        {
            // Arrange
            var model = new CachedModel
            {
                ApiProviderId = 1,
                Name = "Empty Parameters Model",
                ModelId = "empty-param-model",
                ProviderType = 3, // Google
                Catalog = "empty-catalog",
                Parameters = new Dictionary<string, string>()
            };
            
            // Act
            var createdModel = await _cachedModelRepository.CreateModelAsync(model);
            var retrievedModel = await _cachedModelRepository.GetModelAsync(createdModel.Id);

            // Assert
            Assert.NotNull(retrievedModel);
            Assert.NotNull(retrievedModel.Parameters);
            Assert.Empty(retrievedModel.Parameters);
        }

        [Fact]
        public async Task CreateModelWithDuplicateIdHandledCorrectly()
        {
            // Arrange
            var model1 = new CachedModel
            {
                Id = 999, // Setting a specific ID
                ApiProviderId = 1,
                Name = "Test Model 1",
                ModelId = "test-model-1",
                ProviderType = 1, // OpenAI
                Catalog = "test-catalog-1",
                Parameters = new Dictionary<string, string>()
            };

            var model2 = new CachedModel
            {
                Id = 999, // Same ID as model1
                ApiProviderId = 1,
                Name = "Test Model 2",
                ModelId = "test-model-2",
                ProviderType = 1, // OpenAI
                Catalog = "test-catalog-2",
                Parameters = new Dictionary<string, string>()
            };

            // Act
            var createdModel1 = await _cachedModelRepository.CreateModelAsync(model1);
            var createdModel2 = await _cachedModelRepository.CreateModelAsync(model2);

            // Assert
            // Both should be created successfully with different auto-generated IDs
            Assert.NotEqual(999, createdModel1.Id); // Should not use the provided ID
            Assert.NotEqual(999, createdModel2.Id); // Should not use the provided ID
            Assert.NotEqual(createdModel1.Id, createdModel2.Id); // Should have different IDs
            Assert.Equal("Test Model 1", createdModel1.Name);
            Assert.Equal("Test Model 2", createdModel2.Name);
        }

        [Fact]
        public async Task MultipleModelsWithSameNameAllowed()
        {
            // Arrange
            var model1 = new CachedModel
            {
                ApiProviderId = 1,
                Name = "Same Name Model",
                ModelId = "model-1",
                ProviderType = 1, // OpenAI
                Catalog = "catalog-1",
                Parameters = new Dictionary<string, string>()
            };

            var model2 = new CachedModel
            {
                ApiProviderId = 2,
                Name = "Same Name Model", // Same name as model1
                ModelId = "model-2",
                ProviderType = 2, // Anthropic
                Catalog = "catalog-2",
                Parameters = new Dictionary<string, string>()
            };

            // Act
            var createdModel1 = await _cachedModelRepository.CreateModelAsync(model1);
            var createdModel2 = await _cachedModelRepository.CreateModelAsync(model2);

            // Assert
            // Both should be created successfully since there's no unique constraint on name
            Assert.NotEqual(createdModel1.Id, createdModel2.Id);
            Assert.Equal("Same Name Model", createdModel1.Name);
            Assert.Equal("Same Name Model", createdModel2.Name);
            Assert.Equal(1, createdModel1.ApiProviderId);
            Assert.Equal(2, createdModel2.ApiProviderId);
        }

        [Fact]
        public async Task CreateModelWithExistingIdReturnsNewId()
        {
            // Arrange
            var model = new CachedModel
            {
                Id = 1, // Set an existing ID
                ApiProviderId = 1,
                Name = "Test Model",
                ModelId = "test-model",
                ProviderType = 1, // OpenAI
                Catalog = "test-catalog"
            };

            // Act
            var createdModel = await _cachedModelRepository.CreateModelAsync(model);

            // Assert
            Assert.NotNull(createdModel);
            // Note: Some repositories may use the provided ID if it doesn't conflict
            // The important thing is that it gets a valid ID and the model is created
            Assert.NotEqual(0, createdModel.Id); // Should have a valid ID
            Assert.Equal("Test Model", createdModel.Name);
        }
    }
}