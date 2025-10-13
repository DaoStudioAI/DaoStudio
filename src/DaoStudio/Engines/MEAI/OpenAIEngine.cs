
using DaoStudio.Interfaces;
using DaoStudio.Common;
using DaoStudio.Properties;
using DaoStudio.DBStorage.Factory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using System;
using System.ClientModel;
using System.Net.Http;
using System.Threading.Tasks;
using DaoStudio.Utilities;
using System.ClientModel.Primitives;

namespace DaoStudio.Engines.MEAI
{
    /// <summary>
    /// Engine implementation for OpenAI and OpenRouter providers
    /// </summary>
    internal class OpenAIEngine : BaseEngine
    {
        private readonly ILoggerFactory _loggerFactory;

        public OpenAIEngine(
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
                    _logger.LogError("Cannot initialize OpenAI Client: API provider {ProviderName} not found", _person.ProviderName);
                    throw new UIException(string.Format(Resources.Error_ApiProviderNotFound, _person.ProviderName));
                }

                // Use placeholder key if null since OpenAI SDK requires one
                string apiKey = string.IsNullOrEmpty(provider.ApiKey) ? "sk-placeholder-key-for-null-api-key" : provider.ApiKey;

                OpenAIClientOptions options;

#if  DEBUG
                // LoggingHttpMessageHandler will disable http streaming
                var httpLogger = _loggerFactory.CreateLogger("HttpClient.RawMessages");
                var innerHandler = new HttpClientHandler();
                var loggingHandler = new LoggingHttpMessageHandler(innerHandler, httpLogger, logHeaders: true, logContent: true);
                var httpClient = new HttpClient(loggingHandler);
                
                // Add custom HTTP headers from Person.Parameters
                ApplyHttpHeaders(httpClient);
                
                options = new OpenAIClientOptions
                {
                    Endpoint = new Uri(provider.ApiEndpoint),
                    Transport = new HttpClientPipelineTransport(httpClient, true, _loggerFactory)
                };
#else
                var httpClient = new HttpClient();
                
                // Add custom HTTP headers from Person.Parameters
                ApplyHttpHeaders(httpClient);
                
                options = new OpenAIClientOptions
                {
                    Endpoint = new Uri(provider.ApiEndpoint),
                    Transport = new HttpClientPipelineTransport(httpClient)
                };
#endif

                // OpenRouter uses OpenAI-compatible API
                var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);

                // Create builder
                var builder = new ChatClientBuilder(client.GetChatClient(_person.ModelId).AsIChatClient());
                var chatClient = builder.UseFunctionInvocation().Build();
                
                _logger.LogInformation("OpenAI/OpenRouter client initialized for model {ModelId} at endpoint {Endpoint}", 
                    _person.ModelId, provider.ApiEndpoint);
                
                return chatClient;
            }
            catch (Exception ex)
            {
                var errorMessage = GetInitializationErrorMessage("OpenAI/OpenRouter", ex);
                _logger.LogError(ex, errorMessage);
                throw new UIException(Resources.Error_InitializeChatClient);
            }
        }

        /// <summary>
        /// Apply custom HTTP headers from Person.Parameters to HttpClient
        /// </summary>
        private void ApplyHttpHeaders(HttpClient httpClient)
        {
            if (_person.Parameters == null || !_person.Parameters.TryGetValue(PersonParameterNames.HttpHeaders, out var headersString))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(headersString))
            {
                return;
            }

            try
            {
                // Parse headers as key-value pairs (one per line)
                var lines = headersString.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var colonIndex = line.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var headerName = line.Substring(0, colonIndex).Trim();
                        var headerValue = line.Substring(colonIndex + 1).Trim();
                        
                        if (!string.IsNullOrEmpty(headerName) && !string.IsNullOrEmpty(headerValue))
                        {
                            httpClient.DefaultRequestHeaders.Add(headerName, headerValue);
                            _logger.LogDebug("Added HTTP header: {HeaderName}", headerName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing HTTP headers from Person.Parameters");
            }
        }
    }
}