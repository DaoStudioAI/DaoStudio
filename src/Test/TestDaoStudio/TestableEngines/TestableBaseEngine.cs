using DaoStudio.Engines.MEAI;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.DBStorage.Factory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestDaoStudio.TestableEngines
{
    /// <summary>
    /// Testable version of BaseEngine that exposes protected methods for testing
    /// </summary>
    internal class TestableBaseEngine : BaseEngine
    {
        private readonly IChatClient? _injectedChatClient;

        public TestableBaseEngine(
            IPerson person, 
            ILogger<BaseEngine> logger,
            StorageFactory storage,
            IPlainAIFunctionFactory plainAIFunctionFactory,
            ISettings settings,
            IChatClient? injectedChatClient = null) 
            : base(person, logger, storage, plainAIFunctionFactory, settings)
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

            // For testing, return a mock chat client
            throw new System.NotImplementedException("TestableBaseEngine requires an injected IChatClient for testing");
        }

        // Expose protected methods for testing
        public async Task<AITool[]?> TestProcessToolsWithConflictResolutionAsync(
            Dictionary<string, List<FunctionWithDescription>>? tools,
            ISession session)
        {
            return await ProcessToolsWithConflictResolutionAsync(tools, session);
        }

        public AITool[]? TestProcessToolsWithConflictResolution(
            Dictionary<string, List<FunctionWithDescription>>? tools,
            ISession session)
        {
            // This method is now obsolete but kept for backward compatibility in existing tests
            return TestProcessToolsWithConflictResolutionAsync(tools, session).GetAwaiter().GetResult();
        }

        public string TestSanitizeKeyForPrefix(string key)
        {
            return SanitizeKeyForPrefix(key);
        }

        public string TestMakeUniquePrefix(string prefix, Dictionary<string, int> usedPrefixes)
        {
            return MakeUniquePrefix(prefix, usedPrefixes);
        }

        public FunctionWithDescription TestCreateFunctionWithPrefixedName(
            FunctionWithDescription originalFunction,
            string prefix)
        {
            return CreateFunctionWithPrefixedName(originalFunction, prefix);
        }
    }
}