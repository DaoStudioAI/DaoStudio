using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Interfaces
{
    /// <summary>
    /// Interface for application repository operations
    /// </summary>
    public interface IApplicationRepository
    {
        /// <summary>
        /// Get an application by ID
        /// </summary>
        /// <param name="id">The ID of the application</param>
        /// <returns>Application or null if not found</returns>
        Task<Application?> GetApplicationAsync(long id);

        /// <summary>
        /// Get an application by name
        /// </summary>
        /// <param name="name">The name of the application</param>
        /// <returns>Application or null if not found</returns>
        Task<Application?> GetApplicationByNameAsync(string name);
        
        /// <summary>
        /// Check if an application with the given name exists
        /// </summary>
        /// <param name="name">The name to check</param>
        /// <returns>True if the application exists</returns>
        bool ApplicationExistsByName(string name);

        /// <summary>
        /// Create a new application
        /// </summary>
        /// <param name="application">The application to create</param>
        /// <returns>The created application with assigned ID</returns>
        Task<Application> CreateApplicationAsync(Application application);

        /// <summary>
        /// Save an application
        /// </summary>
        /// <param name="application">The application to save</param>
        /// <returns>True if successful</returns>
        Task<bool> SaveApplicationAsync(Application application);

        /// <summary>
        /// Delete an application
        /// </summary>
        /// <param name="id">The ID of the application to delete</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteApplicationAsync(long id);

        /// <summary>
        /// Get all applications
        /// </summary>
        /// <returns>List of all applications</returns>
        Task<IEnumerable<Application>> GetAllApplicationsAsync();
    }
}
