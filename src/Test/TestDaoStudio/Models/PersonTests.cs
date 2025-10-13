using DaoStudio;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using FluentAssertions;
using TestDaoStudio.Helpers;

namespace TestDaoStudio.Models;

/// <summary>
/// Unit tests for the Person model class.
/// Tests property getters/setters, validation logic, and serialization/deserialization.
/// </summary>
public class PersonTests
{
    [Fact]
    public void Person_DefaultConstructor_CreatesInstanceWithDefaultValues()
    {
        // Act
        var person = new Person();

        // Assert
        person.Id.Should().Be(0);
        person.Name.Should().BeEmpty();
        person.Description.Should().BeEmpty();
        person.Image.Should().BeNull();
        person.IsEnabled.Should().BeTrue();
        person.ProviderName.Should().BeEmpty();
        person.ModelId.Should().BeEmpty();
        person.PresencePenalty.Should().BeNull();
        person.FrequencyPenalty.Should().BeNull();
        person.TopP.Should().BeNull();
        person.TopK.Should().BeNull();
        person.Temperature.Should().BeNull();
        person.Capability1.Should().BeNull();
        person.Capability2.Should().BeNull();
        person.Capability3.Should().BeNull();
        person.DeveloperMessage.Should().BeNull();
        person.ToolNames.Should().BeEmpty();
        person.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Person_PropertySettersAndGetters_WorkCorrectly()
    {
        // Arrange
        var person = new Person();
        var now = DateTime.UtcNow;
        var imageData = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        person.Id = 123;
        person.Name = "Test Assistant";
        person.Description = "A test AI assistant";
        person.Image = imageData;
        person.IsEnabled = true;
        person.ProviderName = "OpenAI";
        person.ModelId = "gpt-4";
        person.PresencePenalty = 0.5;
        person.FrequencyPenalty = 0.3;
        person.TopP = 0.9;
        person.TopK = 50;
        person.Temperature = 0.7;
        person.Capability1 = 1000L;
        person.Capability2 = 2000L;
        person.Capability3 = 3000L;
        person.DeveloperMessage = "You are helpful";
        person.ToolNames = new string[] { "weather", "calculator" };
        person.Parameters = new Dictionary<string, string> { ["temperature"] = "0.7" };
        ((IPerson)person).PersonType = PersonType.Normal;
        person.LastModified = now;
        person.CreatedAt = now;

        // Assert
        person.Id.Should().Be(123);
        person.Name.Should().Be("Test Assistant");
        person.Description.Should().Be("A test AI assistant");
        person.Image.Should().BeEquivalentTo(imageData);
        person.IsEnabled.Should().BeTrue();
        person.ProviderName.Should().Be("OpenAI");
        person.ModelId.Should().Be("gpt-4");
        person.PresencePenalty.Should().Be(0.5);
        person.FrequencyPenalty.Should().Be(0.3);
        person.TopP.Should().Be(0.9);
        person.TopK.Should().Be(50);
        person.Temperature.Should().Be(0.7);
        person.Capability1.Should().Be(1000L);
        person.Capability2.Should().Be(2000L);
        person.Capability3.Should().Be(3000L);
        person.DeveloperMessage.Should().Be("You are helpful");
        person.ToolNames.Should().Equal(new string[] { "weather", "calculator" });
        person.Parameters.Should().ContainKey("temperature");
        person.Parameters["temperature"].Should().Be("0.7");
        ((IPerson)person).PersonType.Should().Be(PersonType.Normal);
        person.LastModified.Should().Be(now);
        person.CreatedAt.Should().Be(now);
    }

    [Theory]
    [InlineData(PersonType.Normal)]
    public void Person_PersonTypeProperty_HandlesAllValidTypes(PersonType personType)
    {
        // Arrange
        var person = new Person();

        // Act
        ((IPerson)person).PersonType = personType;

        // Assert
        ((IPerson)person).PersonType.Should().Be(personType);
    }

    [Fact]
    public void FromDBPerson_WithValidDBPerson_ConvertsCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var imageData = new byte[] { 1, 2, 3, 4, 5 };
    var dbPerson = new DaoStudio.DBStorage.Models.Person
        {
            Id = 123,
            Name = "Test Assistant",
            Description = "A test AI assistant",
            Image = imageData,
            IsEnabled = true,
            ProviderName = "OpenAI",
            ModelId = "gpt-4",
            PresencePenalty = 0.5,
            FrequencyPenalty = 0.3,
            TopP = 0.9,
            TopK = 50,
            Temperature = 0.7,
            Capability1 = 1000L,
            Capability2 = 2000L,
            Capability3 = 3000L,
            DeveloperMessage = "You are helpful",
            ToolNames = new string[] { "weather", "calculator" },
            Parameters = new Dictionary<string, string> { ["temperature"] = "0.7" },
            PersonType = (int)PersonType.Normal,
            LastModified = now,
            CreatedAt = now
        };

        // Act
        var person = Person.FromDBPerson(dbPerson);

        // Assert
        person.Id.Should().Be(123);
        person.Name.Should().Be("Test Assistant");
        person.Description.Should().Be("A test AI assistant");
        person.Image.Should().BeEquivalentTo(imageData);
        person.IsEnabled.Should().BeTrue();
        person.ProviderName.Should().Be("OpenAI");
        person.ModelId.Should().Be("gpt-4");
        person.PresencePenalty.Should().Be(0.5);
        person.FrequencyPenalty.Should().Be(0.3);
        person.TopP.Should().Be(0.9);
        person.TopK.Should().Be(50);
        person.Temperature.Should().Be(0.7);
        person.Capability1.Should().Be(1000L);
        person.Capability2.Should().Be(2000L);
        person.Capability3.Should().Be(3000L);
        person.DeveloperMessage.Should().Be("You are helpful");
        person.ToolNames.Should().Equal(new string[] { "weather", "calculator" });
        person.Parameters.Should().ContainKey("temperature");
        person.Parameters["temperature"].Should().Be("0.7");
        ((IPerson)person).PersonType.Should().Be(PersonType.Normal);
        person.LastModified.Should().Be(now);
        person.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void FromDBPerson_WithNullDBPerson_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => Person.FromDBPerson(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("dbPerson");
    }

    [Fact]
    public void MessageTestHelper_CreateTestPerson_CreatesValidPerson()
    {
        // Act
        var person = MessageTestHelper.CreateTestPerson();

        // Assert
        MessageTestHelper.IsValidPerson(person).Should().BeTrue();
        person.Name.Should().NotBeEmpty();
        person.Description.Should().NotBeEmpty();
        ((IPerson)person).PersonType.Should().Be(PersonType.Normal);
    }

    [Fact]
    public void MessageTestHelper_CreateTestUser_CreatesUserPerson()
    {
        // Act
        var person = MessageTestHelper.CreateTestUser("John Doe");

        // Assert
        person.Name.Should().Be("John Doe");
        ((IPerson)person).PersonType.Should().Be(PersonType.Normal);
        person.ProviderName.Should().BeEmpty();
        person.ModelId.Should().BeEmpty();
    }

    [Fact]
    public void MessageTestHelper_CreateAnthropicTestPerson_CreatesAnthropicPerson()
    {
        // Act
        var person = MessageTestHelper.CreateAnthropicTestPerson();

        // Assert
        person.Name.Should().Be("Claude Assistant");
        person.ProviderName.Should().Be("Anthropic");
        person.ModelId.Should().Be("claude-3-haiku-20240307");
        ((IPerson)person).PersonType.Should().Be(PersonType.Normal);
    }

    [Fact]
    public void MessageTestHelper_CreatePersonWithParameters_SetsParametersCorrectly()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { PersonParameterNames.LimitMaxContextLength, "200" },
            { PersonParameterNames.AdditionalContent, "test content" }
        };

