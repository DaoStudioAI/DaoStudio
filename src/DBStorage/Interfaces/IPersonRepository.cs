using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Interfaces
{

    public interface IPersonRepository
    {

        Task<Person?> GetPersonAsync(long id);


        Task<Person> CreatePersonAsync(Person person);


        Task<bool> SavePersonAsync(Person person);


        Task<bool> DeletePersonAsync(long id);


        Task<IEnumerable<Person>> GetAllPersonsAsync(bool includeImage = true);



        Task<IEnumerable<Person>> GetPersonsByProviderNameAsync(string providerName); // Renamed method and changed parameter type


        Task<IEnumerable<Person>> GetEnabledPersonsAsync(bool includeImage = true);
        

        bool PersonNameExists(string name, long excludeId = 0);


        Task<Person?> GetPersonByNameAsync(string name);


        Task<IEnumerable<Person>> GetPersonsByNamesAsync(IEnumerable<string> names, bool includeImage = true);
    }
} 