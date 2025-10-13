
using DaoStudio.Interfaces;
using DaoStudio.Common;
using DaoStudio.Properties;
using DaoStudio.DBStorage.Factory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Amazon.BedrockRuntime;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace DaoStudio.Engines.MEAI
{
    /// <summary>
    /// Engine implementation for AWS Bedrock provider
    /// </summary>
    internal class AWSBedrockEngine : BaseEngine
    {
        private readonly ILoggerFactory _loggerFactory;

        public AWSBedrockEngine(
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
                    _logger.LogError("Cannot initialize AWS Bedrock Client: API provider {ProviderName} not found", _person.ProviderName);
                    throw new UIException(string.Format(Resources.Error_ApiProviderNotFound, _person.ProviderName));
                }

                // AWS Bedrock integration using AWSSDK.Extensions.Bedrock.MEAI
                // Resolve region
                if (!provider.Parameters.TryGetValue("Region", out var region))
                {
                    region = "us-east-1";
                }
                var regionEndpoint = RegionEndpoint.GetBySystemName(region);

                // Resolve AWS credentials
                AWSCredentials? awsCredentials = null;

                // 1. Try profile name from parameters
                if (provider.Parameters.TryGetValue("ProfileName", out var profileName) && !string.IsNullOrWhiteSpace(profileName))
                {
                    var chain = new CredentialProfileStoreChain();
                    if (chain.TryGetAWSCredentials(profileName, out var profCreds))
                    {
                        awsCredentials = profCreds;
                    }
                }

                // 2. Try access key / secret key passed directly
                if (awsCredentials == null)
                {
                    var accessKeyId = provider.ApiKey;
                    if (string.IsNullOrWhiteSpace(accessKeyId) && provider.Parameters.TryGetValue("AccessKeyId", out var accessKeyIdParam))
                    {
                        accessKeyId = accessKeyIdParam;
                    }

                    if (provider.Parameters.TryGetValue("SecretAccessKey", out var secretKey) &&
                        !string.IsNullOrWhiteSpace(accessKeyId) &&
                        !string.IsNullOrWhiteSpace(secretKey))
                    {
                        awsCredentials = new BasicAWSCredentials(accessKeyId, secretKey);
                    }
                }

                // 3. Fallback to default credential chain if still null
#pragma warning disable CS0618 // Type or member is obsolete
                awsCredentials ??= FallbackCredentialsFactory.GetCredentials();
#pragma warning restore CS0618 // Type or member is obsolete

                // Create Bedrock runtime client
                var bedrockRuntimeClient = new AmazonBedrockRuntimeClient(awsCredentials, regionEndpoint);

                // Create IChatClient via MEAI extension
                var bedrockChatClient = bedrockRuntimeClient.AsIChatClient(_person.ModelId);

                // Build chat client with function invocation support
                var builder = new ChatClientBuilder(bedrockChatClient);
                var chatClient = builder.UseFunctionInvocation().UseLogging(_loggerFactory).Build();
                
                _logger.LogInformation("AWS Bedrock client initialized for model {ModelId} in region {Region}", 
                    _person.ModelId, region);
                
                return chatClient;
            }
            catch (Exception ex)
            {
                var errorMessage = GetInitializationErrorMessage("AWS Bedrock", ex);
                _logger.LogError(ex, errorMessage);
                throw new UIException(Resources.Error_InitializeChatClient);
            }
        }
    }
}