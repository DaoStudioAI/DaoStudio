using DaoStudio;
using FluentAssertions;

namespace TestDaoStudio.Models;

/// <summary>
/// Unit tests for the ApiProvider model class.
/// </summary>
public class ApiProviderTests
{
    [Fact]
    public void ApiProvider_DefaultConstructor_CreatesInstanceWithDefaultValues()
    {
        // Act
        var provider = new ApiProvider();

        // Assert
        provider.Id.Should().Be(0);
        provider.Name.Should().BeEmpty();
        provider.ApiEndpoint.Should().BeEmpty();
        provider.ApiKey.Should().BeNull();
        provider.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ApiProvider_PropertySettersAndGetters_WorkCorrectly()
    {
        // Arrange
        var provider = new ApiProvider();
        var now = DateTime.UtcNow;

        // Act
        provider.Id = 123;
        provider.Name = "OpenAI";
        provider.ApiKey = "sk-test123";
        provider.ApiEndpoint = "https://api.openai.com/v1";
        provider.IsEnabled = true;
        provider.CreatedAt = now;
        provider.LastModified = now;

        // Assert
        provider.Id.Should().Be(123);
        provider.Name.Should().Be("OpenAI");
        provider.ApiKey.Should().Be("sk-test123");
        provider.ApiEndpoint.Should().Be("https://api.openai.com/v1");
        provider.IsEnabled.Should().BeTrue();
        provider.CreatedAt.Should().Be(now);
        provider.LastModified.Should().Be(now);
    }

    [Fact]
    public void ApiProvider_WithValidData_IsValid()
    {
        // Arrange & Act
        var provider = new ApiProvider
        {
            Name = "TestProvider",
            ApiKey = "test-key",
            ApiEndpoint = "https://api.test.com",
            IsEnabled = true
        };

        // Assert
        provider.Name.Should().NotBeNullOrEmpty();
        provider.ApiKey.Should().NotBeNullOrEmpty();
        provider.ApiEndpoint.Should().NotBeNullOrEmpty();
        provider.IsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData("OpenAI", "https://api.openai.com/v1")]
    [InlineData("Anthropic", "https://api.anthropic.com")]
    [InlineData("Google", "https://generativelanguage.googleapis.com")]
    public void ApiProvider_WithDifferentProviders_HandlesProperly(string name, string apiEndpoint)
    {
        // Arrange & Act
        var provider = new ApiProvider
        {
            Name = name,
            ApiEndpoint = apiEndpoint,
            IsEnabled = true
        };

        // Assert
        provider.Name.Should().Be(name);
        provider.ApiEndpoint.Should().Be(apiEndpoint);
        provider.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ApiProvider_ToString_ReturnsExpectedFormat()
    {
        // Arrange
        var provider = new ApiProvider
        {
            Name = "OpenAI"
        };

        // Act
        var result = provider.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
    }
}
