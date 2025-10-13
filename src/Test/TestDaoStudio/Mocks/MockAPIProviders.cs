using DaoStudio.Interfaces;
using Moq;

namespace TestDaoStudio.Mocks;

/// <summary>
/// Mock API provider implementations for testing purposes.
/// Provides pre-configured mock API providers with predictable behaviors.
/// </summary>
public static class MockAPIProviders
{
    /// <summary>
    /// Creates a mock OpenAI API provider with standard configuration.
    /// </summary>
    public static IApiProvider CreateMockOpenAIProvider(string? customApiKey = null, bool isEnabled = true)
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns("OpenAI");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("https://api.openai.com/v1");
        mockProvider.Setup(p => p.ApiKey).Returns(customApiKey ?? "sk-test-openai-key-123");
        mockProvider.Setup(p => p.IsEnabled).Returns(isEnabled);
        mockProvider.Setup(p => p.Id).Returns(1);
        mockProvider.Setup(p => p.CreatedAt).Returns(DateTime.UtcNow.AddDays(-30));
        mockProvider.Setup(p => p.LastModified).Returns(DateTime.UtcNow);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock Anthropic API provider with standard configuration.
    /// </summary>
    public static IApiProvider CreateMockAnthropicProvider(string? customApiKey = null, bool isEnabled = true)
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns("Anthropic");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("https://api.anthropic.com");
        mockProvider.Setup(p => p.ApiKey).Returns(customApiKey ?? "sk-ant-test-key-123");
        mockProvider.Setup(p => p.IsEnabled).Returns(isEnabled);
        mockProvider.Setup(p => p.Id).Returns(2);
        mockProvider.Setup(p => p.CreatedAt).Returns(DateTime.UtcNow.AddDays(-25));
        mockProvider.Setup(p => p.LastModified).Returns(DateTime.UtcNow);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock Google API provider with standard configuration.
    /// </summary>
    public static IApiProvider CreateMockGoogleProvider(string? customApiKey = null, bool isEnabled = true)
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns("Google");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("https://generativelanguage.googleapis.com/v1");
        mockProvider.Setup(p => p.ApiKey).Returns(customApiKey ?? "AIza-test-google-key-123");
        mockProvider.Setup(p => p.IsEnabled).Returns(isEnabled);
        mockProvider.Setup(p => p.Id).Returns(3);
        mockProvider.Setup(p => p.CreatedAt).Returns(DateTime.UtcNow.AddDays(-20));
        mockProvider.Setup(p => p.LastModified).Returns(DateTime.UtcNow);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock AWS Bedrock API provider with standard configuration.
    /// </summary>
    public static IApiProvider CreateMockAWSBedrockProvider(string? customApiKey = null, bool isEnabled = true)
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns("AWS Bedrock");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("https://bedrock-runtime.us-east-1.amazonaws.com");
        mockProvider.Setup(p => p.ApiKey).Returns(customApiKey ?? "AKIA-test-aws-key-123");
        mockProvider.Setup(p => p.IsEnabled).Returns(isEnabled);
        mockProvider.Setup(p => p.Id).Returns(4);
        mockProvider.Setup(p => p.CreatedAt).Returns(DateTime.UtcNow.AddDays(-15));
        mockProvider.Setup(p => p.LastModified).Returns(DateTime.UtcNow);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock Ollama API provider with standard configuration.
    /// </summary>
    public static IApiProvider CreateMockOllamaProvider(bool isEnabled = true)
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns("Ollama");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("http://localhost:11434");
        mockProvider.Setup(p => p.ApiKey).Returns((string?)null); // Ollama doesn't require API key
        mockProvider.Setup(p => p.IsEnabled).Returns(isEnabled);
        mockProvider.Setup(p => p.Id).Returns(5);
        mockProvider.Setup(p => p.CreatedAt).Returns(DateTime.UtcNow.AddDays(-10));
        mockProvider.Setup(p => p.LastModified).Returns(DateTime.UtcNow);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock API provider with custom configuration.
    /// </summary>
    public static IApiProvider CreateMockCustomProvider(
        string name, 
        string baseUrl, 
        string? apiKey = null, 
        bool isEnabled = true,
        int id = 999)
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns(name);
    mockProvider.Setup(p => p.ApiEndpoint).Returns(baseUrl);
        mockProvider.Setup(p => p.ApiKey).Returns(apiKey);
        mockProvider.Setup(p => p.IsEnabled).Returns(isEnabled);
        mockProvider.Setup(p => p.Id).Returns(id);
        mockProvider.Setup(p => p.CreatedAt).Returns(DateTime.UtcNow.AddDays(-5));
        mockProvider.Setup(p => p.LastModified).Returns(DateTime.UtcNow);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock API provider that throws exceptions for error testing.
    /// </summary>
    public static IApiProvider CreateMockErrorProvider(Exception exceptionToThrow)
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns("ErrorProvider");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("https://error.test.com");
        mockProvider.Setup(p => p.ApiKey).Throws(exceptionToThrow);
        mockProvider.Setup(p => p.IsEnabled).Returns(true);
        mockProvider.Setup(p => p.Id).Returns(-1);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock API provider with invalid configuration for testing edge cases.
    /// </summary>
    public static IApiProvider CreateMockInvalidProvider()
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns("");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("invalid-url");
        mockProvider.Setup(p => p.ApiKey).Returns("");
        mockProvider.Setup(p => p.IsEnabled).Returns(false);
        mockProvider.Setup(p => p.Id).Returns(0);
        mockProvider.Setup(p => p.CreatedAt).Returns(DateTime.MinValue);
        mockProvider.Setup(p => p.LastModified).Returns(DateTime.MinValue);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a collection of different mock API providers for comprehensive testing.
    /// </summary>
    public static Dictionary<string, IApiProvider> CreateProviderCollection()
    {
        return new Dictionary<string, IApiProvider>
        {
            { "OpenAI", CreateMockOpenAIProvider() },
            { "Anthropic", CreateMockAnthropicProvider() },
            { "Google", CreateMockGoogleProvider() },
            { "AWS", CreateMockAWSBedrockProvider() },
            { "Ollama", CreateMockOllamaProvider() },
            { "Disabled", CreateMockOpenAIProvider(isEnabled: false) },
            { "Custom", CreateMockCustomProvider("CustomAI", "https://custom.ai.com", "custom-key") }
        };
    }

