using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for read operations
    public partial class SqliteAPIProviderRepository
    {
        /// <summary>
        /// Get a provider by name
        /// </summary>
        /// <param name="name">The name of the provider</param>
        /// <returns>API provider or null if not found</returns>
        public async Task<APIProvider?> GetProviderByNameAsync(string name)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, ApiEndpoint, ApiKey, Parameters, IsEnabled, LastModified, CreatedAt, ProviderType, Timeout, MaxConcurrency
                FROM APIProviders 
                WHERE Name = @Name;
            ";
            command.Parameters.AddWithValue("@Name", name);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new APIProvider
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    ApiEndpoint = reader.GetString(2),
                    ApiKey = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(4)) 
                    ?? new Dictionary<string, string>(),
                    IsEnabled = reader.GetInt32(5) == 1,
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(6)), TimeZoneInfo.Local),
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(7)), TimeZoneInfo.Local),
                    ProviderType = reader.GetInt32(8),
                    Timeout = reader.GetInt32(9),
                    MaxConcurrency = reader.GetInt32(10)
                };
            }

            return null;
        }

        /// <summary>
        /// Get a provider by ID
        /// </summary>
        /// <param name="id">The ID of the provider</param>
        /// <returns>API provider or null if not found</returns>
        public async Task<APIProvider?> GetProviderAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, ApiEndpoint, ApiKey, Parameters, IsEnabled, LastModified, CreatedAt, ProviderType, Timeout, MaxConcurrency
                FROM APIProviders 
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new APIProvider
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    ApiEndpoint = reader.GetString(2),
                    ApiKey = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(4)) 
                    ?? new Dictionary<string, string>(),
                    IsEnabled = reader.GetInt32(5) == 1,
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(6)), TimeZoneInfo.Local),
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(7)), TimeZoneInfo.Local),
                    ProviderType = reader.GetInt32(8),
                    Timeout = reader.GetInt32(9),
                    MaxConcurrency = reader.GetInt32(10)
                };
            }

            return null;
        }

        /// <summary>
        /// Check if a provider with the given name exists
        /// </summary>
        /// <param name="name">The name to check</param>
        /// <returns>True if the provider exists</returns>
        public bool ProviderExistsByName(string name)
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM APIProviders WHERE Name = @Name;";
            command.Parameters.AddWithValue("@Name", name);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// Check if a provider with the given ID exists
        /// </summary>
        /// <param name="id">The ID to check</param>
        /// <returns>True if the provider exists</returns>
        public bool ProviderExists(long id)
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM APIProviders WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// Get all providers
        /// </summary>
        /// <returns>List of all providers</returns>
        public async Task<IEnumerable<APIProvider>> GetAllProvidersAsync()
        {
            var providers = new List<APIProvider>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, ApiEndpoint, ApiKey, Parameters, IsEnabled, LastModified, CreatedAt, ProviderType, Timeout, MaxConcurrency
                FROM APIProviders;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                providers.Add(new APIProvider
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    ApiEndpoint = reader.GetString(2),
                    ApiKey = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(4)) ?? new Dictionary<string, string>(),
                    IsEnabled = reader.GetInt32(5) == 1,
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(6)), TimeZoneInfo.Local),
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(7)), TimeZoneInfo.Local),
                    ProviderType = reader.GetInt32(8),
                    Timeout = reader.GetInt32(9),
                    MaxConcurrency = reader.GetInt32(10)
                });
            }

            return providers;
        }
    }
} 
