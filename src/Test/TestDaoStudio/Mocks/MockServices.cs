using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Microsoft.Extensions.AI;

namespace TestDaoStudio.Mocks;

/// <summary>
/// Mock implementation of ITool for testing.
/// </summary>
public class MockTool : ITool
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StaticId { get; set; } = string.Empty;
    public string ToolConfig { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public long AppId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public ToolType ToolType { get; set; } = ToolType.Normal;
    public ToolState State { get; set; } = ToolState.Stateless;
    public byte[]? StateData { get; set; }
    public string DevMsg { get; set; } = string.Empty;
}


/// <summary>
/// Mock implementation of IMessageService for testing.
/// </summary>
public class MockMessageService : IMessageService
{
    private readonly List<IMessage> _messages = new();
    private readonly List<string> _operationLog = new();

    public IReadOnlyList<IMessage> Messages => _messages.AsReadOnly();
    public IReadOnlyList<string> OperationLog => _operationLog.AsReadOnly();

    public Task<IMessage> CreateMessageAsync(IMessage message)
    {
        _operationLog.Add($"CreateMessageAsync: {message.Content ?? "<null>"}");
        message.Id = _messages.Count + 1;
        message.CreatedAt = DateTime.UtcNow;
        message.LastModified = DateTime.UtcNow;
        _messages.Add(message);
        return Task.FromResult(message);
    }

    public Task<IMessage?> GetMessageByIdAsync(long messageId)
    {
        _operationLog.Add($"GetMessageByIdAsync: {messageId}");
        var message = _messages.FirstOrDefault(m => m.Id == messageId);
        return Task.FromResult(message);
    }

    public Task<IEnumerable<IMessage>> GetMessagesBySessionIdAsync(long sessionId)
    {
        _operationLog.Add($"GetMessagesBySessionIdAsync: {sessionId}");
        var messages = _messages.Where(m => m.SessionId == sessionId);
        return Task.FromResult(messages);
    }

    public Task<IEnumerable<IMessage>> GetMessagesByParentSessIdAsync(long parentSessId)
    {
        _operationLog.Add($"GetMessagesByParentSessIdAsync: {parentSessId}");
        var messages = _messages.Where(m => m.ParentSessId == parentSessId);
        return Task.FromResult(messages);
    }

    public Task<IEnumerable<IMessage>> GetAllMessagesAsync()
    {
        _operationLog.Add("GetAllMessagesAsync");
        return Task.FromResult<IEnumerable<IMessage>>(_messages);
    }

    public Task<int> DeleteMessagesBySessionIdAsync(long sessionId)
    {
        _operationLog.Add($"DeleteMessagesBySessionIdAsync: {sessionId}");
        var messagesToDelete = _messages.Where(m => m.SessionId == sessionId).ToList();
        foreach (var message in messagesToDelete)
        {
            _messages.Remove(message);
        }
        return Task.FromResult(messagesToDelete.Count);
    }

    public Task<int> DeleteMessagesByParentSessIdAsync(long parentSessId)
    {
        _operationLog.Add($"DeleteMessagesByParentSessIdAsync: {parentSessId}");
        var messagesToDelete = _messages.Where(m => m.ParentSessId == parentSessId).ToList();
        foreach (var message in messagesToDelete)
        {
            _messages.Remove(message);
        }
        return Task.FromResult(messagesToDelete.Count);
    }

    public Task<int> DeleteMessageInSessionAsync(long sessionId, long specifiedMessageId, bool includeSpecifiedMessage)
    {
        _operationLog.Add($"DeleteMessageInSessionAsync: {sessionId}, {specifiedMessageId}, {includeSpecifiedMessage}");
        var sessionMessages = _messages.Where(m => m.SessionId == sessionId).ToList();
        var specifiedMessage = sessionMessages.FirstOrDefault(m => m.Id == specifiedMessageId);
        
        if (specifiedMessage == null)
            return Task.FromResult(0);
            
        var messagesToDelete = new List<IMessage>();
        
        // Find messages to delete based on the specified message
        foreach (var message in sessionMessages)
        {
            if (message.Id > specifiedMessageId || (includeSpecifiedMessage && message.Id == specifiedMessageId))
            {
                messagesToDelete.Add(message);
            }
        }
        
        foreach (var message in messagesToDelete)
        {
            _messages.Remove(message);
        }
        
        return Task.FromResult(messagesToDelete.Count);
    }

    public Task<IMessage> CreateMessageAsync(string content, MessageRole role, MessageType type, long? sessionId = null, bool saveToDisk = true, long parentMsgId = 0, long parentSessId = 0)
    {
        _operationLog.Add($"CreateMessageAsync: {content}, {role}, {type}");
        var message = new TestMessage
        {
            Id = _messages.Count + 1,
            Content = content,
            Role = role,
            Type = type,
            SessionId = sessionId ?? 0,
            ParentMsgId = parentMsgId,
            ParentSessId = parentSessId,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };
        
        if (saveToDisk)
        {
            _messages.Add(message);
        }
        
        return Task.FromResult<IMessage>(message);
    }

