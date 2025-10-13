using DaoStudio;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Microsoft.Extensions.AI;
using Moq;

namespace TestDaoStudio.Infrastructure;

/// <summary>
/// Factory for creating mock AI engines for testing purposes.
/// Provides pre-configured mock engines with predictable behaviors.
/// </summary>
public static class MockEngineFactory
{
    /// <summary>
    /// Creates a mock OpenAI engine with standard responses.
    /// </summary>
    public static IEngine CreateMockOpenAIEngine(string? customResponse = null)
    {
        var mockEngine = new Mock<IEngine>();
        var response = customResponse ?? "This is a mock OpenAI response.";

        // Create a mock person for OpenAI
        var mockPerson = new Mock<IPerson>();
        mockPerson.Setup(p => p.Name).Returns("MockOpenAI");
        mockPerson.Setup(p => p.ModelId).Returns("gpt-4-mock");
        mockPerson.Setup(p => p.ProviderName).Returns("OpenAI");

        mockEngine.Setup(e => e.Person).Returns(mockPerson.Object);

        mockEngine.Setup(e => e.GetMessageAsync(
                It.IsAny<List<IMessage>>(),
                It.IsAny<Dictionary<string, List<FunctionWithDescription>>?>(),
                It.IsAny<ISession>(),
                It.IsAny<CancellationToken>()))
                  .ReturnsAsync(CreateMockMessageResponse(response));

        return mockEngine.Object;
    }

    /// <summary>
    /// Creates a mock Google engine with standard responses.
    /// </summary>
    public static IEngine CreateMockGoogleEngine(string? customResponse = null)
    {
        var mockEngine = new Mock<IEngine>();
        var response = customResponse ?? "This is a mock Google Gemini response.";

        // Create a mock person for Google
        var mockPerson = new Mock<IPerson>();
        mockPerson.Setup(p => p.Name).Returns("MockGoogle");
        mockPerson.Setup(p => p.ModelId).Returns("gemini-pro-mock");
        mockPerson.Setup(p => p.ProviderName).Returns("Google");

        mockEngine.Setup(e => e.Person).Returns(mockPerson.Object);

        mockEngine.Setup(e => e.GetMessageAsync(
                It.IsAny<List<IMessage>>(),
                It.IsAny<Dictionary<string, List<FunctionWithDescription>>?>(),
                It.IsAny<ISession>(),
                It.IsAny<CancellationToken>()))
                  .ReturnsAsync(CreateMockMessageResponse(response));

        return mockEngine.Object;
    }

    /// <summary>
    /// Creates a mock AWS Bedrock engine with standard responses.
    /// </summary>
    public static IEngine CreateMockAWSBedrockEngine(string? customResponse = null)
    {
        var mockEngine = new Mock<IEngine>();
        var response = customResponse ?? "This is a mock AWS Bedrock Claude response.";

        // Create a mock person for AWS Bedrock
        var mockPerson = new Mock<IPerson>();
        mockPerson.Setup(p => p.Name).Returns("MockAWSBedrock");
        mockPerson.Setup(p => p.ModelId).Returns("claude-3-sonnet-mock");
        mockPerson.Setup(p => p.ProviderName).Returns("AWS");

        mockEngine.Setup(e => e.Person).Returns(mockPerson.Object);

        mockEngine.Setup(e => e.GetMessageAsync(
                It.IsAny<List<IMessage>>(),
                It.IsAny<Dictionary<string, List<FunctionWithDescription>>?>(),
                It.IsAny<ISession>(),
                It.IsAny<CancellationToken>()))
                  .ReturnsAsync(CreateMockMessageResponse(response));

        return mockEngine.Object;
    }

    /// <summary>
    /// Creates a mock Ollama engine with standard responses.
    /// </summary>
    public static IEngine CreateMockOllamaEngine(string? customResponse = null)
    {
        var mockEngine = new Mock<IEngine>();
        var response = customResponse ?? "This is a mock Ollama response.";

        // Create a mock person for Ollama
        var mockPerson = new Mock<IPerson>();
        mockPerson.Setup(p => p.Name).Returns("MockOllama");
        mockPerson.Setup(p => p.ModelId).Returns("llama2-mock");
        mockPerson.Setup(p => p.ProviderName).Returns("Ollama");

        mockEngine.Setup(e => e.Person).Returns(mockPerson.Object);

        mockEngine.Setup(e => e.GetMessageAsync(
                It.IsAny<List<IMessage>>(),
                It.IsAny<Dictionary<string, List<FunctionWithDescription>>?>(),
                It.IsAny<ISession>(),
                It.IsAny<CancellationToken>()))
                  .ReturnsAsync(CreateMockMessageResponse(response));

        return mockEngine.Object;
    }

