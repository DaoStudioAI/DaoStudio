using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for write operations
    public partial class SqliteApplicationRepository
    {
        /// <summary>
        /// Create a new application
        /// </summary>
        /// <param name="application">The application to create</param>
        /// <returns>The created application with assigned ID</returns>
        public async Task<Application> CreateApplicationAsync(Application application)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));

            // Prevent duplicate names which violate the UNIQUE index on Name
            if (ApplicationExistsByName(application.Name))
                throw new InvalidOperationException($"An application with the name '{application.Name}' already exists. Please choose a different name.");

            var now = DateTime.UtcNow;
            application.LastModified = now;
            application.CreatedAt = now;

            // Generate a new unique ID
            application.Id = IdGenerator.GenerateUniqueId(ApplicationExists);

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Applications (Id, Name, BriefDescription, Description, LastModified, CreatedAt)
                VALUES (@Id, @Name, @BriefDescription, @Description, @LastModified, @CreatedAt);
            ";
            command.Parameters.AddWithValue("@Id", application.Id);
            command.Parameters.AddWithValue("@Name", application.Name);
            command.Parameters.AddWithValue("@BriefDescription", application.BriefDescription ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Description", application.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@LastModified", (ulong)application.LastModified.ToFileTimeUtc());
            command.Parameters.AddWithValue("@CreatedAt", (ulong)application.CreatedAt.ToFileTimeUtc());

            await command.ExecuteNonQueryAsync();
            return application;
        }

        /// <summary>
        /// Save changes to an existing application
        /// </summary>
        /// <param name="application">The application to update</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SaveApplicationAsync(Application application)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));

            if (application.Id == 0)
            {
                throw new ArgumentException("Cannot save application with ID 0. Use CreateApplicationAsync for new applications.");
            }

            application.LastModified = DateTime.UtcNow;

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Applications 
                SET Name = @Name, BriefDescription = @BriefDescription, Description = @Description, LastModified = @LastModified
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", application.Id);
            command.Parameters.AddWithValue("@Name", application.Name);
            command.Parameters.AddWithValue("@BriefDescription", application.BriefDescription ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Description", application.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@LastModified", (ulong)application.LastModified.ToFileTimeUtc());

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }

        /// <summary>
        /// Delete an application
        /// </summary>
        /// <param name="id">The ID of the application to delete</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeleteApplicationAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM Applications WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", id);

            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }

        /// <summary>
        /// Check if an application with the given ID exists
        /// </summary>
        /// <param name="id">The ID to check</param>
        /// <returns>True if the application exists</returns>
        private bool ApplicationExists(long id)
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(1) FROM Applications WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", id);

            var result = command.ExecuteScalar();
            return Convert.ToInt32(result) > 0;
        }
    }
}
