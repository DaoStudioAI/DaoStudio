using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace DaoStudio.DBStorage.Migrations
{
    /// <summary>
    /// Migration to add an index to the Settings table
    /// </summary>
    //public class Migration_001_AddSettingsIndex : BaseMigration
    //{
    //    /// <inheritdoc />
    //    public override int TargetVersion => 1;
        
    //    /// <inheritdoc />
    //    public override string Description => "Adds an index to the Settings table for faster querying";
        
    //    /// <inheritdoc />
    //    public override async Task<bool> ApplyAsync(SqliteConnection connection)
    //    {
    //        // Create an index on the ApplicationName column
    //        string sql = @"
    //            CREATE INDEX IF NOT EXISTS idx_settings_app_name 
    //            ON Settings(ApplicationName);
    //        ";
            
    //        return await ExecuteSqlAsync(connection, sql);
    //    }
    //}
} 