using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using DaoStudio.DBStorage.Models;

namespace PerformanceStorage
{
    /// <summary>
    /// Utility class for generating large volumes of test data for load testing database repositories
    /// </summary>
    public static class TestDataGenerator
    {
        private static long _uniqueCounter = 0;
        
        /// <summary>
        /// Creates a new Random instance with a predictable seed for test isolation
        /// </summary>
        /// <param name="testSpecificSeed">Optional test-specific seed. If not provided, uses a deterministic seed based on current counter</param>
        /// <returns>A new Random instance with predictable seed</returns>
        public static Random CreateRandom(int? testSpecificSeed = null)
        {
            return new Random(testSpecificSeed ?? (42 + (int)Interlocked.Read(ref _uniqueCounter)));
        }
        
        /// <summary>
        /// Public access to a random instance for consistency across tests
        /// Note: This creates a new instance each time for test isolation
        /// </summary>
        public static Random Random => CreateRandom();
        private static readonly string[] _providerNames = { "OpenAI", "Anthropic", "Google", "Local", "OpenRouter", "Ollama", "LLama", "AWSBedrock" };
        private static readonly string[] _modelIds = { "gpt-4o", "gpt-4o-mini", "claude-3-5-sonnet", "claude-3-haiku", "gemini-pro", "llama-3.1-70b" };
        private static readonly string[] _toolNames = { "WebSearch", "FileManager", "CodeGenerator", "DataAnalyzer", "ImageProcessor", "Calculator" };
        private static readonly string[] _promptCategories = { "ChatBot", "CodeAssistant", "DataAnalysis", "Creative", "Educational", "Technical" };
        private static readonly string[] _firstNames = { "Alice", "Bob", "Charlie", "Diana", "Edward", "Fiona", "George", "Hannah", "Ivan", "Julia" };
        private static readonly string[] _lastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez" };

        /// <summary>
        /// Generate multiple Settings entities for bulk load testing
        /// </summary>
        public static List<Settings> GenerateSettings(int count, int propertiesPerSetting = 50)
        {
            var random = CreateRandom();
            var settings = new List<Settings>();
            var timestamp = DateTime.UtcNow.Ticks;
            
            for (int i = 0; i < count; i++)
            {
                var uniqueId = System.Threading.Interlocked.Increment(ref _uniqueCounter);
                var properties = new Dictionary<string, string>();
                for (int j = 0; j < propertiesPerSetting; j++)
                {
                    properties[$"Property{j}"] = GenerateRandomString(50, 200);
                }

                settings.Add(new Settings
                {
                    ApplicationName = $"TestApp{uniqueId}_{timestamp % 1000000}",
                    Version = random.Next(1, 100),
                    Properties = properties,
                    LastModified = DateTime.UtcNow.AddMinutes(-random.Next(0, 10080)), // Within last week
                    Theme = random.Next(0, 3) // 0=Light, 1=Dark, 2=System
                });
            }
            
            return settings;
        }

        /// <summary>
        /// Generate multiple APIProvider entities for bulk load testing
        /// </summary>
        public static List<APIProvider> GenerateAPIProviders(int count, int parametersPerProvider = 20)
        {
            var random = CreateRandom();
            var providers = new List<APIProvider>();
            var timestamp = DateTime.UtcNow.Ticks;
            
            for (int i = 0; i < count; i++)
            {
                var uniqueId = System.Threading.Interlocked.Increment(ref _uniqueCounter);
                var parameters = new Dictionary<string, string>();
                for (int j = 0; j < parametersPerProvider; j++)
                {
                    parameters[$"param{j}"] = GenerateRandomString(10, 100);
                }

                providers.Add(new APIProvider
                {
                    Name = $"{_providerNames[random.Next(_providerNames.Length)]}_Provider_{uniqueId}_{timestamp % 1000000}",
                    ApiEndpoint = $"https://api-{uniqueId}.example.com/v1",
                    ApiKey = GenerateRandomString(32, 64),
                    Parameters = parameters,
                    IsEnabled = random.NextDouble() > 0.2, // 80% enabled
                    LastModified = DateTime.UtcNow.AddMinutes(-random.Next(0, 10080)),
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                    ProviderType = random.Next(0, 8), // 0-7 provider types
                    Timeout = random.Next(5000, 60000),
                    MaxConcurrency = random.Next(1, 50)
                });
            }
            
            return providers;
        }