        // Act
        var person = MessageTestHelper.CreatePersonWithParameters(parameters);

        // Assert
        person.Parameters.Should().NotBeNullOrEmpty();
        person.Parameters.Should().ContainKey(PersonParameterNames.LimitMaxContextLength);
        person.Parameters.Should().ContainKey(PersonParameterNames.AdditionalContent);
    }

    [Fact]
    public void Person_WithMinimalRequiredFields_IsValid()
    {
        // Arrange
        var person = new Person
        {
            Name = "Minimal Person",
            Description = "Minimal description"
        };

        // Act & Assert
        MessageTestHelper.IsValidPerson(person).Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "Description")]
    [InlineData("", "Description")]
    [InlineData("Name", null)]
    [InlineData("Name", "")]
    public void Person_WithMissingRequiredFields_IsInvalid(string? name, string? description)
    {
        // Arrange
        var person = new Person
        {
            Name = name!,
            Description = description!
        };

        // Act & Assert
        MessageTestHelper.IsValidPerson(person).Should().BeFalse();
    }

    [Fact]
    public void Person_ImplementsIPerson_Correctly()
    {
        // Arrange
        var person = new Person();

        // Act & Assert
        person.Should().BeAssignableTo<IPerson>();
        
        // Verify interface contract
        var iPerson = (IPerson)person;
        iPerson.Name.Should().Be(person.Name);
        iPerson.Description.Should().Be(person.Description);
        iPerson.PersonType.Should().Be((PersonType)person.PersonType);
    }

    [Fact]
    public void Person_WithTools_HandlesToolNamesCorrectly()
    {
        // Arrange
        var person = new Person
        {
            ToolNames = new string[] { "weather", "calculator", "search" }
        };

        // Act
        var toolNames = person.ToolNames;

        // Assert
        toolNames.Should().NotBeNull();
        toolNames.Should().HaveCount(3);
        toolNames.Should().Contain("weather");
        toolNames.Should().Contain("calculator");
        toolNames.Should().Contain("search");
    }

    [Fact]
    public void Person_WithEmptyToolNames_HandlesGracefully()
    {
        // Arrange
        var person = new Person
        {
            ToolNames = Array.Empty<string>()
        };

        // Act
        var toolNames = person.ToolNames;

        // Assert
        toolNames.Should().NotBeNull();
        toolNames.Should().BeEmpty();
    }
}
