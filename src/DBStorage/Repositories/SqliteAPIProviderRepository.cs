using System;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// SQLite implementation of API provider repository
    /// </summary>
    public partial class SqliteAPIProviderRepository : SqliteBaseRepository, IAPIProviderRepository
    {
        /// <summary>
        /// Initialize a new instance of SqliteAPIProviderRepository
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public SqliteAPIProviderRepository(string databasePath) : base(databasePath)
        {
            Initialize();
        }

        // Implemented in partial classes:
        // - SqliteLlmProviderRepository.Initialization.cs
        // - SqliteLlmProviderRepository.Read.cs
        // - SqliteLlmProviderRepository.Write.cs
    }
} 