using System;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// Implementation of ILlmToolRepository using SQLite
    /// </summary>
    public partial class SqliteLlmToolRepository : SqliteBaseRepository, ILlmToolRepository
    {
        /// <summary>
        /// Constructor with database path
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public SqliteLlmToolRepository(string databasePath) : base(databasePath)
        {
            Initialize();
        }
    }
} 