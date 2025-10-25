using DaoStudio.Common.Plugins;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using System.Linq;

namespace TestNamingTool.TestInfrastructure.Mocks
{
    public class MockHostSession : ISession, IHostSession
    {
        private readonly List<IPerson> _persons;
        private Dictionary<string, List<FunctionWithDescription>>? _tools;
        private bool _disposed = false;
        private bool _returnToolInvoked = false;
        private bool _returnToolInvocationPending = false;

        /// <summary>
        /// Indicates what boolean value should be supplied for the <c>success</c>
        /// parameter when the return-result tool is invoked automatically by the
        /// mock session.  Defaults to <c>true</c> which preserves existing unit-
        /// test behaviour.
        /// </summary>
        public bool AutoInvokeSuccess { get; set; } = true;

        /// <summary>
        /// Determines whether the mock session should automatically invoke the
        /// registered <see cref="CustomReturnResultTool"/> as soon as it becomes
        /// available (either during <see cref="SetTools"/> or the first
        /// <see cref="SendMessageAsync(HostSessMsgType,string)"/> call).
        ///
        /// Default value is <c>true</c> to keep the previous behaviour which
        /// helps most tests finish quickly.   Certain tests – for example those
        /// that validate cancellation/timeout behaviour – can set this property
        /// to <c>false</c> in order to observe the code-under-test without an
        /// immediate successful completion.
        /// </summary>
        public bool AutoInvokeReturnTool { get; set; } = true;

    /// <summary>
    /// When true, automatically invoke the return-result tool as soon as the first prompt is sent.
    /// Defaults to false to preserve historical behaviour where invocation occurs after the first urging message.
    /// </summary>
    public bool InvokeReturnToolOnPrompt { get; set; } = false;

        public MockHostSession(long id, long? parentSessionId = null)
        {
            Id = id;
            ParentSessionId = parentSessionId;
            _persons = new List<IPerson>();
            CurrentCancellationToken = new CancellationTokenSource();
            ToolExecutionMode = ToolExecutionMode.Auto;
            Title = $"Mock Session {id}";
            Description = "Mock session for testing";
            CreatedAt = DateTime.UtcNow;
            LastModified = DateTime.UtcNow;
            
            // Initialize mock properties
            MsgMaxLoopCount = 100;
            TotalTokenCount = 0;
            InputTokenCount = 0;
            OutputTokenCount = 0;
            AdditionalTokenProperties = new Dictionary<string, string>();
            CurrentPerson = new MockPerson("TestPerson", "Test Person Description");
        }

