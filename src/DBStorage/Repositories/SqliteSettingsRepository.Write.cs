using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for write operations
    public partial class SqliteSettingsRepository
    {
        /// <summary>
        /// Save settings for an application
        /// </summary>
        /// <param name="settings">The settings to save</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SaveSettingsAsync(Settings settings)
        {
            settings.LastModified = DateTime.UtcNow;

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Settings (ApplicationName, Version, Properties, LastModified, Theme)
                VALUES (@ApplicationName, @Version, @Properties, @LastModified, @Theme);
            ";
            command.Parameters.AddWithValue("@ApplicationName", settings.ApplicationName);
            command.Parameters.AddWithValue("@Version", settings.Version);
            command.Parameters.AddWithValue("@Properties", JsonSerializer.Serialize(settings.Properties));
            command.Parameters.AddWithValue("@LastModified", (ulong)settings.LastModified.ToFileTimeUtc());
            command.Parameters.AddWithValue("@Theme", settings.Theme);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>
        /// Delete settings for an application
        /// </summary>
        /// <param name="applicationName">The name of the application</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeleteSettingsAsync(string applicationName)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Settings WHERE ApplicationName = @ApplicationName;";
            command.Parameters.AddWithValue("@ApplicationName", applicationName);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }
} 
