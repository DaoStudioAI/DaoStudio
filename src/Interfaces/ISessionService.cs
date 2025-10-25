using System;

namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Service interface for session management operations
    /// </summary>
    public interface ISessionService : IDisposable
    {
        #region Session Management

        /// <summary>
        /// Event fired when a subsession is created for any session.
        /// </summary>
        event EventHandler<ISession>? SubsessionCreated;

        /// <summary>
        /// Create a new session for a person
        /// </summary>
        /// <param name="person">The person to create a session for</param>
        /// <param name="parentSessionId">Optional parent session ID for creating child sessions</param>
        /// <returns>The created session</returns>
        Task<ISession> CreateSession(IPerson person, long? parentSessionId = null);

        /// <summary>
        /// Open an existing session by ID
        /// </summary>
        /// <param name="sessionId">The ID of the session to open</param>
        /// <returns>The opened session</returns>
        Task<ISession> OpenSession(long sessionId);

        /// <summary>
        /// Open an existing host session by ID
        /// </summary>
        /// <param name="sessionId">The ID of the session to open</param>
        /// <returns>The opened session</returns>
        Task<ISession> OpenHostSession(long sessionId);

        /// <summary>
        /// Start a new session with optional parent and person
        /// </summary>
        /// <param name="parentSessId">Optional parent session ID</param>
        /// <param name="personName">Optional person name for the session</param>
        /// <returns>The created session</returns>
        Task<ISession> StartNewSession(long? parentSessId, string? personName = null);

        /// <summary>
        /// Gets all sessions from the database
        /// </summary>
        /// <param name="includeParentSessions">Whether to include parent sessions (sessions without a parent). Default is true.</param>
        /// <param name="includeChildSessions">Whether to include child sessions (sessions with a parent). Default is true.</param>
        /// <returns>Collection of all session information</returns>
        Task<IEnumerable<SessionInfo>> GetAllSessionsAsync(bool includeParentSessions = true, bool includeChildSessions = true);


        /// <summary>
        /// Saves a session's current state to the database
        /// </summary>
        /// <param name="session">The session to save</param>
        /// <returns>True if the session was successfully saved, false otherwise</returns>
        Task<bool> SaveSessionAsync(ISession session);

        /// <summary>
        /// Deletes a session from the database
        /// </summary>
        /// <param name="sessionId">The ID of the session to delete</param>
        /// <returns>True if the session was successfully deleted, false otherwise</returns>
        Task<bool> DeleteSessionAsync(long sessionId);

        #endregion
    }
}