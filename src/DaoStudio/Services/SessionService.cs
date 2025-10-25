using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.Services
{
    /// <summary>
    /// Service implementation for session management operations
    /// </summary>
    internal class SessionService : ISessionService
    {
        private readonly IMessageService messageService;
        private readonly ISessionRepository sessionRepository;
        private readonly IToolService toolService;
        private readonly IPersonRepository personRepository;
        private readonly IPeopleService peopleService;
        private readonly IPluginService pluginService;
        private readonly IEngineService engineService;
        private readonly ILogger<SessionService> logger;
        private readonly ILoggerFactory loggerFactory;

        public event EventHandler<ISession>? SubsessionCreated;

        public SessionService(IMessageService messageService,
            ISessionRepository sessionRepository,
            IToolService toolService,
            IPersonRepository personRepository,
            IPeopleService peopleService,
            IPluginService pluginService,
            IEngineService engineService,
            ILogger<SessionService> logger,
            ILoggerFactory loggerFactory)
        {
            this.messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            this.sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
            this.toolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
            this.personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
            this.peopleService = peopleService ?? throw new ArgumentNullException(nameof(peopleService));
            this.pluginService = pluginService ?? throw new ArgumentNullException(nameof(pluginService));
            this.engineService = engineService ?? throw new ArgumentNullException(nameof(engineService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        #region Session Management

        /// <summary>
        /// Create a new session for a person
        /// </summary>
        /// <param name="person">The person to create a session for</param>
        /// <param name="parentSessionId">Optional parent session ID for creating child sessions</param>
        /// <returns>The created session</returns>
        public async Task<ISession> CreateSession(IPerson person, long? parentSessionId = null)
        {
            if (person == null)
            {
                throw new ArgumentNullException(nameof(person));
            }
            // Create a new session
            var dbSession = new DaoStudio.DBStorage.Models.Session
            {
                Title = person.Name,
                Description = string.Empty,
                Logo = null,
                PersonNames = new List<string> { person.Name },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                ParentSessId = parentSessionId
            };
            var createdSession = await sessionRepository.CreateSessionAsync(dbSession);

            // Create the session directly using the created session with proper ID
            var session = new Session(messageService, sessionRepository,
                toolService, createdSession, person, loggerFactory.CreateLogger<Session>(), pluginService, engineService, peopleService);
            await session.InitializeAsync();

            // If this is a child session, initialize parent filters
            if (parentSessionId.HasValue)
            {
                var parentSession = await OpenSession(parentSessionId.Value);
            }

            // Only add subsession ID to additional data if it exists
            if (parentSessionId.HasValue)
            {
                // Get the parent session to post the information message on it
                var parentSession = await OpenSession(parentSessionId.Value);
                
                // Create subsession information message using IMessageService
                var subsessionMessage = await messageService.CreateMessageAsync(
                    "Subsession created", 
                    Interfaces.MessageRole.User, 
                    Interfaces.MessageType.Information,
                    parentSessionId.Value,
                    false);

                // Add subsession ID as binary data
                subsessionMessage.AddBinaryData(
                    "SubsessionId", 
                    Interfaces.MsgBinaryDataType.SubsessionId, 
                    BitConverter.GetBytes(session.Id));

                // Save the message
                await messageService.SaveMessageAsync(subsessionMessage, true);

                // Notify the parent session's listeners about the new information message
                await parentSession.FireMessageChangedAsync(subsessionMessage, MessageChangeType.New);

                // Update parent's last modified timestamp
                await parentSession.UpdateSessionLastModifiedAsync();

                // Fire the SubsessionCreated event
                SubsessionCreated?.Invoke(parentSession, session);
            }

            return session;
        }

        /// <summary>
        /// Open an existing session by ID
        /// </summary>
        /// <param name="sessionId">The ID of the session to open</param>
        /// <returns>The opened session</returns>
        public async Task<ISession> OpenSession(long sessionId)
        {
            if (sessionId == 0)
                throw new ArgumentException("Session ID must be other than 0", nameof(sessionId));
                
            // Open the session from database
            var dbSession = await sessionRepository.GetSessionAsync(sessionId);
            if (dbSession == null)
            {
                throw new ArgumentException($"Session with ID {sessionId} not found");
            }

            IPerson? person = null;

            if (dbSession.PersonNames != null && dbSession.PersonNames.Count > 0)
            {
                string personName = dbSession.PersonNames[0];
                    person = await peopleService.GetPersonAsync(personName);

                if (person == null)
                {
                    throw new ArgumentException($"Person with name '{personName}' not found");
                }
            }
            else
            {
                // No person names stored for this session – retrieve the first enabled person
                var persons = await personRepository.GetEnabledPersonsAsync(false);
                var dbPerson = persons.FirstOrDefault();

                if (dbPerson == null)
                {
                    throw new Exception("No enabled persons available");
                }

                person = DaoStudio.Person.FromDBPerson(dbPerson);

                // Persist the person name so we don't have to do this next time
                dbSession.PersonNames = new List<string> { person.Name };
                await sessionRepository.SaveSessionAsync(dbSession);
            }

            var session = new Session(messageService, sessionRepository, toolService, dbSession, person, loggerFactory.CreateLogger<Session>(), pluginService, engineService, peopleService);
            await session.InitializeAsync();

            return session;
        }

        /// <summary>
        /// Open an existing host session by ID
        /// </summary>
        /// <param name="sessionId">The ID of the session to open</param>
        /// <returns>The opened session</returns>
        public async Task<ISession> OpenHostSession(long sessionId)
        {
            return await OpenSession(sessionId);
        }

        /// <summary>
        /// Start a new session with optional parent and person
        /// </summary>
        /// <param name="parentSessId">Optional parent session ID</param>
        /// <param name="personName">Optional person name for the session</param>
        /// <returns>The created session</returns>
        public async Task<ISession> StartNewSession(long? parentSessId, string? personName = null)
        {
            // Determine which person to use for the session
            IPerson? sessionPerson;
            long? parentSessionId = parentSessId;

            if (!string.IsNullOrWhiteSpace(personName))
            {
                // Use the explicitly provided person name
                var dbPerson = await personRepository.GetPersonByNameAsync(personName)
                    ?? throw new Exception($"Person with name '{personName}' not found");
                sessionPerson = DaoStudio.Person.FromDBPerson(dbPerson);
            }
            else if (parentSessId.HasValue)
            {
                // Use the person from the parent session
                var parentSession = await OpenSession(parentSessId.Value);
                sessionPerson = parentSession.CurrentPerson;
            }
            else
            {
                // Get the first enabled person
                var persons = await personRepository.GetEnabledPersonsAsync(false);
                var dbPerson = persons.FirstOrDefault();
                if (dbPerson == null)
                {
                    throw new Exception("No enabled persons available");
                }
                sessionPerson = DaoStudio.Person.FromDBPerson(dbPerson);
            }

            // Create a new session with the selected person
            var session = await CreateSession(sessionPerson, parentSessionId);

            // Return the session - caller will handle WaitChildSessionAsync
            return session;
        }

        /// <summary>
        /// Gets all sessions from the database
        /// </summary>
        /// <param name="includeParentSessions">Whether to include parent sessions (sessions without a parent). Default is true.</param>
        /// <param name="includeChildSessions">Whether to include child sessions (sessions with a parent). Default is true.</param>
        /// <returns>Collection of all session information</returns>
        public async Task<IEnumerable<SessionInfo>> GetAllSessionsAsync(bool includeParentSessions = true, bool includeChildSessions = true)
        {
            try
            {
                // Convert boolean parameters to SessionInclusionOptions
                SessionInclusionOptions inclusionOptions;
                if (includeParentSessions && includeChildSessions)
                {
                    inclusionOptions = SessionInclusionOptions.All;
                }
                else if (includeParentSessions && !includeChildSessions)
                {
                    inclusionOptions = SessionInclusionOptions.ParentsOnly;
                }
                else if (!includeParentSessions && includeChildSessions)
                {
                    inclusionOptions = SessionInclusionOptions.ChildrenOnly;
                }
                else
                {
                    // Both false - return empty collection
                    return new List<SessionInfo>();
                }

                var dbSessions = await sessionRepository.GetAllSessionsAsync(inclusionOptions);

                // PERFORMANCE FIX: Batch load all unique person names to avoid N+1 queries
                var uniquePersonNames = dbSessions
                    .Where(s => s.PersonNames != null && s.PersonNames.Count > 0)
                    .SelectMany(s => s.PersonNames)
                    .Distinct()
                    .ToList();

                // Get all persons in a single batch operation
                var personLookup = new Dictionary<string, IPerson>();
                if (uniquePersonNames.Count > 0)
                {
                    var dbPersons = await personRepository.GetPersonsByNamesAsync(uniquePersonNames, false);
                    foreach (var dbPerson in dbPersons)
                    {
                        personLookup[dbPerson.Name] = DaoStudio.Person.FromDBPerson(dbPerson);
                    }
                }

                var sessionInfos = new List<SessionInfo>();
                foreach (var dbSession in dbSessions)
                {
                    // Get person information from the pre-loaded lookup
                    IPerson? currentPerson = null;
                    if (dbSession.PersonNames != null && dbSession.PersonNames.Count > 0)
                    {
                        var firstPersonName = dbSession.PersonNames.First();
                        personLookup.TryGetValue(firstPersonName, out currentPerson);
                    }

                    // Create SessionInfo from database session
                    var sessionInfo = new SessionInfo
                    {
                        Id = dbSession.Id,
                        ParentSessionId = dbSession.ParentSessId,
                        Title = dbSession.Title ?? string.Empty,
                        Description = dbSession.Description ?? string.Empty,
                        CreatedAt = dbSession.CreatedAt,
                        LastModified = dbSession.LastModified,
                        SessionStatus = SessionStatus.Idle, // Default status for database sessions
                        CurrentPerson = currentPerson
                    };

                    sessionInfos.Add(sessionInfo);
                }

                return sessionInfos;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting all sessions");
                throw;
            }
        }


        /// <summary>
        /// Saves a session's current state to the database
        /// </summary>
        /// <param name="session">The session to save</param>
        /// <returns>True if the session was successfully saved, false otherwise</returns>
        public async Task<bool> SaveSessionAsync(ISession session)
        {
            try
            {
                // Session is the underlying type we're using
                if (session is not Session underlyingSession)
                {
                    logger.LogError("Session is not a Session type for saving");
                    return false;
                }

                // Get the session repository and the database session model
                // sessionRepository is already available as a field

                // Get the current database session
                var field = typeof(Session).GetField("dbsess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(underlyingSession) is not DaoStudio.DBStorage.Models.Session dbSession)
                {
                    logger.LogError("Unable to access database session model for session {SessionId}", session.Id);
                    return false;
                }

                // Update the last modified timestamp
                dbSession.LastModified = DateTime.UtcNow;

                // Save the session
                var result = await sessionRepository.SaveSessionAsync(dbSession);

                logger.LogDebug("Saved session {SessionId}, result: {Result}", session.Id, result);

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving session {SessionId}", session.Id);
                return false;
            }
        }

        /// <summary>
        /// Deletes a session from the database
        /// </summary>
        /// <param name="sessionId">The ID of the session to delete</param>
        /// <returns>True if the session was successfully deleted, false otherwise</returns>
        public async Task<bool> DeleteSessionAsync(long sessionId)
        {
            try
            {
                logger.LogInformation("Deleting session {SessionId}", sessionId);

                // Delete all messages belonging to this session
                try
                {
                    await messageService.DeleteMessagesBySessionIdAsync(sessionId);
                }
                catch (Exception ex)
                {
                    // Log but do not fail the entire deletion process because of messaging cleanup
                    logger.LogError(ex, "Error deleting messages for session {SessionId}", sessionId);
                }

                // Delete from database
                var result = await sessionRepository.DeleteSessionAsync(sessionId);

                logger.LogInformation("Session {SessionId} deletion result: {Result}", sessionId, result);
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting session {SessionId}", sessionId);
                return false;
            }
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            // No cleanup needed since we don't cache sessions
        }

        #endregion
    }
}