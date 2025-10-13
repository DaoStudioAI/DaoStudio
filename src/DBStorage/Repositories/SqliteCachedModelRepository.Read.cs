using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for read operations
    public partial class SqliteCachedModelRepository
    {
        /// <summary>
        /// Get a cached model by ID
        /// </summary>
        /// <param name="id">The ID of the cached model</param>
        /// <returns>Cached model or null if not found</returns>
        public async Task<CachedModel?> GetModelAsync(long id)
        {
            ThrowIfDisposed();
            
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, ApiProviderId, Name, ModelId, ProviderType, Catalog, Parameters
                FROM CachedModels
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new CachedModel
                {
                    Id = reader.GetInt64(0),
                    ApiProviderId = reader.GetInt64(1),
                    Name = reader.GetString(2),
                    ModelId = reader.GetString(3),
                    ProviderType = reader.GetInt32(4),
                    Catalog = reader.GetString(5),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(6)) ?? new Dictionary<string, string>()
                };
            }

            return null;
        }

        /// <summary>
        /// Get all cached models
        /// </summary>
        /// <returns>List of all cached models</returns>
        public async Task<IEnumerable<CachedModel>> GetAllModelsAsync()
        {
            ThrowIfDisposed();
            
            var models = new List<CachedModel>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, ApiProviderId, Name, ModelId, ProviderType, Catalog, Parameters
                FROM CachedModels
                ORDER BY Name;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                models.Add(new CachedModel
                {
                    Id = reader.GetInt64(0),
                    ApiProviderId = reader.GetInt64(1),
                    Name = reader.GetString(2),
                    ModelId = reader.GetString(3),
                    ProviderType = reader.GetInt32(4),
                    Catalog = reader.GetString(5),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(6)) ?? new Dictionary<string, string>()
                });
            }

            return models;
        }

        /// <summary>
        /// Get cached models by provider ID
        /// </summary>
        /// <param name="providerId">The provider ID to filter by</param>
        /// <returns>List of cached models for the specified provider</returns>
        public async Task<IEnumerable<CachedModel>> GetModelsByProviderIdAsync(long providerId)
        {
            ThrowIfDisposed();
            
            var models = new List<CachedModel>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, ApiProviderId, Name, ModelId, ProviderType, Catalog, Parameters
                FROM CachedModels
                WHERE ApiProviderId = @ApiProviderId
                ORDER BY Name;
            ";
            command.Parameters.AddWithValue("@ApiProviderId", providerId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                models.Add(new CachedModel
                {
                    Id = reader.GetInt64(0),
                    ApiProviderId = reader.GetInt64(1),
                    Name = reader.GetString(2),
                    ModelId = reader.GetString(3),
                    ProviderType = reader.GetInt32(4),
                    Catalog = reader.GetString(5),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(6)) ?? new Dictionary<string, string>()
                });
            }

            return models;
        }

        /// <summary>
        /// Get cached models by provider ID, provider type and catalog
        /// </summary>
        /// <param name="providerId">The provider ID to filter by</param>
        /// <param name="providerType">The provider type to filter by</param>
        /// <param name="catalog">The catalog to filter by</param>
        /// <returns>List of cached models matching the criteria</returns>
        public async Task<IEnumerable<CachedModel>> GetModelsByCriteriaAsync(long providerId, int providerType, string catalog)
        {
            ThrowIfDisposed();
            
            var models = new List<CachedModel>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, ApiProviderId, Name, ModelId, ProviderType, Catalog, Parameters
                FROM CachedModels
                WHERE ApiProviderId = @ApiProviderId
                  AND ProviderType = @ProviderType
                  AND Catalog = @Catalog
                ORDER BY Name;
            ";
            command.Parameters.AddWithValue("@ApiProviderId", providerId);
            command.Parameters.AddWithValue("@ProviderType", providerType);
            command.Parameters.AddWithValue("@Catalog", catalog);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                models.Add(new CachedModel
                {
                    Id = reader.GetInt64(0),
                    ApiProviderId = reader.GetInt64(1),
                    Name = reader.GetString(2),
                    ModelId = reader.GetString(3),
                    ProviderType = reader.GetInt32(4),
                    Catalog = reader.GetString(5),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(6)) ?? new Dictionary<string, string>()
                });
            }

            return models;
        }
    }
} 
