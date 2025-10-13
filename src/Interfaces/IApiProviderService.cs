using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Service interface for API provider operations
    /// </summary>
    public interface IApiProviderService
    {
        #region Events

        /// <summary>
        /// Event raised when a provider is created, updated, or deleted
        /// </summary>
        event EventHandler<ProviderOperationEventArgs>? ProviderChanged;

        #endregion
        /// <summary>
        /// Gets all API providers
        /// </summary>
        /// <returns>Collection of API providers</returns>
        Task<IEnumerable<IApiProvider>> GetAllApiProvidersAsync();

        /// <summary>
        /// Gets an API provider by ID
        /// </summary>
        /// <param name="providerId">Provider ID</param>
        /// <returns>The API provider or null if not found</returns>
        Task<IApiProvider?> GetApiProviderByIdAsync(long providerId);


        /// <summary>
        /// Creates a new API provider from parameters
        /// </summary>
        /// <param name="name">Provider name</param>
        /// <param name="providerType">Provider type</param>
        /// <param name="apiEndpoint">API endpoint</param>
        /// <param name="apiKey">API key</param>
        /// <param name="parameters">Additional parameters</param>
        /// <param name="isEnabled">Whether the provider is enabled</param>
        /// <returns>The created provider</returns>
        Task<IApiProvider> CreateApiProviderAsync(string name, ProviderType providerType, string apiEndpoint, 
            string? apiKey = null, Dictionary<string, string>? parameters = null, bool isEnabled = true);


        /// <summary>
        /// Updates an existing API provider
        /// </summary>
        /// <param name="provider">Provider to update</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateApiProviderAsync(IApiProvider provider);

        /// <summary>
        /// Deletes an API provider
        /// </summary>
        /// <param name="providerId">Provider ID to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DeleteApiProviderAsync(long providerId);

        /// <summary>
        /// Gets API providers by type
        /// </summary>
        /// <param name="providerType">Provider type to filter by</param>
        /// <returns>Collection of API providers of the specified type</returns>
        Task<IEnumerable<IApiProvider>> GetApiProvidersByTypeAsync(ProviderType providerType);

        /// <summary>
        /// Gets supported provider templates for creating new providers
        /// </summary>
        /// <returns>Collection of provider templates with names</returns>
        List<(string name, IApiProvider template)> GetSupportProviders();
    }
}