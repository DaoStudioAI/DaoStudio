using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Common;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for write operations
    public partial class SqlitePersonRepository
    {
        public async Task<Person> CreatePersonAsync(Person person)
        {
            if (PersonNameExists(person.Name))
            {
                throw new InvalidOperationException($"A person with the name '{person.Name}' already exists.");
            }

            person.LastModified = DateTime.UtcNow;
            person.CreatedAt = DateTime.UtcNow;

            // Generate a new unique ID
            person.Id = IdGenerator.GenerateUniqueId(PersonExists);

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Persons (Id, Name, Description, ProviderName, ModelId, PresencePenalty, FrequencyPenalty, TopP, TopK, Temperature, Capability1, Capability2, Capability3, Image, ToolNames, Parameters, IsEnabled, LastModified, DeveloperMessage, CreatedAt, PersonType, AppId)
                VALUES (@Id, @Name, @Description, @ProviderName, @ModelId, @PresencePenalty, @FrequencyPenalty, @TopP, @TopK, @Temperature, @Capability1, @Capability2, @Capability3, @Image, @ToolNames, @Parameters, @IsEnabled, @LastModified, @DeveloperMessage, @CreatedAt, @PersonType, @AppId);
            ";
            command.Parameters.AddWithValue("@Id", person.Id);
            command.Parameters.AddWithValue("@Name", person.Name);
            command.Parameters.AddWithValue("@Description", person.Description);
            command.Parameters.AddWithValue("@ProviderName", person.ProviderName); // Updated parameter name and property
            command.Parameters.AddWithValue("@ModelId", person.ModelId);
            command.Parameters.AddWithValue("@PresencePenalty", person.PresencePenalty ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@FrequencyPenalty", person.FrequencyPenalty ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@TopP", person.TopP ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@TopK", person.TopK ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Temperature", person.Temperature ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Capability1", person.Capability1 ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Capability2", person.Capability2 ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Capability3", person.Capability3 ?? (object)DBNull.Value);
            // Handle potential null image
            var imageParam = new SqliteParameter("@Image", person.Image ?? (object)DBNull.Value) { DbType = System.Data.DbType.Binary };
            command.Parameters.Add(imageParam);
            command.Parameters.AddWithValue("@ToolNames", JsonSerializer.Serialize(person.ToolNames)); // Updated parameter name and property
            command.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(person.Parameters));
            command.Parameters.AddWithValue("@IsEnabled", person.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@LastModified", (ulong)person.LastModified.ToFileTimeUtc());
            command.Parameters.AddWithValue("@DeveloperMessage", person.DeveloperMessage ?? string.Empty);
            command.Parameters.AddWithValue("@CreatedAt", (ulong)person.CreatedAt.ToFileTimeUtc());
            command.Parameters.AddWithValue("@PersonType", person.PersonType);
            command.Parameters.AddWithValue("@AppId", person.AppId);

            await command.ExecuteNonQueryAsync();
            return person;
        }

        public async Task<bool> SavePersonAsync(Person person)
        {
            if (person.Id == 0)
            {
                throw new ArgumentException("Cannot save person with ID 0. Use CreatePersonAsync for new persons.");
            }

            if (PersonNameExists(person.Name, person.Id))
            {
                throw new InvalidOperationException($"Another person with the name '{person.Name}' already exists.");
            }

            person.LastModified = DateTime.UtcNow;

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Persons 
                SET Name = @Name,
                    Description = @Description,
                    ProviderName = @ProviderName,
                    ModelId = @ModelId,
                    PresencePenalty = @PresencePenalty,
                    FrequencyPenalty = @FrequencyPenalty,
                    TopP = @TopP,
                    TopK = @TopK,
                    Temperature = @Temperature,
                    Capability1 = @Capability1,
                    Capability2 = @Capability2,
                    Capability3 = @Capability3,
                    Image = @Image,
                    ToolNames = @ToolNames,
                    Parameters = @Parameters,
                    IsEnabled = @IsEnabled,
                    LastModified = @LastModified,
                    DeveloperMessage = @DeveloperMessage,
                    PersonType = @PersonType,
                    AppId = @AppId
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", person.Id);
            command.Parameters.AddWithValue("@Name", person.Name);
            command.Parameters.AddWithValue("@Description", person.Description);
            command.Parameters.AddWithValue("@ProviderName", person.ProviderName); // Updated parameter name and property
            command.Parameters.AddWithValue("@ModelId", person.ModelId);
            command.Parameters.AddWithValue("@PresencePenalty", person.PresencePenalty ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@FrequencyPenalty", person.FrequencyPenalty ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@TopP", person.TopP ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@TopK", person.TopK ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Temperature", person.Temperature ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Capability1", person.Capability1 ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Capability2", person.Capability2 ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Capability3", person.Capability3 ?? (object)DBNull.Value);
            // Handle potential null image
            var imageParamUpdate = new SqliteParameter("@Image", person.Image ?? (object)DBNull.Value) { DbType = System.Data.DbType.Binary };
            command.Parameters.Add(imageParamUpdate);
            command.Parameters.AddWithValue("@ToolNames", JsonSerializer.Serialize(person.ToolNames)); // Updated parameter name and property
            command.Parameters.AddWithValue("@Parameters", JsonSerializer.Serialize(person.Parameters));
            command.Parameters.AddWithValue("@IsEnabled", person.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@LastModified", (ulong)person.LastModified.ToFileTimeUtc());
            command.Parameters.AddWithValue("@DeveloperMessage", person.DeveloperMessage ?? string.Empty);
            command.Parameters.AddWithValue("@PersonType", person.PersonType);
            command.Parameters.AddWithValue("@AppId", person.AppId);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeletePersonAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Persons WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            return await command.ExecuteNonQueryAsync() > 0;
        }
    }
}

