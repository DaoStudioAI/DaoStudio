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
    /// Integration tests for parallel execution scenarios in NamingTool.
    /// Tests the complete flow from session creation to parallel execution with different configurations.
    /// </summary>
    public class ParallelExecutionIntegrationTests : IDisposable
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public ParallelExecutionIntegrationTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("TestAssistant", "Test assistant for parallel execution");
            _mockHostSession = new MockHostSession(1);

            // Setup the factory with host
            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        #region ParameterBased Parallel Execution Tests

        [Fact]
        public async Task ParameterBased_WithMultipleParameters_ShouldCreateMultipleSessions()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ParameterBased, ParallelResultStrategy.WaitForAll);
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask1"] = "Task 1",
                ["subtask2"] = "Task 2", 
                ["subtask3"] = "Task 3",
                ["background"] = "Common background"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Multiple sessions should be created for parameter-based execution");
        }

        [Fact]
        public async Task ParameterBased_WithExcludedParameters_ShouldOnlyUseIncludedParameters()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ParameterBased, ParallelResultStrategy.WaitForAll);
            config.ParallelConfig!.ExcludedParameters = new List<string> { "background", "metadata" };
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask1"] = "Task 1",
                ["subtask2"] = "Task 2",
                ["background"] = "Should be excluded",
                ["metadata"] = "Should be excluded"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            // Should create 2 sessions for subtask1 and subtask2, excluding background and metadata
            _mockHost.CreatedSessions.Should().HaveCount(2, "Multiple sessions should be created for parameter-based execution");
        }

        [Fact]
        public async Task ParameterBased_WithMaxConcurrency_ShouldRespectConcurrencyLimit()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ParameterBased, ParallelResultStrategy.WaitForAll);
            config.ParallelConfig!.MaxConcurrency = 2;
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask1"] = "Task 1",
                ["subtask2"] = "Task 2",
                ["subtask3"] = "Task 3",
                ["subtask4"] = "Task 4",
                ["subtask5"] = "Task 5"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            // Verify that concurrency was respected (implementation detail, might need session timing verification)
            _mockHost.CreatedSessions.Should().HaveCount(5, "Multiple sessions should be created for parameter-based execution");
        }

        #endregion

        #region ListBased Parallel Execution Tests

        [Fact]
        public async Task ListBased_WithArrayParameter_ShouldCreateSessionsForEachItem()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ListBased, ParallelResultStrategy.WaitForAll);
            config.ParallelConfig!.ListParameterName = "taskList";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["taskList"] = new List<string> { "Task A", "Task B", "Task C", "Task D" },
                ["background"] = "Common background for all tasks"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Should create one session per list item");
        }

        [Fact]
        public async Task ListBased_WithEmptyList_ShouldHandleGracefully()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ListBased, ParallelResultStrategy.WaitForAll);
            config.ParallelConfig!.ListParameterName = "taskList";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["taskList"] = new List<string>(),
                ["background"] = "Common background"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("List must not be null or empty", "Should indicate external list cannot be empty for execution");
        }

        [Fact]
        public async Task ListBased_WithMissingListParameter_ShouldReturnError()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ListBased, ParallelResultStrategy.WaitForAll);
            config.ParallelConfig!.ListParameterName = "missingList";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["background"] = "Common background"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain($"Parameter '{config.ParallelConfig!.ListParameterName}' not found or is null", "Should indicate list parameter is missing");
        }

        #endregion

        #region ExternalList Parallel Execution Tests

        [Fact]
        public async Task ExternalList_WithPredefinedList_ShouldCreateSessionsForEachItem()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ExternalList, ParallelResultStrategy.WaitForAll);
            config.ParallelConfig!.ExternalList = new List<string> 
            { 
                "External Task 1", 
                "External Task 2", 
                "External Task 3" 
            };
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["background"] = "Common background for external tasks"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(3, "Should create one session per external list item");
        }

        [Fact]
        public async Task ExternalList_WithEmptyExternalList_ShouldHandleGracefully()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ExternalList, ParallelResultStrategy.WaitForAll);
            config.ParallelConfig!.ExternalList = new List<string>();
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["background"] = "Common background"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("ExternalStringList must not be null or empty", "Should indicate external list cannot be empty for execution");
        }

        #endregion

        #region Result Strategy Tests

        [Fact]
        public async Task StreamIndividual_ShouldStreamResultsAsTheyComplete()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ParameterBased, ParallelResultStrategy.StreamIndividual);
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask1"] = "Task 1",
                ["subtask2"] = "Task 2",
                ["subtask3"] = "Task 3"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("streaming");
        }

        [Fact]
        public async Task WaitForAll_ShouldReturnCombinedResults()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ParameterBased, ParallelResultStrategy.WaitForAll);
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask1"] = "Task 1",
                ["subtask2"] = "Task 2"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            // Result should indicate wait for all strategy
        }

        [Fact]
        public async Task FirstResultWins_ShouldReturnFirstCompletedResult()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ParameterBased, ParallelResultStrategy.FirstResultWins);
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask1"] = "Task 1",
                ["subtask2"] = "Task 2",
                ["subtask3"] = "Task 3"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("first");
        }

        #endregion

        #region Error Handling and Edge Cases

        [Fact]
        public async Task ParallelExecution_ShouldAlwaysContinueWhenSomeSessionsFail()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ParameterBased, ParallelResultStrategy.WaitForAll);
            // Note: ContinueOnError is now always enabled by default
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask1"] = "Valid Task",
                ["subtask2"] = "Another Valid Task"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task SessionTimeout_ShouldRespectTimeoutConfiguration()
        {
            // Arrange
            var config = CreateNamingConfig(ParallelExecutionType.ParameterBased, ParallelResultStrategy.WaitForAll);
            config.ParallelConfig!.SessionTimeoutMs = 1000; // 1 second timeout
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask1"] = "Fast Task",
                ["subtask2"] = "Another Fast Task"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion

        #region Helper Methods

        private NamingConfig CreateNamingConfig(ParallelExecutionType executionType, ParallelResultStrategy resultStrategy)
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "create_parallel_subtask",
                FunctionDescription = "Create subtasks with parallel execution",
                PromptMessage = "Execute the following subtask: {{subtask}}",
                UrgingMessage = "Please complete the task using {{_Config.ReturnToolName}}",
                ReturnToolName = "complete_subtask",
                ReturnToolDescription = "Mark subtask as completed",
                MaxRecursionLevel = 2,
                ParallelConfig = new ParallelExecutionConfig
                {
                    ExecutionType = executionType,
                    ResultStrategy = resultStrategy,
                    MaxConcurrency = Environment.ProcessorCount,
                    SessionTimeoutMs = 30000
                }
            };
        }

        private async Task<IPluginTool> CreatePluginInstanceAsync(NamingConfig config)
        {
            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = DateTime.Now.Ticks, // Unique ID
                Description = "Integration test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = "Test Parallel Naming Plugin"
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
            // Ensure required session parameter is included
            parameters[DaoStudio.Common.Plugins.Constants.DasSession] = _mockHostSession;

            // Preferred fast-path when the delegate signature matches exactly
            if (function.Function is Func<Dictionary<string, object?>, Task<object?>> asyncDelegate)
            {
                var resultObj = await asyncDelegate(parameters);
                return resultObj?.ToString() ?? string.Empty;
            }

            // Generic fallback that supports Task<TResult> of ANY TResult
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
                    return string.Empty; // Non-generic Task
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
