using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Service interface for cached model operations
    /// </summary>
    public interface ICachedModelService
    {
        /// <summary>
        /// Gets all cached models
        /// </summary>
        /// <returns>Collection of cached models</returns>
        Task<IEnumerable<ICachedModel>> GetAllCachedModelsAsync();

        /// <summary>
        /// Gets a cached model by ID
        /// </summary>
        /// <param name="modelId">Model ID</param>
        /// <returns>The cached model or null if not found</returns>
        Task<ICachedModel?> GetCachedModelByIdAsync(long modelId);


        /// <summary>
        /// Refreshes cached models from all providers
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task RefreshCachedModelsAsync();

        /// <summary>
        /// Saves models to the cached model repository for a specific provider
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <param name="providerType">The provider type</param>
        /// <param name="models">List of model names to cache</param>
        /// <returns>Task representing the async operation</returns>
        Task SaveModelsToCacheAsync(long providerId, ProviderType providerType, List<string> models);
    }
}