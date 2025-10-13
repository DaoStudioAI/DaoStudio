using DaoStudio.DBStorage.Common;
using DaoStudio.DBStorage.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace DaoStudio.DBStorage.Repositories
{

    // Partial class implementation for write operations
    public partial class SqliteCachedModelRepository
    {

        /// <summary>
        /// Check if a provider with the given ID exists
        /// </summary>
        /// <param name="id">The ID to check</param>
        /// <param name="connection">Optional connection to reuse. If null, creates a new connection.</param>
        /// <returns>True if the provider exists</returns>
        public bool CachedModelExists(long id, SqliteConnection connection)
        {

                // Use the provided connection
                var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(1) FROM CachedModels WHERE Id = @Id;";
                command.Parameters.AddWithValue("@Id", id);

                return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// Create a new cached model
        /// </summary>
        /// <param name="model">The cached model to create</param>
        /// <returns>The created cached model with assigned ID</returns>
        public async Task<CachedModel> CreateModelAsync(CachedModel model)
        {
            ThrowIfDisposed();
            
            var connection = await GetConnectionAsync();


            // Generate a new unique ID
            model.Id = IdGenerator.GenerateUniqueId(id => CachedModelExists(id, connection));

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO CachedModels (Id,ApiProviderId, Name, ModelId, ProviderType, Catalog, Parameters)
                VALUES (@Id,@ApiProviderId, @Name, @ModelId, @ProviderType, @Catalog, @Parameters);
                SELECT last_insert_rowid();
            ";
            command.Parameters.AddWithValue("@Id", model.Id);
            command.Parameters.AddWithValue("@ApiProviderId", model.ApiProviderId);
            command.Parameters.AddWithValue("@Name", model.Name);
            command.Parameters.AddWithValue("@ModelId", model.ModelId);
            command.Parameters.AddWithValue("@ProviderType", model.ProviderType);
            command.Parameters.AddWithValue("@Catalog", model.Catalog);
            command.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(model.Parameters));

            var result = await command.ExecuteScalarAsync();
            // Always assign the database-generated ID, ignoring any pre-existing ID
            model.Id = Convert.ToInt64(result);

            return model;
        }
        
        /// <summary>
        /// Create multiple cached models in a single operation
        /// </summary>
        /// <param name="models">List of cached models to create</param>
        /// <returns>Number of models successfully created</returns>
        public async Task<int> CreateModelsAsync(IEnumerable<CachedModel> models)
        {
            ThrowIfDisposed();
            
            var connection = await GetConnectionAsync();
            
            // Start a transaction for better performance
            using var transaction = connection.BeginTransaction();
            
            try
            {
                int count = 0;
                
                // Prepare a single command with parameters
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO CachedModels (Id, ApiProviderId, Name, ModelId, ProviderType, Catalog, Parameters)
                    VALUES (@Id, @ApiProviderId, @Name, @ModelId, @ProviderType, @Catalog, @Parameters);
                ";
                
                // Add parameters
                var idParam = command.Parameters.Add("@Id", SqliteType.Integer);
                var providerIdParam = command.Parameters.Add("@ApiProviderId", SqliteType.Integer);
                var nameParam = command.Parameters.Add("@Name", SqliteType.Text);
                var modelIdParam = command.Parameters.Add("@ModelId", SqliteType.Text);
                var providerTypeParam = command.Parameters.Add("@ProviderType", SqliteType.Integer);
                var catalogParam = command.Parameters.Add("@Catalog", SqliteType.Text);
                var parametersParam = command.Parameters.Add("@Parameters", SqliteType.Text);
                
                // Execute for each model
                foreach (var model in models)
                {
                    model.Id = IdGenerator.GenerateUniqueId(id => CachedModelExists(id, connection));

                    idParam.Value = model.Id;
                    providerIdParam.Value = model.ApiProviderId;
                    nameParam.Value = model.Name;
                    modelIdParam.Value = model.ModelId;
                    providerTypeParam.Value = (int)model.ProviderType;
                    catalogParam.Value = model.Catalog;
                    parametersParam.Value = JsonSerializer.Serialize(model.Parameters);
                    
                    await command.ExecuteNonQueryAsync();
                    count++;
                }
                
                // Commit the transaction
                transaction.Commit();
                
                return count;
            }
            catch (Exception)
            {
                // Roll back the transaction in case of error
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Save a cached model
        /// </summary>
        /// <param name="model">The cached model to save</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SaveModelAsync(CachedModel model)
        {
            ThrowIfDisposed();
            
            if (model.Id == 0)
            {
                throw new ArgumentException("Cannot save model with ID 0. Use CreateModelAsync for new models.");
            }
            
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE CachedModels
                SET ApiProviderId = @ApiProviderId,
                    Name = @Name,
                    ModelId = @ModelId,
                    ProviderType = @ProviderType,
                    Catalog = @Catalog,
                    Parameters = @Parameters
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", model.Id);
            command.Parameters.AddWithValue("@ApiProviderId", model.ApiProviderId);
            command.Parameters.AddWithValue("@Name", model.Name);
            command.Parameters.AddWithValue("@ModelId", model.ModelId);
            command.Parameters.AddWithValue("@ProviderType", model.ProviderType);
            command.Parameters.AddWithValue("@Catalog", model.Catalog);
            command.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(model.Parameters));

            var affectedRows = await command.ExecuteNonQueryAsync();
            return affectedRows > 0;
        }

        /// <summary>
        /// Delete a cached model
        /// </summary>
        /// <param name="id">The ID of the cached model to delete</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeleteModelAsync(long id)
        {
            ThrowIfDisposed();
            
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM CachedModels WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            var affectedRows = await command.ExecuteNonQueryAsync();
            return affectedRows > 0;
        }
        
        /// <summary>
        /// Delete all cached models for a provider
        /// </summary>
        /// <param name="providerId">The provider ID to delete models for</param>
        /// <returns>Number of models deleted</returns>
        public async Task<int> DeleteModelsByProviderIdAsync(long providerId)
        {
            ThrowIfDisposed();
            
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM CachedModels WHERE ApiProviderId = @ApiProviderId;";
            command.Parameters.AddWithValue("@ApiProviderId", providerId);

            var affectedRows = await command.ExecuteNonQueryAsync();
            return affectedRows;
        }
    }
} 
