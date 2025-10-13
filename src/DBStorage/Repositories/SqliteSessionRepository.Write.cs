using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for write operations
    public partial class SqliteSessionRepository
    {
        /// <summary>
        /// Create a new session
        /// </summary>
        /// <param name="session">The session to create</param>
        /// <returns>The created session with assigned ID</returns>
        public async Task<Session> CreateSessionAsync(Session session)
        {
            session.LastModified = DateTime.UtcNow;
            session.CreatedAt = DateTime.UtcNow;

            // Generate a new unique ID
            session.Id = IdGenerator.GenerateUniqueId(SessionExists);

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            // Insert all relevant fields. Note: column name is PersonNames (JSON array) in the schema.
            command.CommandText = @"
                INSERT INTO Sessions (Id, Title, Description, Logo, PersonNames, ToolNames, ParentSessId, CreatedAt, LastModified,
                                    TotalTokenCount, OutputTokenCount, InputTokenCount, AdditionalCounts, Properties, SessionType, AppId, PreviousId)
                VALUES (@Id, @Title, @Description, @Logo, @PersonNames, @ToolNames, @ParentSessId, @CreatedAt, @LastModified,
                        @TotalTokenCount, @OutputTokenCount, @InputTokenCount, @AdditionalCounts, @Properties, @SessionType, @AppId, @PreviousId);
            ";
            command.Parameters.AddWithValue("@Id", session.Id);
            command.Parameters.AddWithValue("@Title", session.Title);
            command.Parameters.AddWithValue("@Description", session.Description);
            command.Parameters.AddWithValue("@Logo", session.Logo as object ?? DBNull.Value);
            command.Parameters.AddWithValue("@PersonNames", JsonSerializer.Serialize(session.PersonNames));
            command.Parameters.AddWithValue("@ToolNames", JsonSerializer.Serialize(session.ToolNames));
            command.Parameters.AddWithValue("@SessionType", session.SessionType);
            command.Parameters.AddWithValue("@AppId", session.AppId);
            command.Parameters.AddWithValue("@PreviousId", session.PreviousId as object ?? DBNull.Value);
            command.Parameters.AddWithValue("@ParentSessId", session.ParentSessId as object ?? DBNull.Value);
            command.Parameters.AddWithValue("@CreatedAt", (ulong)session.CreatedAt.ToFileTimeUtc());
            command.Parameters.AddWithValue("@LastModified", (ulong)session.LastModified.ToFileTimeUtc());
            command.Parameters.AddWithValue("@TotalTokenCount", session.TotalTokenCount);
            command.Parameters.AddWithValue("@OutputTokenCount", session.OutputTokenCount);
            command.Parameters.AddWithValue("@InputTokenCount", session.InputTokenCount);
            command.Parameters.AddWithValue("@AdditionalCounts", session.AdditionalCounts);
            command.Parameters.AddWithValue("@Properties", JsonSerializer.Serialize(session.Properties));

            await command.ExecuteNonQueryAsync();
            return session;
        }

        /// <summary>
        /// Save changes to an existing session
        /// </summary>
        /// <param name="session">The session to update</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SaveSessionAsync(Session session)
        {
            if (session.Id == 0)
            {
                throw new ArgumentException("Cannot save session with ID 0. Use CreateSessionAsync for new sessions.");
            }

            session.LastModified = DateTime.UtcNow;

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Sessions 
                SET Title = @Title,
                    Description = @Description,
                    Logo = @Logo,
                    PersonNames = @PersonNames,
                    ToolNames = @ToolNames,
                    ParentSessId = @ParentSessId,
                    LastModified = @LastModified,
                    TotalTokenCount = @TotalTokenCount,
                    OutputTokenCount = @OutputTokenCount,
                    InputTokenCount = @InputTokenCount,
                    AdditionalCounts = @AdditionalCounts,
                    Properties = @Properties,
                    SessionType = @SessionType,
                    AppId = @AppId,
                    PreviousId = @PreviousId
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", session.Id);
            command.Parameters.AddWithValue("@Title", session.Title);
            command.Parameters.AddWithValue("@Description", session.Description);
            command.Parameters.AddWithValue("@Logo", session.Logo as object ?? DBNull.Value);
            command.Parameters.AddWithValue("@PersonNames", JsonSerializer.Serialize(session.PersonNames));
            command.Parameters.AddWithValue("@ToolNames", JsonSerializer.Serialize(session.ToolNames));
            command.Parameters.AddWithValue("@SessionType", session.SessionType);
            command.Parameters.AddWithValue("@AppId", session.AppId);
            command.Parameters.AddWithValue("@PreviousId", session.PreviousId as object ?? DBNull.Value);
            command.Parameters.AddWithValue("@ParentSessId", session.ParentSessId as object ?? DBNull.Value);
            command.Parameters.AddWithValue("@LastModified", (ulong)session.LastModified.ToFileTimeUtc());
            command.Parameters.AddWithValue("@TotalTokenCount", session.TotalTokenCount);
            command.Parameters.AddWithValue("@OutputTokenCount", session.OutputTokenCount);
            command.Parameters.AddWithValue("@InputTokenCount", session.InputTokenCount);
            command.Parameters.AddWithValue("@AdditionalCounts", session.AdditionalCounts);
            command.Parameters.AddWithValue("@Properties", JsonSerializer.Serialize(session.Properties));

            return await command.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>
        /// Delete a session
        /// </summary>
        /// <param name="id">The ID of the session to delete</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeleteSessionAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Sessions WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }
} 
