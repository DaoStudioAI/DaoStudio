using System;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// Implementation of ILlmPromptRepository using SQLite
    /// </summary>
    public partial class SqliteLlmPromptRepository : SqliteBaseRepository, ILlmPromptRepository
    {
        /// <summary>
        /// Constructor with database path
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public SqliteLlmPromptRepository(string databasePath) : base(databasePath)
        {
            Initialize();
        }
    }
} 