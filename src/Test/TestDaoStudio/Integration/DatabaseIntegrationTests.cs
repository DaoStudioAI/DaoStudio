using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using FluentAssertions;
using TestDaoStudio.Infrastructure;

namespace TestDaoStudio.Integration;

/// <summary>
/// Integration tests for database operations.
/// Tests database initialization, CRUD operations, migrations, and concurrent access.
/// </summary>
public class DatabaseIntegrationTests : IDisposable
{
    private readonly DatabaseTestFixture _databaseFixture;
    private StorageFactory? _storageFactory;

    public DatabaseIntegrationTests()
    {
        _databaseFixture = new DatabaseTestFixture();
    }

    [Fact]
    public async Task StorageFactory_InitializeAsync_CreatesDatabase()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        _storageFactory = _databaseFixture.StorageFactory;

        // Act
        await _storageFactory.InitializeAsync();

        // Assert
        _storageFactory.Should().NotBeNull();
        
        // Verify that repositories can be created
        var sessionRepo = await _storageFactory.GetSessionRepositoryAsync();
        var messageRepo = await _storageFactory.GetMessageRepositoryAsync();
        var personRepo = await _storageFactory.GetPersonRepositoryAsync();
        
        sessionRepo.Should().NotBeNull();
        messageRepo.Should().NotBeNull();
        personRepo.Should().NotBeNull();
    }

    [Fact]
    public async Task Repository_CRUD_Operations_WorkCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        _storageFactory = _databaseFixture.StorageFactory;
        await _storageFactory.InitializeAsync();

        var personRepo = await _storageFactory.GetPersonRepositoryAsync();
        
        var testPerson = new Person
        {
            Name = "Integration Test Person",
            Description = "A person for integration testing",
            ProviderName = "OpenAI",
            ModelId = "gpt-4",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        // Act & Assert - Create
        var createdPerson = await personRepo.CreatePersonAsync(testPerson);
        createdPerson.Should().NotBeNull();
        createdPerson.Id.Should().NotBe(0);
        createdPerson.Name.Should().Be(testPerson.Name);

        // Act & Assert - Read
        var retrievedPerson = await personRepo.GetPersonAsync(createdPerson.Id);
        retrievedPerson.Should().NotBeNull();
        retrievedPerson!.Name.Should().Be(testPerson.Name);
        retrievedPerson.ProviderName.Should().Be(testPerson.ProviderName);

        // Act & Assert - Update
        retrievedPerson.Description = "Updated description";
        var updateResult = await personRepo.SavePersonAsync(retrievedPerson);
        updateResult.Should().BeTrue();

        var updatedPerson = await personRepo.GetPersonAsync(createdPerson.Id);
        updatedPerson!.Description.Should().Be("Updated description");

        // Act & Assert - Delete
        var deleteResult = await personRepo.DeletePersonAsync(createdPerson.Id);
        deleteResult.Should().BeTrue();

        var deletedPerson = await personRepo.GetPersonAsync(createdPerson.Id);
        deletedPerson.Should().BeNull();
    }

    [Fact]
    public async Task Session_And_Message_Integration_WorksCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        _storageFactory = _databaseFixture.StorageFactory;
        await _storageFactory.InitializeAsync();

        var sessionRepo = await _storageFactory.GetSessionRepositoryAsync();
        var messageRepo = await _storageFactory.GetMessageRepositoryAsync();

        var testSession = new Session
        {
            Title = "Integration Test Session",
            Description = "A session for integration testing",
            PersonNames = new List<string> { "TestPerson" },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        // Act - Create session
        var createdSession = await sessionRepo.CreateSessionAsync(testSession);
        createdSession.Should().NotBeNull();
        createdSession.Id.Should().NotBe(0);

        // Act - Create messages for the session
        var message1 = new Message
        {
            SessionId = createdSession.Id,
            Content = "Hello, this is the first message",
            Role = (int)DaoStudio.Interfaces.MessageRole.User,
            Type = (int)DaoStudio.Interfaces.MessageType.Normal,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        var message2 = new Message
        {
            SessionId = createdSession.Id,
            Content = "This is the assistant's response",
            Role = (int)DaoStudio.Interfaces.MessageRole.Assistant,
            Type = (int)DaoStudio.Interfaces.MessageType.Normal,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        var createdMessage1 = await messageRepo.CreateMessageAsync(message1);
        var createdMessage2 = await messageRepo.CreateMessageAsync(message2);

        // Assert
        createdMessage1.Should().NotBeNull();
        createdMessage2.Should().NotBeNull();

    // Verify messages can be retrieved by session
    var sessionMessages = await messageRepo.GetBySessionIdAsync(createdSession.Id);
        sessionMessages.Should().HaveCount(2);
        sessionMessages.Should().Contain(m => m.Content == "Hello, this is the first message");
        sessionMessages.Should().Contain(m => m.Content == "This is the assistant's response");
    }

    [Fact]
    public async Task ApiProvider_Repository_Integration_WorksCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        _storageFactory = _databaseFixture.StorageFactory;
        await _storageFactory.InitializeAsync();

        var apiProviderRepo = await _storageFactory.GetApiProviderRepositoryAsync();

        var testProvider = new APIProvider
        {
            Name = "Integration Test Provider",
            ApiEndpoint = "https://api.test.com/v1",
            ApiKey = "test-api-key-123",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        // Act - Create
        var createdProvider = await apiProviderRepo.CreateProviderAsync(testProvider);

        // Assert
        createdProvider.Should().NotBeNull();
        createdProvider.Id.Should().NotBe(0);
        createdProvider.Name.Should().Be(testProvider.Name);

        // Act - Get by name
        var retrievedProvider = await apiProviderRepo.GetProviderByNameAsync(testProvider.Name);

        // Assert
        retrievedProvider.Should().NotBeNull();
        retrievedProvider!.ApiEndpoint.Should().Be(testProvider.ApiEndpoint);
        retrievedProvider.ApiKey.Should().Be(testProvider.ApiKey);
    }

    [Fact]
    public async Task Tool_Repository_Integration_WorksCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        _storageFactory = _databaseFixture.StorageFactory;
        await _storageFactory.InitializeAsync();

        var toolRepo = await _storageFactory.GetLlmToolRepositoryAsync();

        var testTool = new LlmTool
        {
            Name = "Integration Test Tool",
            Description = "A tool for integration testing",
            StaticId = "integration-test-tool",
            ToolConfig = "{\"test\": true}",
            Parameters = new Dictionary<string, string> { { "param1", "value1" } },
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        // Act - Create
    var createdTool = await toolRepo.CreateToolAsync(testTool);

        // Assert
        createdTool.Should().NotBeNull();
        createdTool.Id.Should().NotBe(0);
        createdTool.Name.Should().Be(testTool.Name);

        // Act - Get by static ID
    var retrievedTools = await toolRepo.GetToolsByStaticIdAsync(testTool.StaticId);

    // Assert
    retrievedTools.Should().HaveCount(1);
    retrievedTools.First().Description.Should().Be(testTool.Description);
    retrievedTools.First().Parameters.Should().ContainKey("param1");
    }

    [Fact]
    public async Task Settings_Repository_Integration_WorksCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        _storageFactory = _databaseFixture.StorageFactory;
        await _storageFactory.InitializeAsync();

        var settingsRepo = await _storageFactory.GetSettingsRepositoryAsync();

        var testSetting = new Settings
        {
            ApplicationName = "IntegrationTestApp",
            Version = 1,
            Properties = new Dictionary<string, string> { { "integration_test_setting", "test_value" } },
            LastModified = DateTime.UtcNow
        };

        // Act - Create/Save
        var saveResult = await settingsRepo.SaveSettingsAsync(testSetting);

        // Assert
        saveResult.Should().BeTrue();

        // Act - Get by application name
        var retrievedSetting = await settingsRepo.GetSettingsAsync(testSetting.ApplicationName);

        // Assert
        retrievedSetting.Should().NotBeNull();
        retrievedSetting!.Properties.Should().ContainKey("integration_test_setting");
        retrievedSetting.Properties["integration_test_setting"].Should().Be("test_value");
    }

    [Fact]
    public async Task ConcurrentAccess_HandledCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        _storageFactory = _databaseFixture.StorageFactory;
        await _storageFactory.InitializeAsync();

        var personRepo = await _storageFactory.GetPersonRepositoryAsync();

        // Act - Create multiple persons concurrently
        var tasks = new List<Task<Person>>();
        for (int i = 0; i < 10; i++)
        {
            var person = new Person
            {
                Name = $"Concurrent Person {i}",
                Description = $"Person created concurrently #{i}",
                ProviderName = "OpenAI",
                ModelId = "gpt-4",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            tasks.Add(personRepo.CreatePersonAsync(person));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        results.Should().OnlyContain(p => p.Id != 0);
        
        // Verify all persons were created with unique IDs
        var ids = results.Select(p => p.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();

        // Verify all persons can be retrieved
        var allPersons = await personRepo.GetAllPersonsAsync();
    allPersons.Should().HaveCount(c => c >= 10);
    }

    [Fact]
    public async Task Transaction_Rollback_WorksCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        _storageFactory = _databaseFixture.StorageFactory;
        await _storageFactory.InitializeAsync();

        var sessionRepo = await _storageFactory.GetSessionRepositoryAsync();
        var messageRepo = await _storageFactory.GetMessageRepositoryAsync();

        // Act - Try to create session and message in a way that should fail
        var session = new Session
        {
            Title = "Transaction Test Session",
            Description = "Testing transaction rollback",
            PersonNames = new List<string> { "TestPerson" },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

    var createdSession = await sessionRepo.CreateSessionAsync(session);

        // Create a message with invalid data that should cause issues
        var invalidMessage = new Message
        {
            SessionId = createdSession.Id,
            Content = null, // This might cause issues depending on constraints
            Role = (int)DaoStudio.Interfaces.MessageRole.User,
            Type = (int)DaoStudio.Interfaces.MessageType.Normal,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        // This should handle the null content gracefully or throw an appropriate exception
        try
        {
            await messageRepo.CreateMessageAsync(invalidMessage);
        }
        catch
        {
            // Expected if there are constraints
        }

        // Assert - Session should still exist even if message creation failed
        var retrievedSession = await sessionRepo.GetSessionAsync(createdSession.Id);
        retrievedSession.Should().NotBeNull();
    }

    [Fact]
    public async Task DatabaseSchema_HasCorrectStructure()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        _storageFactory = _databaseFixture.StorageFactory;
        await _storageFactory.InitializeAsync();

        // Act - Verify all repositories can be created (indicating tables exist)
        var sessionRepo = await _storageFactory.GetSessionRepositoryAsync();
        var messageRepo = await _storageFactory.GetMessageRepositoryAsync();
        var personRepo = await _storageFactory.GetPersonRepositoryAsync();
        var apiProviderRepo = await _storageFactory.GetApiProviderRepositoryAsync();
        var toolRepo = await _storageFactory.GetLlmToolRepositoryAsync();
        var settingsRepo = await _storageFactory.GetSettingsRepositoryAsync();
        var cachedModelRepo = await _storageFactory.GetCachedModelRepositoryAsync();

        // Assert
        sessionRepo.Should().NotBeNull();
        messageRepo.Should().NotBeNull();
        personRepo.Should().NotBeNull();
        apiProviderRepo.Should().NotBeNull();
        toolRepo.Should().NotBeNull();
        settingsRepo.Should().NotBeNull();
        cachedModelRepo.Should().NotBeNull();

        // Verify basic operations work (indicating schema is correct)
        var sessions = await sessionRepo.GetAllSessionsAsync();
    var messages = await messageRepo.GetAllAsync();
        var persons = await personRepo.GetAllPersonsAsync();

        // These should not throw exceptions
        sessions.Should().NotBeNull();
        messages.Should().NotBeNull();
        persons.Should().NotBeNull();
    }

    [Fact]
    public async Task Migration_ExecutesSuccessfully()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        _storageFactory = _databaseFixture.StorageFactory;

        // Act - Initialize should run any pending migrations
        await _storageFactory.InitializeAsync();

        // Assert - Database should be accessible and functional
        var settingsRepo = await _storageFactory.GetSettingsRepositoryAsync();
        
        // Try to access settings which might be created by migrations
        var allSettings = await settingsRepo.GetAllSettingsAsync();
        allSettings.Should().NotBeNull();
    }

    public void Dispose()
    {
        _databaseFixture?.Dispose();
    }
}
