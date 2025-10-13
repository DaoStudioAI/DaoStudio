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
    /// Integration tests for various NamingConfig configurations and validation scenarios.
    /// Tests the complete workflow with different configuration variations.
    /// </summary>
    public class ConfigurationIntegrationTests : IDisposable
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public ConfigurationIntegrationTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("ConfigTestAssistant", "Test assistant for configuration testing");
            _mockHostSession = new MockHostSession(1);

            // Setup the factory with host
            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        #region Input Parameter Configuration Tests

        [Fact]
        public async Task InputParameters_CustomConfiguration_ShouldValidateCorrectly()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "custom_task_processor",
                FunctionDescription = "Process tasks with custom parameters",
                PromptMessage = "Process {{taskName}} with priority {{priority}} and deadline {{deadline}}",
                UrgingMessage = "Complete using {{_Config.ReturnToolName}}",
                ReturnToolName = "submit_task_result",
                MaxRecursionLevel = 2,
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "taskName", Description = "Name of the task", IsRequired = true, Type = ParameterType.String },
                    new ParameterConfig { Name = "priority", Description = "Task priority level", IsRequired = true, Type = ParameterType.String },
                    new ParameterConfig { Name = "deadline", Description = "Task deadline", IsRequired = false, Type = ParameterType.String },
                    new ParameterConfig { Name = "metadata", Description = "Additional metadata", IsRequired = false, Type = ParameterType.Object }
                }
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestDataValid = new Dictionary<string, object?>
            {
                ["taskName"] = "Integration Test Task",
                ["priority"] = "HIGH",
                ["deadline"] = "2024-12-31",
                ["metadata"] = new { category = "test", tags = new[] { "integration", "config" } }
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestDataValid);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded", "Should succeed with valid required parameters");
        }

        [Fact]
        public async Task InputParameters_MissingRequiredParameter_ShouldReturnValidationError()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "strict_task_processor",
                FunctionDescription = "Process tasks with strict parameter validation",
                PromptMessage = "Process {{taskName}} with {{requiredField}}",
                UrgingMessage = "Complete using {{_Config.ReturnToolName}}",
                ReturnToolName = "submit_strict_result",
                MaxRecursionLevel = 1,
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "taskName", Description = "Name of the task", IsRequired = true },
                    new ParameterConfig { Name = "requiredField", Description = "A required field", IsRequired = true }
                }
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestDataMissing = new Dictionary<string, object?>
            {
                ["taskName"] = "Test Task"
                // Missing requiredField
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestDataMissing);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("required", "Should indicate missing required parameters");
        }

        [Fact]
        public async Task InputParameters_ComplexNestedObjects_ShouldHandleCorrectly()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "complex_data_processor",
                FunctionDescription = "Process complex nested data structures",
                PromptMessage = "Process {{taskData.name}} with config {{taskData.config.setting}}",
                UrgingMessage = "Complete using {{_Config.ReturnToolName}}",
                ReturnToolName = "submit_complex_result",
                MaxRecursionLevel = 1,
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig 
                    { 
                        Name = "taskData", 
                        Description = "Complex task data", 
                        IsRequired = true, 
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true },
                            new ParameterConfig { 
                                Name = "config", 
                                Type = ParameterType.Object, 
                                IsRequired = false,
                                ObjectProperties = new List<ParameterConfig> {
                                    new ParameterConfig { Name = "setting", Type = ParameterType.String }
                                }
                            }
                        }
                    }
                }
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var complexData = new
            {
                name = "Complex Test Task",
                config = new
                {
                    setting = "advanced",
                    options = new[] { "opt1", "opt2" }
                },
                metadata = new { version = "1.0" }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["taskData"] = complexData
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded", "Should handle complex nested objects");
        }

        #endregion

        #region Return Parameter Configuration Tests

        [Fact]
        public async Task ReturnParameters_CustomConfiguration_ShouldCreateCorrectReturnTool()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "custom_return_processor",
                FunctionDescription = "Process with custom return parameters",
                PromptMessage = "Complete the task: {{task}}",
                UrgingMessage = "Use {{_Config.ReturnToolName}} to return results",
                ReturnToolName = "custom_result_submitter",
                ReturnToolDescription = "Submit results with custom format",
                MaxRecursionLevel = 1,
                ReturnParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "isComplete", Description = "Whether task is complete", IsRequired = true, Type = ParameterType.Bool },
                    new ParameterConfig { Name = "resultData", Description = "Result data", IsRequired = true, Type = ParameterType.Object },
                    new ParameterConfig { Name = "confidence", Description = "Confidence score", IsRequired = false, Type = ParameterType.Number },
                    new ParameterConfig 
                    { 
                        Name = "tags", 
                        Description = "Result tags", 
                        IsRequired = false, 
                        Type = ParameterType.Array,
                        ArrayElementConfig = new ParameterConfig { Type = ParameterType.String }
                    }
                }
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["task"] = "Test custom return parameters"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded", "Should succeed with custom return parameters");
        }

        [Fact]
        public async Task ReturnParameters_ArrayConfiguration_ShouldHandleArrayTypes()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "array_result_processor",
                FunctionDescription = "Process tasks that return arrays",
                PromptMessage = "Generate list for: {{request}}",
                UrgingMessage = "Submit results using {{_Config.ReturnToolName}}",
                ReturnToolName = "submit_array_results",
                MaxRecursionLevel = 1,
                ReturnParameters = new List<ParameterConfig>
                {
                    new ParameterConfig 
                    { 
                        Name = "items", 
                        Description = "Array of result items", 
                        IsRequired = true, 
                        Type = ParameterType.Array,
                        ArrayElementConfig = new ParameterConfig 
                        { 
                            Type = ParameterType.Object,
                            ObjectProperties = new List<ParameterConfig> {
                                new ParameterConfig { Name = "id", Type = ParameterType.String, IsRequired = true },
                                new ParameterConfig { Name = "value", Type = ParameterType.String, IsRequired = true },
                                new ParameterConfig { Name = "score", Type = ParameterType.Number, IsRequired = false }
                            }
                        }
                    }
                }
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["request"] = "Generate test items with complex structure"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded", "Should handle complex array return types");
        }

        #endregion

        #region Parallel Configuration Variations

        [Fact]
        public async Task ParallelConfig_AllExecutionTypes_ShouldWorkWithDifferentConfigurations()
        {
            var executionTypes = new[] 
            { 
                ParallelExecutionType.ParameterBased,
                ParallelExecutionType.ListBased,
                ParallelExecutionType.ExternalList
            };

            foreach (var executionType in executionTypes)
            {
                // Arrange
                var config = CreateParallelConfig(executionType);
                var pluginInstance = await CreatePluginInstanceAsync(config);
                var functions = await GetFunctionsAsync(pluginInstance);
                var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

                var requestData = CreateRequestDataForExecutionType(executionType);

                // Act
                var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

                // Assert
                result.Should().NotBeNull($"Should work with {executionType} execution type");
                
                // Reset for next iteration
                _mockHost.ClearSessions();
            }
        }

        [Fact]
        public async Task ParallelConfig_AllResultStrategies_ShouldProduceCorrectOutput()
        {
            var resultStrategies = new[] 
            { 
                ParallelResultStrategy.StreamIndividual,
                ParallelResultStrategy.WaitForAll,
                ParallelResultStrategy.FirstResultWins
            };

            foreach (var strategy in resultStrategies)
            {
                // Arrange
                var config = new NamingConfig
                {
                    ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                    FunctionName = $"test_{strategy.ToString().ToLower()}",
                    FunctionDescription = $"Test {strategy} result strategy",
                    PromptMessage = "Process: {{task}}",
                    UrgingMessage = "Complete using {{_Config.ReturnToolName}}",
                    ReturnToolName = "submit_result",
                    MaxRecursionLevel = 1,
                    ParallelConfig = new ParallelExecutionConfig
                    {
                        ExecutionType = ParallelExecutionType.ParameterBased,
                        ResultStrategy = strategy,
                        MaxConcurrency = 2
                    }
                };

                var pluginInstance = await CreatePluginInstanceAsync(config);
                var functions = await GetFunctionsAsync(pluginInstance);
                var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

                var requestData = new Dictionary<string, object?>
                {
                    ["task1"] = $"Test task for {strategy}",
                    ["task2"] = $"Another task for {strategy}"
                };

                // Act
                var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

                // Assert
                result.Should().NotBeNull($"Should work with {strategy} result strategy");
                
                // Reset for next iteration
                _mockHost.ClearSessions();
            }
        }

        #endregion

        #region Template Configuration Tests

        [Fact]
        public async Task TemplateConfiguration_ComplexScribanTemplates_ShouldRenderCorrectly()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "template_processor",
                FunctionDescription = "Process with complex Scriban templates",
                PromptMessage = @"
