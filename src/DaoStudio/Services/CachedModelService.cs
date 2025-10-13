using Anthropic.SDK;
using DaoStudio.Common.Plugins;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using DaoStudio.Interfaces;
using GenerativeAI;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OpenAI;
using System.ClientModel;
using System.IO;

namespace DaoStudio.Services
{
    /// <summary>
    /// Service implementation for cached model operations
    /// </summary>
    public class CachedModelService : ICachedModelService
    {
        private readonly IAPIProviderRepository apiProviderRepository;
        private readonly ICachedModelRepository cachedModelRepository;
        private readonly ILogger<CachedModelService> logger;

        public CachedModelService(IAPIProviderRepository apiProviderRepository,
            ICachedModelRepository cachedModelRepository, ILogger<CachedModelService> logger)
        {
            this.apiProviderRepository = apiProviderRepository ?? throw new ArgumentNullException(nameof(apiProviderRepository));
            this.cachedModelRepository = cachedModelRepository ?? throw new ArgumentNullException(nameof(cachedModelRepository));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<string>> GetLlmModelList(APIProvider provider)
        {
            // Only test HTTP connection for providers that use HTTP endpoints
            if (provider.ProviderType != (int)ProviderType.LLama && provider.ProviderType != (int)ProviderType.AWSBedrock)
            {
                bool isConnected = await TestHttpConnectionAsync(provider.ApiEndpoint);
                if (!isConnected && provider.ProviderType != (int)ProviderType.Ollama)
                {
                    throw new HttpRequestException($"Unable to connect to the API endpoint: {provider.ApiEndpoint}");
                }
            }

            switch (provider.ProviderType)
            {
                case (int)ProviderType.OpenAI:
                    return await GetOpenAIModelList(provider);
                case (int)ProviderType.OpenRouter:
                    return await GetOpenAIModelList(provider);
                case (int)ProviderType.Google:
                    return await GetGoogleModelList(provider);
                case (int)ProviderType.Anthropic:
                    return await GetAnthropicModelList(provider);
                case (int)ProviderType.Ollama:
                    return await GetOllamaModelList(provider);
                case (int)ProviderType.LLama:
                    return await GetLLamaModelList(provider);
                case (int)ProviderType.AWSBedrock:
                    return await GetAWSBedrockModelList(provider);
                default:
                    throw new NotImplementedException($"Unknown ProviderType {provider.ProviderType}");
            }
        }

        /// <summary>
        /// Refreshes the cached models from all providers in the background
        /// </summary>
        /// <param name="providers">List of providers to refresh models from</param>
        public async Task RefreshCachedModelsAsync()
        {
            try
            {
                var providers = await apiProviderRepository.GetAllProvidersAsync();

                // Only refresh if we have providers
                if (providers == null || !providers.Any())
                {
                    return;
                }

                // Process each enabled provider
                await Parallel.ForEachAsync(providers.Where(p => p.IsEnabled), new ParallelOptions { MaxDegreeOfParallelism = 3 },
                    async (provider, cts) =>
                    {
                        try
                        {
                            // Convert to APIProvider
                            var apiProvider = (APIProvider)provider;

                            // Get models from the provider
                            var models = await GetLlmModelList(apiProvider);

                            // Save to cache
                            await SaveModelsToCacheAsync(provider, models);
                        }
                        catch (Exception providerEx)
                        {
                            // Log error but continue with other providers
                            logger.LogError(providerEx, $"Error refreshing models from provider {provider.Name}");
                        }
                    });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error refreshing cached models");
            }
        }

        #region ICachedModelService Implementation

        /// <summary>
        /// Gets all cached models
        /// </summary>
        /// <returns>Collection of cached models</returns>
        public async Task<IEnumerable<ICachedModel>> GetAllCachedModelsAsync()
        {
            try
            {
                var dbModels = await cachedModelRepository.GetAllModelsAsync();
                return dbModels.Select(m => CachedModel.FromDBCachedModel(m));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting all cached models");
                throw;
            }
        }

        /// <summary>
        /// Gets a cached model by ID
        /// </summary>
        /// <param name="modelId">Model ID</param>
        /// <returns>The cached model or null if not found</returns>
        public async Task<ICachedModel?> GetCachedModelByIdAsync(long modelId)
        {
            try
            {
                var dbModel = await cachedModelRepository.GetModelAsync(modelId);
                return dbModel != null ? CachedModel.FromDBCachedModel(dbModel) : null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting cached model by ID: {ModelId}", modelId);
                throw;
            }
        }

        /// <summary>
        /// Saves models to the cached model repository for a specific provider
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <param name="providerType">The provider type</param>
        /// <param name="models">List of model names to cache</param>
        /// <returns>Task representing the async operation</returns>
        public async Task SaveModelsToCacheAsync(long providerId, Interfaces.ProviderType providerType, List<string> models)
        {
            if (models == null)
                return;

            try
            {
                
                // First delete all existing models for this provider
                await cachedModelRepository.DeleteModelsByProviderIdAsync(providerId);

                // Skip if no models to add
                if (models.Count == 0)
                    return;

                // Create a collection to hold all models for bulk insert
                var modelsToCreate = new List<DBStorage.Models.CachedModel>();

                // Prepare models to add
                foreach (var modelName in models)
                {
                    var cachedModel = new DBStorage.Models.CachedModel
                    {
                        ApiProviderId = providerId,
                        ProviderType = (int)providerType,
                        Catalog = string.Empty, // Default empty catalog
                        Name = modelName,
                        ModelId = modelName,
                    };

                    // For OpenRouter, check if the model follows the "Catalog/ModelName" pattern
                    if (providerType == Interfaces.ProviderType.OpenRouter)
                    {
                        var parts = modelName.Split('/', 2);

                        if (parts.Length == 2)
                        {
                            // Only set catalog and name if it follows the pattern
                            cachedModel.Catalog = parts[0];
                            cachedModel.Name = parts[1];
                        }
                    }

                    modelsToCreate.Add(cachedModel);
                }

                // Use bulk insert instead of individual inserts
                await cachedModelRepository.CreateModelsAsync(modelsToCreate);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error caching models for provider {ProviderId}", providerId);
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

        private async Task<List<string>> GetOpenAIModelList(APIProvider provider)
        {
            try
            {
                string apiKey = string.IsNullOrEmpty(provider.ApiKey) ? "sk-placeholder-key-for-null-api-key" : provider.ApiKey;

                OpenAIClient client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
                {
                    Endpoint = new Uri(provider.ApiEndpoint),
                });
                var mc = client.GetOpenAIModelClient();
                var ms = await mc.GetModelsAsync();
                var dere = ms.GetRawResponse();
                var de = dere.Content.ToString();
                return ms.Value.Select(x => x.Id).ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching OpenAI models");
                return new List<string>();
            }
        }

        private async Task<List<string>> GetGoogleModelList(APIProvider provider)
        {
            try
            {
                string apiKey = provider.ApiKey ?? "placeholder-api-key-for-null";

                var googleAI = new GoogleAi(apiKey);
                var models = await googleAI.ListModelsAsync();

                var modelList = new List<string>();
                if (models?.Models == null)
                {
                    return modelList;
                }
                foreach (var model in models.Models)
                {
                    if (model?.Name?.StartsWith("models/") == true)
                    {
                        modelList.Add(model.Name.Substring("models/".Length));
                    }
                    else if (model?.Name != null)
                    {
                        modelList.Add(model.Name);
                    }
                }

                return modelList;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching Google models");
                return new();
            }
        }

        private async Task<List<string>> GetAnthropicModelList(APIProvider provider)
        {
            try
            {
                if (string.IsNullOrEmpty(provider.ApiKey))
                {
                    logger.LogWarning("No API key provided for Anthropic provider, returning empty model list");
                    return new List<string>();
                }

                var client = new Anthropic.SDK.AnthropicClient(provider.ApiKey);
                var models = await client.Models.ListModelsAsync();
                return models.Models.Select(model => model.Id).ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching Anthropic models");
                return new();
            }
        }

        private async Task<List<string>> GetOllamaModelList(APIProvider provider)
        {
            try
            {
                var ollamaClient = new OllamaSharp.OllamaApiClient(new Uri(provider.ApiEndpoint));
                var models = await ollamaClient.ListLocalModelsAsync();
                return models.Select(m => m.Name).ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching Ollama models from {Endpoint}", provider.ApiEndpoint);
                return new List<string>();
            }
        }

        private async Task<List<string>> GetLLamaModelList(APIProvider provider)
        {
            try
            {
                if (provider.Parameters.TryGetValue("ModelPath", out var modelPath) && !string.IsNullOrEmpty(modelPath))
                {
                    if (Directory.Exists(modelPath))
                    {
                        var modelFiles = Directory.GetFiles(modelPath, "*.gguf", SearchOption.TopDirectoryOnly)
                            .Select(f => Path.GetFileName(f))
                            .ToList();
                        return modelFiles;
                    }
                    else if (File.Exists(modelPath))
                    {
                        return new List<string> { Path.GetFileName(modelPath) };
                    }
                }

                return new List<string>
                {
                    "llama-2-7b-chat.gguf",
                    "llama-2-13b-chat.gguf",
                    "codellama-7b-instruct.gguf",
                    "mistral-7b-instruct.gguf"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching LLama models");
                return new List<string>();
            }
        }

        private async Task<List<string>> GetAWSBedrockModelList(APIProvider provider)
        {
            try
            {
                return new List<string>
                {
                    "anthropic.claude-3-5-sonnet-20241022-v2:0",
                    "anthropic.claude-3-5-sonnet-20240620-v1:0",
                    "anthropic.claude-3-5-haiku-20241022-v1:0",
                    "anthropic.claude-3-opus-20240229-v1:0",
                    "anthropic.claude-3-sonnet-20240229-v1:0",
                    "anthropic.claude-3-haiku-20240307-v1:0",
                    "anthropic.claude-v2:1",
                    "anthropic.claude-v2",
                    "anthropic.claude-instant-v1",
                    "amazon.titan-text-lite-v1",
                    "amazon.titan-text-express-v1",
                    "amazon.titan-embed-text-v1",
                    "meta.llama2-70b-chat-v1",
                    "meta.llama2-13b-chat-v1",
                    "cohere.command-text-v14",
                    "cohere.command-light-text-v14",
                    "cohere.embed-english-v3",
                    "ai21.j2-ultra-v1",
                    "ai21.j2-mid-v1"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching AWS Bedrock models");
                return new List<string> { "anthropic.claude-3-5-sonnet-20241022-v2:0" };
            }
        }

        /// <summary>
        /// Saves models to the cached model repository for any provider type,
        /// with special handling for OpenRouter models which use Catalog/ModelName format
        /// </summary>
        /// <param name="provider">The provider associated with these models</param>
        /// <param name="models">List of model names to cache</param>
        private async Task SaveModelsToCacheAsync(APIProvider provider, List<string> models)
        {
            if (models == null)
                return;

            try
            {
                
                // First delete all existing models for this provider
                await cachedModelRepository.DeleteModelsByProviderIdAsync(provider.Id);

                // Skip if no models to add
                if (models.Count == 0)
                    return;

                // Create a collection to hold all models for bulk insert
                var modelsToCreate = new List<CachedModel>();

                // Prepare models to add
                foreach (var modelName in models)
                {
                    var cachedModel = new CachedModel
                    {
                        ApiProviderId = provider.Id,
                        ProviderType = provider.ProviderType,
                        Catalog = string.Empty, // Default empty catalog
                        Name = modelName,
                        ModelId = modelName,
                    };

                    // For OpenRouter, check if the model follows the "Catalog/ModelName" pattern
                    if (provider.ProviderType == (int)ProviderType.OpenRouter)
                    {
                        var parts = modelName.Split('/', 2);

                        if (parts.Length == 2)
                        {
                            // Only set catalog and name if it follows the pattern
                            cachedModel.Catalog = parts[0];
                            cachedModel.Name = parts[1];
                        }
                    }

                    modelsToCreate.Add(cachedModel);
                }

                // Use bulk insert instead of individual inserts
                await cachedModelRepository.CreateModelsAsync(modelsToCreate);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error caching models for {provider.Name} provider");
            }
        }

        #endregion
    }
}