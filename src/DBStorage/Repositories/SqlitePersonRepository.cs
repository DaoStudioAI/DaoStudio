using System;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// Implementation of IPersonRepository using SQLite
    /// </summary>
    public partial class SqlitePersonRepository : SqliteBaseRepository, IPersonRepository
    {
        /// <summary>
        /// Constructor with database path
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public SqlitePersonRepository(string databasePath) : base(databasePath)
        {
            Initialize();
        }
    }
} 