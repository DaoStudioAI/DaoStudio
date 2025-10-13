using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for read operations
    public partial class SqliteSettingsRepository
    {
        /// <summary>
        /// Get settings by application name
        /// </summary>
        /// <param name="applicationName">The name of the application</param>
        /// <returns>Settings object or null if not found</returns>
        public async Task<Settings?> GetSettingsAsync(string applicationName)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Settings WHERE ApplicationName = @ApplicationName;";
            command.Parameters.AddWithValue("@ApplicationName", applicationName);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Settings
                {
                    ApplicationName = reader.GetString(0),
                    Version = reader.GetInt32(1),
                    Properties = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(2)) ?? new Dictionary<string, string>(),
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(3)), TimeZoneInfo.Local),
                    Theme = reader.IsDBNull(4) ? 2 : reader.GetInt32(4) // 0=Light, 1=Dark, 2=System
                };
            }

            return null;
        }

        /// <summary>
        /// Get all application settings
        /// </summary>
        /// <returns>List of all settings</returns>
        public async Task<IEnumerable<Settings>> GetAllSettingsAsync()
        {
            var settings = new List<Settings>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Settings;";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                settings.Add(new Settings
                {
                    ApplicationName = reader.GetString(0),
                    Version = reader.GetInt32(1),
                    Properties = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(2)) ?? new Dictionary<string, string>(),
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(3)), TimeZoneInfo.Local),
                    Theme = reader.IsDBNull(4) ? 2 : reader.GetInt32(4) // 0=Light, 1=Dark, 2=System
                });
            }

            return settings;
        }
    }
} 