Task: {{task.name}}
Priority: {{task.priority}}
Due: {{task.dueDate}}
{{~ if task.subtasks ~}}
Subtasks:
{{~ for subtask in task.subtasks ~}}
- {{subtask.name}}: {{subtask.status}}
{{~ end ~}}
{{~ end ~}}
Use {{_Config.ReturnToolName}} when complete.",
                UrgingMessage = "Please complete {{task.name}} and use {{_Config.ReturnToolName}}!",
                ReturnToolName = "complete_templated_task",
                MaxRecursionLevel = 1,
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "task", Description = "Complex task object", IsRequired = true, Type = ParameterType.Object }
                }
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var complexTask = new
            {
                name = "Complex Template Test",
                priority = "HIGH",
                dueDate = "2024-12-31",
                subtasks = new[]
                {
                    new { name = "Subtask 1", status = "pending" },
                    new { name = "Subtask 2", status = "in-progress" },
                    new { name = "Subtask 3", status = "completed" }
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["task"] = complexTask
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded", "Should handle complex Scriban templates");
            
            // Verify that the template was processed by checking session messages
            _mockHost.CreatedSessions.Should().HaveCount(1);
            var session = (MockHostSession)_mockHost.CreatedSessions.First();
            session.SentMessages.Should().NotBeEmpty("Template should have been rendered and sent");
        }


        #endregion

        #region Configuration Edge Cases

        [Fact]
        public async Task Configuration_MinimalValid_ShouldWorkWithDefaults()
        {
            // Arrange - Minimal valid configuration
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                PromptMessage = "Simple task: {{task}}",
                UrgingMessage = "Please complete using {{_Config.ReturnToolName}}"
                // Using all other defaults
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["task"] = "Minimal configuration test"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded", "Should work with minimal valid configuration");
        }

        [Fact]
        public async Task Configuration_MaximalComplex_ShouldHandleAllFeatures()
        {
            // Arrange - Complex configuration with all features enabled
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "ultimate_task_processor",
                FunctionDescription = "Process tasks with all available features",
                PromptMessage = @"
Ultimate Task Processing:
{{~ for param in inputParams ~}}
{{param.name}}: {{param.value}}
{{~ end ~}}
Configuration: {{_Config.FunctionDescription}}
Return Tool: {{_Config.ReturnToolName}}",
                UrgingMessage = "URGENT: Complete {{taskName}} using {{_Config.ReturnToolName}} with priority {{priority}}!",
                ReturnToolName = "ultimate_task_completer",
                ReturnToolDescription = "Complete ultimate tasks with comprehensive results",
                MaxRecursionLevel = 3,
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "taskName", IsRequired = true, Type = ParameterType.String },
                    new ParameterConfig { Name = "priority", IsRequired = true, Type = ParameterType.String },
                    new ParameterConfig { Name = "complexData", IsRequired = false, Type = ParameterType.Object },
                    new ParameterConfig 
                    { 
                        Name = "itemList", 
                        IsRequired = false, 
                        Type = ParameterType.Array,
                        ArrayElementConfig = new ParameterConfig { Type = ParameterType.String }
                    }
                },
                ReturnParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "success", IsRequired = true, Type = ParameterType.Bool },
                    new ParameterConfig { Name = "message", IsRequired = true, Type = ParameterType.String },
                    new ParameterConfig { Name = "results", IsRequired = false, Type = ParameterType.Array }
                },
                ParallelConfig = new ParallelExecutionConfig
                {
                    ExecutionType = ParallelExecutionType.ParameterBased,
                    ResultStrategy = ParallelResultStrategy.WaitForAll,
                    MaxConcurrency = 4,
                    SessionTimeoutMs = 60000,
                    ExcludedParameters = new List<string> { "complexData" }
                }
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["taskName"] = "Ultimate Integration Test",
                ["priority"] = "CRITICAL",
                ["complexData"] = new { metadata = "should be excluded from parallel execution" },
                ["itemList"] = new[] { "item1", "item2", "item3" }
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded", "Should handle maximal complex configuration");
        }

        #endregion

        #region Helper Methods

        private NamingConfig CreateParallelConfig(ParallelExecutionType executionType)
        {
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = $"parallel_{executionType.ToString().ToLower()}",
                FunctionDescription = $"Test {executionType} parallel execution",
                PromptMessage = "Process parallel task: {{task}}",
                UrgingMessage = "Complete using {{_Config.ReturnToolName}}",
                ReturnToolName = "submit_parallel_result",
                MaxRecursionLevel = 1,
                ParallelConfig = new ParallelExecutionConfig
                {
                    ExecutionType = executionType,
                    ResultStrategy = ParallelResultStrategy.WaitForAll,
                    MaxConcurrency = 2
                }
            };

            if (executionType == ParallelExecutionType.ListBased)
            {
                config.ParallelConfig.ListParameterName = "taskList";
            }
            else if (executionType == ParallelExecutionType.ExternalList)
            {
                config.ParallelConfig.ExternalList = new List<string> { "External Task 1", "External Task 2" };
            }

            return config;
        }

        private Dictionary<string, object?> CreateRequestDataForExecutionType(ParallelExecutionType executionType)
        {
            return executionType switch
            {
                ParallelExecutionType.ParameterBased => new Dictionary<string, object?>
                {
                    ["task1"] = "Parameter-based task 1",
                    ["task2"] = "Parameter-based task 2"
                },
                ParallelExecutionType.ListBased => new Dictionary<string, object?>
                {
                    ["taskList"] = new List<string> { "List-based task 1", "List-based task 2" }
                },
                ParallelExecutionType.ExternalList => new Dictionary<string, object?>
                {
                    ["task"] = "External list execution test"
                },
                _ => new Dictionary<string, object?>
                {
                    ["task"] = "Default task"
                }
            };
        }

        private async Task<IPluginTool> CreatePluginInstanceAsync(NamingConfig config)
        {
            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = DateTime.Now.Ticks, // Unique ID
                Description = "Configuration integration test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = "Test Configuration Naming Plugin"
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
            // Add DasSession parameter as required by the naming function
            parameters[DaoStudio.Common.Plugins.Constants.DasSession] = _mockHostSession;
            
            // Cast delegate to correct type and invoke
            if (function.Function is Func<Dictionary<string, object?>, Task<object?>> asyncDelegate)
            {
                var resultObj = await asyncDelegate(parameters);
                return resultObj?.ToString() ?? string.Empty;
            }
            else
            {
                // Fallback to DynamicInvoke for other delegate types
                var invocationResult = function.Function.DynamicInvoke(parameters);

                // If the result is any Task (e.g., Task<string>, Task<int>), await it and extract the result via reflection
                if (invocationResult is Task task)
                {
                    await task.ConfigureAwait(false);

                    // Attempt to get the Result property via reflection for Task<TResult>
                    var taskType = task.GetType();
                    if (taskType.IsGenericType && taskType.GetProperty("Result") is { } resultProp)
                    {
                        var awaitedResult = resultProp.GetValue(task);
                        return awaitedResult?.ToString() ?? string.Empty;
                    }

                    // Non-generic Task (void return)
                    return string.Empty;
                }

                // Non-task synchronous result
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
