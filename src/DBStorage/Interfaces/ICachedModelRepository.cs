using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Interfaces
{
    /// <summary>
    /// Interface for cached model repository operations
    /// </summary>
    public interface ICachedModelRepository
    {
        /// <summary>
        /// Get a cached model by ID
        /// </summary>
        /// <param name="id">The ID of the cached model</param>
        /// <returns>Cached model or null if not found</returns>
        Task<CachedModel?> GetModelAsync(long id);

        /// <summary>
        /// Create a new cached model
        /// </summary>
        /// <param name="model">The cached model to create</param>
        /// <returns>The created cached model with assigned ID</returns>
        Task<CachedModel> CreateModelAsync(CachedModel model);

        /// <summary>
        /// Create multiple cached models in a single operation
        /// </summary>
        /// <param name="models">List of cached models to create</param>
        /// <returns>Number of models successfully created</returns>
        Task<int> CreateModelsAsync(IEnumerable<CachedModel> models);

        /// <summary>
        /// Save a cached model
        /// </summary>
        /// <param name="model">The cached model to save</param>
        /// <returns>True if successful</returns>
        Task<bool> SaveModelAsync(CachedModel model);

        /// <summary>
        /// Delete a cached model
        /// </summary>
        /// <param name="id">The ID of the cached model to delete</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteModelAsync(long id);

        /// <summary>
        /// Delete all cached models for a provider
        /// </summary>
        /// <param name="providerId">The provider ID to delete models for</param>
        /// <returns>Number of models deleted</returns>
        Task<int> DeleteModelsByProviderIdAsync(long providerId);

        /// <summary>
        /// Get all cached models
        /// </summary>
        /// <returns>List of all cached models</returns>
        Task<IEnumerable<CachedModel>> GetAllModelsAsync();

        /// <summary>
        /// Get cached models by provider ID
        /// </summary>
        /// <param name="providerId">The provider ID to filter by</param>
        /// <returns>List of cached models for the specified provider</returns>
        Task<IEnumerable<CachedModel>> GetModelsByProviderIdAsync(long providerId);

        /// <summary>
        /// Get cached models by provider ID, provider type and catalog
        /// </summary>
        /// <param name="providerId">The provider ID to filter by</param>
        /// <param name="providerType">The provider type to filter by</param>
        /// <param name="catalog">The catalog to filter by</param>
        /// <returns>List of cached models matching the criteria</returns>
        Task<IEnumerable<CachedModel>> GetModelsByCriteriaAsync(long providerId, int providerType, string catalog);
    }
} 