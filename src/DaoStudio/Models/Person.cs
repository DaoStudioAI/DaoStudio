using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;

namespace DaoStudio
{
    /// <summary>
    /// Person class that extends the DBStorage Person model and implements the IPerson interface
    /// </summary>
    internal class Person : DBStorage.Models.Person, IPerson
    {
        // IPerson interface properties are already implemented by the base class
        // Name and Description properties are inherited from DBStorage.Models.Person
        
        // The base class already has all the necessary properties:
        // - string Name { get; set; }
        // - string Description { get; set; }
        // These satisfy the IPerson interface requirements

        /// <summary>
        /// Creates a new DaoStudio.Person from a DBStorage.Models.Person
        /// </summary>
        /// <param name="dbPerson">The DBStorage Person to convert</param>
        /// <returns>A new DaoStudio.Person instance</returns>
        public static Person FromDBPerson(DBStorage.Models.Person dbPerson)
        {
            if (dbPerson == null)
                throw new ArgumentNullException(nameof(dbPerson));

            var person = new Person
            {
                Id = dbPerson.Id,
                Name = dbPerson.Name,
                Description = dbPerson.Description,
                Image = dbPerson.Image,
                IsEnabled = dbPerson.IsEnabled,
                ProviderName = dbPerson.ProviderName,
                ModelId = dbPerson.ModelId,
                PresencePenalty = dbPerson.PresencePenalty,
                FrequencyPenalty = dbPerson.FrequencyPenalty,
                TopP = dbPerson.TopP,
                TopK = dbPerson.TopK,
                Temperature = dbPerson.Temperature,
                Capability1 = dbPerson.Capability1,
                Capability2 = dbPerson.Capability2,
                Capability3 = dbPerson.Capability3,
                DeveloperMessage = dbPerson.DeveloperMessage,
                ToolNames = dbPerson.ToolNames,
                Parameters = dbPerson.Parameters,
                LastModified = dbPerson.LastModified,
                CreatedAt = dbPerson.CreatedAt
            };

            return person;
        }

        // Explicit interface implementation to handle enum type mismatch between
        // DaoStudio.Common.Plugins.PersonType and DaoStudio.DBStorage.Models.PersonType
        PersonType IPerson.PersonType
        {
            get => (PersonType)(int)base.PersonType;
            set => base.PersonType = (int)value;
        }
    }
}