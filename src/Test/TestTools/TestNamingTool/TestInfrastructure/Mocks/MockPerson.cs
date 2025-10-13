using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;

namespace TestNamingTool.TestInfrastructure.Mocks
{
    public class MockPerson : IPerson, IHostPerson
    {
        public MockPerson(string name, string description = "")
        {
            Name = name;
            Description = description;
        }

    // IPerson members
    public long Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public byte[]? Image { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string ProviderName { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public double? PresencePenalty { get; set; }
    public double? FrequencyPenalty { get; set; }
    public double? TopP { get; set; }
    public int? TopK { get; set; }
    public double? Temperature { get; set; }
    public long? Capability1 { get; set; }
    public long? Capability2 { get; set; }
    public long? Capability3 { get; set; }
    public string? DeveloperMessage { get; set; } = string.Empty;
    public string[] ToolNames { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public PersonType PersonType { get; set; } = PersonType.Normal;
    public long AppId { get; set; }

        public override string ToString()
        {
            return $"{Name}: {Description}";
        }

        public override bool Equals(object? obj)
        {
            if (obj is IPerson other)
            {
                return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode(StringComparison.OrdinalIgnoreCase);
        }
    }
}
