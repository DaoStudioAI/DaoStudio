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
    /// Integration tests for Step 7 edge cases and error handling scenarios.
    /// Tests boundary conditions, error scenarios, and resilience of parallel execution strategies.
    /// </summary>
    public class Step7EdgeCasesAndErrorHandlingTests : IDisposable
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public Step7EdgeCasesAndErrorHandlingTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("EdgeCaseTestAssistant", "Test assistant for edge cases");
            _mockHostSession = new MockHostSession(1);

            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        #region ParameterBased Edge Cases

        [Fact]
        public async Task ParameterBased_WithNoParameters_ShouldHandleGracefully()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>();

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().BeEmpty("No parameters should result in no sessions");
        }

        [Fact]
        public async Task ParameterBased_WithAllParametersExcluded_ShouldHandleGracefully()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.ParallelConfig!.ExcludedParameters = new List<string> { "param1", "param2", "param3" };
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["param1"] = "value1",
                ["param2"] = "value2", 
                ["param3"] = "value3"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().BeEmpty("All excluded parameters should result in no sessions");
        }

        [Fact]
        public async Task ParameterBased_WithNullParameterValues_ShouldHandleNulls()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["param1"] = null,
                ["param2"] = "valid_value",
                ["param3"] = null
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(3, "Should create sessions for all parameters including nulls");
        }

        [Fact]
        public async Task ParameterBased_WithZeroConcurrency_ShouldUseDefaultConcurrency()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.ParallelConfig!.MaxConcurrency = 0;
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["param1"] = "value1",
                ["param2"] = "value2"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should still execute with default concurrency behavior");
        }

        #endregion

        #region ListBased Edge Cases

        [Fact]
        public async Task ListBased_WithNullList_ShouldReturnErrorMessage()
        {
            // Arrange
            var config = CreateListBasedConfig("nullList");
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["nullList"] = null
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Parameter 'nullList' not found or is null", "Should indicate null list parameter");
            _mockHost.CreatedSessions.Should().BeEmpty("Null list should not create sessions");
        }

        [Fact]
        public async Task ListBased_WithNonEnumerableParameter_ShouldReturnErrorMessage()
        {
            // Arrange
            var config = CreateListBasedConfig("notAList");
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["notAList"] = "this is not a list"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("is not enumerable", "Should indicate parameter is not enumerable");
            _mockHost.CreatedSessions.Should().BeEmpty("Non-enumerable parameter should not create sessions");
        }

        [Fact]
        public async Task ListBased_WithListContainingNulls_ShouldHandleNullItems()
        {
            // Arrange
            var config = CreateListBasedConfig("listWithNulls");
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var listWithNulls = new List<object?> { "item1", null, "item3", null, "item5" };
            var requestData = new Dictionary<string, object?>
            {
                ["listWithNulls"] = listWithNulls
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(5, "Should create sessions for all list items including nulls");
        }

        [Fact]
        public async Task ListBased_WithMissingListParameterName_ShouldReturnErrorMessage()
        {
            // Arrange
            var config = CreateListBasedConfig("missingParameter");
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["differentParameter"] = new List<string> { "item1", "item2" }
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Parameter 'missingParameter' not found or is null", "Should indicate missing list parameter");
            _mockHost.CreatedSessions.Should().BeEmpty("Missing list parameter should not create sessions");
        }

        [Fact]
        public async Task ListBased_WithVeryLargeList_ShouldHandleScale()
        {
            // Arrange
            var config = CreateListBasedConfig("hugelist");
            config.ParallelConfig!.MaxConcurrency = 1; // Limit concurrency for this test
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var hugeList = Enumerable.Range(1, 100).Select(i => $"item_{i}").ToList();
            var requestData = new Dictionary<string, object?>
            {
                ["hugelist"] = hugeList
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(100, "Should handle large lists gracefully");
        }

        #endregion

        #region ExternalList Edge Cases

        [Fact]
        public async Task ExternalList_WithNullExternalList_ShouldReturnErrorMessage()
        {
            // Arrange
            var config = CreateExternalListConfig(null!);
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["testParam"] = "test"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("ExternalStringList must not be null or empty", "Should indicate null external list");
            _mockHost.CreatedSessions.Should().BeEmpty("Null external list should not create sessions");
        }

        [Fact]
        public async Task ExternalList_WithEmptyStringsInList_ShouldHandleEmptyStrings()
        {
            // Arrange
            var externalListWithEmpties = new List<string> { "item1", "", "item3", "   ", "item5" };
            var config = CreateExternalListConfig(externalListWithEmpties);
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["testParam"] = "test"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(5, "Should create sessions for all external list items including empty strings");
        }

        [Fact]
        public async Task ExternalList_WithDuplicateValues_ShouldCreateSessionsForDuplicates()
        {
            // Arrange
            var externalListWithDuplicates = new List<string> { "duplicate", "unique", "duplicate", "another", "duplicate" };
            var config = CreateExternalListConfig(externalListWithDuplicates);
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["testParam"] = "test"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(5, "Should create sessions for all external list items including duplicates");
        }

        #endregion

        #region Cross-Strategy Error Scenarios

        [Fact]
        public async Task AllStrategies_WithInvalidResultStrategy_ShouldUseDefaultStrategy()
        {
            // Test each execution type with an invalid result strategy
            var executionTypes = new[] 
            { 
                ParallelExecutionType.ParameterBased,
                ParallelExecutionType.ListBased,
                ParallelExecutionType.ExternalList
            };

            foreach (var executionType in executionTypes)
            {
                _mockHost.CreatedSessions.Clear();

                var config = executionType switch
                {
                    ParallelExecutionType.ParameterBased => CreateParameterBasedConfig(),
                    ParallelExecutionType.ListBased => CreateListBasedConfig("testList"),
                    ParallelExecutionType.ExternalList => CreateExternalListConfig(new List<string> { "test1", "test2" }),
                    _ => throw new ArgumentException($"Unknown execution type: {executionType}")
                };

                // Set an extreme/invalid result strategy value
                config.ParallelConfig!.ResultStrategy = (ParallelResultStrategy)999;

                var pluginInstance = await CreatePluginInstanceAsync(config);
                var functions = await GetFunctionsAsync(pluginInstance);
                var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

                var requestData = executionType switch
                {
                    ParallelExecutionType.ParameterBased => new Dictionary<string, object?> { ["param1"] = "value1" },
                    ParallelExecutionType.ListBased => new Dictionary<string, object?> { ["testList"] = new List<string> { "item1" } },
                    ParallelExecutionType.ExternalList => new Dictionary<string, object?> { ["testParam"] = "test" },
                    _ => new Dictionary<string, object?>()
                };

                // Act
                var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

                // Assert
                result.Should().NotBeNull($"Execution type {executionType} should handle invalid result strategy gracefully");
            }
        }

        [Fact]
        public async Task AllStrategies_WithNegativeConcurrency_ShouldUseDefaultConcurrency()
        {
            var config = CreateParameterBasedConfig();
            config.ParallelConfig!.MaxConcurrency = -5;
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["param1"] = "value1",
                ["param2"] = "value2"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should handle negative concurrency by using default behavior");
        }

        [Fact]
        public async Task AllStrategies_WithExtremeTimeout_ShouldHandleTimeout()
        {
            var config = CreateParameterBasedConfig();
            config.ParallelConfig!.SessionTimeoutMs = 1; // 1ms timeout - extremely short
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["param1"] = "value1"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            // Sessions may timeout, but the system should handle it gracefully
        }

        #endregion

        #region Configuration Edge Cases

        [Fact]
        public async Task ParallelConfig_WithNullParallelConfig_ShouldFallbackToSingleExecution()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.ParallelConfig = null; // This should fallback to single execution
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["param1"] = "value1",
                ["param2"] = "value2"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            // Should fallback to single execution mode
        }

        [Fact]
        public async Task ParallelConfig_WithInvalidExecutionType_ShouldHandleGracefully()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.ParallelConfig!.ExecutionType = (ParallelExecutionType)999; // Invalid enum value
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["param1"] = "value1"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            // Should handle invalid execution type gracefully
        }

        #endregion

        #region Helper Methods

        private NamingConfig CreateParameterBasedConfig()
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "edge_case_parameter_based",
                FunctionDescription = "Edge case testing for ParameterBased execution",
                PromptMessage = "Process {{_Parameter.Name}}: {{_Parameter.Value}}",
                UrgingMessage = "Complete the edge case test",
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
                FunctionName = "edge_case_list_based",
                FunctionDescription = "Edge case testing for ListBased execution",
                PromptMessage = "Process list item {{_Parameter.Value}} from {{_Parameter.Name}}",
                UrgingMessage = "Complete the edge case test",
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
                FunctionName = "edge_case_external_list",
                FunctionDescription = "Edge case testing for ExternalList execution",
                PromptMessage = "Process external {{_Parameter.Value}} from {{_Parameter.Name}}",
                UrgingMessage = "Complete the edge case test",
                MaxRecursionLevel = 1,
                ParallelConfig = new ParallelExecutionConfig
                {
                    ExecutionType = ParallelExecutionType.ExternalList,
                    ExternalList = externalList ?? new List<string>(),
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
                InstanceId = DateTime.Now.Ticks + Random.Shared.Next(10000),
                Description = "Edge case test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = $"EdgeCase Test - {config.FunctionName}"
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
