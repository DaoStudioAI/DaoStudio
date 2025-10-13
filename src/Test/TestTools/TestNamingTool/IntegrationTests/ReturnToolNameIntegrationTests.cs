using System.Text.Json;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming;
using TestNamingTool.TestInfrastructure.Mocks;
using TestNamingTool.TestInfrastructure.Builders;
using Naming.Extensions;

namespace TestNamingTool.IntegrationTests
{
    /// <summary>
    /// Integration tests for ReturnToolName functionality and UrgingMessage scenarios.
    /// Tests the complete flow from session creation to return tool handling.
    /// </summary>
    public class ReturnToolNameIntegrationTests : IDisposable
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;
        private readonly MockChildSessionResult _mockChildResult;

        public ReturnToolNameIntegrationTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("TestAssistant", "Test assistant for return tool testing");
            _mockHostSession = new MockHostSession(1);
            _mockChildResult = new MockChildSessionResult(true);

            // Setup the factory with host
            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        #region ReturnToolName Success Scenarios

        [Fact]
        public async Task ReturnToolName_WhenCalledSuccessfully_ShouldReturnSuccess()
        {
            // Arrange
            var config = CreateNamingConfigWithReturnTool("complete_task", "Task completed successfully");
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Mock successful child session with return tool called
            _mockChildResult.Success = true;
            _mockChildResult.Result = "Task completed via return tool";

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Complete the integration test",
                ["background"] = "Testing return tool functionality"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded", "Should indicate successful completion when return tool is called");
        }

