using DaoStudio.Engines.MEAI;
using DaoStudio.Interfaces;
using DaoStudio.DBStorage.Factory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace TestDaoStudio.TestableEngines
{
    /// <summary>
    /// Testable version of AWSBedrockEngine that allows injecting IChatClient for testing
    /// </summary>
    internal class TestableAWSBedrockEngine : AWSBedrockEngine
    {
        private readonly IChatClient? _injectedChatClient;

        public TestableAWSBedrockEngine(
            IPerson person, 
            ILogger<BaseEngine> logger,
            ILoggerFactory loggerFactory,
            StorageFactory storage,
            IPlainAIFunctionFactory plainAIFunctionFactory,
            ISettings settings,
            IChatClient? injectedChatClient = null) 
            : base(person, logger, loggerFactory, storage, plainAIFunctionFactory, settings)
        {
            _injectedChatClient = injectedChatClient;
        }

        protected override async Task<IChatClient> CreateChatClientAsync()
        {
            // If a chat client is injected for testing, use it instead of creating a real one
            if (_injectedChatClient != null)
            {
                return _injectedChatClient;
            }

            // Otherwise, use the base implementation
            return await base.CreateChatClientAsync();
        }
    }
}
