using System;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// Implementation of ICachedModelRepository using SQLite
    /// </summary>
    public partial class SqliteCachedModelRepository : SqliteBaseRepository, ICachedModelRepository
    {
        /// <summary>
        /// Constructor with database path
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public SqliteCachedModelRepository(string databasePath) : base(databasePath)
        {
            Initialize();
        }
    }
} 