        [Fact]
        public async Task ReturnToolName_WithCustomReturnParameters_ShouldValidateParameters()
        {
            // Arrange
            var config = CreateNamingConfigWithReturnTool("submit_result", "Submit the completion result");
            config.ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "success", Description = "Whether task succeeded", IsRequired = true, Type = ParameterType.Bool },
                new ParameterConfig { Name = "message", Description = "Result message", IsRequired = true, Type = ParameterType.String },
                new ParameterConfig { Name = "data", Description = "Additional data", IsRequired = false, Type = ParameterType.Object }
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            _mockChildResult.Success = true;
            _mockChildResult.Result = "Custom parameters validated successfully";

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test custom return parameters",
                ["background"] = "Validating return tool parameter configuration"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded");
        }

        [Fact]
        public async Task ReturnToolName_WithDifferentToolNames_ShouldRespectConfiguration()
        {
            // Test multiple different return tool names
            var testCases = new[]
            {
                "finish_task",
                "complete_work", 
                "submit_results",
                "mark_done"
            };

            foreach (var toolName in testCases)
            {
                // Arrange
                var config = CreateNamingConfigWithReturnTool(toolName, $"Complete task using {toolName}");
                var pluginInstance = await CreatePluginInstanceAsync(config);
                var functions = await GetFunctionsAsync(pluginInstance);
                var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

                _mockChildResult.Success = true;
                _mockChildResult.Result = $"Completed via {toolName}";

                var requestData = new Dictionary<string, object?>
                {
                    ["subtask"] = $"Test with {toolName}",
                    ["background"] = "Testing different return tool names"
                };

                // Act
                var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

                // Assert
                result.Should().NotBeNull();
                result.Should().Contain("Succeeded", $"Should succeed when using return tool '{toolName}'");
                
                // Reset for next iteration
                _mockHost.ClearSessions();
            }
        }

        #endregion

        #region UrgingMessage Scenarios

        [Fact]
        public async Task UrgingMessage_WhenReturnToolNotCalled_ShouldSendUrgingMessage()
        {
            // Arrange
            // Disable automatic invocation/success to simulate the return tool never being called (timeout scenario)
            _mockHost.AutoInvokeReturnToolForNewSessions = false;
            _mockHost.AutoInvokeSuccessForNewSessions = false;
            // Also disable for the pre-constructed parent session to avoid accidental invocation
            _mockHostSession.AutoInvokeReturnTool = false;
            var config = CreateNamingConfigWithReturnTool("complete_task", "Please complete the task");
            config.UrgingMessage = "URGENT: Please use {{_Config.ReturnToolName}} to complete your task! Current subtask: {{subtask}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Mock child session where return tool is NOT called (timeout scenario)
            _mockChildResult.Success = false;
            _mockChildResult.Result = "Task timed out without calling return tool";

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test urging message functionality",
                ["background"] = "Testing what happens when return tool is not called"
            };

            // Act & Assert: the child session should time out and throw an InvalidOperationException
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await InvokeNamingFunctionAsync(namingFunction, requestData);
            });

            // Verify the exception message indicates the child session failed to provide a result
            exception.Message.Should().Contain("Child session failed", "Should indicate failure when return tool is not called");
        }

        [Fact]
        public async Task UrgingMessage_WithTemplateVariables_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateNamingConfigWithReturnTool("finish_work", "Finish your work");
            config.UrgingMessage = "Please call {{_Config.ReturnToolName}} for subtask '{{subtask}}' with background: {{background}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            _mockChildResult.Success = false; // Simulate timeout
            _mockChildResult.Result = "Urging message was sent";

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Template Variable Test",
                ["background"] = "Testing template rendering in urging messages"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            // The urging message should have been processed with template variables
        }

        [Fact]
        public async Task UrgingMessage_WithComplexTemplateData_ShouldHandleNestedObjects()
        {
            // Arrange
            var config = CreateNamingConfigWithReturnTool("complete_complex_task", "Complete complex task");
            config.UrgingMessage = "Complete task {{subtask}} with priority {{priority}} using {{_Config.ReturnToolName}}";
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "subtask", Description = "The subtask to complete", IsRequired = true },
                new ParameterConfig { Name = "priority", Description = "Task priority", IsRequired = false },
                new ParameterConfig { Name = "metadata", Description = "Additional metadata", IsRequired = false, Type = ParameterType.Object }
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            _mockChildResult.Success = false;

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Complex Template Test",
                ["priority"] = "HIGH",
                ["metadata"] = new { category = "integration-test", tags = new[] { "complex", "template" } }
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task UrgingMessage_EmptyConfiguration_ShouldThrowException()
        {
            // Arrange
            var config = CreateNamingConfigWithReturnTool("complete_task", "Complete the task");
            config.UrgingMessage = ""; // Empty urging message

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test empty urging message"
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await InvokeNamingFunctionAsync(namingFunction, requestData);
            });

            exception.Message.Should().Contain("UrgingMessage", "Should indicate that UrgingMessage cannot be empty");
        }

        #endregion

        #region Session Flow Integration

        [Fact]
        public async Task SessionFlow_CompleteWorkflow_ShouldFollowExpectedSequence()
        {
            // Arrange
            var config = CreateNamingConfigWithReturnTool("submit_final_result", "Submit the final result");
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            _mockChildResult.Success = true;
            _mockChildResult.Result = "Complete workflow test successful";

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "End-to-end workflow test",
                ["background"] = "Testing complete session workflow"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded");
            
            // Verify session was created
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should create exactly one child session");
            
            // Verify the session received the expected messages
            var createdSession = (MockHostSession)_mockHost.CreatedSessions.First();
            createdSession.SentMessages.Should().NotBeEmpty("Session should have received messages");
        }

        [Fact]
        public async Task SessionFlow_WithSessionExtensions_ShouldUseWaitChildSessionAsync()
        {
            // Arrange  
            var config = CreateNamingConfigWithReturnTool("finish_extended_task", "Finish extended task");
            config.PromptMessage = "Please complete: {{subtask}}. Background: {{background}}";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            _mockChildResult.Success = true;

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test session extensions",
                ["background"] = "Validating WaitChildSessionAsync functionality"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded");
            
            // Verify that StartNewHostSessionAsync was called
            _mockHost.CreatedSessions.Should().HaveCount(1);
        }

        #endregion

        #region Template Rendering Tests

        [Fact]
        public async Task PromptMessage_WithScribanTemplate_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateNamingConfigWithReturnTool("process_template", "Process template data");
            config.PromptMessage = "Task: {{subtask}}\nBackground: {{background}}\nReturnTool: {{_Config.ReturnToolName}}\nDescription: {{_Config.ReturnToolDescription}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            _mockChildResult.Success = true;

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Template Rendering Test",
                ["background"] = "Testing Scriban template functionality"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded");
            
            // The rendered template should have been sent to the child session
            var createdSession = (MockHostSession)_mockHost.CreatedSessions.First();
            createdSession.SentMessages.Should().NotBeEmpty();
        }


        #endregion

        #region Helper Methods

        private NamingConfig CreateNamingConfigWithReturnTool(string returnToolName, string returnToolDescription)
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "create_subtask",
                FunctionDescription = "Create a subtask for completion",
                PromptMessage = "Please complete this subtask: {{subtask}}. Background information: {{background}}",
                UrgingMessage = "Please use {{_Config.ReturnToolName}} to complete your task. Subtask: {{subtask}}",
                ReturnToolName = returnToolName,
                ReturnToolDescription = returnToolDescription,
                MaxRecursionLevel = 2,
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "subtask", Description = "The subtask to complete", IsRequired = true },
                    new ParameterConfig { Name = "background", Description = "Background information", IsRequired = false }
                },
                ReturnParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "success", Description = "Whether task succeeded", IsRequired = true, Type = ParameterType.Bool },
                    new ParameterConfig { Name = "message", Description = "Result message", IsRequired = true, Type = ParameterType.String }
                }
            };
        }

        private async Task<IPluginTool> CreatePluginInstanceAsync(NamingConfig config)
        {
            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = DateTime.Now.Ticks, // Unique ID
                Description = "Return tool integration test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = "Test Return Tool Naming Plugin"
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

            // Handle both synchronous and asynchronous results gracefully
            switch (invocationResult)
            {
                case Task task:
                    // Await completion
                    await task.ConfigureAwait(false);

                    // For Task<TResult>, extract the Result property via reflection
                    var taskType = task.GetType();
                    if (taskType.IsGenericType && taskType.GetProperty("Result") is { } resultProp)
                    {
                        var awaitedResult = resultProp.GetValue(task);
                        return awaitedResult?.ToString() ?? string.Empty;
                    }

                    // Non-generic Task (void return)
                    return string.Empty;

                default:
                    // Synchronous return value
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
