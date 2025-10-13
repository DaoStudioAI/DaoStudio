using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Interfaces
{
    /// <summary>
    /// Interface for Session repository operations
    /// </summary>
    public interface ISessionRepository
    {
        /// <summary>
        /// Get a session by ID
        /// </summary>
        /// <param name="id">The ID of the session</param>
        /// <returns>Session or null if not found</returns>
        Task<Session?> GetSessionAsync(long id);

        /// <summary>
        /// Create a new session
        /// </summary>
        /// <param name="session">The session to create</param>
        /// <returns>The created session with assigned ID</returns>
        Task<Session> CreateSessionAsync(Session session);

        /// <summary>
        /// Get all sessions
        /// </summary>
        /// <param name="inclusionOptions">Options for including child sessions</param>
        /// <returns>List of sessions based on inclusion options</returns>
        Task<IEnumerable<Session>> GetAllSessionsAsync(SessionInclusionOptions inclusionOptions = SessionInclusionOptions.All);

        /// <summary>
        /// Save a session
        /// </summary>
        /// <param name="session">The session to save</param>
        /// <returns>True if successful</returns>
        Task<bool> SaveSessionAsync(Session session);

        /// <summary>
        /// Delete a session
        /// </summary>
        /// <param name="id">The ID of the session to delete</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteSessionAsync(long id);



        /// <summary>
        /// Get sessions by parent session ID
        /// </summary>
        /// <param name="parentSessId">The parent session ID to filter by</param>
        /// <returns>List of sessions with the specified parent session ID</returns>
        Task<IEnumerable<Session>> GetSessionsByParentSessIdAsync(long parentSessId);
    }
} 