
using DaoStudio.Interfaces;
using DaoStudio.Common;
using DaoStudio.Properties;
using DaoStudio.DBStorage.Factory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Anthropic.SDK;

namespace DaoStudio.Engines.MEAI
{
    /// <summary>
    /// Engine implementation for Anthropic Claude provider
    /// </summary>
    internal class AnthropicEngine : BaseEngine
    {
        private readonly ILoggerFactory _loggerFactory;

        public AnthropicEngine(
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
                    _logger.LogError("Cannot initialize Anthropic Client: API provider {ProviderName} not found", _person.ProviderName);
                    throw new UIException(string.Format(Resources.Error_ApiProviderNotFound, _person.ProviderName));
                }

                if (string.IsNullOrEmpty(provider.ApiKey))
                {
                    _logger.LogWarning("No API key provided for Anthropic provider");
                    throw new UIException("API Key is required for Anthropic provider to initialize chat client");
                }

                // Initialize Anthropic SDK client
                var apiAuthentication = new APIAuthentication(provider.ApiKey);
                var anthropicClient = new AnthropicClient(apiAuthentication);

                // Create builder using the IChatClient from Anthropic.SDK
                var builder = new ChatClientBuilder(anthropicClient.Messages);
                var chatClient = builder.UseFunctionInvocation().UseLogging(_loggerFactory).Build();
                
                _logger.LogInformation("Anthropic Claude client initialized for model {ModelId}", _person.ModelId);
                
                return chatClient;
            }
            catch (Exception ex)
            {
                var errorMessage = GetInitializationErrorMessage("Anthropic Claude", ex);
                _logger.LogError(ex, errorMessage);
                throw new UIException(Resources.Error_InitializeChatClient);
            }
        }
    }
}