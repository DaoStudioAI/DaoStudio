using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    // Partial class implementation for read operations
    public partial class SqlitePersonRepository
    {
        public async Task<Person?> GetPersonAsync(long id)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, Description, ProviderName, ModelId, PresencePenalty, FrequencyPenalty, TopP, TopK, Temperature, Capability1, Capability2, Capability3, Image, ToolNames, Parameters, IsEnabled, LastModified, DeveloperMessage, CreatedAt, PersonType, AppId
                FROM Persons 
                WHERE Id = @Id;
            ";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Person
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    ProviderName = reader.GetString(3),
                    ModelId = reader.GetString(4),
                    PresencePenalty = !reader.IsDBNull(5) ? reader.GetDouble(5) : null,
                    FrequencyPenalty = !reader.IsDBNull(6) ? reader.GetDouble(6) : null,
                    TopP = !reader.IsDBNull(7) ? reader.GetDouble(7) : null,
                    TopK = !reader.IsDBNull(8) ? reader.GetInt32(8) : null,
                    Temperature = !reader.IsDBNull(9) ? reader.GetDouble(9) : null,
                    Capability1 = !reader.IsDBNull(10) ? reader.GetInt64(10) : null,
                    Capability2 = !reader.IsDBNull(11) ? reader.GetInt64(11) : null,
                    Capability3 = !reader.IsDBNull(12) ? reader.GetInt64(12) : null,
                    Image = !reader.IsDBNull(13) ? ReadBlob(reader, 13) : null,
                    ToolNames = JsonSerializer.Deserialize<string[]>(reader.GetString(14)) ?? Array.Empty<string>(),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(15)) ?? new Dictionary<string, string>(),
                    IsEnabled = reader.GetInt32(16) == 1,
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(17)), TimeZoneInfo.Local),
                    DeveloperMessage = !reader.IsDBNull(18) ? reader.GetString(18) : null,
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(19)), TimeZoneInfo.Local),
                    PersonType = reader.GetInt32(20),
                    AppId = reader.GetInt64(21)
                };
            }

            return null;
        }

        public bool PersonExists(long id)
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM Persons WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        public bool PersonNameExists(string name, long excludeId = 0)
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            if (excludeId != 0)
            {
                command.CommandText = "SELECT COUNT(1) FROM Persons WHERE Name = @Name AND Id != @ExcludeId;";
                command.Parameters.AddWithValue("@ExcludeId", excludeId);
            }
            else
            {
                command.CommandText = "SELECT COUNT(1) FROM Persons WHERE Name = @Name;";
            }
            command.Parameters.AddWithValue("@Name", name);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        public async Task<IEnumerable<Person>> GetAllPersonsAsync(bool includeImage = true)
        {
            var persons = new List<Person>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, Description, ProviderName, ModelId, PresencePenalty, FrequencyPenalty, TopP, TopK, Temperature, Capability1, Capability2, Capability3, Image, ToolNames, Parameters, IsEnabled, LastModified, DeveloperMessage, CreatedAt, PersonType, AppId
                FROM Persons
                ORDER BY CreatedAt;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                persons.Add(ReadPersonFromReader(reader, includeImage));
            }

            return persons;
        }

        public async Task<IEnumerable<Person>> GetPersonsByProviderNameAsync(string providerName) // Renamed method and parameter
        {
            var persons = new List<Person>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, Description, ProviderName, ModelId, PresencePenalty, FrequencyPenalty, TopP, TopK, Temperature, Capability1, Capability2, Capability3, Image, ToolNames, Parameters, IsEnabled, LastModified, DeveloperMessage, CreatedAt, PersonType, AppId
                FROM Persons
                WHERE ProviderName = @ProviderName
                ORDER BY CreatedAt;
            ";
            command.Parameters.AddWithValue("@ProviderName", providerName); // Updated parameter name

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                persons.Add(new Person
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    ProviderName = reader.GetString(3),
                    ModelId = reader.GetString(4),
                    PresencePenalty = !reader.IsDBNull(5) ? reader.GetDouble(5) : null,
                    FrequencyPenalty = !reader.IsDBNull(6) ? reader.GetDouble(6) : null,
                    TopP = !reader.IsDBNull(7) ? reader.GetDouble(7) : null,
                    TopK = !reader.IsDBNull(8) ? reader.GetInt32(8) : null,
                    Temperature = !reader.IsDBNull(9) ? reader.GetDouble(9) : null,
                    Capability1 = !reader.IsDBNull(10) ? reader.GetInt64(10) : null,
                    Capability2 = !reader.IsDBNull(11) ? reader.GetInt64(11) : null,
                    Capability3 = !reader.IsDBNull(12) ? reader.GetInt64(12) : null,
                    Image = !reader.IsDBNull(13) ? ReadBlob(reader, 13) : null,
                    ToolNames = JsonSerializer.Deserialize<string[]>(reader.GetString(14)) ?? Array.Empty<string>(),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(15)) ?? new Dictionary<string, string>(),
                    IsEnabled = reader.GetInt32(16) == 1,
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(17)), TimeZoneInfo.Local),
                    DeveloperMessage = !reader.IsDBNull(18) ? reader.GetString(18) : null,
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(19)), TimeZoneInfo.Local),
                    PersonType = reader.GetInt32(20),
                    AppId = reader.GetInt64(21)
                });
            }

            return persons;
        }


        public async Task<IEnumerable<Person>> GetEnabledPersonsAsync(bool includeImage = true)
        {
            var persons = new List<Person>();

            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, Description, ProviderName, ModelId, PresencePenalty, FrequencyPenalty, TopP, TopK, Temperature, Capability1, Capability2, Capability3, Image, ToolNames, Parameters, IsEnabled, LastModified, DeveloperMessage, CreatedAt, PersonType, AppId
                FROM Persons
                WHERE IsEnabled = 1
                ORDER BY CreatedAt;
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                persons.Add(ReadPersonFromReader(reader, includeImage));
            }

            return persons;
        }


        public async Task<Person?> GetPersonByNameAsync(string name)
        {
            var connection = await GetConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, Description, ProviderName, ModelId, PresencePenalty, FrequencyPenalty, TopP, TopK, Temperature, Capability1, Capability2, Capability3, Image, ToolNames, Parameters, IsEnabled, LastModified, DeveloperMessage, CreatedAt, PersonType, AppId
                FROM Persons 
                WHERE Name = @Name;
            ";
            command.Parameters.AddWithValue("@Name", name);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Person
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    ProviderName = reader.GetString(3),
                    ModelId = reader.GetString(4),
                    PresencePenalty = !reader.IsDBNull(5) ? reader.GetDouble(5) : null,
                    FrequencyPenalty = !reader.IsDBNull(6) ? reader.GetDouble(6) : null,
                    TopP = !reader.IsDBNull(7) ? reader.GetDouble(7) : null,
                    TopK = !reader.IsDBNull(8) ? reader.GetInt32(8) : null,
                    Temperature = !reader.IsDBNull(9) ? reader.GetDouble(9) : null,
                    Capability1 = !reader.IsDBNull(10) ? reader.GetInt64(10) : null,
                    Capability2 = !reader.IsDBNull(11) ? reader.GetInt64(11) : null,
                    Capability3 = !reader.IsDBNull(12) ? reader.GetInt64(12) : null,
                    Image = !reader.IsDBNull(13) ? ReadBlob(reader, 13) : null,
                    ToolNames = JsonSerializer.Deserialize<string[]>(reader.GetString(14)) ?? Array.Empty<string>(),
                    Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(15)) ?? new Dictionary<string, string>(),
                    IsEnabled = reader.GetInt32(16) == 1,
                    LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(17)), TimeZoneInfo.Local),
                    DeveloperMessage = !reader.IsDBNull(18) ? reader.GetString(18) : null,
                    CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(19)), TimeZoneInfo.Local),
                    PersonType = reader.GetInt32(20),
                    AppId = reader.GetInt64(21)
                };
            }

            return null;
        }

        public async Task<IEnumerable<Person>> GetPersonsByNamesAsync(IEnumerable<string> names, bool includeImage = true)
        {
            var persons = new List<Person>();
            var namesList = names?.ToList();
            
            if (namesList == null || !namesList.Any())
            {
                return persons;
            }

            var connection = await GetConnectionAsync();

            // Build parameterized IN clause
            var parameters = namesList.Select((name, index) => $"@Name{index}").ToArray();
            var inClause = string.Join(",", parameters);

            var command = connection.CreateCommand();
            command.CommandText = @$"
                SELECT Id, Name, Description, ProviderName, ModelId, PresencePenalty, FrequencyPenalty, TopP, TopK, Temperature, Capability1, Capability2, Capability3, Image, ToolNames, Parameters, IsEnabled, LastModified, DeveloperMessage, CreatedAt, PersonType, AppId
                FROM Persons 
                WHERE Name IN ({inClause})
                ORDER BY Name;
            ";

            // Add parameters
            for (int i = 0; i < namesList.Count; i++)
            {
                command.Parameters.AddWithValue($"@Name{i}", namesList[i]);
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                persons.Add(ReadPersonFromReader(reader, includeImage));
            }

            return persons;
        }

        // Helper method to read blob data
        private byte[]? ReadBlob(SqliteDataReader reader, int ordinal)
        {
            const int bufferSize = 4096;
            byte[] buffer = new byte[bufferSize];
            long bytesRead;
            long fieldOffset = 0;
            using var stream = new System.IO.MemoryStream();

            while ((bytesRead = reader.GetBytes(ordinal, fieldOffset, buffer, 0, bufferSize)) > 0)
            {
                stream.Write(buffer, 0, (int)bytesRead);
                fieldOffset += bytesRead;
            }
            return stream.Length > 0 ? stream.ToArray() : null;
        }

        // Helper method to create Person object with optional image loading
        private Person ReadPersonFromReader(SqliteDataReader reader, bool includeImage)
        {
            return new Person
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                ProviderName = reader.GetString(3),
                ModelId = reader.GetString(4),
                PresencePenalty = !reader.IsDBNull(5) ? reader.GetDouble(5) : null,
                FrequencyPenalty = !reader.IsDBNull(6) ? reader.GetDouble(6) : null,
                TopP = !reader.IsDBNull(7) ? reader.GetDouble(7) : null,
                TopK = !reader.IsDBNull(8) ? reader.GetInt32(8) : null,
                Temperature = !reader.IsDBNull(9) ? reader.GetDouble(9) : null,
                Capability1 = !reader.IsDBNull(10) ? reader.GetInt64(10) : null,
                Capability2 = !reader.IsDBNull(11) ? reader.GetInt64(11) : null,
                Capability3 = !reader.IsDBNull(12) ? reader.GetInt64(12) : null,
                Image = includeImage && !reader.IsDBNull(13) ? ReadBlob(reader, 13) : null,
                ToolNames = JsonSerializer.Deserialize<string[]>(reader.GetString(14)) ?? Array.Empty<string>(),
                Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(15)) ?? new Dictionary<string, string>(),
                IsEnabled = reader.GetInt32(16) == 1,
                LastModified = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(17)), TimeZoneInfo.Local),
                DeveloperMessage = !reader.IsDBNull(18) ? reader.GetString(18) : null,
                CreatedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.FromFileTimeUtc(reader.GetInt64(19)), TimeZoneInfo.Local),
                PersonType = reader.GetInt32(20),
                AppId = reader.GetInt64(21)
            };
        }
    }
}

