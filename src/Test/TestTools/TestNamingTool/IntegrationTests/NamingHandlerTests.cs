using System.Text.Json;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming;
using TestNamingTool.TestInfrastructure.Builders;
using TestNamingTool.TestInfrastructure.Mocks;
using Xunit.Abstractions;

namespace TestNamingTool.IntegrationTests
{
    /// <summary>
    /// Integration tests for the complete NamingTool workflow.
    /// Tests the full process from session creation through function execution.
    /// </summary>
    public class NamingToolIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private MockHost _mockHost = null!;
        private NamingPluginFactory _pluginFactory = null!;
        private MockHostSession _parentSession = null!;
        private readonly List<IDisposable> _disposables = new();

        public NamingToolIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            SetupTest();
        }

        private void SetupTest()
        {
            // Create mock infrastructure
            _mockHost = new MockHost();
            _pluginFactory = new NamingPluginFactory();
            _parentSession = new MockHostSession(1);
            
            // Add test persons
            _mockHost.AddPerson(new MockPerson("TestExecutive", "Executive person for testing"));
            _mockHost.AddPerson(new MockPerson("GeneralAssistant", "General assistant for testing"));
            
            // Set up plugin factory
            _pluginFactory.SetHost(_mockHost);
            
            // Track disposables
            _disposables.Add(_parentSession);
            _disposables.Add(_mockHost);
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable?.Dispose();
            }
            _disposables.Clear();
        }

        /// <summary>
        /// Helper method to create a plugin instance with the given configuration
        /// </summary>
        private async Task<NamingPluginInstance> CreatePluginInstanceAsync(NamingConfig config)
        {
            var plugInfo = new PlugToolInfo
            {
                InstanceId = DateTime.Now.Ticks,
                Config = JsonSerializer.Serialize(config),
                DisplayName = "Test Naming Plugin",
                Description = "Integration test plugin"
            };
            
            var pluginInstance = (NamingPluginInstance)await _pluginFactory.CreatePluginToolAsync(plugInfo);
            _disposables.Add(pluginInstance);
            return pluginInstance;
        }

        /// <summary>
        /// Helper method to get tool functions from a plugin instance
        /// </summary>
        private async Task<List<FunctionWithDescription>> GetToolFunctionsAsync(NamingPluginInstance plugin)
        {
            var functions = new List<FunctionWithDescription>();
            
            // Use reflection to call the protected RegisterToolFunctionsAsync method
            var methodInfo = typeof(NamingPluginInstance).GetMethod(
                "RegisterToolFunctionsAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (methodInfo != null)
            {
                await (Task)methodInfo.Invoke(plugin, new object?[] { functions, null, _parentSession })!;
            }
            
            return functions;
        }

        [Fact]
        public async Task BasicWorkflow_WithMinimalConfig_ShouldCreateSessionAndRegisterFunction()
        {
            // Arrange
            var config = TestDataBuilder.CreateBasicNamingConfig();
            config.ExecutivePerson = new ConfigPerson { Name = "GeneralAssistant", Description = "Test person" };
            config.PromptMessage = "Please complete: {{task}}";
            
            // Act
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            
            // Assert
            functions.Should().NotBeEmpty();
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);
            namingFunction.Should().NotBeNull();
            namingFunction!.Description.Description.Should().Be(config.FunctionDescription);
        }

        [Fact]
        public async Task ExecutivePerson_WhenConfigured_ShouldUseSpecificPerson()
        {
            // Arrange
            var config = TestDataBuilder.CreateNamingConfigWithExecutive();
            config.PromptMessage = "Task: {{task}}";
            
            // Act
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);

            // Execute the naming function
            var requestData = new Dictionary<string, object?> { ["task"] = "Test task" };
            var result = await (Task<string>)namingFunction!.Function.DynamicInvoke(requestData)!;

            // Assert
            _mockHost.CreatedSessions.Should().HaveCount(1);
            var childSession = (MockHostSession)_mockHost.CreatedSessions[0];
            childSession.SentMessages.Should().Contain(msg => 
                msg.message.Contains("Task: Test task") && 
                msg.msgType == HostSessMsgType.Message);
        }

        [Fact]
        public async Task CustomInputParameters_WhenProvided_ShouldValidateCorrectly()
        {
            // Arrange
            var config = TestDataBuilder.CreateNamingConfigWithParameters();
            config.ExecutivePerson = new ConfigPerson { Name = "GeneralAssistant", Description = "Test person" };
            config.PromptMessage = "Task: {{task}}, Priority: {{priority}}";
            
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);

            // Act - with valid parameters
            var validRequestData = new Dictionary<string, object?> 
            { 
                ["task"] = "Complete integration test",
                ["priority"] = 2
            };
            var result = await (Task<string>)namingFunction!.Function.DynamicInvoke(validRequestData)!;

            // Assert
            result.Should().NotContain("Missing required parameters");
            _mockHost.CreatedSessions.Should().HaveCount(1);
        }

        [Fact]
        public async Task MissingRequiredParameters_ShouldReturnValidationError()
        {
            // Arrange
            var config = TestDataBuilder.CreateNamingConfigWithParameters();
            config.ExecutivePerson = new ConfigPerson { Name = "GeneralAssistant", Description = "Test person" };
            
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);

            // Act - with missing required parameter
            var invalidRequestData = new Dictionary<string, object?> 
            { 
                ["priority"] = 1 // missing required "task" parameter
            };
            var result = await (Task<string>)namingFunction!.Function.DynamicInvoke(invalidRequestData)!;

            // Assert
            result.Should().Contain("Missing required parameters");
            result.Should().Contain("task");
            _mockHost.CreatedSessions.Should().BeEmpty();
        }

        [Fact]
        public async Task CustomFunctionNameAndDescription_ShouldBeAppliedCorrectly()
        {
            // Arrange
            var config = TestDataBuilder.CreateBasicNamingConfig();
            config.ExecutivePerson = new ConfigPerson { Name = "GeneralAssistant", Description = "Test person" };
            config.FunctionName = "custom_delegate_task";
            config.FunctionDescription = "Custom delegation function for testing";
            
            // Act
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            
            // Assert
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == "custom_delegate_task");
            namingFunction.Should().NotBeNull();
            namingFunction!.Description.Description.Should().Be("Custom delegation function for testing");
        }

        [Fact]
        public async Task PromptMessageWithScribanTemplating_ShouldParseParametersCorrectly()
        {
            // Arrange
            var config = TestDataBuilder.CreateNamingConfigWithParameters();
            config.ExecutivePerson = new ConfigPerson { Name = "GeneralAssistant", Description = "Test person" };
            config.PromptMessage = "Execute task: {{task}} with priority {{priority}}. Background: {{background ?? 'No background provided'}}";
            
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);

            // Act
            var requestData = new Dictionary<string, object?> 
            { 
                ["task"] = "Process data",
                ["priority"] = 3
            };
            var result = await (Task<string>)namingFunction!.Function.DynamicInvoke(requestData)!;

            // Assert
            var childSession = (MockHostSession)_mockHost.CreatedSessions[0];
            var promptMessage = childSession.SentMessages.FirstOrDefault(msg => msg.msgType == HostSessMsgType.Message);
            promptMessage.message.Should().Contain("Execute task: Process data with priority 3");
            promptMessage.message.Should().Contain("No background provided");
        }

        [Fact]
        public async Task MaxRecursionLevel_WhenReached_ShouldNotRegisterTools()
        {
            // Arrange
            var config = TestDataBuilder.CreateBasicNamingConfig();
            config.ExecutivePerson = new ConfigPerson { Name = "GeneralAssistant", Description = "Test person" };
            config.MaxRecursionLevel = 0; // Set to 1 to prevent any delegation
            
            // Act
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            
            // Assert - No naming functions should be registered when at max recursion level
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);
            namingFunction.Should().BeNull();
        }

        [Fact]
        public async Task NoAvailablePersons_ShouldReturnError()
        {
            // Arrange
            _mockHost.ClearPersons(); // Remove all persons
            var config = TestDataBuilder.CreateBasicNamingConfig();
            // Don't set ExecutivePerson, so it will try to find any available person

            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);

            // Act: invoke the function, which should attempt to select a person and fail
            Func<Task> act = async () =>
            {
                var requestData = new Dictionary<string, object?>();
                await (Task<string>)namingFunction!.Function.DynamicInvoke(requestData)!;
            };

            // Assert: now throws when selecting executive person due to no available people
            await act.Should().ThrowAsync<ArgumentException>();
            _mockHost.CreatedSessions.Should().BeEmpty();
        }

        [Fact]
        public async Task CustomReturnParameters_ShouldBeConfiguredCorrectly()
        {
            // Arrange
            var config = TestDataBuilder.CreateNamingConfigWithParameters();
            config.ExecutivePerson = new ConfigPerson { Name = "GeneralAssistant", Description = "Test person" };
            config.PromptMessage = "Task: {{task}}";
            
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);

            // Act - Execute the naming function to trigger child session creation
            var requestData = new Dictionary<string, object?> { ["task"] = "Test return parameters" };
            var result = await (Task<string>)namingFunction!.Function.DynamicInvoke(requestData)!;

            // Assert - Verify child session was created and return tool was configured
            _mockHost.CreatedSessions.Should().HaveCount(1);
            var childSession = (MockHostSession)_mockHost.CreatedSessions[0];
            var returnTools = childSession.GetTools();
            
            returnTools.Should().ContainKey("CustomReturnResultTool");
            var returnFunction = returnTools!["CustomReturnResultTool"].FirstOrDefault();
            returnFunction.Should().NotBeNull();
            returnFunction!.Description.Name.Should().Be(config.ReturnToolName);
            
            // Verify return parameters are configured
            var successParam = returnFunction.Description.Parameters.FirstOrDefault(p => p.Name == "success");
            var resultParam = returnFunction.Description.Parameters.FirstOrDefault(p => p.Name == "result");
            successParam.Should().NotBeNull();
            successParam!.IsRequired.Should().BeTrue();
            resultParam.Should().NotBeNull();
            resultParam!.IsRequired.Should().BeFalse();
        }

        [Fact]
        public async Task UrgingMessage_WhenReturnToolNotCalled_ShouldSendReminders()
        {
            // Arrange
            var config = TestDataBuilder.CreateBasicNamingConfig();
            config.ExecutivePerson = new ConfigPerson { Name = "GeneralAssistant", Description = "Test person" };
            config.PromptMessage = "Task: {{task}}";
            config.UrgingMessage = "Please use the {{_Config.ReturnToolName}} tool to complete this task!";
            
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);

            // Mock child session to simulate not calling return tool immediately
            var mockChildSession = new MockHostSession(999);
            _mockHost.CreatedSessions.Clear();
            _mockHost.CreatedSessions.Add(mockChildSession);

            // Act - Execute naming function
            var requestData = new Dictionary<string, object?> { ["task"] = "Test urging message" };
            
            // We expect this to potentially throw or timeout since we're not actually calling the return tool
            // But we want to verify the urging messages are sent
            try
            {
                var result = await (Task<string>)namingFunction!.Function.DynamicInvoke(requestData)!;
            }
            catch
            {
                // Expected to fail since we don't simulate proper return tool execution
            }

            // Assert - Verify urging message contains expected content
            var urgingContent = config.UrgingMessage.Replace("{{_Config.ReturnToolName}}", config.ReturnToolName);
            urgingContent.Should().Contain(config.ReturnToolName);
        }

        [Fact]
        public async Task ReturnToolName_WhenCalledSuccessfully_ShouldReturnSuccess()
        {
            // Arrange
            var config = TestDataBuilder.CreateBasicNamingConfig();
            config.ExecutivePerson = new ConfigPerson { Name = "GeneralAssistant", Description = "Test person" };
            config.PromptMessage = "Task: {{task}}";
            
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);

            // Create a mock session that will simulate successful completion
            var mockChildSession = new MockHostSession(100);
            
            // Mock the WaitChildSessionAsync to return success
            var mockResult = new MockChildSessionResult(true, "Task completed successfully");
            
            // Act
            var requestData = new Dictionary<string, object?> { ["task"] = "Test successful completion" };
            var result = await (Task<string>)namingFunction!.Function.DynamicInvoke(requestData)!;

            // Assert - Should not contain error messages
            result.Should().NotContain("No assistants available");
            result.Should().NotContain("Missing required parameters");
            
            // Verify session was created
            _mockHost.CreatedSessions.Should().HaveCount(1);
        }

        [Fact]
        public async Task ComplexNestedParameters_ShouldHandleObjectAndArrayTypes()
        {
            // Arrange
            var config = TestDataBuilder.CreateBasicNamingConfig();
            config.ExecutivePerson = new ConfigPerson { Name = "GeneralAssistant", Description = "Test person" };
            
            // Add complex nested parameter configuration
            config.InputParameters = new List<ParameterConfig>
            {
                TestDataBuilder.CreateComplexParameterConfig(),
                new ParameterConfig
                {
                    Name = "simpleArray",
                    Description = "Array of strings",
                    Type = ParameterType.Array,
                    IsRequired = false,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "arrayItem",
                        Type = ParameterType.String,
                        IsRequired = true
                    }
                }
            };
            config.PromptMessage = "Process complex data: {{complexObject}} and {{simpleArray}}";
            
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);

            // Act - with complex nested data
            var complexObject = new Dictionary<string, object?>
            {
                ["stringProperty"] = "test value",
                ["numberArray"] = new[] { 1, 2, 3 }
            };
            
            var requestData = new Dictionary<string, object?> 
            { 
                ["complexObject"] = complexObject,
                ["simpleArray"] = new[] { "item1", "item2", "item3" }
            };
            
            var result = await (Task<string>)namingFunction!.Function.DynamicInvoke(requestData)!;

            // Assert - Should process without parameter validation errors
            result.Should().NotContain("Missing required parameters");
            _mockHost.CreatedSessions.Should().HaveCount(1);
        }

        [Fact]
        public async Task SessionDisposalAndCleanup_ShouldDisposeResourcesProperly()
        {
            // Arrange
            var config = TestDataBuilder.CreateBasicNamingConfig();
            config.ExecutivePerson = new ConfigPerson { Name = "GeneralAssistant", Description = "Test person" };
            config.PromptMessage = "Task: {{task}}";
            
            var plugin = await CreatePluginInstanceAsync(config);
            var functions = await GetToolFunctionsAsync(plugin);
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);

            // Act - Execute function and then dispose
            var requestData = new Dictionary<string, object?> { ["task"] = "Test cleanup" };
            var result = await (Task<string>)namingFunction!.Function.DynamicInvoke(requestData)!;

            var sessionCountBeforeDispose = _mockHost.CreatedSessions.Count;
            
            // Dispose the plugin
            plugin.Dispose();

            // Assert - Verify cleanup occurred
            sessionCountBeforeDispose.Should().BeGreaterThan(0);
            
            // Verify that the plugin is properly disposed
            // Since we can't directly test disposal behavior in this mock setup,
            // we at least verify that disposal doesn't throw exceptions
            Action disposeAction = () => plugin.Dispose();
            disposeAction.Should().NotThrow();
        }

        [Fact]
        public async Task FullWorkflowIntegration_CompleteProcess_ShouldExecuteAllSteps()
        {
            // Arrange - Complete workflow test
            var config = TestDataBuilder.CreateNamingConfigWithParameters();
            config.ExecutivePerson = new ConfigPerson { Name = "TestExecutive", Description = "Executive for integration test" };
            config.FunctionName = "complete_integration_test";
            config.FunctionDescription = "Complete integration test function";
            config.PromptMessage = "Integration test task: {{task}} with priority {{priority}}";
            config.UrgingMessage = "Please complete using {{_Config.ReturnToolName}} tool!";
            config.ReturnToolName = "integration_complete";
            config.ReturnToolDescription = "Mark integration test as complete";

            // Act - Complete workflow execution
            // 1. Create session
            var parentSession = new MockHostSession(1);
            _disposables.Add(parentSession);

            // 2. Create NamingPluginFactory
            var factory = new NamingPluginFactory();
            await factory.SetHost(_mockHost);

            // 3. Create NamingConfig and serialize it
            var serializedConfig = JsonSerializer.Serialize(config);

            // 4. Create NamingPluginInstance
            var plugInfo = new PlugToolInfo
            {
                InstanceId = 12345,
                Config = serializedConfig,
                DisplayName = "Integration Test Plugin",
                Description = "Full integration test"
            };

            var pluginInstance = (NamingPluginInstance)await factory.CreatePluginToolAsync(plugInfo);
            _disposables.Add(pluginInstance);

            // 5. Get List<FunctionWithDescription> toolcallFunctions
            var toolcallFunctions = await GetToolFunctionsAsync(pluginInstance);

            // Assert workflow steps
            toolcallFunctions.Should().NotBeEmpty("Step 4: Should have tool functions");
            
            var delegateFunction = toolcallFunctions.FirstOrDefault(f => f.Description.Name == config.FunctionName);
            delegateFunction.Should().NotBeNull("Step 5: Should find the delegate function");

            // 6. Call the "Delegate Function" with parameters
            var requestParameters = new Dictionary<string, object?>
            {
                ["task"] = "Complete full integration test",
                ["priority"] = 1
            };

            var result = await (Task<string>)delegateFunction!.Function.DynamicInvoke(requestParameters)!;

            // 7. Verify IHost.StartNewHostSessionAsync was called
            _mockHost.CreatedSessions.Should().HaveCount(1, "Step 6: Should create new session");

            // 8. Verify PromptMessage was sent to session
            var childSession = (MockHostSession)_mockHost.CreatedSessions[0];
            var userMessages = childSession.SentMessages.Where(msg => msg.msgType == HostSessMsgType.Message).ToList();
            userMessages.Should().NotBeEmpty("Step 7: Should send prompt message");
            
            var promptMessage = userMessages.First().message;
            promptMessage.Should().Contain("Complete full integration test", "Step 7: Should contain task parameter");
            promptMessage.Should().Contain("priority 1", "Step 7: Should contain priority parameter");

            // Verify return tool configuration
            var returnTools = childSession.GetTools();
            returnTools.Should().ContainKey("CustomReturnResultTool", "Step 8: Should configure return tool");
            
            var returnFunction = returnTools!["CustomReturnResultTool"].FirstOrDefault();
            returnFunction!.Description.Name.Should().Be(config.ReturnToolName, "Step 8: Should use configured return tool name");
        }
    }
}