    /// <summary>
    /// Creates a mock engine that throws exceptions for error testing.
    /// </summary>
    public static IEngine CreateErrorEngine(Exception exceptionToThrow)
    {
        var mockEngine = new Mock<IEngine>();

        // Create a mock person for error engine
        var mockPerson = new Mock<IPerson>();
        mockPerson.Setup(p => p.Name).Returns("MockErrorEngine");
        mockPerson.Setup(p => p.ModelId).Returns("error-mock");
        mockPerson.Setup(p => p.ProviderName).Returns("Error");

        mockEngine.Setup(e => e.Person).Returns(mockPerson.Object);

        mockEngine.Setup(e => e.GetMessageAsync(
                It.IsAny<List<IMessage>>(),
                It.IsAny<Dictionary<string, List<FunctionWithDescription>>?>(),
                It.IsAny<ISession>(),
                It.IsAny<CancellationToken>()))
                  .ThrowsAsync(exceptionToThrow);

        return mockEngine.Object;
    }

    /// <summary>
    /// Creates a mock engine with custom behavior setup.
    /// </summary>
    public static Mock<IEngine> CreateCustomMockEngine(string name, string modelId)
    {
        var mockEngine = new Mock<IEngine>();

        // Create a mock person for custom engine
        var mockPerson = new Mock<IPerson>();
        mockPerson.Setup(p => p.Name).Returns(name);
        mockPerson.Setup(p => p.ModelId).Returns(modelId);
        mockPerson.Setup(p => p.ProviderName).Returns("Custom");

        mockEngine.Setup(e => e.Person).Returns(mockPerson.Object);

        return mockEngine;
    }

    /// <summary>
    /// Creates a mock engine that simulates slow responses for performance testing.
    /// </summary>
    public static IEngine CreateSlowEngine(int delayMs = 2000, string? customResponse = null)
    {
        var mockEngine = new Mock<IEngine>();
        var response = customResponse ?? "This is a slow mock response.";

        // Create a mock person for slow engine
        var mockPerson = new Mock<IPerson>();
        mockPerson.Setup(p => p.Name).Returns("MockSlowEngine");
        mockPerson.Setup(p => p.ModelId).Returns("slow-mock");
        mockPerson.Setup(p => p.ProviderName).Returns("Slow");

        mockEngine.Setup(e => e.Person).Returns(mockPerson.Object);

        mockEngine.Setup(e => e.GetMessageAsync(
                It.IsAny<List<IMessage>>(),
                It.IsAny<Dictionary<string, List<FunctionWithDescription>>?>(),
                It.IsAny<ISession>(),
                It.IsAny<CancellationToken>()))
                  .Returns(async (List<IMessage> messages, Dictionary<string, List<FunctionWithDescription>>? tools, ISession session, CancellationToken token) =>
                  {
                      await Task.Delay(delayMs, token);
                      return CreateMockMessageResponse(response);
                  });

        return mockEngine.Object;
    }

    /// <summary>
    /// Creates a mock engine that supports function calling.
    /// </summary>
    public static IEngine CreateFunctionCallingEngine()
    {
        var mockEngine = new Mock<IEngine>();

        // Create a mock person for function calling engine
        var mockPerson = new Mock<IPerson>();
        mockPerson.Setup(p => p.Name).Returns("MockFunctionEngine");
        mockPerson.Setup(p => p.ModelId).Returns("function-mock");
        mockPerson.Setup(p => p.ProviderName).Returns("Function");

        mockEngine.Setup(e => e.Person).Returns(mockPerson.Object);

        mockEngine.Setup(e => e.GetMessageAsync(
                It.IsAny<List<IMessage>>(),
                It.IsAny<Dictionary<string, List<FunctionWithDescription>>?>(),
                It.IsAny<ISession>(),
                It.IsAny<CancellationToken>()))
                  .ReturnsAsync((List<IMessage> messages, Dictionary<string, List<FunctionWithDescription>>? tools, ISession session, CancellationToken token) =>
                  {
                      var lastMessage = messages.LastOrDefault();
                      // Simulate function call response
                      if (lastMessage?.Content?.Contains("weather") == true)
                      {
                          return CreateMockMessageResponseWithTools("I'll check the weather for you.");
                      }

                      return CreateMockMessageResponse("This is a function-calling mock response.");
                  });

        return mockEngine.Object;
    }

