namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Service interface for person management operations
    /// </summary>
    public interface IPeopleService
    {
        #region Events

        /// <summary>
        /// Event raised when a person is created, updated, or deleted
        /// </summary>
        event EventHandler<PersonOperationEventArgs>? PersonChanged;

        #endregion

        #region Person CRUD Operations

        /// <summary>
        /// Gets persons by name filter
        /// </summary>
        /// <param name="name">The name filter (null for all persons)</param>
        /// <returns>List of persons matching the criteria</returns>
        Task<List<IPerson>> GetPersonsAsync(string? name);


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
        Task<IPerson> CreatePersonAsync(string name, string description, 
        byte[]? image = null, bool isEnabled = true, string? providerName = null, 
        string? modelId = null, string? developerMessage = null, string[]? toolNames = null, 
        Dictionary<string, string>? parameters = null, double? presencePenalty = null, 
        double? frequencyPenalty = null, double? topP = null, int? topK = null, 
        double? temperature = null);

        /// <summary>
        /// Updates an existing person in the database
        /// </summary>
        /// <param name="person">The person to update</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdatePersonAsync(IPerson person);

        /// <summary>
        /// Creates or updates a person based on whether it has an ID
        /// </summary>
        /// <param name="person">The person to save</param>
        /// <returns>The saved person, or null if there was an error</returns>
        Task<IPerson?> SavePersonAsync(IPerson person);

        /// <summary>
        /// Gets a specific person by name
        /// </summary>
        /// <param name="name">The name of the person to retrieve</param>
        /// <returns>The person if found, null otherwise</returns>
        Task<IPerson?> GetPersonAsync(string name);

        /// <summary>
        /// Gets all people from the database
        /// </summary>
        /// <returns>List of all people</returns>
        Task<List<IPerson>> GetAllPeopleAsync();

        /// <summary>
        /// Gets all enabled people from the database
        /// </summary>
        /// <returns>List of enabled people</returns>
        Task<List<IPerson>> GetEnabledPersonsAsync();

        /// <summary>
        /// Deletes a person from the database by ID
        /// </summary>
        /// <param name="id">The ID of the person to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DeletePersonAsync(long id);

        #endregion
    }
}