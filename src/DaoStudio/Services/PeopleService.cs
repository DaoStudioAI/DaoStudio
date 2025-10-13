using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.DBStorage.Interfaces;

namespace DaoStudio.Services
{
    /// <summary>
    /// Service implementation for person management operations
    /// </summary>
    public class PeopleService : IPeopleService
    {
        private readonly IPersonRepository personRepository;
        private readonly ILogger<PeopleService> logger;

        public PeopleService(IPersonRepository personRepository, ILogger<PeopleService> logger)
        {
            this.personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Events

        /// <summary>
        /// Event raised when a person is created, updated, or deleted
        /// </summary>
        public event EventHandler<PersonOperationEventArgs>? PersonChanged;

        #endregion

        #region Person CRUD Operations

        /// <summary>
        /// Gets persons by name filter
        /// </summary>
        /// <param name="name">The name filter (null for all persons)</param>
        /// <returns>List of persons matching the criteria</returns>
        public async Task<List<IPerson>> GetPersonsAsync(string? name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                // Get a specific person by name using the existing GetPersonAsync function
                var person = await GetPersonAsync(name);
                if (person == null)
                {
                    throw new Exception($"Person with name '{name}' not found");
                }
                return new List<IPerson> { person };
            }
            else
            {
                // Get all persons using the existing GetAllPeopleAsync function
                var persons = await GetAllPeopleAsync();
                if (!persons.Any())
                {
                    return new List<IPerson>();
                }

                // Convert each Person to PersonWrapper and return as List<IPerson>
                return persons.Cast<IPerson>().ToList();
            }
        }

        /// <summary>
        /// Creates a new person from parameters
        /// </summary>
        /// <param name="name">Person name</param>
        /// <param name="description">Person description</param>
        /// <param name="image">Person image</param>
        /// <param name="isEnabled">Whether the person is enabled</param>
        /// <param name="providerName">Provider name</param>
        /// <param name="modelId">Model ID</param>
        /// <param name="developerMessage">Developer message</param>
        /// <param name="toolNames">Tool names</param>
        /// <param name="parameters">Additional parameters</param>
        /// <param name="presencePenalty">Presence penalty parameter</param>
        /// <param name="frequencyPenalty">Frequency penalty parameter</param>
        /// <param name="topP">Top-p sampling parameter</param>
        /// <param name="topK">Top-k sampling parameter</param>
        /// <param name="temperature">Temperature parameter</param>
        /// <returns>The created person</returns>
        public async Task<IPerson> CreatePersonAsync(string name, string description, byte[]? image = null, bool isEnabled = true,

            string? providerName = null, string? modelId = null, string? developerMessage = null,
            string[]? toolNames = null, Dictionary<string, string>? parameters = null,
            double? presencePenalty = null, double? frequencyPenalty = null, double? topP = null, 
            int? topK = null, double? temperature = null)
        {
            try
            {
                // Validate required arguments
                if (name == null) throw new ArgumentNullException(nameof(name));
                if (description == null) throw new ArgumentNullException(nameof(description));

                var dbPerson = new DBStorage.Models.Person
                {
                    Name = name,
                    Description = description,
                    Image = image,
                    IsEnabled = isEnabled,
                    ProviderName = providerName ?? string.Empty,
                    ModelId = modelId ?? string.Empty,
                    DeveloperMessage = developerMessage ?? string.Empty,
                    ToolNames = toolNames ?? Array.Empty<string>(),
                    Parameters = parameters ?? new Dictionary<string, string>(),
                    PresencePenalty = presencePenalty,
                    FrequencyPenalty = frequencyPenalty,
                    TopP = topP,
                    TopK = topK,
                    Temperature = temperature,
                    LastModified = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                var createdPerson = await personRepository.CreatePersonAsync(dbPerson);
                var newPerson = Person.FromDBPerson(createdPerson);

                PersonChanged?.Invoke(this, new PersonOperationEventArgs(PersonOperationType.Created, newPerson));
                return newPerson;
            }
            catch (InvalidOperationException)
            {
                // Preserve specific invalid operation scenarios (e.g., duplicate names)
                throw; // Let callers handle
            }
            catch (ArgumentException)
            {
                // Bubble up argument issues directly
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating person from parameters");
                throw new Exception("Failed to create person", ex);
            }
        }


        /// <summary>
        /// Updates an existing person in the database
        /// </summary>
        /// <param name="person">The person to update</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> UpdatePersonAsync(IPerson person)
        {
            if (person == null)
                throw new ArgumentNullException(nameof(person));
            if (string.IsNullOrWhiteSpace(person.Name))
                throw new ArgumentNullException(nameof(person));
            try
            {

                // Convert interface to DB model
                DBStorage.Models.Person dbPerson;
                if (person is Person DaoStudioPerson)
                {
                    dbPerson = new DBStorage.Models.Person
                    {
                        Id = DaoStudioPerson.Id,
                        Name = DaoStudioPerson.Name,
                        Description = DaoStudioPerson.Description,
                        Image = DaoStudioPerson.Image,
                        IsEnabled = DaoStudioPerson.IsEnabled,
                        ProviderName = DaoStudioPerson.ProviderName,
                        ModelId = DaoStudioPerson.ModelId,
                        DeveloperMessage = DaoStudioPerson.DeveloperMessage,
                        ToolNames = DaoStudioPerson.ToolNames,
                        Parameters = DaoStudioPerson.Parameters,
                        PresencePenalty = DaoStudioPerson.PresencePenalty,
                        FrequencyPenalty = DaoStudioPerson.FrequencyPenalty,
                        TopP = DaoStudioPerson.TopP,
                        TopK = DaoStudioPerson.TopK,
                        Temperature = DaoStudioPerson.Temperature,
                        LastModified = DateTime.UtcNow,
                        CreatedAt = DaoStudioPerson.CreatedAt
                    };
                }
                else
                {
                    dbPerson = new DBStorage.Models.Person
                    {
                        Id = person.Id,
                        Name = person.Name,
                        Description = person.Description,
                        PresencePenalty = person.PresencePenalty,
                        FrequencyPenalty = person.FrequencyPenalty,
                        TopP = person.TopP,
                        TopK = person.TopK,
                        Temperature = person.Temperature,
                        LastModified = DateTime.UtcNow
                    };
                }

                await personRepository.SavePersonAsync(dbPerson);
                PersonChanged?.Invoke(this, new PersonOperationEventArgs(PersonOperationType.Updated, person));
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating person");
                return false;
            }
        }

        /// <summary>
        /// Creates or updates a person based on whether it has an ID
        /// </summary>
        /// <param name="person">The person to save</param>
        /// <returns>The saved person, or null if there was an error</returns>
        public async Task<IPerson?> SavePersonAsync(IPerson person)
        {
            if (person.Id == 0)
            {
                throw new ArgumentException("SavePersonAsync couldn't be used to create a person");
            }
            else
            {
                // Update existing person
                var success = await UpdatePersonAsync(person);
                return success ? person : null;
            }
        }

        /// <summary>
        /// Gets a specific person by name
        /// </summary>
        /// <param name="name">The name of the person to retrieve</param>
        /// <returns>The person if found, null otherwise</returns>
        public async Task<IPerson?> GetPersonAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name cannot be null or empty", nameof(name));
            }

