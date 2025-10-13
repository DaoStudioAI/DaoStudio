using DaoStudio.Interfaces;
using System;
using System.Collections.Generic;

namespace DaoStudio
{
    /// <summary>
    /// ApiProvider wrapper that extends DBStorage APIProvider and implements IApiProvider
    /// </summary>
    internal class ApiProvider : DBStorage.Models.APIProvider, IApiProvider
    {

        // Implement ProviderType interface property with proper type conversion
        ProviderType IApiProvider.ProviderType
        {
            get => (ProviderType)(int)base.ProviderType;
            set => base.ProviderType = (int)value;
        }

        /// <summary>
        /// Creates a new DaoStudio.Models.ApiProvider from a DBStorage.Models.APIProvider
        /// </summary>
        /// <param name="dbApiProvider">The DBStorage APIProvider to convert</param>
        /// <returns>A new DaoStudio.Models.ApiProvider instance</returns>
        public static ApiProvider FromDBApiProvider(DBStorage.Models.APIProvider dbApiProvider)
        {
            if (dbApiProvider == null)
                throw new ArgumentNullException(nameof(dbApiProvider));

            var apiProvider = new ApiProvider
            {
                Id = dbApiProvider.Id,
                Name = dbApiProvider.Name,
                ApiEndpoint = dbApiProvider.ApiEndpoint,
                ApiKey = dbApiProvider.ApiKey,
                Parameters = new Dictionary<string, string>(dbApiProvider.Parameters),
                IsEnabled = dbApiProvider.IsEnabled,
                LastModified = dbApiProvider.LastModified,
                CreatedAt = dbApiProvider.CreatedAt
            };

            // Set the ProviderType through the base class property
            apiProvider.ProviderType = dbApiProvider.ProviderType;

            return apiProvider;
        }

        /// <summary>
        /// Converts to DBStorage.Models.APIProvider
        /// </summary>
        /// <returns>A new DBStorage.Models.APIProvider instance</returns>
        public DBStorage.Models.APIProvider ToDBApiProvider()
        {
            return new DBStorage.Models.APIProvider
            {
                Id = this.Id,
                Name = this.Name,
                ApiEndpoint = this.ApiEndpoint,
                ApiKey = this.ApiKey,
                Parameters = new Dictionary<string, string>(this.Parameters),
                IsEnabled = this.IsEnabled,
                LastModified = this.LastModified,
                CreatedAt = this.CreatedAt,
                ProviderType = this.ProviderType // Use base class property
            };
        }
    }
}