using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Repositories
{
    /// <summary>
    /// SQLite implementation of the message repository
    /// </summary>
    public partial class SqliteMessageRepository : SqliteBaseRepository, IMessageRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteMessageRepository"/> class.
        /// </summary>
        /// <param name="databasePath">Path to the SQLite database file</param>
        public SqliteMessageRepository(string databasePath) : base(databasePath)
        {
            Initialize();
        }
    }
} 