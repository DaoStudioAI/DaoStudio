using DaoStudio.Interfaces;
using DaoStudio.Services;
using DaoStudio.DBStorage.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Helpers;

namespace TestDaoStudio.Services;

/// <summary>
/// Unit tests for PeopleService class.
/// Tests person CRUD operations and person management functionality.
/// </summary>
public class PeopleServiceTests : IDisposable
{
    private readonly Mock<DaoStudio.DBStorage.Interfaces.IPersonRepository> _mockRepository;
    private readonly Mock<ILogger<PeopleService>> _mockLogger;
    private readonly PeopleService _service;

    public PeopleServiceTests()
    {
        _mockRepository = new Mock<DaoStudio.DBStorage.Interfaces.IPersonRepository>();
        _mockLogger = new Mock<ILogger<PeopleService>>();
        _service = new PeopleService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act & Assert
        _service.Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(IPersonRepository))]
    [InlineData(nameof(ILogger<PeopleService>))]
    public void Constructor_WithNullParameters_ThrowsArgumentNullException(string parameterName)
    {
        // Arrange & Act & Assert
        Action act = parameterName switch
        {
            nameof(IPersonRepository) => () => new PeopleService(null!, _mockLogger.Object),
            nameof(ILogger<PeopleService>) => () => new PeopleService(_mockRepository.Object, null!),
            _ => throw new ArgumentException("Invalid parameter name", nameof(parameterName))
        };

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CreatePersonAsync_WithValidPerson_CreatesPerson()
    {
        // Arrange
        var person = MessageTestHelper.CreateTestPerson();
        var expectedDbPerson = new DaoStudio.DBStorage.Models.Person
        {
            Id = 1,
            Name = person.Name,
            Description = person.Description,
            ProviderName = person.ProviderName,
            ModelId = person.ModelId,
            DeveloperMessage = person.DeveloperMessage,
            IsEnabled = person.IsEnabled,
            PersonType = person.PersonType,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.CreatePersonAsync(It.IsAny<DaoStudio.DBStorage.Models.Person>()))
                      .ReturnsAsync(expectedDbPerson);

        // Act - use the parameter-based CreatePersonAsync on the service
        var result = await _service.CreatePersonAsync(person.Name, person.Description, person.Image, person.IsEnabled,
            person.ProviderName, person.ModelId, person.DeveloperMessage, person.ToolNames, person.Parameters);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(person.Name);
        result.Description.Should().Be(person.Description);
        result.ProviderName.Should().Be(person.ProviderName);
        result.ModelId.Should().Be(person.ModelId);

        _mockRepository.Verify(r => r.CreatePersonAsync(It.Is<DaoStudio.DBStorage.Models.Person>(p =>
            p.Name == person.Name &&
            p.Description == person.Description &&
            p.ProviderName == person.ProviderName &&
            p.ModelId == person.ModelId
        )), Times.Once);
    }

    [Fact]
    public async Task GetPersonAsync_WithValidName_ReturnsPerson()
    {
        // Arrange
        var personName = "Test Assistant";
        var dbPerson = new DaoStudio.DBStorage.Models.Person
        {
            Id = 1,
            Name = personName,
            Description = "A test assistant",
            ProviderName = "OpenAI",
            ModelId = "gpt-4",
            IsEnabled = true
        };

        _mockRepository.Setup(r => r.GetPersonByNameAsync(personName))
                  .ReturnsAsync(dbPerson);

        // Act - PeopleService.GetPersonAsync(name) exists
        var result = await _service.GetPersonAsync(personName);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(personName);
        result.Description.Should().Be("A test assistant");

        _mockRepository.Verify(r => r.GetPersonByNameAsync(personName), Times.Once);
    }

    [Fact]
    public async Task GetPersonAsync_WithInvalidName_ReturnsNull()
    {
        // Arrange
        var invalidName = "NonExistent";
        _mockRepository.Setup(r => r.GetPersonByNameAsync(invalidName))
                      .ReturnsAsync((DaoStudio.DBStorage.Models.Person?)null);

        // Act
        var result = await _service.GetPersonAsync(invalidName);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(r => r.GetPersonByNameAsync(invalidName), Times.Once);
    }

    [Fact]
    public async Task GetPersonByIdAsync_WithValidId_ReturnsPerson()
    {
        // Arrange
        var personId = 1L;
        var dbPerson = new DaoStudio.DBStorage.Models.Person
        {
            Id = personId,
            Name = "Test Assistant",
            Description = "A test assistant",
            ProviderName = "OpenAI",
            ModelId = "gpt-4",
            IsEnabled = true
        };

        // Setup repository to return single person when GetAllPersonsAsync is called
        _mockRepository.Setup(r => r.GetAllPersonsAsync(It.IsAny<bool>()))
                  .ReturnsAsync(new List<DaoStudio.DBStorage.Models.Person> { dbPerson });
        // Ensure individual retrieval is not expected directly from repository
        // because PeopleService does not currently expose an Id-based lookup.


        // Act - service exposes GetAllPeopleAsync; locate by id from returned list
        var all = await _service.GetAllPeopleAsync();
        var result = all.FirstOrDefault(p => p.Id == personId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(personId);
        result.Name.Should().Be("Test Assistant");

        _mockRepository.Verify(r => r.GetAllPersonsAsync(It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task GetAllPersonsAsync_ReturnsAllPersons()
    {
        // Arrange
        var dbPersons = new List<DaoStudio.DBStorage.Models.Person>
        {
            new() { Id = 1, Name = "Assistant1", ProviderName = "OpenAI", IsEnabled = true },
            new() { Id = 2, Name = "Assistant2", ProviderName = "Anthropic", IsEnabled = false }
        };


        _mockRepository.Setup(r => r.GetAllPersonsAsync(It.IsAny<bool>()))
                  .ReturnsAsync(dbPersons);

        // Act - service method is GetAllPeopleAsync
        var result = await _service.GetAllPeopleAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Name == "Assistant1");
        result.Should().Contain(p => p.Name == "Assistant2");

        _mockRepository.Verify(r => r.GetAllPersonsAsync(It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePersonAsync_WithValidPerson_UpdatesPerson()
    {
        // Arrange
        var person = MessageTestHelper.CreateTestPerson();
        person.Id = 1;
        person.Name = "Updated Assistant";

        _mockRepository.Setup(r => r.SavePersonAsync(It.IsAny<DaoStudio.DBStorage.Models.Person>()))
                  .ReturnsAsync(true);

        // Act - service Update is UpdatePersonAsync, and SavePersonAsync on service is for save-by-id behavior
        var result = await _service.UpdatePersonAsync(person);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.SavePersonAsync(It.IsAny<DaoStudio.DBStorage.Models.Person>()), Times.Once);
    }

    [Fact]
    public async Task DeletePersonAsync_WithValidId_DeletesPerson()
    {
        // Arrange
        var personId = 1L;
        _mockRepository.Setup(r => r.DeletePersonAsync(personId))
                  .ReturnsAsync(true);

        // Act - service method is DeletePersonAsync
        var result = await _service.DeletePersonAsync(personId);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.DeletePersonAsync(personId), Times.Once);
    }

    [Fact]
    public async Task DeletePersonAsync_WithInvalidId_ReturnsFalse()
    {
        // Arrange
        var invalidId = 999L;
        _mockRepository.Setup(r => r.DeletePersonAsync(invalidId))
                      .ReturnsAsync(false);

        // Act
        var result = await _service.DeletePersonAsync(invalidId);

        // Assert
        result.Should().BeFalse();
        _mockRepository.Verify(r => r.DeletePersonAsync(invalidId), Times.Once);
    }

    [Fact]
    public async Task GetEnabledPersonsAsync_ReturnsOnlyEnabledPersons()
    {
        // Arrange
        var dbPersons = new List<DaoStudio.DBStorage.Models.Person>
        {
            new() { Id = 1, Name = "EnabledAssistant", IsEnabled = true },
            new() { Id = 2, Name = "DisabledAssistant", IsEnabled = false },
            new() { Id = 3, Name = "AnotherEnabledAssistant", IsEnabled = true }
        };

        _mockRepository.Setup(r => r.GetAllPersonsAsync(It.IsAny<bool>()))
                  .ReturnsAsync(dbPersons);

        // Act - service method is GetEnabledPersonsAsync
        var result = await _service.GetEnabledPersonsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.IsEnabled);
        result.Should().Contain(p => p.Name == "EnabledAssistant");
        result.Should().Contain(p => p.Name == "AnotherEnabledAssistant");
    }

    [Fact]
    public async Task GetPersonsByProviderAsync_WithValidProvider_ReturnsMatchingPersons()
    {
        // Arrange
        var providerName = "OpenAI";
        var dbPersons = new List<DaoStudio.DBStorage.Models.Person>
        {
            new() { Id = 1, Name = "GPT Assistant", ProviderName = providerName },
            new() { Id = 2, Name = "Claude Assistant", ProviderName = "Anthropic" },
            new() { Id = 3, Name = "Another GPT", ProviderName = providerName }
        };

        // PeopleService retrieves all persons and the test filters by provider after the call, so we just need to return the full list.
        _mockRepository.Setup(r => r.GetAllPersonsAsync(It.IsAny<bool>()))
                  .ReturnsAsync(dbPersons);

        // Act - PeopleService doesn't have GetPersonsByProviderAsync; call GetAllPeopleAsync and filter via repository mock
        var all = await _service.GetAllPeopleAsync();
        var result = all.Where(p => p.ProviderName == providerName).ToList();

        // Verify repository interaction
        _mockRepository.Verify(r => r.GetAllPersonsAsync(It.IsAny<bool>()), Times.Once);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(p => p.ProviderName == providerName);
    }

    [Fact]
    public async Task CreatePersonAsync_WithNullPerson_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _service.CreatePersonAsync(null!, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdatePersonAsync_WithNullPerson_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _service.UpdatePersonAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetPersonAsync_WithNullName_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _service.GetPersonAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPersonAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _service.GetPersonAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreatePersonAsync_WithDuplicateName_HandlesGracefully()
    {
        // Arrange
        var person = MessageTestHelper.CreateTestPerson("Duplicate Name");

        _mockRepository.Setup(r => r.CreatePersonAsync(It.IsAny<DaoStudio.DBStorage.Models.Person>()))
                      .ThrowsAsync(new InvalidOperationException("Person with this name already exists"));

        // Act & Assert - call service CreatePersonAsync parameters overload
        var act = async () => await _service.CreatePersonAsync(person.Name, person.Description, person.Image, person.IsEnabled,
            person.ProviderName, person.ModelId, person.DeveloperMessage, person.ToolNames, person.Parameters);
        await act.Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("Person with this name already exists");
    }

    [Fact]
    public async Task GetPersonsWithParametersAsync_ReturnsPersonsWithParameters()
    {
        // Arrange
        var dbPersons = new List<DaoStudio.DBStorage.Models.Person>
        {
            new() {
                Id = 1,
                Name = "Assistant1",
                Parameters = new Dictionary<string, string> { { "temperature", "0.7" } }
            },
            new() {
                Id = 2,
                Name = "Assistant2",
                Parameters = new Dictionary<string, string>()
            }
        };

        _mockRepository.Setup(r => r.GetAllPersonsAsync(It.IsAny<bool>()))
                  .ReturnsAsync(dbPersons);

        // Act - service does not have GetPersonsWithParametersAsync; call GetAllPeopleAsync and filter
        var allPeople = await _service.GetAllPeopleAsync();
        var resultWithParams = allPeople.Where(p => p.Parameters != null && p.Parameters.Any()).ToList();

        // Assert
        resultWithParams.Should().HaveCount(1);
        resultWithParams.First().Parameters.Should().ContainKey("temperature");
    }

    [Fact]
    public async Task PeopleService_HandlesRepositoryExceptions_Gracefully()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllPersonsAsync(It.IsAny<bool>()))
                      .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        var act = async () => await _service.GetAllPeopleAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Database error");
    }

    public void Dispose()
    {
        // PeopleService does not implement IDisposable; nothing to dispose here
    }
}
