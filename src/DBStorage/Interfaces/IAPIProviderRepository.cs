using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Interfaces
{
    /// <summary>
    /// Interface for API provider repository operations
    /// </summary>
    public interface IAPIProviderRepository
    {
        /// <summary>
        /// Get a provider by ID
        /// </summary>
        /// <param name="id">The ID of the provider</param>
        /// <returns>API provider or null if not found</returns>
        Task<APIProvider?> GetProviderAsync(long id);

        /// <summary>
        /// Get a provider by name
        /// </summary>
        /// <param name="name">The name of the provider</param>
        /// <returns>API provider or null if not found</returns>
        Task<APIProvider?> GetProviderByNameAsync(string name);
        
        /// <summary>
        /// Check if a provider with the given name exists
        /// </summary>
        /// <param name="name">The name to check</param>
        /// <returns>True if the provider exists</returns>
        bool ProviderExistsByName(string name);

        /// <summary>
        /// Create a new provider
        /// </summary>
        /// <param name="provider">The provider to create</param>
        /// <returns>The created provider with assigned ID</returns>
        Task<APIProvider> CreateProviderAsync(APIProvider provider);

        /// <summary>
        /// Save a provider
        /// </summary>
        /// <param name="provider">The provider to save</param>
        /// <returns>True if successful</returns>
        Task<bool> SaveProviderAsync(APIProvider provider);

        /// <summary>
        /// Delete a provider
        /// </summary>
        /// <param name="id">The ID of the provider to delete</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteProviderAsync(long id);

        /// <summary>
        /// Get all providers
        /// </summary>
        /// <returns>List of all providers</returns>
        Task<IEnumerable<APIProvider>> GetAllProvidersAsync();
    }
} 