using DaoStudio;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using System.Text;
using System.Text.Json;

namespace TestDaoStudio.Helpers;

/// <summary>
/// Helper class for creating test messages, persons, and common test scenarios.
/// Provides standardized test data to prevent duplication across tests.
/// </summary>
public static class MessageTestHelper
{
    #region Person Creation

    /// <summary>
    /// Creates a basic test person with default values.
    /// </summary>
    internal static Person CreateTestPerson(
        string name = "Test Assistant",
        string description = "A test AI assistant",
        string providerName = "OpenAI",
        string modelId = "gpt-4",
        PersonType personType = PersonType.Normal)
    {
        // Normalize provider name for tests
        var normalizedProvider = string.Equals(providerName, "TestProvider", StringComparison.OrdinalIgnoreCase)
            ? "OpenAI" // fallback to existing provider type
            : providerName;

        return new Person
        {
            Id = 1,
            Name = name,
            Description = description,
            ProviderName = normalizedProvider,
            ModelId = modelId,
            DeveloperMessage = "You are a helpful assistant for testing purposes.",
            IsEnabled = true,
            PersonType = (int)personType,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a user person for testing user interactions.
    /// </summary>
    internal static Person CreateTestUser(string name = "Test User")
    {
        return CreateTestPerson(
            name: name,
            description: "A test user",
            providerName: "",
            modelId: "",
            personType: PersonType.Normal);
    }

    /// <summary>
    /// Creates an Anthropic-based test person.
    /// </summary>
    internal static Person CreateAnthropicTestPerson()
    {
        return CreateTestPerson(
            name: "Claude Assistant",
            description: "A test Anthropic Claude assistant",
            providerName: "Anthropic",
            modelId: "claude-3-haiku-20240307");
    }

    /// <summary>
    /// Creates a person with specific parameters.
    /// </summary>
    internal static Person CreatePersonWithParameters(Dictionary<string, object> parameters)
    {
        var person = CreateTestPerson();
        person.Parameters = parameters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty);
        return person;
    }

    #endregion

    #region Message Creation

    /// <summary>
    /// Creates a basic test message with default values.
    /// </summary>
    internal static Message CreateTestMessage(
        long sessionId = 1,
        MessageRole role = MessageRole.User,
        string content = "Hello, this is a test message.",
        MessageType type = MessageType.Normal)
    {
        return new Message
        {
            Id = 1,
            SessionId = sessionId,
            Content = content,
            Role = (int)role,
            Type = (int)type,
            BinaryVersion = 0,
            ParentMsgId = 0,
            ParentSessId = 0,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a user message.
    /// </summary>
    internal static Message CreateUserMessage(string content = "Hello, how can you help me?", long sessionId = 1)
    {
        return CreateTestMessage(sessionId, MessageRole.User, content);
    }

    /// <summary>
    /// Creates an assistant response message.
    /// </summary>
    internal static Message CreateAssistantMessage(string content = "I'm here to help! What would you like to know?", long sessionId = 1)
    {
        return CreateTestMessage(sessionId, MessageRole.Assistant, content);
    }

    /// <summary>
    /// Creates a system message.
    /// </summary>
    internal static Message CreateSystemMessage(string content = "You are a helpful assistant.", long sessionId = 1)
    {
        return CreateTestMessage(sessionId, MessageRole.System, content);
    }

    /// <summary>
    /// Creates a message with binary data attached.
    /// </summary>
    internal static Message CreateMessageWithBinaryData(
        string textContent = "Here is an image:",
        byte[]? imageData = null,
        string imageName = "test-image.png")
    {
        var message = CreateUserMessage(textContent);
        
        imageData ??= CreateSampleImageBytes();
        message.AddBinaryData(imageName, MsgBinaryDataType.Image, imageData);
        
        return message;
    }

    /// <summary>
    /// Creates a message with tool call data.
    /// </summary>
    internal static Message CreateToolCallMessage(
        string functionName = "get_weather",
        Dictionary<string, object>? parameters = null)
    {
        parameters ??= new Dictionary<string, object> { { "location", "New York" } };
        
        var toolCall = new
        {
            id = "tool_call_123",
            type = "function",
            function = new
            {
                name = functionName,
                arguments = JsonSerializer.Serialize(parameters)
            }
        };

        var message = CreateAssistantMessage("I'll check the weather for you.");
        var toolCallJson = JsonSerializer.Serialize(toolCall);
        message.AddBinaryData("tool_call", MsgBinaryDataType.ToolCall, Encoding.UTF8.GetBytes(toolCallJson));
        
        return message;
    }

    /// <summary>
    /// Creates a tool call result message.
    /// </summary>
    internal static Message CreateToolResultMessage(
        string toolCallId = "tool_call_123",
        object? result = null)
    {
        result ??= new { temperature = "22°C", condition = "sunny" };
        
        var toolResult = new
        {
            tool_call_id = toolCallId,
            content = JsonSerializer.Serialize(result)
        };

        var message = CreateSystemMessage("Tool execution completed.");
        var resultJson = JsonSerializer.Serialize(toolResult);
        message.AddBinaryData("tool_result", MsgBinaryDataType.ToolCallResult, Encoding.UTF8.GetBytes(resultJson));
        
        return message;
    }

    /// <summary>
    /// Creates a very long message for testing message size limits.
    /// </summary>
    internal static Message CreateLongMessage(int targetLength = 10000)
    {
        var content = new StringBuilder();
        var baseText = "This is a very long message used for testing message size limits and handling. ";
        
        while (content.Length < targetLength)
        {
            content.Append(baseText);
        }
        
        return CreateUserMessage(content.ToString().Substring(0, targetLength));
    }

    /// <summary>
    /// Creates an empty message for testing edge cases.
    /// </summary>
    internal static Message CreateEmptyMessage()
    {
        return CreateUserMessage("");
    }

    /// <summary>
    /// Creates an information-type message with JSON content.
    /// </summary>
    internal static Message CreateInformationMessage(object data)
    {
        var jsonContent = JsonSerializer.Serialize(data);
        return CreateTestMessage(1, MessageRole.System, jsonContent, MessageType.Information);
    }

    #endregion

    #region Conversation Scenarios

    /// <summary>
    /// Creates a basic conversation scenario with user question and assistant response.
    /// </summary>
    internal static List<Message> CreateBasicConversation(long sessionId = 1)
    {
        return new List<Message>
        {
            CreateUserMessage("What is the capital of France?", sessionId),
            CreateAssistantMessage("The capital of France is Paris.", sessionId)
        };
    }

    /// <summary>
    /// Creates a conversation with system message, user message, and assistant response.
    /// </summary>
    internal static List<Message> CreateConversationWithSystem(long sessionId = 1)
    {
        return new List<Message>
        {
            CreateSystemMessage("You are a geography expert.", sessionId),
            CreateUserMessage("What is the capital of France?", sessionId),
            CreateAssistantMessage("The capital of France is Paris, a beautiful city known for its culture and history.", sessionId)
        };
    }

    /// <summary>
    /// Creates a conversation that includes tool calls.
    /// </summary>
    internal static List<Message> CreateToolCallConversation(long sessionId = 1)
    {
        return new List<Message>
        {
            CreateUserMessage("What's the weather like in New York?", sessionId),
            CreateToolCallMessage("get_weather", new Dictionary<string, object> { { "location", "New York" } }),
            CreateToolResultMessage("tool_call_123", new { temperature = "22°C", condition = "sunny" }),
            CreateAssistantMessage("The weather in New York is currently sunny with a temperature of 22°C.", sessionId)
        };
    }

    /// <summary>
    /// Creates a multi-turn conversation for testing session persistence.
    /// </summary>
    internal static List<Message> CreateMultiTurnConversation(long sessionId = 1)
    {
        return new List<Message>
        {
            CreateUserMessage("Hi there!", sessionId),
            CreateAssistantMessage("Hello! How can I help you today?", sessionId),
            CreateUserMessage("Can you tell me about machine learning?", sessionId),
            CreateAssistantMessage("Machine learning is a subset of artificial intelligence that focuses on algorithms that can learn and make decisions from data.", sessionId),
            CreateUserMessage("That's interesting! Can you give me an example?", sessionId),
            CreateAssistantMessage("Sure! A common example is email spam filtering, where the system learns to identify spam emails based on patterns in previous examples.", sessionId)
        };
    }

    #endregion

    #region Test Data Helpers

    /// <summary>
    /// Creates sample image bytes for testing binary data.
    /// </summary>
    public static byte[] CreateSampleImageBytes()
    {
        // Create a simple PNG-like header (not a real PNG, just for testing)
        return new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, /* fake PNG data */ };
    }

    /// <summary>
    /// Creates sample audio bytes for testing.
    /// </summary>
    public static byte[] CreateSampleAudioBytes()
    {
        // Create a simple WAV-like header (not real WAV, just for testing)
        return new byte[] { 0x52, 0x49, 0x46, 0x46, /* fake WAV data */ };
    }

    /// <summary>
    /// Creates invalid message data for testing error handling.
    /// </summary>
    internal static Message CreateInvalidMessage()
    {
        return new Message
        {
            Id = -1, // Invalid ID
            SessionId = -1, // Invalid session ID
            Content = null, // Null content
            Role = (int)MessageRole.Unknown, // Unknown role
            CreatedAt = DateTime.MinValue // Invalid date
        };
    }

    /// <summary>
    /// Creates a message with null binary content for testing edge cases.
    /// </summary>
    internal static Message CreateMessageWithNullBinary()
    {
        var message = CreateUserMessage("Test message with null binary");
        message.BinaryContents = null;
        return message;
    }

    /// <summary>
    /// Creates test API provider parameters.
    /// </summary>
    public static Dictionary<string, object> CreateOpenAIParameters()
    {
        return new Dictionary<string, object>
        {
            { "temperature", 0.7 },
            { "max_tokens", 150 },
            { "top_p", 1.0 },
            { "frequency_penalty", 0.0 },
            { "presence_penalty", 0.0 }
        };
    }

    /// <summary>
    /// Creates test API provider parameters for Anthropic.
    /// </summary>
    public static Dictionary<string, object> CreateAnthropicParameters()
    {
        return new Dictionary<string, object>
        {
            { "temperature", 0.7 },
            { "max_tokens", 150 },
            { "top_p", 1.0 }
        };
    }

    /// <summary>
    /// Creates a batch of test messages for performance testing.
    /// </summary>
    internal static List<Message> CreateMessageBatch(int count, long sessionId = 1)
    {
        var messages = new List<Message>();
        
        for (int i = 0; i < count; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            var content = $"Test message number {i + 1}";
            var message = CreateTestMessage(sessionId, role, content);
            message.Id = i + 1;
            messages.Add(message);
        }
        
        return messages;
    }

    #endregion

    #region Tool Creation

    /// <summary>
    /// Creates a test tool for testing purposes.
    /// </summary>
    internal static DaoStudio.DBStorage.Models.LlmTool CreateTestTool(
        string name = "TestTool",
        string description = "A test tool for testing purposes")
    {
        return new DaoStudio.DBStorage.Models.LlmTool
        {
            Id = 1,
            Name = name,
            Description = description,
            StaticId = $"test-{name.ToLower()}",
            ToolConfig = "{}",
            Parameters = new Dictionary<string, string>(),
            IsEnabled = true,
            AppId = 0,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };
    }

    #endregion

    #region Validation Helpers

    /// <summary>
    /// Validates that a message has all required fields set correctly.
    /// </summary>
    public static bool IsValidMessage(IMessage message)
    {
        return message != null
            && message.Id != 0
            && message.SessionId != 0
            && Enum.IsDefined(typeof(MessageRole), (MessageRole)message.Role)
            && Enum.IsDefined(typeof(MessageType), (MessageType)message.Type)
            && message.CreatedAt != DateTime.MinValue;
    }

    /// <summary>
    /// Validates that a person has all required fields set correctly.
    /// </summary>
    public static bool IsValidPerson(IPerson person)
    {
        return person != null
            && !string.IsNullOrEmpty(person.Name)
            && !string.IsNullOrEmpty(person.Description);
    }

    #endregion
}
