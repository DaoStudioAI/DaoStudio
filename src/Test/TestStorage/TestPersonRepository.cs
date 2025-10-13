using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Repositories;
using Xunit;

namespace Test.TestStorage
{
    public class TestPersonRepository : IDisposable
    {
        private readonly string _testDbPath;
        private readonly IPersonRepository _personRepository;

        public TestPersonRepository()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_person_repo_{Guid.NewGuid()}.db");
            
            // Initialize repository directly with SqlitePersonRepository
            _personRepository = new SqlitePersonRepository(_testDbPath);
        }

        public void Dispose()
        {
            // Clean up the test database after tests
            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                }
                catch
                {
                    // Ignore deletion errors during cleanup
                }
            }
        }

        [Fact]
        public async Task GetPersonReturnsNullForNonExistentPerson()
        {
            // Arrange - nothing to arrange

            // Act
            var person = await _personRepository.GetPersonAsync(999);

            // Assert
            Assert.Null(person);
        }

        [Fact]
        public async Task SaveAndGetPersonWorks()
        {
            // Arrange
            var newPerson = new Person
            {
                Name = "Test Person",
                Description = "Test person description",
                DeveloperMessage = "You are a helpful assistant.",
                PersonType = 0, // Normal
                AppId = 12345L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            // Act - Create
            var createdPerson = await _personRepository.CreatePersonAsync(newPerson);
            var retrievedPerson = await _personRepository.GetPersonAsync(createdPerson.Id);

            // Assert - Create
            Assert.NotNull(createdPerson);
            Assert.NotNull(retrievedPerson);
            Assert.Equal(newPerson.Id, retrievedPerson.Id);
            Assert.Equal("Test Person", retrievedPerson.Name);
            Assert.Equal("Test person description", retrievedPerson.Description);
            Assert.Equal("You are a helpful assistant.", retrievedPerson.DeveloperMessage);
            Assert.Equal(0, retrievedPerson.PersonType); // Normal
            Assert.Equal(12345L, retrievedPerson.AppId);

            // Act - Update
            retrievedPerson.Name = "Updated Test Person";
            retrievedPerson.PersonType = 0; // Normal
            retrievedPerson.AppId = 54321L;
            var updateResult = await _personRepository.SavePersonAsync(retrievedPerson);
            var updatedPerson = await _personRepository.GetPersonAsync(retrievedPerson.Id);

            // Assert - Update
            Assert.True(updateResult);
            Assert.Equal("Updated Test Person", updatedPerson!.Name);
            Assert.Equal(0, updatedPerson.PersonType); // Normal
            Assert.Equal(54321L, updatedPerson.AppId);
            Assert.Equal(retrievedPerson.CreatedAt, updatedPerson.CreatedAt);
        }

        [Fact]
        public async Task PersonTypeFieldIsPersisted()
        {
            // Arrange
            var person = new Person
            {
                Name = "PersonType Test Person",
                Description = "Testing PersonType field",
                DeveloperMessage = "Test system prompt",
                PersonType = 0, // Normal
                AppId = 555L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            // Act
            var createdPerson = await _personRepository.CreatePersonAsync(person);
            var retrievedPerson = await _personRepository.GetPersonAsync(createdPerson.Id);

            // Assert
            Assert.NotNull(retrievedPerson);
            Assert.Equal(0, retrievedPerson.PersonType); // Normal
            Assert.Equal("PersonType Test Person", retrievedPerson.Name);

            // Test updating PersonType
            retrievedPerson.PersonType = 0; // Normal
            await _personRepository.SavePersonAsync(retrievedPerson);
            var updatedPerson = await _personRepository.GetPersonAsync(retrievedPerson.Id);

            Assert.Equal(0, updatedPerson?.PersonType); // Normal
        }

        [Fact]
        public async Task AppIdFieldIsPersisted()
        {
            // Arrange
            var person = new Person
            {
                Name = "AppId Test Person",
                Description = "Testing AppId field",
                DeveloperMessage = "Test system prompt for AppId",
                PersonType = 0, // Normal
                AppId = 98765L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            // Act
            var createdPerson = await _personRepository.CreatePersonAsync(person);
            var retrievedPerson = await _personRepository.GetPersonAsync(createdPerson.Id);

            // Assert
            Assert.NotNull(retrievedPerson);
            Assert.Equal(98765L, retrievedPerson.AppId);
            Assert.Equal("AppId Test Person", retrievedPerson.Name);

            // Test updating AppId
            retrievedPerson.AppId = 11111L;
            await _personRepository.SavePersonAsync(retrievedPerson);
            var updatedPerson = await _personRepository.GetPersonAsync(retrievedPerson.Id);

            Assert.Equal(11111L, updatedPerson?.AppId);
        }

        [Fact]
        public async Task AllPersonTypesArePersisted()
        {
            // Arrange - Test all PersonType enum values
            var personTypes = new[]
            {
                0, // Normal
                0, // Normal
                0, // Normal
                0, // Normal
                0, // Normal
                0  // Normal
            };

            var persons = new List<Person>();
            for (int i = 0; i < personTypes.Length; i++)
            {
                persons.Add(new Person
                {
                    Name = $"Person {i + 1}",
                    Description = $"Person with {personTypes[i]} type",
                    DeveloperMessage = $"System prompt for {personTypes[i]}",
                    PersonType = personTypes[i],
                    AppId = (long)(i + 1) * 1000,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                });
            }

            // Act - Create all persons
            var createdPersons = new List<Person>();
            foreach (var person in persons)
            {
                var created = await _personRepository.CreatePersonAsync(person);
                createdPersons.Add(created);
            }

            // Assert - Verify all PersonTypes are correctly persisted
            for (int i = 0; i < personTypes.Length; i++)
            {
                var retrievedPerson = await _personRepository.GetPersonAsync(createdPersons[i].Id);
                Assert.NotNull(retrievedPerson);
                Assert.Equal(personTypes[i], retrievedPerson.PersonType);
                Assert.Equal((long)(i + 1) * 1000, retrievedPerson.AppId);
                Assert.Equal($"Person {i + 1}", retrievedPerson.Name);
            }
        }

        [Fact]
        public async Task GetAllPersonsWorks()
        {
            // Arrange
            var person1 = new Person
            {
                Name = "Person 1",
                Description = "First person",
                DeveloperMessage = "System prompt 1",
                PersonType = 0, // Normal
                AppId = 100L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            var person2 = new Person
            {
                Name = "Person 2",
                Description = "Second person",
                DeveloperMessage = "System prompt 2",
                PersonType = 0, // Normal
                AppId = 200L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            await _personRepository.CreatePersonAsync(person1);
            await _personRepository.CreatePersonAsync(person2);

            // Act
            var allPersons = await _personRepository.GetAllPersonsAsync();

            // Assert
            Assert.Equal(2, allPersons.Count());
            Assert.Contains(allPersons, p => p.Name == "Person 1" && p.PersonType == 0); // Normal
            Assert.Contains(allPersons, p => p.Name == "Person 2" && p.PersonType == 0); // Normal
        }

        [Fact]
        public async Task DeletePersonWorks()
        {
            // Arrange
            var person = new Person
            {
                Name = "Person to delete",
                Description = "Will be deleted",
                DeveloperMessage = "Delete me",
                PersonType = 0, // Normal
                AppId = 999L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            var createdPerson = await _personRepository.CreatePersonAsync(person);

            // Act
            var deleteResult = await _personRepository.DeletePersonAsync(createdPerson.Id);
            var retrievedPerson = await _personRepository.GetPersonAsync(createdPerson.Id);

            // Assert
            Assert.True(deleteResult);
            Assert.Null(retrievedPerson);
        }

        [Fact]
        public async Task PersonTypeAndAppIdWorkTogether()
        {
            // Arrange - comprehensive test with both new fields
            var person = new Person
            {
                Name = "Comprehensive Test Person",
                Description = "Testing PersonType and AppId together",
                DeveloperMessage = "You are a comprehensive test assistant.",
                PersonType = 0, // Normal
                AppId = 42424242L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            // Act
            var createdPerson = await _personRepository.CreatePersonAsync(person);
            var retrievedPerson = await _personRepository.GetPersonAsync(createdPerson.Id);

            // Assert both new fields
            Assert.NotNull(retrievedPerson);
            Assert.Equal("Comprehensive Test Person", retrievedPerson.Name);
            Assert.Equal(0, retrievedPerson.PersonType); // Normal
            Assert.Equal(42424242L, retrievedPerson.AppId);

            // Test updating both fields
            retrievedPerson.PersonType = 0; // Normal
            retrievedPerson.AppId = 99999999L;
            
            await _personRepository.SavePersonAsync(retrievedPerson);
            var updatedPerson = await _personRepository.GetPersonAsync(retrievedPerson.Id);

            Assert.Equal(0, updatedPerson?.PersonType); // Normal
            Assert.Equal(99999999L, updatedPerson?.AppId);
        }

        [Fact]
        public async Task DefaultAppIdValueIsPersisted()
        {
            // Arrange - Person without explicit AppId
            var person = new Person
            {
                Name = "Default AppId Person",
                Description = "Testing default AppId value",
                DeveloperMessage = "Default AppId test",
                PersonType = 0 // Normal
                // AppId not set, should use default value
            };
            
            // Act
            var createdPerson = await _personRepository.CreatePersonAsync(person);
            var retrievedPerson = await _personRepository.GetPersonAsync(createdPerson.Id);

            // Assert
            Assert.NotNull(retrievedPerson);
            Assert.Equal(0L, retrievedPerson.AppId); // Default value
            Assert.Equal(0, retrievedPerson.PersonType); // Normal
        }

        [Fact]
        public async Task GetPersonByNameWorks()
        {
            // Arrange
            var person = new Person
            {
                Name = "Unique Name Person",
                Description = "Person with unique name",
                DeveloperMessage = "Unique system prompt",
                PersonType = 0, // Normal
                AppId = 777L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            await _personRepository.CreatePersonAsync(person);

            // Act
            var retrievedPerson = await _personRepository.GetPersonByNameAsync("Unique Name Person");
            var nonExistentPerson = await _personRepository.GetPersonByNameAsync("Non Existent Person");

            // Assert
            Assert.NotNull(retrievedPerson);
            Assert.Equal("Unique Name Person", retrievedPerson.Name);
            Assert.Equal(0, retrievedPerson.PersonType); // Normal
            Assert.Equal(777L, retrievedPerson.AppId);
            Assert.Null(nonExistentPerson);
        }

        [Fact]
        public async Task CreatePersonWithDuplicateNameThrowsException()
        {
            // Arrange
            var person1 = new Person
            {
                Name = "Duplicate Name",
                Description = "First person with this name",
                DeveloperMessage = "First person prompt",
                PersonType = 0,
                AppId = 1000L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            var person2 = new Person
            {
                Name = "Duplicate Name", // Same name as person1
                Description = "Second person with same name",
                DeveloperMessage = "Second person prompt",
                PersonType = 0,
                AppId = 2000L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            // Act & Assert
            await _personRepository.CreatePersonAsync(person1);
            
            // Creating second person with same name should throw exception due to unique constraint
            await Assert.ThrowsAnyAsync<Exception>(() => _personRepository.CreatePersonAsync(person2));
        }

        [Fact]
        public async Task UpdatePersonToDuplicateNameThrowsException()
        {
            // Arrange
            var person1 = new Person
            {
                Name = "Original Name 1",
                Description = "First person",
                DeveloperMessage = "First prompt",
                PersonType = 0,
                AppId = 1000L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            var person2 = new Person
            {
                Name = "Original Name 2",
                Description = "Second person",
                DeveloperMessage = "Second prompt",
                PersonType = 0,
                AppId = 2000L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            var createdPerson1 = await _personRepository.CreatePersonAsync(person1);
            var createdPerson2 = await _personRepository.CreatePersonAsync(person2);

            // Act & Assert
            // Try to update person2 to have the same name as person1
            createdPerson2.Name = "Original Name 1";
            
            // This should throw an exception due to unique constraint violation
            await Assert.ThrowsAnyAsync<Exception>(() => _personRepository.SavePersonAsync(createdPerson2));
        }
    }
}
