using System;
using DaoStudio.Interfaces;

namespace DaoStudio
{
    /// <summary>
    /// CachedModel wrapper that extends DBStorage CachedModel and implements ICachedModel
    /// </summary>
    internal class CachedModel : DBStorage.Models.CachedModel, ICachedModel
    {
        // Implement ProviderType interface property with proper type conversion
        ProviderType ICachedModel.ProviderType
        {
            get => (ProviderType)(int)base.ProviderType;
            set => base.ProviderType = (int)value;
        }

        /// <summary>
        /// Creates a new DaoStudio.Models.CachedModel from a DBStorage.Models.CachedModel
        /// </summary>
        /// <param name="dbCachedModel">The DBStorage CachedModel to convert</param>
        /// <returns>A new DaoStudio.Models.CachedModel instance</returns>
        public static CachedModel FromDBCachedModel(DBStorage.Models.CachedModel dbCachedModel)
        {
            if (dbCachedModel == null)
                throw new ArgumentNullException(nameof(dbCachedModel));

            var cachedModel = new CachedModel
            {
                Id = dbCachedModel.Id,
                ApiProviderId = dbCachedModel.ApiProviderId,
                Name = dbCachedModel.Name,
                ModelId = dbCachedModel.ModelId,
                Catalog = dbCachedModel.Catalog
            };

            // Set the ProviderType through the base class property
            cachedModel.ProviderType = dbCachedModel.ProviderType;

            return cachedModel;
        }

        /// <summary>
        /// Converts to DBStorage.Models.CachedModel
        /// </summary>
        /// <returns>A new DBStorage.Models.CachedModel instance</returns>
        public DBStorage.Models.CachedModel ToDBCachedModel()
        {
            return new DBStorage.Models.CachedModel
            {
                Id = this.Id,
                ApiProviderId = this.ApiProviderId,
                Name = this.Name,
                ModelId = this.ModelId,
                ProviderType = this.ProviderType, // Use base class property
                Catalog = this.Catalog
            };
        }
    }
}