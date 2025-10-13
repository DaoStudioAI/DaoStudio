
using DaoStudio.Interfaces;
using DaoStudio.Common;
using DaoStudio.Properties;
using DaoStudio.DBStorage.Factory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using GenerativeAI.Microsoft;

namespace DaoStudio.Engines.MEAI
{
    /// <summary>
    /// Engine implementation for Google Gemini provider
    /// </summary>
    internal class GoogleEngine : BaseEngine
    {
        public GoogleEngine(
            IPerson person, 
            ILogger<BaseEngine> logger,
            StorageFactory storage,
            IPlainAIFunctionFactory plainAIFunctionFactory,
            ISettings settings) 
            : base(person, logger, storage, plainAIFunctionFactory, settings)
        {
        }

        protected override async Task<IChatClient> CreateChatClientAsync()
        {
            try
            {
                var apiProviderRepository = await _storage.GetApiProviderRepositoryAsync();
                var provider = await apiProviderRepository.GetProviderByNameAsync(_person.ProviderName);

                if (provider == null)
                {
                    _logger.LogError("Cannot initialize Google Client: API provider {ProviderName} not found", _person.ProviderName);
                    throw new UIException(string.Format(Resources.Error_ApiProviderNotFound, _person.ProviderName));
                }

                // Use placeholder key if null since GenerativeAIChatClient constructor expects non-null
                string apiKey = provider.ApiKey ?? "placeholder-api-key-for-null";

                var chatClient = new GenerativeAIChatClient(apiKey, _person.ModelId, true);
                
                _logger.LogInformation("Google Gemini client initialized for model {ModelId}", _person.ModelId);
                
                return chatClient;
            }
            catch (Exception ex)
            {
                var errorMessage = GetInitializationErrorMessage("Google Gemini", ex);
                _logger.LogError(ex, errorMessage);
                throw new UIException(Resources.Error_InitializeChatClient);
            }
        }
    }
}