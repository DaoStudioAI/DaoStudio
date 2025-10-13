using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;

namespace TestDaoStudio.Mocks;

/// <summary>
/// Mock implementation of IPerson with configurable properties for testing.
/// Allows tests to easily create person instances with specific characteristics.
/// </summary>
public class MockPerson : IPerson
{
    public long Id { get; set; } = 1;
    public string Name { get; set; } = "Test Person";
    public string Description { get; set; } = "A test person for unit testing";
    public byte[]? Image { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string ProviderName { get; set; } = "OpenAI";
    public string ModelId { get; set; } = "test-model";
    public double? PresencePenalty { get; set; }
    public double? FrequencyPenalty { get; set; }
    public double? TopP { get; set; }
    public int? TopK { get; set; }
    public double? Temperature { get; set; }
    public long? Capability1 { get; set; }
    public long? Capability2 { get; set; }
    public long? Capability3 { get; set; }
    public string? DeveloperMessage { get; set; } = "You are a test assistant";
    public string[] ToolNames { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    public PersonType PersonType { get; set; } = PersonType.Normal;
    public long AppId { get; set; } = 1;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a mock person with default assistant configuration.
    /// </summary>
    public static MockPerson CreateAssistant(
        string name = "Test Assistant",
        string providerName = "OpenAI",
        string modelId = "gpt-4")
    {
        return new MockPerson
        {
            Name = name,
            Description = $"Test {name} for unit testing",
            ProviderName = providerName,
            ModelId = modelId,
            PersonType = PersonType.Normal,
            DeveloperMessage = "You are a helpful test assistant.",
            IsEnabled = true
        };
    }

    /// <summary>
    /// Creates a mock user person.
    /// </summary>
    public static MockPerson CreateUser(string name = "Test User")
    {
        return new MockPerson
        {
            Name = name,
            Description = $"Test user: {name}",
            ProviderName = "",
            ModelId = "",
            PersonType = PersonType.Normal,
            DeveloperMessage = "",
            IsEnabled = true
        };
    }

    /// <summary>
    /// Creates a mock person with specific tools configured.
    /// </summary>
    public static MockPerson CreateWithTools(params string[] toolNames)
    {
        return new MockPerson
        {
            ToolNames = toolNames,
            Description = $"Test assistant with tools: {string.Join(", ", toolNames)}"
        };
    }

    /// <summary>
    /// Creates a mock person with custom parameters.
    /// </summary>
    public static MockPerson CreateWithParameters(string parameters)
    {
        return new MockPerson
        {
            Parameters = new Dictionary<string, string> { ["custom"] = parameters },
            Description = "Test assistant with custom parameters"
        };
    }

    /// <summary>
    /// Creates a disabled mock person for testing enabled/disabled scenarios.
    /// </summary>
    public static MockPerson CreateDisabled()
    {
        return new MockPerson
        {
            Name = "Disabled Assistant",
            Description = "A disabled test assistant",
            IsEnabled = false
        };
    }
}
