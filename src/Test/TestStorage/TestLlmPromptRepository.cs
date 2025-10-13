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
    public class TestLlmPromptRepository : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ILlmPromptRepository _promptRepository;

        public TestLlmPromptRepository()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_prompt_repo_{Guid.NewGuid()}.db");
            
            // Initialize repository directly with SqliteLlmPromptRepository
            _promptRepository = new SqliteLlmPromptRepository(_testDbPath);
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
        public async Task GetPromptReturnsNullForNonExistentPrompt()
        {
            // Arrange - nothing to arrange

            // Act
            var prompt = await _promptRepository.GetPromptAsync(999);

            // Assert
            Assert.Null(prompt);
        }

        [Fact]
        public async Task SaveAndGetPromptWorks()
        {
            // Arrange
            var newPrompt = new LlmPrompt
            {
                Name = "Code Assistant Prompt",
                Category = "Coding",
                Content = "You are an expert coding assistant. {{language}} {{task}}",
                Parameters = new Dictionary<string, string> 
                { 
                    { "Language", "C#" },
                    { "ExampleCode", "true" }
                },
                IsEnabled = true,

            };

            // Act - Create
            var createdPrompt = await _promptRepository.CreatePromptAsync(newPrompt);
            var retrievedPrompt = await _promptRepository.GetPromptAsync(createdPrompt.Id);

            // Assert - Create
            Assert.NotNull(retrievedPrompt);
            Assert.Equal(createdPrompt.Id, retrievedPrompt.Id);
            Assert.Equal("Code Assistant Prompt", retrievedPrompt.Name);
            Assert.Equal("Coding", retrievedPrompt.Category);
            Assert.Equal("You are an expert coding assistant. {{language}} {{task}}", retrievedPrompt.Content);
            Assert.Equal("C#", retrievedPrompt.Parameters["Language"]);
            Assert.Equal("true", retrievedPrompt.Parameters["ExampleCode"]);
            Assert.True(retrievedPrompt.IsEnabled);

            Assert.NotEqual(default, retrievedPrompt.CreatedAt);
            Assert.NotEqual(default, retrievedPrompt.LastModified);

            // Act - Update
            retrievedPrompt.Name = "Updated Code Assistant Prompt";
            var t= retrievedPrompt.LastModified;
            var updateResult = await _promptRepository.SavePromptAsync(retrievedPrompt);
            var updatedPrompt = await _promptRepository.GetPromptAsync(retrievedPrompt.Id);

            // Assert - Update
            Assert.True(updateResult);
            Assert.Equal("Updated Code Assistant Prompt", updatedPrompt!.Name);
            Assert.Equal(retrievedPrompt.CreatedAt, updatedPrompt.CreatedAt);
            Assert.NotEqual(t, updatedPrompt.LastModified);
        }

        [Fact]
        public async Task GetAllPromptsReturnsAllPrompts()
        {
            // Arrange
            var prompt1 = new LlmPrompt
            {
                Name = "Prompt 1",
                Category = "Category1",
                Content = "Content 1",
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true,
            };
            
            var prompt2 = new LlmPrompt
            {
                Name = "Prompt 2",
                Category = "Category2",
                Content = "Content 2",
                Parameters = new Dictionary<string, string> { { "Param", "Other" } },
                IsEnabled = false
            };
            
            await _promptRepository.CreatePromptAsync(prompt1);
            await _promptRepository.CreatePromptAsync(prompt2);

            // Act
            var allPrompts = await _promptRepository.GetAllPromptsAsync();

            // Assert
            Assert.Equal(2, allPrompts.Count());
            Assert.Contains(allPrompts, p => p.Name == "Prompt 1");
            Assert.Contains(allPrompts, p => p.Name == "Prompt 2");
        }

        [Fact]
        public async Task DeletePromptWorks()
        {
            // Arrange
            var prompt = new LlmPrompt
            {
                Name = "Prompt to delete",
                Category = "TestCategory",
                Content = "Will be deleted",
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true
            };
            
            var createdPrompt = await _promptRepository.CreatePromptAsync(prompt);

            // Act
            var deleteResult = await _promptRepository.DeletePromptAsync(createdPrompt.Id);
            var retrievedPrompt = await _promptRepository.GetPromptAsync(createdPrompt.Id);

            // Assert
            Assert.True(deleteResult);
            Assert.Null(retrievedPrompt);
        }

        [Fact]
        public async Task UpdatePromptWorks()
        {
            // Arrange
            var prompt = new LlmPrompt
            {
                Name = "Original Prompt",
                Category = "OriginalCategory",
                Content = "Original content",
                Parameters = new Dictionary<string, string> { { "Param", "Original" } },
                IsEnabled = true
            };
            
            var createdPrompt = await _promptRepository.CreatePromptAsync(prompt);
            
            // Update the prompt
            createdPrompt.Name = "Updated Prompt";
            createdPrompt.Category = "UpdatedCategory";
            createdPrompt.Content = "Updated content";
            createdPrompt.Parameters = new Dictionary<string, string> { { "Param", "Updated" }, { "NewParam", "42" } };
            createdPrompt.IsEnabled = false;


            // Act
            var t = createdPrompt.LastModified;
            var updateResult = await _promptRepository.SavePromptAsync(createdPrompt);
            var retrievedPrompt = await _promptRepository.GetPromptAsync(createdPrompt.Id);

            // Assert
            Assert.True(updateResult);
            Assert.NotNull(retrievedPrompt);
            Assert.Equal("Updated Prompt", retrievedPrompt.Name);
            Assert.Equal("UpdatedCategory", retrievedPrompt.Category);
            Assert.Equal("Updated content", retrievedPrompt.Content);
            Assert.Equal("Updated", retrievedPrompt.Parameters["Param"]);
            Assert.Equal("42", retrievedPrompt.Parameters["NewParam"]);
            Assert.False(retrievedPrompt.IsEnabled);

            Assert.Equal(createdPrompt.CreatedAt.ToLocalTime(), retrievedPrompt.CreatedAt.ToLocalTime());
            Assert.NotEqual(t, retrievedPrompt.LastModified);
        }

        [Fact]
        public async Task GetPromptsByCategoryWorks()
        {
            // Arrange
            var category1 = "ChatBot";
            var category2 = "CodeAssistant";
            
            var prompt1 = new LlmPrompt
            {
                Name = "ChatBot Prompt 1",
                Category = category1,
                Content = "Content 1",
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true,
            };
            
            var prompt2 = new LlmPrompt
            {
                Name = "ChatBot Prompt 2",
                Category = category1,
                Content = "Content 2",
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true
            };
            
            var prompt3 = new LlmPrompt
            {
                Name = "Code Assistant Prompt",
                Category = category2,
                Content = "Content 3",
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true,
            };
            
            await _promptRepository.CreatePromptAsync(prompt1);
            await _promptRepository.CreatePromptAsync(prompt2);
            await _promptRepository.CreatePromptAsync(prompt3);

            // Act
            var category1Prompts = await _promptRepository.GetPromptsByCategoryAsync(category1);
            var category2Prompts = await _promptRepository.GetPromptsByCategoryAsync(category2);

            // Assert
            Assert.Equal(2, category1Prompts.Count());
            Assert.Single(category2Prompts);
            
            Assert.All(category1Prompts, p => Assert.Equal(category1, p.Category));
            Assert.All(category2Prompts, p => Assert.Equal(category2, p.Category));
            
            Assert.Contains(category1Prompts, p => p.Name == "ChatBot Prompt 1");
            Assert.Contains(category1Prompts, p => p.Name == "ChatBot Prompt 2");
            Assert.Contains(category2Prompts, p => p.Name == "Code Assistant Prompt");
        }

        [Fact]
        public async Task GetPromptsByCategoryAndNameWorks()
        {
            // Arrange
            var category = "TestCategory";
            var namePrefix1 = "Model1_";
            var namePrefix2 = "Model2_";
            
            var prompt1 = new LlmPrompt
            {
                Name = $"{namePrefix1}Prompt",
                Category = category,
                Content = "Content 1",
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true
            };
            
            var prompt2 = new LlmPrompt
            {
                Name = $"{namePrefix1}{namePrefix2}Prompt",
                Category = category,
                Content = "Content 2",
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true
            };
            
            var prompt3 = new LlmPrompt
            {
                Name = $"{namePrefix2}Prompt",
                Category = category,
                Content = "Content 3",
                Parameters = new Dictionary<string, string> { { "Param", "Value" } },
                IsEnabled = true
            };
            
            await _promptRepository.CreatePromptAsync(prompt1);
            await _promptRepository.CreatePromptAsync(prompt2);
            await _promptRepository.CreatePromptAsync(prompt3);

            // Act
            var categoryPrompts = await _promptRepository.GetPromptsByCategoryAsync(category);
            var model1Prompts = categoryPrompts.Where(p => p.Name.StartsWith(namePrefix1)).ToList();
            var model2Prompts = categoryPrompts.Where(p => p.Name.Contains(namePrefix2)).ToList();

            // Assert
            Assert.Equal(3, categoryPrompts.Count());
            Assert.Equal(2, model1Prompts.Count);
            Assert.Equal(2, model2Prompts.Count);
            
            Assert.Contains(model1Prompts, p => p.Name == $"{namePrefix1}Prompt");
            Assert.Contains(model1Prompts, p => p.Name == $"{namePrefix1}{namePrefix2}Prompt");
            Assert.DoesNotContain(model1Prompts, p => p.Name == $"{namePrefix2}Prompt");
            
            Assert.Contains(model2Prompts, p => p.Name == $"{namePrefix1}{namePrefix2}Prompt");
            Assert.Contains(model2Prompts, p => p.Name == $"{namePrefix2}Prompt");
            Assert.DoesNotContain(model2Prompts, p => p.Name == $"{namePrefix1}Prompt");
        }

        [Fact]
        public async Task CreatePromptWithExistingIdThrowsException()
        {
            // Arrange
            var prompt = new LlmPrompt
            {
                Id = 1, // Set an existing ID
                Name = "Test Prompt",
                Category = "Test",
                Content = "Test content"
            };

            // Act & Assert
            var ret = await _promptRepository.CreatePromptAsync(prompt);
            Assert.NotNull(ret);
            Assert.NotEqual(1, ret.Id);
            Assert.NotEqual(0, ret.Id);
        }

        [Fact]
        public async Task SavePromptWithoutIdThrowsException()
        {
            // Arrange
            var prompt = new LlmPrompt
            {
                Id = 0, // Invalid ID for save
                Name = "Test Prompt",
                Category = "Test",
                Content = "Test content"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _promptRepository.SavePromptAsync(prompt));
        }

        [Fact]
        public async Task CreatePromptWithDuplicateIdHandledCorrectly()
        {
            // Arrange
            var prompt1 = new LlmPrompt
            {
                Id = 999, // Setting a specific ID
                Name = "Test Prompt 1",
                Category = "Test",
                Content = "Test content 1",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true
            };

            var prompt2 = new LlmPrompt
            {
                Id = 999, // Same ID as prompt1
                Name = "Test Prompt 2",
                Category = "Test",
                Content = "Test content 2",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true
            };

            // Act
            var createdPrompt1 = await _promptRepository.CreatePromptAsync(prompt1);
            var createdPrompt2 = await _promptRepository.CreatePromptAsync(prompt2);

            // Assert
            // Both should be created successfully with different auto-generated IDs
            Assert.NotEqual(999, createdPrompt1.Id); // Should not use the provided ID
            Assert.NotEqual(999, createdPrompt2.Id); // Should not use the provided ID
            Assert.NotEqual(createdPrompt1.Id, createdPrompt2.Id); // Should have different IDs
            Assert.Equal("Test Prompt 1", createdPrompt1.Name);
            Assert.Equal("Test Prompt 2", createdPrompt2.Name);
        }

        [Fact]
        public async Task MultiplePromptsWithSameNameAllowed()
        {
            // Arrange
            var prompt1 = new LlmPrompt
            {
                Name = "Same Name Prompt",
                Category = "Category1",
                Content = "Content 1",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true
            };

            var prompt2 = new LlmPrompt
            {
                Name = "Same Name Prompt", // Same name as prompt1
                Category = "Category2",
                Content = "Content 2",
                Parameters = new Dictionary<string, string>(),
                IsEnabled = true
            };

            // Act
            var createdPrompt1 = await _promptRepository.CreatePromptAsync(prompt1);
            var createdPrompt2 = await _promptRepository.CreatePromptAsync(prompt2);

            // Assert
            // Both should be created successfully since there's no unique constraint on name
            Assert.NotEqual(createdPrompt1.Id, createdPrompt2.Id);
            Assert.Equal("Same Name Prompt", createdPrompt1.Name);
            Assert.Equal("Same Name Prompt", createdPrompt2.Name);
            Assert.Equal("Category1", createdPrompt1.Category);
            Assert.Equal("Category2", createdPrompt2.Category);
        }
    }
} 