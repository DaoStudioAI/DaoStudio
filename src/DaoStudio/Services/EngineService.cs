using DaoStudio.Engines.MEAI;
using DaoStudio.Interfaces;
using DryIoc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace DaoStudio.Services
{
    /// <summary>
    /// Engine factory service that creates engines based on provider/model/person combinations
    /// </summary>
    public class EngineService : IEngineService
    {
        private readonly Container _container;
        private readonly ILogger<EngineService> _logger;
        public EngineService(Container container, ILogger<EngineService> logger)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEngine> CreateEngineAsync(IPerson person)
        {
            if (person == null)
                throw new ArgumentNullException(nameof(person));

            var engine = CreateEngineInternal(person);

            _logger.LogDebug("Engine for person {PersonName} ({Provider}:{Model}) created", 
                person.Name, person.ProviderName, person.ModelId);

            return await Task.FromResult(engine);
        }

        protected virtual IEngine CreateEngineInternal(IPerson person)
        {
            try
            {
                // Get the provider type to determine which engine to create
                var providerType = GetProviderType(person.ProviderName);

                // Resolve common dependencies from the container
                var loggerForBase = _container.Resolve<ILogger<BaseEngine>>();
                var loggerFactory = _container.Resolve<ILoggerFactory>();
                var storageFactory = _container.Resolve<DaoStudio.DBStorage.Factory.StorageFactory>();
                var plainAIFunctionFactory = _container.Resolve<IPlainAIFunctionFactory>();
                var settings = _container.Resolve<ISettings>();

                IEngine engine = providerType switch
                {
                    ProviderType.OpenAI => new OpenAIEngine(person, loggerForBase, loggerFactory, storageFactory, plainAIFunctionFactory, settings),
                    ProviderType.OpenRouter => new OpenAIEngine(person, loggerForBase, loggerFactory, storageFactory, plainAIFunctionFactory, settings), // OpenRouter uses OpenAI-compatible API
                    ProviderType.Google => new GoogleEngine(person, loggerForBase, storageFactory, plainAIFunctionFactory, settings),
                    ProviderType.Anthropic => new AnthropicEngine(person, loggerForBase, loggerFactory, storageFactory, plainAIFunctionFactory, settings),
                    ProviderType.Ollama => new OllamaEngine(person, loggerForBase, loggerFactory, storageFactory, plainAIFunctionFactory, settings),
                    ProviderType.AWSBedrock => new AWSBedrockEngine(person, loggerForBase, loggerFactory, storageFactory, plainAIFunctionFactory, settings),
                    _ => throw new NotSupportedException($"Engine for provider type {providerType} is not supported")
                };

                _logger.LogInformation("Created new {EngineType} for person {PersonName} ({Provider}:{Model})", 
                    engine.GetType().Name, person.Name, person.ProviderName, person.ModelId);

                return engine;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create engine for person {PersonName} ({Provider}:{Model})", 
                    person.Name, person.ProviderName, person.ModelId);
                throw;
            }
        }

        private ProviderType GetProviderType(string providerName)
        {
            // This is a simplified mapping - in a real implementation, 
            // you might want to look this up from the database or configuration
            return providerName?.ToLowerInvariant() switch
            {
                "openai" => ProviderType.OpenAI,
                "openrouter" => ProviderType.OpenRouter,
                "google" => ProviderType.Google,
                "anthropic" => ProviderType.Anthropic,
                "ollama" => ProviderType.Ollama,
                "awsbedrock" => ProviderType.AWSBedrock,
                "llama" => ProviderType.LLama,
                _ => throw new NotSupportedException($"Provider '{providerName}' is not supported")
            };
        }
    }
}