    /// <summary>
    /// Creates a mock engine that simulates cancellation scenarios.
    /// </summary>
    public static IEngine CreateCancellableEngine()
    {
        var mockEngine = new Mock<IEngine>();

        // Create a mock person for cancellable engine
        var mockPerson = new Mock<IPerson>();
        mockPerson.Setup(p => p.Name).Returns("MockCancellableEngine");
        mockPerson.Setup(p => p.ModelId).Returns("cancellable-mock");
        mockPerson.Setup(p => p.ProviderName).Returns("Cancellable");

        mockEngine.Setup(e => e.Person).Returns(mockPerson.Object);

        mockEngine.Setup(e => e.GetMessageAsync(
                It.IsAny<List<IMessage>>(),
                It.IsAny<Dictionary<string, List<FunctionWithDescription>>?>(),
                It.IsAny<ISession>(),
                It.IsAny<CancellationToken>()))
                  .Returns(async (List<IMessage> messages, Dictionary<string, List<FunctionWithDescription>>? tools, ISession session, CancellationToken token) =>
                  {
                      // Simulate long-running operation that can be cancelled
                      for (int i = 0; i < 100; i++)
                      {
                          token.ThrowIfCancellationRequested();
                          await Task.Delay(10, token);
                      }

                      return CreateMockMessageResponse("This response completed without cancellation.");
                  });

        return mockEngine.Object;
    }

    /// <summary>
    /// Creates a collection of different mock engines for comprehensive testing.
    /// </summary>
    public static Dictionary<string, IEngine> CreateEngineCollection()
    {
        return new Dictionary<string, IEngine>
        {
            { "OpenAI", CreateMockOpenAIEngine() },
            { "Google", CreateMockGoogleEngine() },
            { "AWS", CreateMockAWSBedrockEngine() },
            { "Ollama", CreateMockOllamaEngine() },
            { "Slow", CreateSlowEngine(1000) },
            { "FunctionCalling", CreateFunctionCallingEngine() },
            { "Cancellable", CreateCancellableEngine() }
        };
    }

    /// <summary>
    /// Creates a mock engine with conversation history support.
    /// </summary>
    public static IEngine CreateConversationEngine()
    {
        var mockEngine = new Mock<IEngine>();
        var conversationHistory = new List<IMessage>();

        // Create a mock person for conversation engine
        var mockPerson = new Mock<IPerson>();
        mockPerson.Setup(p => p.Name).Returns("MockConversationEngine");
        mockPerson.Setup(p => p.ModelId).Returns("conversation-mock");
        mockPerson.Setup(p => p.ProviderName).Returns("Conversation");

        mockEngine.Setup(e => e.Person).Returns(mockPerson.Object);

        mockEngine.Setup(e => e.GetMessageAsync(
                It.IsAny<List<IMessage>>(),
                It.IsAny<Dictionary<string, List<FunctionWithDescription>>?>(),
                It.IsAny<ISession>(),
                It.IsAny<CancellationToken>()))
                  .ReturnsAsync((List<IMessage> messages, Dictionary<string, List<FunctionWithDescription>>? tools, ISession session, CancellationToken token) =>
                  {
                      conversationHistory.AddRange(messages);
                      var lastMessage = messages.LastOrDefault();
                      
                      var responseContent = $"I received your message: '{lastMessage?.Content}'. This is message #{conversationHistory.Count} in our conversation.";
                      return CreateMockMessageResponse(responseContent);
                  });

        return mockEngine.Object;
    }

    private static async IAsyncEnumerable<IMessage> CreateMockMessageResponse(string response)
    {
        var mockMessage = new Mock<IMessage>();
        mockMessage.Setup(m => m.Content).Returns(response);
        mockMessage.Setup(m => m.Role).Returns(MessageRole.Assistant);
        mockMessage.Setup(m => m.CreatedAt).Returns(DateTime.UtcNow);
    // Ensure BinaryContents is an empty list by default
    mockMessage.Setup(m => m.BinaryContents).Returns(new List<IMsgBinaryData>());

    yield return mockMessage.Object;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<IMessage> CreateMockMessageResponseWithTools(string response)
    {
        var mockMessage = new Mock<IMessage>();
        mockMessage.Setup(m => m.Content).Returns(response);
        mockMessage.Setup(m => m.Role).Returns(MessageRole.Assistant);
        mockMessage.Setup(m => m.CreatedAt).Returns(DateTime.UtcNow);
        
    // Add mock tool call binary data using IMsgBinaryData with MsgBinaryDataType.ToolCall
    var mockBinary = new Mock<IMsgBinaryData>();
    mockBinary.Setup(b => b.Name).Returns("tool_call");
    mockBinary.Setup(b => b.Type).Returns(MsgBinaryDataType.ToolCall);
    mockBinary.Setup(b => b.Data).Returns(System.Text.Encoding.UTF8.GetBytes("{\"id\":\"call_123\",\"function\":{\"name\":\"get_weather\",\"arguments\":\"{\\\"location\\\":\\\"New York\\\"}\"}}"));

    mockMessage.Setup(m => m.BinaryContents).Returns(new List<IMsgBinaryData> { mockBinary.Object });

    yield return mockMessage.Object;
        await Task.CompletedTask;
    }
}
