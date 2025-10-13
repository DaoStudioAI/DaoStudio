using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaoStudio.Interfaces;
using DaoStudio.DBStorage.Interfaces;

namespace DaoStudio.Services
{
    /// <summary>
    /// Service implementation for API provider operations
    /// </summary>
    public class ApiProviderService : IApiProviderService
    {
        private readonly IAPIProviderRepository repository;
        private readonly ILogger<ApiProviderService> logger;

        public ApiProviderService(IAPIProviderRepository repository, ILogger<ApiProviderService> logger)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Events

        /// <summary>
        /// Event raised when a provider is created, updated, or deleted
        /// </summary>
        public event EventHandler<ProviderOperationEventArgs>? ProviderChanged;

        #endregion

        public List<(string name, IApiProvider template)> GetSupportProviders()
        {
            var dbTemplates = new List<(string name, DBStorage.Models.APIProvider template)>
            {
                ("OpenAI", new DBStorage.Models.APIProvider
                {
                    Name = "OpenAI",
                    ProviderType = (int)ProviderType.OpenAI,
                    ApiEndpoint = "https://api.openai.com/v1",
                    ApiKey = null,
                    Parameters = new Dictionary<string, string>()
                }),
                ("OpenRouter", new DBStorage.Models.APIProvider
                {
                    Name = "OpenRouter",
                    ProviderType = (int)ProviderType.OpenRouter,
                    ApiEndpoint = "https://openrouter.ai/api/v1",
                    ApiKey = null,
                    Parameters = new Dictionary<string, string>()
                }),
                ("Anthropic", new DBStorage.Models.APIProvider
                {
                    Name = "Anthropic",
                    ProviderType = (int)ProviderType.Anthropic,
                    ApiEndpoint = "https://api.anthropic.com",
                    ApiKey = null,
                    Parameters = new Dictionary<string, string>
                    {
                        ["MaxTokens"] = "4096",
                    }
                }),
                ("Google", new DBStorage.Models.APIProvider
                {
                    Name = "Google",
                    ProviderType = (int)ProviderType.Google,
                    ApiEndpoint = "",
                    ApiKey = null,
                    Parameters = new Dictionary<string, string>()
                }),
                ("Ollama", new DBStorage.Models.APIProvider
                {
                    Name = "Ollama",
                    ProviderType = (int)ProviderType.Ollama,
                    ApiEndpoint = "http://localhost:11434",
                    ApiKey = null,
                    Parameters = new Dictionary<string, string>()
                }),
            };

            return dbTemplates.Select(t => (t.name, (IApiProvider)ApiProvider.FromDBApiProvider(t.template))).ToList();
        }

        #region IApiProviderService Implementation

        /// <summary>
        /// Gets all API providers
        /// </summary>
        /// <returns>Collection of API providers</returns>
        public async Task<IEnumerable<IApiProvider>> GetAllApiProvidersAsync()
        {
            try
            {
                var dbProviders = await repository.GetAllProvidersAsync();
                return dbProviders.Select(p => ApiProvider.FromDBApiProvider(p));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting all API providers");
                throw;
            }
        }

        /// <summary>
        /// Gets an API provider by ID
        /// </summary>
        /// <param name="providerId">Provider ID</param>
        /// <returns>The API provider or null if not found</returns>
        public async Task<IApiProvider?> GetApiProviderByIdAsync(long providerId)
        {
            try
            {
                var dbProvider = await repository.GetProviderAsync(providerId);
                return dbProvider != null ? ApiProvider.FromDBApiProvider(dbProvider) : null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting API provider by ID: {ProviderId}", providerId);
                throw;
            }
        }

