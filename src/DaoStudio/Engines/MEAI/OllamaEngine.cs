
using DaoStudio.Interfaces;
using DaoStudio.Common;
using DaoStudio.Properties;
using DaoStudio.DBStorage.Factory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using OllamaSharp;

namespace DaoStudio.Engines.MEAI
{
    /// <summary>
    /// Engine implementation for Ollama provider
    /// </summary>
    internal class OllamaEngine : BaseEngine
    {
        private readonly ILoggerFactory _loggerFactory;

        public OllamaEngine(
            IPerson person, 
            ILogger<BaseEngine> logger, 
            ILoggerFactory loggerFactory,
            StorageFactory storage,
            IPlainAIFunctionFactory plainAIFunctionFactory,
            ISettings settings) 
            : base(person, logger, storage, plainAIFunctionFactory, settings)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        protected override async Task<IChatClient> CreateChatClientAsync()
        {
            try
            {
                var apiProviderRepository = await _storage.GetApiProviderRepositoryAsync();
                var provider = await apiProviderRepository.GetProviderByNameAsync(_person.ProviderName);

                if (provider == null)
                {
                    _logger.LogError("Cannot initialize Ollama Client: API provider {ProviderName} not found", _person.ProviderName);
                    throw new UIException(string.Format(Resources.Error_ApiProviderNotFound, _person.ProviderName));
                }

                // Initialize OllamaSharp client
                var ollamaClient = new OllamaApiClient(new Uri(provider.ApiEndpoint), _person.ModelId);

                // Create builder
                var builder = new ChatClientBuilder(ollamaClient);
                var chatClient = builder.UseFunctionInvocation().UseLogging(_loggerFactory).Build();
                
                _logger.LogInformation("Ollama client initialized for model {ModelId} at endpoint {Endpoint}", 
                    _person.ModelId, provider.ApiEndpoint);
                
                return chatClient;
            }
            catch (Exception ex)
            {
                var errorMessage = GetInitializationErrorMessage("Ollama", ex);
                _logger.LogError(ex, errorMessage);
                throw new UIException(Resources.Error_InitializeChatClient);
            }
        }
    }
}