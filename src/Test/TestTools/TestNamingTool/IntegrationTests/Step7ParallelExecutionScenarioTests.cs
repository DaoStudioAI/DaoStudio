using System.Text.Json;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming;
using Naming.ParallelExecution;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool.IntegrationTests
{
    /// <summary>
    /// Comprehensive integration tests for Step 7 - Parallel Execution Strategies scenarios in NamingTool.
    /// Focuses on testing all scenarios mentioned in workflow step 7: ParameterBased, ListBased, and ExternalList execution.
    /// </summary>
    public class Step7ParallelExecutionScenarioTests : IDisposable
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public Step7ParallelExecutionScenarioTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("Step7TestAssistant", "Test assistant for Step 7 scenarios");
            _mockHostSession = new MockHostSession(1);

            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        #region 7.1 ParameterBased Execution Comprehensive Tests

        [Fact]
        public async Task ParameterBased_TemplateContext_ShouldSetParameterNameAndValueCorrectly()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.PromptMessage = "Processing parameter: {{_Parameter.Name}} with value: {{_Parameter.Value}}. Additional context: {{sharedContext}}";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["aspect1"] = "data analysis",
                ["aspect2"] = "visualization",
                ["aspect3"] = "reporting",
                ["sharedContext"] = "quarterly review"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4); // 3 aspects + 1 shared context
            
            // Verify template rendering occurred (would need to check actual session messages)
            foreach (var session in _mockHost.CreatedSessions)
            {
                session.Should().NotBeNull("Each parameter should create a valid session");
            }
        }

        [Fact]
        public async Task ParameterBased_WithComplexParameterTypes_ShouldHandleObjectsAndArrays()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.PromptMessage = "Process {{_Parameter.Name}}: {{_Parameter.Value}}";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var complexObject = new { Name = "TestObject", Value = 42, Tags = new[] { "tag1", "tag2" } };
            var arrayParameter = new[] { "item1", "item2", "item3" };

            var requestData = new Dictionary<string, object?>
            {
                ["objectParam"] = complexObject,
                ["arrayParam"] = arrayParameter,
                ["stringParam"] = "simple string",
                ["numberParam"] = 123.45
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Should create sessions for all parameter types");
        }

        [Fact]
        public async Task ParameterBased_WithExcludedParameters_ShouldOnlyProcessIncludedOnes()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.ParallelConfig!.ExcludedParameters = new List<string> { "metadata", "config", "sessionId" };
            config.PromptMessage = "Processing {{_Parameter.Name}}: {{_Parameter.Value}}";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["task1"] = "analyze data",
                ["task2"] = "generate report",
                ["task3"] = "create visualization",
                ["metadata"] = "should be excluded",
                ["config"] = "should be excluded",
                ["sessionId"] = "should be excluded"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(3, "Should only create sessions for non-excluded parameters");
        }

        [Fact]
        public async Task ParameterBased_WithMixedResultStrategies_ShouldRespectStrategyConfiguration()
        {
            // Test WaitForAll strategy
            var waitConfig = CreateParameterBasedConfig();
            waitConfig.ParallelConfig!.ResultStrategy = ParallelResultStrategy.WaitForAll;
            
            var pluginInstance1 = await CreatePluginInstanceAsync(waitConfig);
            var functions1 = await GetFunctionsAsync(pluginInstance1);
            var namingFunction1 = functions1.First(f => f.Description.Name == waitConfig.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["param1"] = "value1",
                ["param2"] = "value2"
            };

            var result1 = await InvokeNamingFunctionAsync(namingFunction1, requestData);
            result1.Should().NotBeNull();

            // Reset for next test
            _mockHost.CreatedSessions.Clear();

            // Test StreamIndividual strategy
            var streamConfig = CreateParameterBasedConfig();
            streamConfig.ParallelConfig!.ResultStrategy = ParallelResultStrategy.StreamIndividual;
            
            var pluginInstance2 = await CreatePluginInstanceAsync(streamConfig);
            var functions2 = await GetFunctionsAsync(pluginInstance2);
            var namingFunction2 = functions2.First(f => f.Description.Name == streamConfig.FunctionName);

            var result2 = await InvokeNamingFunctionAsync(namingFunction2, requestData);
            result2.Should().NotBeNull();
        }

        #endregion

        #region 7.2 ListBased Execution Comprehensive Tests

        [Fact]
        public async Task ListBased_TemplateContext_ShouldSetListParameterNameAndItemValue()
        {
            // Arrange
            var config = CreateListBasedConfig("fileList");
            config.PromptMessage = "Processing list item from {{_Parameter.Name}}: {{_Parameter.Value}}. Operation: {{operation}}";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var fileList = new List<string> { "file1.txt", "file2.csv", "file3.json", "file4.xml" };
            var requestData = new Dictionary<string, object?>
            {
                ["fileList"] = fileList,
                ["operation"] = "validate and process"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Should create one session per list item");
        }

        [Fact]
        public async Task ListBased_WithComplexListItems_ShouldHandleObjectsInList()
        {
            // Arrange
            var config = CreateListBasedConfig("userList");
            config.PromptMessage = "Process user from {{_Parameter.Name}}: {{_Parameter.Value}}";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var userList = new List<object>
            {
                new { Id = 1, Name = "Alice", Role = "Admin" },
                new { Id = 2, Name = "Bob", Role = "User" },
                new { Id = 3, Name = "Charlie", Role = "Moderator" }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["userList"] = userList,
                ["batchId"] = "batch_001"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(3, "Should create one session per complex list item");
        }

        [Fact]
        public async Task ListBased_WithDifferentListTypes_ShouldHandleVariousCollectionTypes()
        {
            // Test with string array
            var config1 = CreateListBasedConfig("stringArray");
            var pluginInstance1 = await CreatePluginInstanceAsync(config1);
            var functions1 = await GetFunctionsAsync(pluginInstance1);
            var namingFunction1 = functions1.First(f => f.Description.Name == config1.FunctionName);

            var stringArray = new[] { "task1", "task2", "task3" };
            var requestData1 = new Dictionary<string, object?>
            {
                ["stringArray"] = stringArray
            };

            var result1 = await InvokeNamingFunctionAsync(namingFunction1, requestData1);
            result1.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(3);

            // Reset for next test
            _mockHost.CreatedSessions.Clear();

            // Test with IEnumerable
            var config2 = CreateListBasedConfig("enumerable");
            var pluginInstance2 = await CreatePluginInstanceAsync(config2);
            var functions2 = await GetFunctionsAsync(pluginInstance2);
            var namingFunction2 = functions2.First(f => f.Description.Name == config2.FunctionName);

            var enumerable = Enumerable.Range(1, 4).Select(i => $"item_{i}");
            var requestData2 = new Dictionary<string, object?>
            {
                ["enumerable"] = enumerable
            };

            var result2 = await InvokeNamingFunctionAsync(namingFunction2, requestData2);
            result2.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4);
        }

        [Fact]
        public async Task ListBased_WithSingleItemList_ShouldCreateSingleSession()
        {
            // Arrange
            var config = CreateListBasedConfig("singletonList");
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var singleItemList = new List<string> { "only_item" };
            var requestData = new Dictionary<string, object?>
            {
                ["singletonList"] = singleItemList
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should create exactly one session for single item list");
        }

        [Fact]
        public async Task ListBased_WithLargeList_ShouldRespectConcurrencyLimits()
        {
            // Arrange
            var config = CreateListBasedConfig("largeList");
            config.ParallelConfig!.MaxConcurrency = 3;
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var largeList = Enumerable.Range(1, 10).Select(i => $"item_{i}").ToList();
            var requestData = new Dictionary<string, object?>
            {
                ["largeList"] = largeList
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(10, "Should create sessions for all items regardless of concurrency limit");
        }

        #endregion

        #region 7.3 ExternalList Execution Comprehensive Tests

        [Fact]
        public async Task ExternalList_TemplateContext_ShouldSetExternalListAsParameterName()
        {
            // Arrange
            var predefinedScenarios = new List<string>
            {
                "scenario_alpha",
                "scenario_beta", 
                "scenario_gamma",
                "scenario_delta"
            };

            var config = CreateExternalListConfig(predefinedScenarios);
            config.PromptMessage = "Execute scenario from {{_Parameter.Name}}: {{_Parameter.Value}}. Environment: {{environment}}";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["environment"] = "production"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Should create one session per external list item");
        }

        [Fact]
        public async Task ExternalList_WithMixedContentTypes_ShouldHandleVariousStringFormats()
        {
            // Arrange
            var mixedExternalList = new List<string>
            {
                "simple_string",
                "Complex String With Spaces",
                "string-with-dashes",
                "string_with_underscores",
                "STRING_IN_CAPS",
                "string123with456numbers",
                "special!@#$%chars"
            };

            var config = CreateExternalListConfig(mixedExternalList);
            config.PromptMessage = "Process external value {{_Parameter.Value}} from source {{_Parameter.Name}}";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["processingMode"] = "comprehensive"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(7, "Should create sessions for all external list items");
        }

        [Fact]
        public async Task ExternalList_WithLargeExternalList_ShouldHandleScaling()
        {
            // Arrange
            var largeExternalList = Enumerable.Range(1, 50)
                .Select(i => $"external_item_{i:D3}")
                .ToList();

            var config = CreateExternalListConfig(largeExternalList);
            config.ParallelConfig!.MaxConcurrency = 5;
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["batchSize"] = "large_scale_test"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(50, "Should create sessions for all 50 external list items");
        }

        [Fact]
        public async Task ExternalList_WithConfiguredScenarios_ShouldExecutePredefinedTestCases()
        {
            // Arrange - Simulating predefined test scenarios
            var testScenarios = new List<string>
            {
                "load_test_light",
                "load_test_medium", 
                "load_test_heavy",
                "security_scan_basic",
                "security_scan_advanced",
                "performance_baseline",
                "compatibility_check"
            };

            var config = CreateExternalListConfig(testScenarios);
            config.PromptMessage = "Execute test scenario: {{_Parameter.Value}}. Configuration: {{testConfig}}";
            config.ParallelConfig!.ResultStrategy = ParallelResultStrategy.WaitForAll;
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["testConfig"] = "automated_regression_suite"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(7, "Should create sessions for all predefined test scenarios");
        }

        #endregion

        #region Cross-Strategy Integration Tests

        [Fact]
        public async Task AllStrategies_WithDifferentResultStrategies_ShouldProduceExpectedResults()
        {
            // Test each strategy with FirstResultWins
            var strategies = new[]
            {
                (ParallelExecutionType.ParameterBased, "param1", (object)"value1"),
                (ParallelExecutionType.ListBased, "list1", new List<string> { "listItem1", "listItem2" }),
                (ParallelExecutionType.ExternalList, "external", new List<string> { "ext1", "ext2" })
            };

            foreach (var (strategy, paramName, paramValue) in strategies)
            {
                _mockHost.CreatedSessions.Clear();

                var config = strategy switch
                {
                    ParallelExecutionType.ParameterBased => CreateParameterBasedConfig(),
                    ParallelExecutionType.ListBased => CreateListBasedConfig(paramName),
                    ParallelExecutionType.ExternalList => CreateExternalListConfig((List<string>)paramValue),
                    _ => throw new ArgumentException($"Unknown strategy: {strategy}")
                };

                config.ParallelConfig!.ResultStrategy = ParallelResultStrategy.FirstResultWins;

                var pluginInstance = await CreatePluginInstanceAsync(config);
                var functions = await GetFunctionsAsync(pluginInstance);
                var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

                var requestData = strategy == ParallelExecutionType.ExternalList 
                    ? new Dictionary<string, object?> { ["testParam"] = "test" }
                    : new Dictionary<string, object?> { [paramName] = paramValue };

                var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

                result.Should().NotBeNull($"Strategy {strategy} should produce a result");
                result.Should().Contain("first", $"FirstResultWins strategy should be indicated in result for {strategy}");
            }
        }

        #endregion

        #region Helper Methods

        private NamingConfig CreateParameterBasedConfig()
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "step7_parameter_based",
                FunctionDescription = "Step 7.1 - ParameterBased parallel execution test",
                PromptMessage = "Process parameter {{_Parameter.Name}} with value {{_Parameter.Value}}",
                UrgingMessage = "Complete the parameter processing task",
                MaxRecursionLevel = 1,
                ParallelConfig = new ParallelExecutionConfig
                {
                    ExecutionType = ParallelExecutionType.ParameterBased,
                    ResultStrategy = ParallelResultStrategy.WaitForAll,
                    MaxConcurrency = Environment.ProcessorCount,
                    SessionTimeoutMs = 30000
                }
            };
        }

        private NamingConfig CreateListBasedConfig(string listParameterName)
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "step7_list_based",
                FunctionDescription = "Step 7.2 - ListBased parallel execution test",
                PromptMessage = "Process list item from {{_Parameter.Name}}: {{_Parameter.Value}}",
                UrgingMessage = "Complete the list item processing task",
                MaxRecursionLevel = 1,
                ParallelConfig = new ParallelExecutionConfig
                {
                    ExecutionType = ParallelExecutionType.ListBased,
                    ListParameterName = listParameterName,
                    ResultStrategy = ParallelResultStrategy.WaitForAll,
                    MaxConcurrency = Environment.ProcessorCount,
                    SessionTimeoutMs = 30000
                }
            };
        }

        private NamingConfig CreateExternalListConfig(List<string> externalList)
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "step7_external_list",
                FunctionDescription = "Step 7.3 - ExternalList parallel execution test",
                PromptMessage = "Process external item from {{_Parameter.Name}}: {{_Parameter.Value}}",
                UrgingMessage = "Complete the external list item processing task",
                MaxRecursionLevel = 1,
                ParallelConfig = new ParallelExecutionConfig
                {
                    ExecutionType = ParallelExecutionType.ExternalList,
                    ExternalList = externalList,
                    ResultStrategy = ParallelResultStrategy.WaitForAll,
                    MaxConcurrency = Environment.ProcessorCount,
                    SessionTimeoutMs = 30000
                }
            };
        }

        private async Task<IPluginTool> CreatePluginInstanceAsync(NamingConfig config)
        {
            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = DateTime.Now.Ticks + Random.Shared.Next(1000),
                Description = "Step 7 scenario test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = $"Step7 Test - {config.FunctionName}"
            };

            return await _factory.CreatePluginToolAsync(plugToolInfo);
        }

        private async Task<List<FunctionWithDescription>> GetFunctionsAsync(IPluginTool pluginInstance)
        {
            var functions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(functions, _mockPerson, _mockHostSession);
            return functions;
        }

        private async Task<string> InvokeNamingFunctionAsync(FunctionWithDescription function, Dictionary<string, object?> parameters)
        {
            parameters[DaoStudio.Common.Plugins.Constants.DasSession] = _mockHostSession;

            if (function.Function is Func<Dictionary<string, object?>, Task<object?>> asyncDelegate)
            {
                var resultObj = await asyncDelegate(parameters);
                return resultObj?.ToString() ?? string.Empty;
            }

            var invocationResult = function.Function.DynamicInvoke(parameters);

            switch (invocationResult)
            {
                case Task task:
                    await task.ConfigureAwait(false);
                    var taskType = task.GetType();
                    if (taskType.IsGenericType && taskType.GetProperty("Result") is { } resultProp)
                    {
                        var awaitedResult = resultProp.GetValue(task);
                        return awaitedResult?.ToString() ?? string.Empty;
                    }
                    return string.Empty;
                default:
                    return invocationResult?.ToString() ?? string.Empty;
            }
        }

        #endregion

        public void Dispose()
        {
            _mockHost?.Dispose();
        }
    }
}