        /// <summary>
        /// Generate multiple Person entities for bulk load testing
        /// </summary>
        public static List<Person> GeneratePersons(int count, bool includeImages = false, int toolsPerPerson = 5, int parametersPerPerson = 15)
        {
            var random = CreateRandom();
            var persons = new List<Person>();
            var timestamp = DateTime.UtcNow.Ticks;
            
            for (int i = 0; i < count; i++)
            {
                var uniqueId = System.Threading.Interlocked.Increment(ref _uniqueCounter);
                var toolNames = new string[toolsPerPerson];
                for (int j = 0; j < toolsPerPerson; j++)
                {
                    toolNames[j] = _toolNames[random.Next(_toolNames.Length)];
                }

                var parameters = new Dictionary<string, string>();
                for (int j = 0; j < parametersPerPerson; j++)
                {
                    parameters[$"param{j}"] = GenerateRandomString(10, 100);
                }

                persons.Add(new Person
                {
                    Name = $"{_firstNames[random.Next(_firstNames.Length)]}_{_lastNames[random.Next(_lastNames.Length)]}_{uniqueId}_{timestamp % 1000000}",
                    Description = GenerateRandomString(100, 500),
                    Image = includeImages ? GenerateRandomImage() : null,
                    IsEnabled = random.NextDouble() > 0.15, // 85% enabled
                    ProviderName = _providerNames[random.Next(_providerNames.Length)],
                    ModelId = _modelIds[random.Next(_modelIds.Length)],
                    DeveloperMessage = random.NextDouble() > 0.5 ? GenerateRandomString(50, 200) : null,
                    ToolNames = toolNames,
                    Parameters = parameters,
                    LastModified = DateTime.UtcNow.AddMinutes(-random.Next(0, 10080)),
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                    PersonType = random.Next(0, 3),
                    AppId = random.Next(1, 100)
                });
            }
            
            return persons;
        }

        /// <summary>
        /// Generate multiple LlmTool entities for bulk load testing
        /// </summary>
        public static List<LlmTool> GenerateLlmTools(int count, bool includeStateData = false, int parametersPerTool = 10)
        {
            var random = CreateRandom();
            var tools = new List<LlmTool>();
            var timestamp = DateTime.UtcNow.Ticks;
            
            for (int i = 0; i < count; i++)
            {
                var uniqueId = System.Threading.Interlocked.Increment(ref _uniqueCounter);
                var parameters = new Dictionary<string, string>();
                for (int j = 0; j < parametersPerTool; j++)
                {
                    parameters[$"param{j}"] = GenerateRandomString(10, 100);
                }

                tools.Add(new LlmTool
                {
                    StaticId = $"com.test.tool{uniqueId}",
                    Name = $"{_toolNames[random.Next(_toolNames.Length)]}_{uniqueId}_{timestamp % 1000000}",
                    Description = GenerateRandomString(100, 300),
                    ToolConfig = GenerateRandomString(200, 1000),
                    ToolType = random.Next(0, 3),
                    Parameters = parameters,
                    IsEnabled = random.NextDouble() > 0.1, // 90% enabled
                    LastModified = DateTime.UtcNow.AddMinutes(-random.Next(0, 10080)),
                    State = random.Next(0, 2), // 0=Stateless, 1=Stateful
                    StateData = includeStateData && random.NextDouble() > 0.5 ? GenerateRandomBinaryData() : null,
                    DevMsg = random.NextDouble() > 0.3 ? GenerateRandomString(50, 150) : string.Empty,
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365))
                });
            }
            