        /// <summary>
        /// Creates a new API provider from parameters
        /// </summary>
        /// <param name="name">Provider name</param>
        /// <param name="providerType">Provider type</param>
        /// <param name="apiEndpoint">API endpoint</param>
        /// <param name="apiKey">API key</param>
        /// <param name="parameters">Additional parameters</param>
        /// <param name="isEnabled">Whether the provider is enabled</param>
        /// <returns>The created provider</returns>
        public async Task<IApiProvider> CreateApiProviderAsync(string name, ProviderType providerType, string apiEndpoint, string? apiKey = null, Dictionary<string, string>? parameters = null, bool isEnabled = true)
        {
            try
            {
                var dbProvider = new DBStorage.Models.APIProvider
                {
                    Name = name,
                    ApiEndpoint = apiEndpoint,
                    ApiKey = apiKey,
                    Parameters = parameters ?? new Dictionary<string, string>(),
                    IsEnabled = isEnabled,
                    LastModified = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ProviderType = (int)providerType
                };

                var createdProvider = await repository.CreateProviderAsync(dbProvider);
                var newProvider = ApiProvider.FromDBApiProvider(createdProvider);

                ProviderChanged?.Invoke(this, new ProviderOperationEventArgs(ProviderOperationType.Created, newProvider));
                return newProvider;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating API provider from parameters");
                throw;
            }
        }


        /// <summary>
        /// Updates an existing API provider
        /// </summary>
        /// <param name="provider">Provider to update</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> UpdateApiProviderAsync(IApiProvider provider)
        {
            try
            {
                if (provider == null)
                    throw new ArgumentNullException(nameof(provider));

                // Convert interface to DB model
                DBStorage.Models.APIProvider dbProvider;
                if (provider is ApiProvider DaoStudioProvider)
                {
                    dbProvider = DaoStudioProvider.ToDBApiProvider();
                }
                else
                {
                    dbProvider = new DBStorage.Models.APIProvider
                    {
                        Id = provider.Id,
                        Name = provider.Name,
                        ApiEndpoint = provider.ApiEndpoint,
                        ApiKey = provider.ApiKey,
                        Parameters = new Dictionary<string, string>(provider.Parameters),
                        IsEnabled = provider.IsEnabled,
                        LastModified = DateTime.UtcNow,
                        CreatedAt = provider.CreatedAt,
                        ProviderType = (int)provider.ProviderType
                    };
                }

                var result = await repository.SaveProviderAsync(dbProvider);
                if (result)
                {
                    ProviderChanged?.Invoke(this, new ProviderOperationEventArgs(ProviderOperationType.Updated, provider));
                }
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating API provider: {ProviderId}", provider?.Id);
                return false;
            }
        }

        /// <summary>
        /// Deletes an API provider
        /// </summary>
        /// <param name="providerId">Provider ID to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> DeleteApiProviderAsync(long providerId)
        {
            try
            {
                var result = await repository.DeleteProviderAsync(providerId);
                if (result)
                {
                    ProviderChanged?.Invoke(this, new ProviderOperationEventArgs(providerId));
                }
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting API provider: {ProviderId}", providerId);
                return false;
            }
        }

        /// <summary>
        /// Gets API providers by type
        /// </summary>
        /// <param name="providerType">Provider type to filter by</param>
        /// <returns>Collection of API providers of the specified type</returns>
        public async Task<IEnumerable<IApiProvider>> GetApiProvidersByTypeAsync(ProviderType providerType)
        {
            try
            {
                var allProviders = await repository.GetAllProvidersAsync();
                var filteredProviders = allProviders.Where(p => (int)p.ProviderType == (int)providerType);
                return filteredProviders.Select(p => ApiProvider.FromDBApiProvider(p));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting API providers by type: {ProviderType}", providerType);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<bool> TestHttpConnectionAsync(string endpointUrl)
        {
            if (string.IsNullOrEmpty(endpointUrl) || !Uri.TryCreate(endpointUrl, UriKind.Absolute, out _))
            {
                return false;
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, endpointUrl);
                    client.Timeout = TimeSpan.FromSeconds(5);
                    HttpResponseMessage response = await client.SendAsync(request);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}