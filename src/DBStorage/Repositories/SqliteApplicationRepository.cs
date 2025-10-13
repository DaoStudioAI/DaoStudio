using System;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// SQLite implementation of application repository
    /// </summary>
    public partial class SqliteApplicationRepository : SqliteBaseRepository, IApplicationRepository
    {
        /// <summary>
        /// Initialize a new instance of SqliteApplicationRepository
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public SqliteApplicationRepository(string databasePath) : base(databasePath)
        {
            Initialize();
        }

        // Implemented in partial classes:
        // - SqliteApplicationRepository.Initialization.cs
        // - SqliteApplicationRepository.Read.cs
        // - SqliteApplicationRepository.Write.cs
    }
}