            return tools;
        }

        /// <summary>
        /// Generate multiple LlmPrompt entities for bulk load testing
        /// </summary>
        public static List<LlmPrompt> GenerateLlmPrompts(int count, int parametersPerPrompt = 8)
        {
            var random = CreateRandom();
            var prompts = new List<LlmPrompt>();
            var timestamp = DateTime.UtcNow.Ticks;
            
            for (int i = 0; i < count; i++)
            {
                var uniqueId = System.Threading.Interlocked.Increment(ref _uniqueCounter);
                var parameters = new Dictionary<string, string>();
                for (int j = 0; j < parametersPerPrompt; j++)
                {
                    parameters[$"param{j}"] = GenerateRandomString(10, 50);
                }

                prompts.Add(new LlmPrompt
                {
                    Name = $"Prompt_{uniqueId}_{timestamp % 1000000}_{GenerateRandomString(5, 15)}",
                    Category = _promptCategories[random.Next(_promptCategories.Length)],
                    Content = GenerateRandomString(500, 2000), // Large prompt content
                    Parameters = parameters,
                    IsEnabled = random.NextDouble() > 0.2, // 80% enabled
                    LastModified = DateTime.UtcNow.AddMinutes(-random.Next(0, 10080)),
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365))
                });
            }
            
            return prompts;
        }

        /// <summary>
        /// Generate multiple Session entities for bulk load testing
        /// </summary>
        public static List<Session> GenerateSessions(int count, bool includeLogos = false, int maxHierarchyDepth = 3)
        {
            var random = CreateRandom();
            var sessions = new List<Session>();
            var createdSessions = new List<long>();
            
            for (int i = 0; i < count; i++)
            {
                var toolNames = new List<string>();
                var toolCount = random.Next(1, 6);
                for (int j = 0; j < toolCount; j++)
                {
                    toolNames.Add(_toolNames[random.Next(_toolNames.Length)]);
                }

                var personNames = new List<string>();
                var personCount = random.Next(1, 4);
                for (int j = 0; j < personCount; j++)
                {
                    personNames.Add($"Person{random.Next(1, 100)}");
                }

                var properties = new Dictionary<string, string>
                {
                    ["MaxTokens"] = random.Next(1000, 8000).ToString(),
                    ["Temperature"] = (random.NextDouble() * 2).ToString("F2"),
                    ["TopP"] = (random.NextDouble()).ToString("F2")
                };

                // Randomly assign parent session for hierarchy testing
                long? parentSessId = null;
                if (createdSessions.Count > 0 && random.NextDouble() > 0.7) // 30% chance of having parent
                {
                    parentSessId = createdSessions[random.Next(createdSessions.Count)];
                }

                var session = new Session
                {
                    Id = i + 1, // Sequential IDs for easier testing
                    Title = $"Test Session {i}",
                    Description = GenerateRandomString(50, 200),
                    Logo = includeLogos ? GenerateRandomImage() : null,
                    ToolNames = toolNames,
                    PersonNames = personNames,
                    ParentSessId = parentSessId,
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                    LastModified = DateTime.UtcNow.AddMinutes(-random.Next(0, 10080)),
                    TotalTokenCount = random.Next(0, 100000),
                    OutputTokenCount = random.Next(0, 50000),
                    InputTokenCount = random.Next(0, 50000),
                    AdditionalCounts = random.Next(0, 1000),
                    Properties = properties
                };

                sessions.Add(session);
                createdSessions.Add(session.Id);
            }
            
            return sessions;
        }

        /// <summary>
        /// Generate multiple Message entities for bulk load testing
        /// </summary>
        public static List<Message> GenerateMessages(int count, List<long> sessionIds, bool includeBinaryContent = false)
        {
            var random = CreateRandom();
            var messages = new List<Message>();
            var roles = new[] { 1, 2, 3, 4 }; // 1=User, 2=Assistant, 3=System, 4=Developer
            var messageTypes = new[] { 0, 1 }; // Normal=0, Information=1
            
            for (int i = 0; i < count; i++)
            {
                var sessionId = sessionIds[random.Next(sessionIds.Count)];
                
                var binaryContents = new List<BinaryData>();
                if (includeBinaryContent && random.NextDouble() > 0.7) // 30% chance of binary content
                {
                    var binaryCount = random.Next(1, 4);
                    for (int j = 0; j < binaryCount; j++)
                    {
                        binaryContents.Add(new BinaryData
                        {
                            Data = GenerateRandomBinaryData(),
                            Type = j % 2, // 0 or 1 for different binary types
                            Name = $"file{j}.{(j % 2 == 0 ? "jpg" : "pdf")}"
                        });
                    }
                }

                messages.Add(new Message
                {
                    SessionId = sessionId,
                    Content = GenerateRandomString(100, 1000),
                    Role = roles[random.Next(roles.Length)],
                    Type = messageTypes[random.Next(messageTypes.Length)],
                    BinaryContents = binaryContents.Count > 0 ? binaryContents : null,
                    BinaryVersion = 0,
                    ParentMsgId = random.NextDouble() > 0.8 ? random.Next(1, i + 1) : 0, // 20% chance of parent, 0 means no parent
                    ParentSessId = random.NextDouble() > 0.9 ? sessionIds[random.Next(sessionIds.Count)] : 0, // 10% chance of parent session, 0 means no parent
                    CreatedAt = DateTime.UtcNow.AddMinutes(-random.Next(0, 10080)),
                    LastModified = DateTime.UtcNow.AddMinutes(-random.Next(0, 1440))
                });
            }

            return messages;
        }

        /// <summary>
        /// Generate multiple Message entities for single session testing
        /// </summary>
        public static List<Message> GenerateMessages(int count, long sessionId, bool includeBinaryContent = false)
        {
            return GenerateMessages(count, new List<long> { sessionId }, includeBinaryContent);
        }        /// <summary>
        /// Generate multiple CachedModel entities for bulk load testing
        /// </summary>
        public static List<CachedModel> GenerateCachedModels(int count, List<long> apiProviderIds)
        {
            var random = CreateRandom();
            var models = new List<CachedModel>();
            var catalogs = new[] { "General", "Code", "Chat", "Instruct", "Embedding", "Vision" };
            var timestamp = DateTime.UtcNow.Ticks;
            
            for (int i = 0; i < count; i++)
            {
                var uniqueId = System.Threading.Interlocked.Increment(ref _uniqueCounter);
                models.Add(new CachedModel
                {
                    ApiProviderId = apiProviderIds[random.Next(apiProviderIds.Count)],
                    Name = $"{_modelIds[random.Next(_modelIds.Length)]}_cached_{uniqueId}_{timestamp % 1000000}",
                    ModelId = $"{_providerNames[random.Next(_providerNames.Length)]}/{_modelIds[random.Next(_modelIds.Length)]}",
                    ProviderType = random.Next(0, 8),
                    Catalog = catalogs[random.Next(catalogs.Length)]
                });
            }
            
            return models;
        }

        /// <summary>
        /// Generate random string of specified length range
        /// </summary>
        public static string GenerateRandomString(int minLength, int maxLength)
        {
            var random = CreateRandom();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ";
            var length = random.Next(minLength, maxLength + 1);
            var stringBuilder = new StringBuilder(length);
            
            for (int i = 0; i < length; i++)
            {
                stringBuilder.Append(chars[random.Next(chars.Length)]);
            }
            
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Generate random binary data for images/state data
        /// </summary>
        public static byte[] GenerateRandomBinaryData()
        {
            var random = CreateRandom();
            var size = random.Next(1024, 51200); // 1KB to 50KB
            var data = new byte[size];
            random.NextBytes(data);
            return data;
        }

        /// <summary>
        /// Generate random binary data of specified size range
        /// </summary>
        public static byte[] GenerateRandomBinaryData(int minSize, int maxSize)
        {
            var random = CreateRandom();
            var size = random.Next(minSize, maxSize + 1);
            var data = new byte[size];
            random.NextBytes(data);
            return data;
        }

        /// <summary>
        /// Generate random image data
        /// </summary>
        private static byte[] GenerateRandomImage()
        {
            var random = CreateRandom();
            var size = random.Next(5120, 102400); // 5KB to 100KB for images
            var data = new byte[size];
            random.NextBytes(data);
            return data;
        }

        /// <summary>
        /// Create test data scenarios for comprehensive load testing
        /// </summary>
        public static class Scenarios
        {
            /// <summary>
            /// Small load scenario - suitable for quick tests
            /// </summary>
            public static class Small
            {
                public const int SettingsCount = 100;
                public const int ProvidersCount = 50;
                public const int PersonsCount = 200;
                public const int ToolsCount = 100;
                public const int PromptsCount = 300;
                public const int SessionsCount = 500;
                public const int MessagesCount = 2000;
                public const int CachedModelsCount = 1000;
            }

            /// <summary>
            /// Medium load scenario - realistic production-like volumes
            /// </summary>
            public static class Medium
            {
                public const int SettingsCount = 1000;
                public const int ProvidersCount = 200;
                public const int PersonsCount = 2000;
                public const int ToolsCount = 500;
                public const int PromptsCount = 1500;
                public const int SessionsCount = 5000;
                public const int MessagesCount = 50000;
                public const int CachedModelsCount = 10000;
            }

            /// <summary>
            /// Large load scenario - stress testing with high volumes
            /// </summary>
            public static class Large
            {
                public const int SettingsCount = 5000;
                public const int ProvidersCount = 1000;
                public const int PersonsCount = 10000;
                public const int ToolsCount = 2000;
                public const int PromptsCount = 10000;
                public const int SessionsCount = 25000;
                public const int MessagesCount = 250000;
                public const int CachedModelsCount = 50000;
            }
        }
    }
}