        public long Id { get; }
        public long? ParentSessionId { get; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public ToolExecutionMode ToolExecutionMode { get; set; }
        public CancellationTokenSource? CurrentCancellationToken { get; private set; }

        // Session management
#pragma warning disable CS0067 // Event is never used - this is a mock implementation
    public event EventHandler<PropertyChangeNotification>? PropertyChanged;
#pragma warning restore CS0067

        public SessionStatus SessionStatus => SessionStatus.Idle;

        // Tool calls
        public int MsgMaxLoopCount { get; set; }

        // Token statistics
        public long TotalTokenCount { get; private set; }
        public long InputTokenCount { get; private set; }
        public long OutputTokenCount { get; private set; }
        public Dictionary<string, string>? AdditionalTokenProperties { get; private set; }
#pragma warning disable CS0067 // Event is never used - this is a mock implementation
        public event EventHandler<UsageDetails>? UsageDetailsReceived;
#pragma warning restore CS0067

        // Person
        public IPerson CurrentPerson { get; private set; }

        // Message methods
        public event EventHandler<MessageChangedEventArgs>? OnMessageChanged;

        // Test properties for verification
        public List<(HostSessMsgType msgType, string message)> SentMessages { get; } = 
            new List<(HostSessMsgType, string)>();

    // Convenience properties for tests to inspect last sent prompt/urging message
    public string LastReceivedPrompt { get; private set; } = string.Empty;
    public string LastReceivedUrgingMessage { get; private set; } = string.Empty;

        public List<Dictionary<string, List<FunctionWithDescription>>> SetToolsHistory { get; } = 
            new List<Dictionary<string, List<FunctionWithDescription>>>();

        public void AddPerson(IPerson person)
        {
            _persons.Add(person);
        }

        public void ClearPersons()
        {
            _persons.Clear();
        }

        public Task<List<IPerson>> GetPersonsAsync()
        {
            return Task.FromResult(new List<IPerson>(_persons));
        }

        Task IHostSession.SendMessageAsync(HostSessMsgType msgType, string message)
        {
            SentMessages.Add((msgType, message));

            // ------------------------------------------------------------------
            // NEW: invoke the return-result tool after the FIRST message sent to
            // the child session (this better matches actual engine behaviour).
            // ------------------------------------------------------------------
            // Store prompts for test inspection. The real system uses the same
            // message type for initial prompt and reminders, so distinguish by
            // order: the first message is treated as the initial prompt and any
            // subsequent messages are treated as urging/reminder messages.
            if (string.IsNullOrEmpty(LastReceivedPrompt))
            {
                LastReceivedPrompt = message;

                if (InvokeReturnToolOnPrompt && !_returnToolInvoked)
                {
                    if (_tools == null)
                    {
                        _returnToolInvocationPending = true;
                    }
                    else
                    {
                        _returnToolInvoked = true;
                        TryInvokeReturnTool();
                    }
                }
            }
            else
            {
                LastReceivedUrgingMessage = message;

                // Auto-invoke the return-result tool ONLY after at least one urging/reminder
                // message has been sent. This better reflects the intended flow in
                // WaitChildSessionAsync where reminders are issued if the tool hasn't been
                // called yet, and also allows unit tests to observe the urging message.
                if (AutoInvokeReturnTool && !_returnToolInvoked)
                {
                    _returnToolInvoked = true;
                    TryInvokeReturnTool();
                }
            }

            return Task.CompletedTask;
        }

        // Session management methods
        public Task<bool> DeleteSessionAsync()
        {
            return Task.FromResult(true);
        }

        public Task UpdateSessionLastModifiedAsync()
        {
            LastModified = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        // Tool methods
        public Task<List<ITool>> GetEnabledPersonToolsAsync()
        {
            return Task.FromResult(new List<ITool>());
        }

        // Person methods
        public Task UpdatePersonAsync(IPerson person)
        {
            CurrentPerson = person;
            return Task.CompletedTask;
        }

        // Send a mock message

        public Task<IMessage> SendMessageAsync(string userMessage)
        {
            var mockMessage = new MockMessage(1, userMessage, DateTime.UtcNow);

            if (AutoInvokeReturnTool && !_returnToolInvoked)
            {
                _returnToolInvoked = true;
                TryInvokeReturnTool();
            }

            return Task.FromResult<IMessage>(mockMessage);
        }

        public Task FireMessageChangedAsync(IMessage message, MessageChangeType change)
        {
            var handlers = OnMessageChanged;
            if (handlers == null)
            {
                return Task.CompletedTask;
            }
            var args = new MessageChangedEventArgs(message, change);
            foreach (EventHandler<MessageChangedEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch
                {
                    // Ignore exceptions in mock
                }
            }
            return Task.CompletedTask;
        }

        // Helper classes
        private class MockMessage : IMessage
        {
            public long Id { get; set; }
            public long SessionId { get; set; }
            public string? Content { get; set; }
            public MessageRole Role { get; set; }
            public MessageType Type { get; set; }
            public List<IMsgBinaryData>? BinaryContents { get; set; }
            public int BinaryVersion { get; set; }
            public long ParentMsgId { get; set; }
            public long ParentSessId { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime LastModified { get; set; }

            public MockMessage(long id, string content, DateTime createdAt)
            {
                Id = id;
                Content = content;
                CreatedAt = createdAt;
                LastModified = createdAt;
                Role = MessageRole.User;
                Type = MessageType.Normal;
            }

            // Implements IMessage.AddBinaryData
            public void AddBinaryData(string name, MsgBinaryDataType type, byte[] data)
            {
                if (BinaryContents == null)
                {
                    BinaryContents = new List<IMsgBinaryData>();
                }

                BinaryContents.Add(new MockBinaryData
                {
                    Name = name,
                    Type = type,
                    Data = data
                });
            }

            private class MockBinaryData : IMsgBinaryData
            {
                public string Name { get; set; } = string.Empty;
                public MsgBinaryDataType Type { get; set; }
                public byte[] Data { get; set; } = Array.Empty<byte>();
            }
        }

        private class MockPerson : IPerson
        {
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

            public MockPerson(string name, string description)
            {
                Name = name;
                Description = description;
            }
        }

        public void SetTools(Dictionary<string, List<FunctionWithDescription>>? moduleFunctions)
        {
            // Preserve existing behaviour
            _tools = moduleFunctions != null
                ? new Dictionary<string, List<FunctionWithDescription>>(moduleFunctions)
                : null;

            if (moduleFunctions != null)
            {
                SetToolsHistory.Add(moduleFunctions);

                // If a previous attempt wanted to auto-invoke but tools weren't ready yet,
                // now is a good time to try. Otherwise, defer auto-invocation until after
                // the first reminder message is sent (see SendMessageAsync above).
                if (AutoInvokeReturnTool && _returnToolInvocationPending && !_returnToolInvoked)
                {
                    if (_tools == null)
                    {
                        _returnToolInvocationPending = true;
                    }
                    else
                    {
                        TryInvokeReturnTool();
                    }
                }
            }
        }

        public Dictionary<string, List<FunctionWithDescription>>? GetTools()
        {
            return _tools != null ? 
                new Dictionary<string, List<FunctionWithDescription>>(_tools) : null;
        }

        public void CancelToken()
        {
            CurrentCancellationToken?.Cancel();
        }

        public void ResetCancellationToken()
        {
            CurrentCancellationToken?.Dispose();
            CurrentCancellationToken = new CancellationTokenSource();
        }

        // IHostSession specific methods
        Task<List<IHostPerson>?> IHostSession.GetPersonsAsync()
        {
            // Convert IPerson to IHostPerson using mock adapter
            var hostPersons = _persons.Select(p => new MockHostPersonAdapter(p) as IHostPerson).ToList();
            return Task.FromResult<List<IHostPerson>?>(hostPersons);
        }

        // GetUnderlyingSession removed from IHostSession. The mock implements
        // ISession directly so callers that previously used GetUnderlyingSession()
        // should use the `IHostSession.Id` or cast to ISession where required in tests.

        public void Dispose()
        {
            if (!_disposed)
            {
                CurrentCancellationToken?.Dispose();
                CurrentCancellationToken = null;
                _disposed = true;
            }
        }

        ~MockHostSession()
        {
            Dispose();
        }

        // Filter methods (mock implementations - no-op for testing)
        public void AddFilter(IFilter filter)
        {
            // Mock implementation - no-op
        }

        public void RemoveFilter(IFilter filter)
        {
            // Mock implementation - no-op
        }

        public void ClearFilters(FilterType? type = null, FilterExecutionMode? mode = null)
        {
            // Mock implementation - no-op
        }

        public IReadOnlyList<IFilter> GetFilters(FilterType type, FilterExecutionMode mode)
        {
            // Mock implementation - return empty list
            return new List<IFilter>().AsReadOnly();
        }

        /// <summary>
        /// Attempts to invoke the registered CustomReturnResultTool asynchronously
        /// using dummy arguments that satisfy its required parameters.
        /// </summary>
        private void TryInvokeReturnTool()
        {
            if (_tools == null) return;

            // Try to locate the CustomReturnResultTool more deliberately.
            _tools.TryGetValue("CustomReturnResultTool", out var fnList);
            var returnToolFunction = fnList?.FirstOrDefault();

            // Fallback – locate any function that contains a "success" parameter
            if (returnToolFunction == null)
            {
                returnToolFunction = _tools
                    .SelectMany(kvp => kvp.Value)
                    .FirstOrDefault(f => f.Description.Parameters?.Any(p => p.Name == "success") == true);
            }

            // Final fallback – just the first function available
            if (returnToolFunction == null)
            {
                returnToolFunction = _tools.SelectMany(kvp => kvp.Value).FirstOrDefault();
            }
            if (returnToolFunction == null)
            {
                // Defer invocation until tools are fully populated
                _returnToolInvocationPending = true;
                return;
            }

            // Attempt to cast to the expected delegate; if that fails, try
            // DynamicInvoke as a fallback.
            Func<Dictionary<string, object?>, Task<object?>>? asyncDelegate =
                returnToolFunction.Function as Func<Dictionary<string, object?>, Task<object?>>;

            if (asyncDelegate == null)
            {
                asyncDelegate = args =>
                {
                    var tcs = new TaskCompletionSource<object?>();
                    try
                    {
                        var result = returnToolFunction.Function.DynamicInvoke(args);
                        if (result is Task task)
                        {
                            return task.ContinueWith(_ => (object?)null);
                        }
                        tcs.SetResult(result);
                    }
                    catch
                    {
                        tcs.SetResult(null);
                    }
                    return tcs.Task;
                };
            }

            var args = new Dictionary<string, object?>();
            foreach (var param in returnToolFunction.Description.Parameters ?? new List<FunctionTypeMetadata>())
            {
                if (!param.IsRequired) continue;
                object? value;
                if (param.ParameterType == typeof(bool))
                {
                    // Respect the AutoInvokeSuccess flag so tests can simulate success/failure scenarios
                    value = AutoInvokeSuccess;
                }
                else if (param.ParameterType == typeof(int))
                {
                    value = 1;
                }
                else if (param.ParameterType == typeof(double))
                {
                    value = 1.0;
                }
                else
                {
                    value = "test";
                }
                args[param.Name] = value;
            }

            try
            {
                // Execute synchronously so the TaskCompletionSource inside the
                // tool is set before we return, mirroring the behaviour the
                // tests expect.
                asyncDelegate(args).GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore – tests assert on final NamingHandler result not on
                // internal tool execution exceptions.
            }
        }
    }
}