    public Task<bool> SaveMessageAsync(IMessage message, bool allowCreate)
    {
        _operationLog.Add($"SaveMessageAsync: {message.Id}, allowCreate: {allowCreate}");
        
        if (message.Id == 0)
        {
            if (!allowCreate)
                throw new InvalidOperationException("Cannot save message with Id 0 when allowCreate is false");
                
            message.Id = _messages.Count + 1;
            message.CreatedAt = DateTime.UtcNow;
            _messages.Add(message);
        }
        else
        {
            var existing = _messages.FirstOrDefault(m => m.Id == message.Id);
            if (existing != null)
            {
                var index = _messages.IndexOf(existing);
                _messages[index] = message;
            }
            else
            {
                _messages.Add(message);
            }
        }
        
        message.LastModified = DateTime.UtcNow;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteMessageAsync(long messageId)
    {
        _operationLog.Add($"DeleteMessageAsync: {messageId}");
        var message = _messages.FirstOrDefault(m => m.Id == messageId);
        if (message != null)
        {
            _messages.Remove(message);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<IMessage> UpdateMessageAsync(IMessage message)
    {
        _operationLog.Add($"UpdateMessageAsync: {message.Id}");
        message.LastModified = DateTime.UtcNow;
        var existing = _messages.FirstOrDefault(m => m.Id == message.Id);
        if (existing != null)
        {
            var index = _messages.IndexOf(existing);
            _messages[index] = message;
        }
        return Task.FromResult(message);
    }

    private class TestMessage : IMessage
    {
        public long Id { get; set; }
        public string? Content { get; set; }
        public MessageRole Role { get; set; }
        public MessageType Type { get; set; }
        public long SessionId { get; set; }
        public long ParentMsgId { get; set; }
        public long ParentSessId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public List<IMsgBinaryData>? BinaryContents { get; set; }
        public int BinaryVersion { get; set; }

        public void AddBinaryData(string name, MsgBinaryDataType type, byte[] data)
        {
            BinaryContents ??= new List<IMsgBinaryData>();
            BinaryContents.Add(new TestBinaryData { Name = name, Type = type, Data = data });
        }
    }

    private class TestBinaryData : IMsgBinaryData
    {
        public string Name { get; set; } = string.Empty;
        public MsgBinaryDataType Type { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    public void Clear()
    {
        _messages.Clear();
        _operationLog.Clear();
    }

    public bool WasOperationCalled(string operation)
    {
        return _operationLog.Any(log => log.Contains(operation));
    }
}

/// <summary>
/// Mock implementation of IToolService for testing.
/// </summary>
public class MockToolService : IToolService
{
    private readonly List<ITool> _tools = new();
    private readonly List<string> _operationLog = new();
    private readonly Dictionary<string, object?> _executionResults = new();

    public IReadOnlyList<ITool> Tools => _tools.AsReadOnly();
    public IReadOnlyList<string> OperationLog => _operationLog.AsReadOnly();

    public event EventHandler<ToolOperationEventArgs>? ToolChanged;
    public event EventHandler<ToolListUpdateEventArgs>? ToolListUpdated;

    public Task<ITool> CreateToolAsync(string name, string description, string staticId, string toolConfig = "", Dictionary<string, string>? parameters = null, bool isEnabled = true, long appId = 0)
    {
        _operationLog.Add($"CreateToolAsync: {name}");
        // Create a mock tool implementation
        var tool = new MockTool
        {
            Id = _tools.Count + 1,
            Name = name,
            Description = description,
            StaticId = staticId,
            ToolConfig = toolConfig,
            Parameters = parameters ?? new Dictionary<string, string>(),
            IsEnabled = isEnabled,
            AppId = appId
        };
        _tools.Add(tool);
        ToolListUpdated?.Invoke(this, new ToolListUpdateEventArgs(ToolListUpdateType.Added, tool, _tools.Count));
        return Task.FromResult<ITool>(tool);
    }

    public Task<ITool?> GetToolAsync(long toolId)
    {
        _operationLog.Add($"GetToolAsync: {toolId}");
        var tool = _tools.FirstOrDefault(t => t.Id == toolId);
        return Task.FromResult(tool);
    }


    public Task<IEnumerable<ITool>> GetAllToolsAsync()
    {
        _operationLog.Add("GetAllToolsAsync");
        return Task.FromResult<IEnumerable<ITool>>(_tools);
    }


    public Task<IEnumerable<ITool>> GetToolsByStaticIdAsync(string staticId)
    {
        _operationLog.Add($"GetToolsByStaticIdAsync: {staticId}");
        var tools = _tools.Where(t => t.StaticId == staticId);
        return Task.FromResult(tools);
    }


    public Task<bool> DeleteToolAsync(long toolId)
    {
        _operationLog.Add($"DeleteToolAsync: {toolId}");
        var tool = _tools.FirstOrDefault(t => t.Id == toolId);
        if (tool != null)
        {
            _tools.Remove(tool);
            ToolListUpdated?.Invoke(this, new ToolListUpdateEventArgs(ToolListUpdateType.Removed, toolId, _tools.Count));
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> UpdateToolAsync(ITool tool)
    {
        _operationLog.Add($"UpdateToolAsync: {tool.Id}");
        var existing = _tools.FirstOrDefault(t => t.Id == tool.Id);
        if (existing != null)
        {
            var index = _tools.IndexOf(existing);
            _tools[index] = tool;
            ToolChanged?.Invoke(this, new ToolOperationEventArgs(ToolOperationType.Updated, tool));
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <summary>
    /// Sets up a mock execution result for a tool.
    /// </summary>
    public void SetExecutionResult(string toolName, Dictionary<string, object?> parameters, object? result)
    {
        var key = $"{toolName}:{string.Join(",", parameters.Select(p => $"{p.Key}={p.Value}"))}";
        _executionResults[key] = result;
    }

    /// <summary>
    /// Sets up a simple execution result for a tool with no parameters.
    /// </summary>
    public void SetExecutionResult(string toolName, object? result)
    {
        _executionResults[toolName + ":"] = result;
    }

    public void Clear()
    {
        _tools.Clear();
        _operationLog.Clear();
        _executionResults.Clear();
    }

    public bool WasOperationCalled(string operation)
    {
        return _operationLog.Any(log => log.Contains(operation));
    }
}

/// <summary>
/// Mock implementation of IPluginTool for testing plugin interactions.
/// </summary>
public class MockPluginTool : IPluginTool
{
    public string Name { get; set; } = "MockTool";
    public string Description { get; set; } = "A mock tool for testing";
    public string Version { get; set; } = "1.0.0";
    public bool IsEnabled { get; set; } = true;

    private readonly List<string> _executionLog = new();
    private readonly Dictionary<string, object?> _executionResults = new();

    public IReadOnlyList<string> ExecutionLog => _executionLog.AsReadOnly();

    public Task GetSessionFunctionsAsync(List<FunctionWithDescription> toolcallFunctions, IHostPerson? person, IHostSession? hostSession)
    {
        _executionLog.Add($"GetSessionFunctionsAsync called with {toolcallFunctions?.Count ?? 0} functions");
        return Task.CompletedTask;
    }

    public Task<byte[]?> CloseSessionAsync(IHostSession hostSession)
    {
        _executionLog.Add("CloseSessionAsync called");
        return Task.FromResult<byte[]?>(null);
    }

    public Task<object?> ExecuteAsync(Dictionary<string, object?> parameters)
    {
        var paramString = string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"));
        _executionLog.Add($"ExecuteAsync called with parameters: {paramString}");
        
        // Return pre-configured result or default
        var key = string.Join(",", parameters.Select(p => $"{p.Key}={p.Value}"));
        if (_executionResults.TryGetValue(key, out var result))
        {
            return Task.FromResult(result);
        }
        
        // Default response
        return Task.FromResult<object?>(new { success = true, parameters });
    }

    public Task InitializeAsync()
    {
        _executionLog.Add("InitializeAsync called");
        return Task.CompletedTask;
    }

    public Task<string> GetSchemaAsync()
    {
        _executionLog.Add("GetSchemaAsync called");
        return Task.FromResult(@"{
            ""type"": ""object"",
            ""properties"": {
                ""input"": { ""type"": ""string"", ""description"": ""Test input parameter"" }
            },
            ""required"": [""input""]
        }");
    }

    public void Dispose()
    {
        _executionLog.Add("Dispose called");
    }

    /// <summary>
    /// Sets up a mock execution result for specific parameters.
    /// </summary>
    public void SetExecutionResult(Dictionary<string, object?> parameters, object? result)
    {
        var key = string.Join(",", parameters.Select(p => $"{p.Key}={p.Value}"));
        _executionResults[key] = result;
    }

    public void Clear()
    {
        _executionLog.Clear();
        _executionResults.Clear();
    }

    public bool WasMethodCalled(string method)
    {
        return _executionLog.Any(log => log.Contains(method));
    }

    /// <summary>
    /// Creates a mock plugin tool that simulates throwing an exception.
    /// </summary>
    public static MockPluginTool CreateWithException(Exception exception)
    {
        return new MockPluginTool
        {
            Name = "ErrorTool",
            Description = "A tool that throws exceptions for testing"
            // The exception would be thrown in ExecuteAsync implementation
        };
    }
}
