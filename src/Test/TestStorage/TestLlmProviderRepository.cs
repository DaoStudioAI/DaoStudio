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
    public class TestAPIProviderRepository : IDisposable
    {
        private readonly string _testDbPath;
        private readonly IAPIProviderRepository _providerRepository;

        public TestAPIProviderRepository()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_provider_repo_{Guid.NewGuid()}.db");
            
            // Initialize repository directly with SqliteAPIProviderRepository
            _providerRepository = new SqliteAPIProviderRepository(_testDbPath);
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
        public async Task GetProviderReturnsNullForNonExistentProvider()
        {
            // Arrange - nothing to arrange

            // Act
            var provider = await _providerRepository.GetProviderAsync(999);

            // Assert
            Assert.Null(provider);
        }

        [Fact]
        public async Task SaveAndGetProviderWorks()
        {
            // Arrange
            var newProvider = new APIProvider
            {
                Name = "Test Provider",
                ApiEndpoint = "https://api.test-provider.com/v1",
                ApiKey = "test-api-key-123",
                Parameters = new Dictionary<string, string> 
                { 
                    { "Timeout", "30"},
                    { "MaxRetries", "3" }
                },
                IsEnabled = true,
                ProviderType = 1, // OpenAI
                Timeout = 30,
                MaxConcurrency = 5
            };

            // Act - Create
            var createdProvider = await _providerRepository.CreateProviderAsync(newProvider);
            var retrievedProvider = await _providerRepository.GetProviderAsync(createdProvider.Id);

            // Assert - Create
            Assert.NotNull(retrievedProvider);
            Assert.Equal(createdProvider.Id, retrievedProvider.Id);
            Assert.Equal("Test Provider", retrievedProvider.Name);
            Assert.Equal("https://api.test-provider.com/v1", retrievedProvider.ApiEndpoint);
            Assert.Equal("test-api-key-123", retrievedProvider.ApiKey);
            Assert.Equal("30", retrievedProvider.Parameters["Timeout"]);
            Assert.Equal("3", retrievedProvider.Parameters["MaxRetries"]);
            Assert.True(retrievedProvider.IsEnabled);
            Assert.Equal(1, retrievedProvider.ProviderType); // OpenAI
            Assert.Equal(30, retrievedProvider.Timeout);
            Assert.Equal(5, retrievedProvider.MaxConcurrency);
            Assert.NotEqual(default, retrievedProvider.CreatedAt);
            Assert.NotEqual(default, retrievedProvider.LastModified);

            // Act - Update
            retrievedProvider.Name = "Updated Test Provider";
            retrievedProvider.ProviderType = 2; // Anthropic
            retrievedProvider.Timeout = 60;
            retrievedProvider.MaxConcurrency = 10;
            var t = retrievedProvider.LastModified;
            var updateResult = await _providerRepository.SaveProviderAsync(retrievedProvider);
            var updatedProvider = await _providerRepository.GetProviderAsync(retrievedProvider.Id);

            // Assert - Update
            Assert.True(updateResult);
            Assert.Equal("Updated Test Provider", updatedProvider!.Name);
            Assert.Equal(2, updatedProvider.ProviderType); // Anthropic
            Assert.Equal(60, updatedProvider.Timeout);
            Assert.Equal(10, updatedProvider.MaxConcurrency);
            Assert.Equal(retrievedProvider.CreatedAt, updatedProvider.CreatedAt);
            Assert.NotEqual(t, updatedProvider.LastModified);
        }

        [Fact]
        public async Task GetAllProvidersReturnsAllProviders()
        {
            // Arrange
            var provider1 = new APIProvider
            {
                Name = "Provider 1",
                ApiEndpoint = "https://api.provider1.com",
                ApiKey = "key1",
                Parameters = new Dictionary<string, string> { { "Timeout", "30" } },
                IsEnabled = true,
                ProviderType = 1 // OpenAI
            };
            
            var provider2 = new APIProvider
            {
                Name = "Provider 2",
                ApiEndpoint = "https://api.provider2.com",
                ApiKey = "key2",
                Parameters = new Dictionary<string, string> { { "Timeout", "60" } },
                IsEnabled = false,
                ProviderType = 2 // Anthropic
            };
            
            await _providerRepository.CreateProviderAsync(provider1);
            await _providerRepository.CreateProviderAsync(provider2);

            // Act
            var allProviders = await _providerRepository.GetAllProvidersAsync();

            // Assert
            Assert.Equal(2, allProviders.Count());
            Assert.Contains(allProviders, p => p.Name == "Provider 1");
            Assert.Contains(allProviders, p => p.Name == "Provider 2");
            Assert.Contains(allProviders, p => p.ProviderType == 1); // OpenAI
            Assert.Contains(allProviders, p => p.ProviderType == 2); // Anthropic
        }

        [Fact]
        public async Task DeleteProviderWorks()
        {
            // Arrange
            var provider = new APIProvider
            {
                Name = "Provider to delete",
                ApiEndpoint = "https://api.provider-to-delete.com",
                ApiKey = "delete-key",
                Parameters = new Dictionary<string, string> { { "Timeout", "30" } },
                IsEnabled = true,
                ProviderType = 3 // Google
            };
            
            var createdProvider = await _providerRepository.CreateProviderAsync(provider);

            // Act
            var deleteResult = await _providerRepository.DeleteProviderAsync(createdProvider.Id);
            var retrievedProvider = await _providerRepository.GetProviderAsync(createdProvider.Id);

            // Assert
            Assert.True(deleteResult);
            Assert.Null(retrievedProvider);
        }

        [Fact]
        public async Task UpdateProviderWorks()
        {
            // Arrange
            var provider = new APIProvider
            {
                Name = "Original Provider",
                ApiEndpoint = "https://api.original.com",
                ApiKey = "original-key",
                Parameters = new Dictionary<string, string> { { "Timeout", 30.ToString() } },
                IsEnabled = true,
                ProviderType = 4 // Local
            };
            
            var createdProvider = await _providerRepository.CreateProviderAsync(provider);
            
            // Update the provider
            createdProvider.Name = "Updated Provider";
            createdProvider.Parameters = new Dictionary<string, string> { { "Timeout", "60" }, { "NewParam", "Value" } };
            createdProvider.IsEnabled = false;
            createdProvider.ApiEndpoint = "https://api.updated.com";
            createdProvider.ApiKey = "updated-key";
            createdProvider.ProviderType = 5; // OpenRouter

            // Act
            var t = createdProvider.LastModified;
            var updateResult = await _providerRepository.SaveProviderAsync(createdProvider);
            var retrievedProvider = await _providerRepository.GetProviderAsync(createdProvider.Id);

            // Assert
            Assert.True(updateResult);
            Assert.NotNull(retrievedProvider);
            Assert.Equal("Updated Provider", retrievedProvider.Name);
            Assert.Equal("https://api.updated.com", retrievedProvider.ApiEndpoint);
            Assert.Equal("updated-key", retrievedProvider.ApiKey);
            Assert.Equal("60", retrievedProvider.Parameters["Timeout"]);
            Assert.Equal("Value", retrievedProvider.Parameters["NewParam"]);
            Assert.False(retrievedProvider.IsEnabled);
            Assert.Equal(5, retrievedProvider.ProviderType); // OpenRouter
            Assert.Equal(createdProvider.CreatedAt.ToLocalTime(), retrievedProvider.CreatedAt.ToLocalTime());
            Assert.NotEqual(t, retrievedProvider.LastModified);
        }

        [Fact]
        public async Task CreateProviderWithExistingIdThrowsException()
        {
            // Arrange
            var provider = new APIProvider
            {
                Id = 1, // Set an existing ID
                Name = "Test Provider",
                ProviderType = 0 // Unknown
            };

            // Act & Assert
            var ret = await _providerRepository.CreateProviderAsync(provider);
            Assert.NotNull(ret);
            Assert.NotEqual(1, ret.Id);
            Assert.NotEqual(0, ret.Id);
        }

        [Fact]
        public async Task SaveProviderWithoutIdThrowsException()
        {
            // Arrange
            var provider = new APIProvider
            {
                Id = 0, // Invalid ID for save
                Name = "Test Provider",
                ProviderType = 0 // Unknown
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _providerRepository.SaveProviderAsync(provider));
        }

        [Fact]
        public async Task DeleteNonExistentProviderReturnsFalse()
        {
            // Arrange - nothing to arrange

            // Act
            var result = await _providerRepository.DeleteProviderAsync(999);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SaveProviderWithDifferentProviderTypesWorks()
        {
            // Arrange and create multiple providers with different types
            var openAIProvider = new APIProvider
            {
                Name = "OpenAI Provider",
                ApiEndpoint = "https://api.openai.com",
                ApiKey = "key-openai",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 1 // OpenAI
            };
            
            var anthropicProvider = new APIProvider
            {
                Name = "Anthropic Provider",
                ApiEndpoint = "https://api.anthropic.com",
                ApiKey = "key-anthropic",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 2 // Anthropic
            };
            
            var googleProvider = new APIProvider
            {
                Name = "Google Provider",
                ApiEndpoint = "https://api.google.com",
                ApiKey = "key-google",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 3 // Google
            };
            
            var localProvider = new APIProvider
            {
                Name = "Local Provider",
                ApiEndpoint = "http://localhost",
                ApiKey = "key-local",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 4 // Local
            };
            
            var openRouterProvider = new APIProvider
            {
                Name = "OpenRouter Provider",
                ApiEndpoint = "https://api.openrouter.com",
                ApiKey = "key-openrouter",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 5 // OpenRouter
            };
            
            // Act - Create all providers
            await _providerRepository.CreateProviderAsync(openAIProvider);
            await _providerRepository.CreateProviderAsync(anthropicProvider);
            await _providerRepository.CreateProviderAsync(googleProvider);
            await _providerRepository.CreateProviderAsync(localProvider);
            await _providerRepository.CreateProviderAsync(openRouterProvider);
            
            // Get all providers
            var allProviders = await _providerRepository.GetAllProvidersAsync();
            
            // Assert
            Assert.Equal(5, allProviders.Count());
            Assert.Contains(allProviders, p => p.ProviderType == 1); // OpenAI
            Assert.Contains(allProviders, p => p.ProviderType == 2); // Anthropic
            Assert.Contains(allProviders, p => p.ProviderType == 3); // Google
            Assert.Contains(allProviders, p => p.ProviderType == 4); // Local
            Assert.Contains(allProviders, p => p.ProviderType == 5); // OpenRouter
        }

        [Fact]
        public async Task UpdateProviderTypeWorks()
        {
            // Arrange
            var provider = new APIProvider
            {
                Name = "Type Change Provider",
                ApiEndpoint = "https://api.example.com",
                ApiKey = "key",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 0 // Unknown // Start with Unknown
            };
            
            var createdProvider = await _providerRepository.CreateProviderAsync(provider);
            
            // Act - Change provider type
            createdProvider.ProviderType = 1; // OpenAI
            await _providerRepository.SaveProviderAsync(createdProvider);
            var retrievedProvider1 = await _providerRepository.GetProviderAsync(createdProvider.Id);
            
            // Change again
            retrievedProvider1!.ProviderType = 4; // Local
            await _providerRepository.SaveProviderAsync(retrievedProvider1);
            var retrievedProvider2 = await _providerRepository.GetProviderAsync(createdProvider.Id);
            
            // Assert
            Assert.Equal(4, retrievedProvider2!.ProviderType); // Local
        }

        [Fact]
        public async Task GetProvidersByTypeWorks()
        {
            // Arrange
            var provider1 = new APIProvider
            {
                Name = "OpenAI Provider 1",
                ApiEndpoint = "https://api.openai1.com",
                ApiKey = "key1",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 1 // OpenAI
            };
            
            var provider2 = new APIProvider
            {
                Name = "OpenAI Provider 2",
                ApiEndpoint = "https://api.openai2.com",
                ApiKey = "key2",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 1 // OpenAI
            };
            
            var provider3 = new APIProvider
            {
                Name = "Anthropic Provider",
                ApiEndpoint = "https://api.anthropic.com",
                ApiKey = "key3",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 2 // Anthropic
            };
            
            await _providerRepository.CreateProviderAsync(provider1);
            await _providerRepository.CreateProviderAsync(provider2);
            await _providerRepository.CreateProviderAsync(provider3);
            
            
        }

        [Fact]
        public async Task GetProviderByNameReturnsCorrectProvider()
        {
            // Arrange
            var provider1 = new APIProvider
            {
                Name = "Unique Name Provider",
                ApiEndpoint = "https://api.unique.com",
                ApiKey = "unique-key",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 1 // OpenAI
            };
            
            var provider2 = new APIProvider
            {
                Name = "Another Provider",
                ApiEndpoint = "https://api.another.com",
                ApiKey = "another-key",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 2 // Anthropic
            };
            
            await _providerRepository.CreateProviderAsync(provider1);
            await _providerRepository.CreateProviderAsync(provider2);
            
            // Act
            var retrievedProvider = await _providerRepository.GetProviderByNameAsync("Unique Name Provider");
            var nonExistentProvider = await _providerRepository.GetProviderByNameAsync("Non Existent Provider");
            
            // Assert
            Assert.NotNull(retrievedProvider);
            Assert.Equal("Unique Name Provider", retrievedProvider.Name);
            Assert.Equal(1, retrievedProvider.ProviderType); // OpenAI
            Assert.Null(nonExistentProvider);
        }

        [Fact]
        public async Task ProviderExistsByNameReturnsCorrectResult()
        {
            // Arrange
            var provider = new APIProvider
            {
                Name = "Existing Provider",
                ApiEndpoint = "https://api.existing.com",
                ApiKey = "existing-key",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 3 // Google
            };
            
            await _providerRepository.CreateProviderAsync(provider);
            
            // Act & Assert
            Assert.True(_providerRepository.ProviderExistsByName("Existing Provider"));
            Assert.False(_providerRepository.ProviderExistsByName("Non Existent Provider"));
        }

        [Fact]
        public async Task SaveAndGetProviderWithNullApiKeyWorks()
        {
            // Arrange
            var provider = new APIProvider
            {
                Name = "Test Provider With Null API Key",
                ApiEndpoint = "https://api.test.com",
                ApiKey = null,
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 6 // Ollama
            };

            // Act - Create
            var createdProvider = await _providerRepository.CreateProviderAsync(provider);
            var retrievedProvider = await _providerRepository.GetProviderAsync(createdProvider.Id);

            // Assert - Create
            Assert.NotNull(retrievedProvider);
            Assert.Equal(createdProvider.Id, retrievedProvider.Id);
            Assert.Equal("Test Provider With Null API Key", retrievedProvider.Name);
            Assert.Equal("https://api.test.com", retrievedProvider.ApiEndpoint);
            Assert.Null(retrievedProvider.ApiKey);
            Assert.True(retrievedProvider.IsEnabled);
            Assert.Equal(6, retrievedProvider.ProviderType); // Ollama

            // Act - Update with API Key
            retrievedProvider.ApiKey = "new-api-key";
            var updateResult = await _providerRepository.SaveProviderAsync(retrievedProvider);
            var updatedProvider = await _providerRepository.GetProviderAsync(retrievedProvider.Id);

            // Assert - Update
            Assert.True(updateResult);
            Assert.NotNull(updatedProvider);
            Assert.Equal("new-api-key", updatedProvider.ApiKey);

            // Act - Update back to null
            updatedProvider.ApiKey = null;
            var updateResult2 = await _providerRepository.SaveProviderAsync(updatedProvider);
            var updatedProvider2 = await _providerRepository.GetProviderAsync(updatedProvider.Id);

            // Assert - Update back to null
            Assert.True(updateResult2);
            Assert.NotNull(updatedProvider2);
            Assert.Null(updatedProvider2.ApiKey);
        }

        [Fact]
        public async Task TimeoutAndMaxConcurrencyFieldsArePersisted()
        {
            // Arrange
            var provider = new APIProvider
            {
                Name = "Timeout Test Provider",
                ApiEndpoint = "https://api.timeout-test.com",
                ApiKey = "timeout-test-key",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 1, // OpenAI
                Timeout = 45,
                MaxConcurrency = 8
            };
            
            // Act
            var createdProvider = await _providerRepository.CreateProviderAsync(provider);
            var retrievedProvider = await _providerRepository.GetProviderAsync(createdProvider.Id);

            // Assert
            Assert.NotNull(retrievedProvider);
            Assert.Equal(45, retrievedProvider.Timeout);
            Assert.Equal(8, retrievedProvider.MaxConcurrency);
            Assert.Equal("Timeout Test Provider", retrievedProvider.Name);

            // Test updating Timeout and MaxConcurrency
            retrievedProvider.Timeout = 120;
            retrievedProvider.MaxConcurrency = 3;
            
            await _providerRepository.SaveProviderAsync(retrievedProvider);
            var updatedProvider = await _providerRepository.GetProviderAsync(retrievedProvider.Id);

            Assert.NotNull(updatedProvider);
            Assert.Equal(120, updatedProvider.Timeout);
            Assert.Equal(3, updatedProvider.MaxConcurrency);
        }

        [Fact]
        public async Task DefaultTimeoutAndMaxConcurrencyValuesArePersisted()
        {
            // Arrange - Provider without explicit Timeout and MaxConcurrency
            var provider = new APIProvider
            {
                Name = "Default Values Provider",
                ApiEndpoint = "https://api.default.com",
                ApiKey = "default-key",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 2 // Anthropic
                // Timeout and MaxConcurrency not set, should use default values
            };
            
            // Act
            var createdProvider = await _providerRepository.CreateProviderAsync(provider);
            var retrievedProvider = await _providerRepository.GetProviderAsync(createdProvider.Id);

            // Assert
            Assert.NotNull(retrievedProvider);
            Assert.Equal(30000, retrievedProvider.Timeout); // Default value from model
            Assert.Equal(10, retrievedProvider.MaxConcurrency); // Default value from model
        }

        [Fact]
        public async Task CreateProviderWithDuplicateNameThrowsException()
        {
            // Arrange
            var provider1 = new APIProvider
            {
                Name = "Duplicate Provider Name",
                ApiEndpoint = "https://api.provider1.com",
                ApiKey = "key1",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 1 // OpenAI
            };

            var provider2 = new APIProvider
            {
                Name = "Duplicate Provider Name", // Same name as provider1
                ApiEndpoint = "https://api.provider2.com",
                ApiKey = "key2",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 2 // Anthropic
            };

            // Act & Assert
            await _providerRepository.CreateProviderAsync(provider1);
            
            // Creating second provider with same name should throw exception due to unique constraint
            await Assert.ThrowsAnyAsync<Exception>(() => _providerRepository.CreateProviderAsync(provider2));
        }

        [Fact]
        public async Task UpdateProviderToDuplicateNameThrowsException()
        {
            // Arrange
            var provider1 = new APIProvider
            {
                Name = "Original Provider 1",
                ApiEndpoint = "https://api.provider1.com",
                ApiKey = "key1",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 1 // OpenAI
            };

            var provider2 = new APIProvider
            {
                Name = "Original Provider 2",
                ApiEndpoint = "https://api.provider2.com",
                ApiKey = "key2",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true,
                ProviderType = 2 // Anthropic
            };

            var createdProvider1 = await _providerRepository.CreateProviderAsync(provider1);
            var createdProvider2 = await _providerRepository.CreateProviderAsync(provider2);

            // Act & Assert
            // Try to update provider2 to have the same name as provider1
            createdProvider2.Name = "Original Provider 1";
            
            // This should throw an exception due to unique constraint violation
            await Assert.ThrowsAnyAsync<Exception>(() => _providerRepository.SaveProviderAsync(createdProvider2));
        }
    }
}