            try
            {
                var person = await personRepository.GetPersonByNameAsync(name);
                return person != null ? Person.FromDBPerson(person) : null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting person with name '{Name}'", name);
                return null;
            }
        }

        /// <summary>
        /// Gets all people from the database
        /// </summary>
        /// <returns>List of all people</returns>
        public async Task<List<IPerson>> GetAllPeopleAsync()
        {
            try
            {
                var persons = await personRepository.GetAllPersonsAsync();
                return persons?.Select(p => Person.FromDBPerson(p) as IPerson).ToList() ?? new List<IPerson>();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting all people");
                throw;
            }
        }

        /// <summary>
        /// Gets all enabled people from the database
        /// </summary>
        /// <returns>List of enabled people</returns>
        public async Task<List<IPerson>> GetEnabledPersonsAsync()
        {
            try
            {
                var persons = await personRepository.GetAllPersonsAsync();

                // Filter for enabled persons using the IsEnabled property
                var enabledPersons = persons?.Where(p => p.IsEnabled == true)
                    .Select(p => Person.FromDBPerson(p) as IPerson)
                    .ToList() ?? new List<IPerson>();
                return enabledPersons;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting enabled people");
                return new List<IPerson>();
            }
        }

        /// <summary>
        /// Deletes a person from the database by ID
        /// </summary>
        /// <param name="id">The ID of the person to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> DeletePersonAsync(long id)
        {
            try
            {
                var deleted = await personRepository.DeletePersonAsync(id);

                if (deleted)
                {
                    // Raise the PersonChanged event only when deletion succeeds
                    PersonChanged?.Invoke(this, new PersonOperationEventArgs(id));
                }

                return deleted;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting person with ID {Id}", id);
                return false;
            }
        }

        #endregion
    }
}