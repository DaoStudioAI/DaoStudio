using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for read operations
    public partial class SqliteApplicationRepository
    {
        /// <summary>
        /// Get an application by name
        /// </summary>
        /// <param name="name">The name of the application</param>
        /// <returns>Application or null if not found</returns>
        public async Task<Application?> GetApplicationByNameAsync(string name)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, BriefDescription, Description, LastModified, CreatedAt
                FROM Applications 
                WHERE Name = @Name;
            ";
            command.Parameters.AddWithValue("@Name", name);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Application
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    BriefDescription = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(4)), TimeZoneInfo.Local),
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(5)), TimeZoneInfo.Local)
                };
            }

            return null;
        }

        /// <summary>
        /// Get an application by ID
        /// </summary>
        /// <param name="id">The ID of the application</param>
        /// <returns>Application or null if not found</returns>
        public async Task<Application?> GetApplicationAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, BriefDescription, Description, LastModified, CreatedAt
                FROM Applications 
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Application
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    BriefDescription = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(4)), TimeZoneInfo.Local),
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(5)), TimeZoneInfo.Local)
                };
            }

            return null;
        }

        /// <summary>
        /// Check if an application with the given name exists
        /// </summary>
        /// <param name="name">The name to check</param>
        /// <returns>True if the application exists</returns>
        public bool ApplicationExistsByName(string name)
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(1) FROM Applications WHERE Name = @Name;
            ";
            command.Parameters.AddWithValue("@Name", name);

            var result = command.ExecuteScalar();
            return Convert.ToInt32(result) > 0;
        }

        /// <summary>
        /// Get all applications
        /// </summary>
        /// <returns>List of all applications</returns>
        public async Task<IEnumerable<Application>> GetAllApplicationsAsync()
        {
            var connection = await GetConnectionAsync();
            var applications = new List<Application>();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, BriefDescription, Description, LastModified, CreatedAt
                FROM Applications 
                ORDER BY Name;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                applications.Add(new Application
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    BriefDescription = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(4)), TimeZoneInfo.Local),
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(5)), TimeZoneInfo.Local)
                });
            }

            return applications;
        }
    }
}