    /// <summary>
    /// Creates a mock API provider with rate limiting behavior.
    /// </summary>
    public static IApiProvider CreateMockRateLimitedProvider()
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns("RateLimited");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("https://ratelimited.api.com");
        mockProvider.Setup(p => p.ApiKey).Returns("rate-limited-key");
        mockProvider.Setup(p => p.IsEnabled).Returns(true);
        mockProvider.Setup(p => p.Id).Returns(100);

        // Add custom property for rate limit tracking
        var callCount = 0;
        mockProvider.Setup(p => p.GetType().GetProperty("CallCount"))
                   .Returns(() => ++callCount);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock API provider with authentication issues.
    /// </summary>
    public static IApiProvider CreateMockUnauthorizedProvider()
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns("Unauthorized");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("https://unauthorized.api.com");
        mockProvider.Setup(p => p.ApiKey).Returns("invalid-key");
        mockProvider.Setup(p => p.IsEnabled).Returns(true);
        mockProvider.Setup(p => p.Id).Returns(401);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock API provider for testing network timeouts.
    /// </summary>
    public static IApiProvider CreateMockTimeoutProvider()
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns("TimeoutProvider");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("https://timeout.api.com");
        mockProvider.Setup(p => p.ApiKey).Returns("timeout-key");
        mockProvider.Setup(p => p.IsEnabled).Returns(true);
        mockProvider.Setup(p => p.Id).Returns(408);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock API provider with SSL/TLS issues.
    /// </summary>
    public static IApiProvider CreateMockSSLProvider()
    {
        var mockProvider = new Mock<IApiProvider>();

        mockProvider.Setup(p => p.Name).Returns("SSLProvider");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("https://ssl-error.api.com");
        mockProvider.Setup(p => p.ApiKey).Returns("ssl-key");
        mockProvider.Setup(p => p.IsEnabled).Returns(true);
        mockProvider.Setup(p => p.Id).Returns(495);

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock API provider that returns different responses based on call count.
    /// </summary>
    public static IApiProvider CreateMockStatefulProvider()
    {
        var mockProvider = new Mock<IApiProvider>();
        var callCount = 0;

        mockProvider.Setup(p => p.Name).Returns("StatefulProvider");
    mockProvider.Setup(p => p.ApiEndpoint).Returns("https://stateful.api.com");
        mockProvider.Setup(p => p.IsEnabled).Returns(true);
        mockProvider.Setup(p => p.Id).Returns(200);

        // API key changes based on call count to simulate state changes
        mockProvider.Setup(p => p.ApiKey).Returns(() => 
        {
            callCount++;
            return $"stateful-key-{callCount}";
        });

        return mockProvider.Object;
    }

    /// <summary>
    /// Creates a mock API provider for testing configuration updates.
    /// </summary>
    public static Mock<IApiProvider> CreateMockUpdatableProvider()
    {
        var mockProvider = new Mock<IApiProvider>();
        
        var name = "UpdatableProvider";
    var baseUrl = "https://updatable.api.com";
        var apiKey = "updatable-key";
        var isEnabled = true;

        mockProvider.Setup(p => p.Name).Returns(() => name);
    mockProvider.Setup(p => p.ApiEndpoint).Returns(() => baseUrl);
        mockProvider.Setup(p => p.ApiKey).Returns(() => apiKey);
        mockProvider.Setup(p => p.IsEnabled).Returns(() => isEnabled);
        mockProvider.Setup(p => p.Id).Returns(300);

        // Allow updating properties
        mockProvider.SetupSet(p => p.Name = It.IsAny<string>()).Callback<string>(value => name = value);
    mockProvider.SetupSet(p => p.ApiEndpoint = It.IsAny<string>()).Callback<string>(value => baseUrl = value);
        mockProvider.SetupSet(p => p.ApiKey = It.IsAny<string>()).Callback<string>(value => apiKey = value);
        mockProvider.SetupSet(p => p.IsEnabled = It.IsAny<bool>()).Callback<bool>(value => isEnabled = value);

        return mockProvider;
    }

    /// <summary>
    /// Creates mock API providers for all major AI service providers.
    /// </summary>
    public static List<IApiProvider> CreateAllMajorProviders()
    {
        return new List<IApiProvider>
        {
            CreateMockOpenAIProvider(),
            CreateMockAnthropicProvider(),
            CreateMockGoogleProvider(),
            CreateMockAWSBedrockProvider(),
            CreateMockOllamaProvider(),
            CreateMockCustomProvider("Cohere", "https://api.cohere.ai/v1", "cohere-key"),
            CreateMockCustomProvider("Hugging Face", "https://api-inference.huggingface.co", "hf-key"),
            CreateMockCustomProvider("Azure OpenAI", "https://your-resource.openai.azure.com", "azure-key")
        };
    }
}
