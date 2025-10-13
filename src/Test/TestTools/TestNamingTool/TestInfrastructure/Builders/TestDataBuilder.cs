using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using Naming;
using Naming.ParallelExecution;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool.TestInfrastructure.Builders
{
    internal class TestDataBuilder
    {
        public static NamingConfig CreateBasicNamingConfig()
        {
            return new NamingConfig
            {
                Version = 1,
                MaxRecursionLevel = 2,
                FunctionName = "test_function",
                FunctionDescription = "Test function description",
                ReturnToolName = "test_return",
                ReturnToolDescription = "Test return description",
                UrgingMessage = "Please complete the task",
                PromptMessage = "Here is your task: {{task}}"
            };
        }

        public static NamingConfig CreateNamingConfigWithExecutive()
        {
            var config = CreateBasicNamingConfig();
            config.ExecutivePerson = new ConfigPerson
            {
                Name = "TestExecutive",
                Description = "Test executive person"
            };
            return config;
        }

        public static NamingConfig CreateNamingConfigWithParameters()
        {
            var config = CreateBasicNamingConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "task",
                    Description = "The task to complete",
                    IsRequired = true,
                    Type = ParameterType.String
                },
                new ParameterConfig
                {
                    Name = "priority",
                    Description = "Task priority",
                    IsRequired = false,
                    Type = ParameterType.Number
                }
            };

            config.ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "success",
                    Description = "Whether the task was successful",
                    IsRequired = true,
                    Type = ParameterType.Bool
                },
                new ParameterConfig
                {
                    Name = "result",
                    Description = "The task result",
                    IsRequired = false,
                    Type = ParameterType.String
                }
            };

            return config;
        }

        public static NamingConfig CreateNamingConfigWithParallelExecution()
        {
            var config = CreateNamingConfigWithExecutive();
            config.ParallelConfig = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                MaxConcurrency = 3,
                ResultStrategy = ParallelResultStrategy.WaitForAll,
                SessionTimeoutMs = 60000 // 1 minute
            };
            return config;
        }

        public static MockHost CreateMockHostWithPersons(params string[] personNames)
        {
            var host = new MockHost();
            foreach (var name in personNames)
            {
                host.AddPerson(new MockPerson(name, $"Description for {name}"));
            }
            return host;
        }

        public static Dictionary<string, object?> CreateBasicRequestData()
        {
            return new Dictionary<string, object?>
            {
                ["task"] = "Complete the test task",
                ["priority"] = 1,
                ["background"] = "Test background information"
            };
        }

        public static Dictionary<string, object?> CreateRequestDataWithSession(ISession session)
        {
            var requestData = CreateBasicRequestData();
            requestData[DaoStudio.Common.Plugins.Constants.DasSession] = session;
            return requestData;
        }

        public static ParallelExecutionConfig CreateBasicParallelConfig()
        {
            return new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                MaxConcurrency = 2,
                ResultStrategy = ParallelResultStrategy.WaitForAll,
                SessionTimeoutMs = 30000
            };
        }

        public static ParallelExecutionConfig CreateListBasedParallelConfig()
        {
            return new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ListBased,
                ListParameterName = "items",
                MaxConcurrency = 4,
                ResultStrategy = ParallelResultStrategy.StreamIndividual,
                SessionTimeoutMs = 45000
            };
        }

        public static ParallelExecutionConfig CreateExternalListParallelConfig()
        {
            return new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ExternalList,
                ExternalList = new List<string> { "item1", "item2", "item3" },
                MaxConcurrency = 3,
                ResultStrategy = ParallelResultStrategy.FirstResultWins,
                SessionTimeoutMs = 20000
            };
        }

        public static ParameterConfig CreateComplexParameterConfig()
        {
            return new ParameterConfig
            {
                Name = "complexObject",
                Description = "A complex nested object",
                Type = ParameterType.Object,
                IsRequired = true,
                ObjectProperties = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        Name = "stringProperty",
                        Type = ParameterType.String,
                        IsRequired = true
                    },
                    new ParameterConfig
                    {
                        Name = "numberArray",
                        Type = ParameterType.Array,
                        IsRequired = false,
                        ArrayElementConfig = new ParameterConfig
                        {
                            Name = "arrayElement",
                            Type = ParameterType.Number,
                            IsRequired = true
                        }
                    }
                }
            };
        }

        public static List<ParameterConfig> CreateSampleParameters()
        {
            return new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "name",
                    Description = "The name",
                    Type = ParameterType.String,
                    IsRequired = true
                },
                new ParameterConfig
                {
                    Name = "age",
                    Description = "The age",
                    Type = ParameterType.Number,
                    IsRequired = false
                },
                new ParameterConfig
                {
                    Name = "active",
                    Description = "Whether active",
                    Type = ParameterType.Bool,
                    IsRequired = true
                }
            };
        }
    }
}
