using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for write operations
    public partial class SqliteAPIProviderRepository
    {
        /// <summary>
        /// Create a new provider
        /// </summary>
        /// <param name="provider">The provider to create</param>
        /// <returns>The created provider with assigned ID</returns>
        public async Task<APIProvider> CreateProviderAsync(APIProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            // Prevent duplicate names which violate the UNIQUE index on Name
            if (ProviderExistsByName(provider.Name))
                throw new InvalidOperationException($"An API provider with the name '{provider.Name}' already exists. Please choose a different name.");

            var now = DateTime.UtcNow;
            provider.LastModified = now;
            provider.CreatedAt = now;

            // Generate a new unique ID
            provider.Id = IdGenerator.GenerateUniqueId(ProviderExists);

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO APIProviders (Id, Name, ApiEndpoint, ApiKey, Parameters, IsEnabled, LastModified, CreatedAt, ProviderType, Timeout, MaxConcurrency)
                VALUES (@Id, @Name, @ApiEndpoint, @ApiKey, @Parameters, @IsEnabled, @LastModified, @CreatedAt, @ProviderType, @Timeout, @MaxConcurrency);
            ";
            command.Parameters.AddWithValue("@Id", provider.Id);
            command.Parameters.AddWithValue("@Name", provider.Name);
            command.Parameters.AddWithValue("@ApiEndpoint", provider.ApiEndpoint);
            command.Parameters.AddWithValue("@ApiKey", provider.ApiKey ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(provider.Parameters));
            command.Parameters.AddWithValue("@IsEnabled", provider.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@LastModified", (ulong)provider.LastModified.ToFileTimeUtc());
            command.Parameters.AddWithValue("@CreatedAt", (ulong)provider.CreatedAt.ToFileTimeUtc());
            command.Parameters.AddWithValue("@ProviderType", provider.ProviderType);
            command.Parameters.AddWithValue("@Timeout", provider.Timeout);
            command.Parameters.AddWithValue("@MaxConcurrency", provider.MaxConcurrency);

            await command.ExecuteNonQueryAsync();
            return provider;
        }

        /// <summary>
        /// Save changes to an existing provider
        /// </summary>
        /// <param name="provider">The provider to update</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SaveProviderAsync(APIProvider provider)
        {
            if (provider.Id == 0)
            {
                throw new ArgumentException("Cannot save provider with ID 0. Use CreateProviderAsync for new providers.");
            }

            provider.LastModified = DateTime.UtcNow;

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE APIProviders 
                SET Name = @Name,
                    ApiEndpoint = @ApiEndpoint,
                    ApiKey = @ApiKey,
                    Parameters = @Parameters,
                    IsEnabled = @IsEnabled,
                    LastModified = @LastModified,
                    ProviderType = @ProviderType,
                    Timeout = @Timeout,
                    MaxConcurrency = @MaxConcurrency
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", provider.Id);
            command.Parameters.AddWithValue("@Name", provider.Name);
            command.Parameters.AddWithValue("@ApiEndpoint", provider.ApiEndpoint);
            command.Parameters.AddWithValue("@ApiKey", provider.ApiKey ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(provider.Parameters));
            command.Parameters.AddWithValue("@IsEnabled", provider.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@LastModified", (ulong)provider.LastModified.ToFileTimeUtc());
            command.Parameters.AddWithValue("@ProviderType", provider.ProviderType);
            command.Parameters.AddWithValue("@Timeout", provider.Timeout);
            command.Parameters.AddWithValue("@MaxConcurrency", provider.MaxConcurrency);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>
        /// Delete a provider
        /// </summary>
        /// <param name="id">The ID of the provider to delete</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeleteProviderAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM APIProviders WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }
} 
