using System;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// Implementation of ISessionRepository using SQLite
    /// </summary>
    public partial class SqliteSessionRepository : SqliteBaseRepository, ISessionRepository
    {
        /// <summary>
        /// Constructor with database path
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public SqliteSessionRepository(string databasePath) : base(databasePath)
        {
            Initialize();
        }
    }
} 