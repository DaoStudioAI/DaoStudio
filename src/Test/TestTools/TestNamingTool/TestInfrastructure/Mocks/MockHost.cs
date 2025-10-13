using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using System.Linq;

namespace TestNamingTool.TestInfrastructure.Mocks
{
    public class MockHost : IHost, IDisposable
    {
        private readonly List<IPerson> _persons;
        private readonly object _createdSessionsLock = new object();

        /// <summary>
        /// Determines whether newly created <see cref="MockHostSession"/> objects
        /// should automatically invoke the registered <c>CustomReturnResultTool</c>
        /// as soon as the first message is sent (default: <c>true</c> to preserve
        /// existing behaviour).
        ///
        /// Test cases that need to verify scenarios where the return-result tool
        /// is <em>not</em> called automatically can set this property to
        /// <c>false</c> before the child session is created.
        /// </summary>
        public bool AutoInvokeReturnToolForNewSessions { get; set; } = true;

        /// <summary>
        /// Specifies the value to use for the <c>success</c> parameter when the
        /// automatically-invoked return-result tool is executed by newly created
        /// <see cref="MockHostSession"/> instances.  Defaults to <c>true</c> to
        /// preserve historical behaviour.
        /// </summary>
        public bool AutoInvokeSuccessForNewSessions { get; set; } = true;

    /// <summary>
    /// Determines whether new sessions should invoke the return tool immediately upon receiving the first prompt.
    /// Defaults to false to maintain historical behaviour where the call happens after a reminder.
    /// </summary>
    public bool InvokeReturnToolOnPromptForNewSessions { get; set; } = false;
        private readonly ConcurrentDictionary<long, MockHostSession> _sessions;
        private long _nextSessionId;

        public MockHost()
        {
            _persons = new List<IPerson>();
            _sessions = new ConcurrentDictionary<long, MockHostSession>();
            _nextSessionId = 0;
        }

        public List<ISession> CreatedSessions { get; } = new List<ISession>();

        public void AddPerson(IPerson person)
        {
            _persons.Add(person);
        }

        public void ClearPersons()
        {
            _persons.Clear();
        }

        public Task<List<IPerson>> GetPersonsAsync(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Task.FromResult(_persons);
            }

            var filteredPersons = _persons.Where(p => 
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            return Task.FromResult(filteredPersons);
        }

        public Task<ISession> StartNewSession(long? parentSessId, string? personName = null)
        {
            var sessionId = Interlocked.Increment(ref _nextSessionId);
            
            var session = new MockHostSession(sessionId, parentSessId)
            {
                // Propagate the desired auto-invoke behaviour and success flag to the new session
                AutoInvokeReturnTool = AutoInvokeReturnToolForNewSessions,
                AutoInvokeSuccess = AutoInvokeSuccessForNewSessions,
                InvokeReturnToolOnPrompt = InvokeReturnToolOnPromptForNewSessions
            };
            _sessions[sessionId] = session;
            lock (_createdSessionsLock)
            {
                CreatedSessions.Add(session);
            }

            return Task.FromResult<ISession>(session);
        }

        public Task<ISession> OpenHostSession(long sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult<ISession>(session);
            }
            
            throw new ArgumentException($"Session with ID {sessionId} not found.", nameof(sessionId));
        }

        public async Task<IHostSession> StartNewHostSessionAsync(IHostSession? parent, string? personName = null)
        {
            var parentSessId = parent?.Id;
            var newSession = await StartNewSession(parentSessId, personName);
            return (MockHostSession)newSession; // MockHostSession now implements IHostSession
        }

        public async Task<List<IHostPerson>> GetHostPersonsAsync(string? name)
        {
            var persons = await GetPersonsAsync(name);
            return persons.Select(p => new MockHostPersonAdapter(p)).Cast<IHostPerson>().ToList();
        }

        public string GetPluginConfigureFolderPath(string pluginId)
        {
            // Return a test configuration folder path and include sanitized pluginId
            var basePath = Path.Combine(Path.GetTempPath(), "DaoStudio", "Config");
            if (string.IsNullOrWhiteSpace(pluginId)) return basePath;

            var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct().ToArray();
            var sanitized = new string(pluginId.Where(c => !invalidChars.Contains(c)).ToArray());
            if (string.IsNullOrWhiteSpace(sanitized)) return basePath;

            return Path.Combine(basePath, sanitized);
        }

        public MockHostSession? GetMockSession(long sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }

        public void ClearSessions()
        {
            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }
            _sessions.Clear();
            CreatedSessions.Clear();
        }

        public void Dispose()
        {
            ClearSessions();
        }
    }
}
