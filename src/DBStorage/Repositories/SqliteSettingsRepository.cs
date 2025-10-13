using System;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// Implementation of ISettingsRepository using SQLite
    /// </summary>
    public partial class SqliteSettingsRepository : SqliteBaseRepository, ISettingsRepository
    {
        /// <summary>
        /// Constructor with database path
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public SqliteSettingsRepository(string databasePath) : base(databasePath)
        {
            Initialize();
        }
    }
} 