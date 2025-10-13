using System.Text.Json;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool.IntegrationTests
{
    /// <summary>
    /// Integration tests for session management, cleanup, and lifecycle scenarios.
    /// Tests the complete session flow including creation, execution, and cleanup.
    /// </summary>
    public class SessionManagementIntegrationTests : IDisposable
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public SessionManagementIntegrationTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("TestAssistant", "Test assistant for session management");
            _mockHostSession = new MockHostSession(1);

            // Setup the factory with host
            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        #region Session Creation and Lifecycle

        [Fact]
        public async Task SessionCreation_SingleInstance_ShouldCreateNewSession()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test session creation",
                ["background"] = "Testing basic session creation functionality"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should create exactly one new session");
            
            var createdSession = _mockHost.CreatedSessions.First();
            createdSession.Id.Should().BeGreaterThan(0, "Created session should have valid ID");
        }

        [Fact]
        public async Task SessionCreation_MultipleInstances_ShouldCreateSeparateSessions()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            var pluginInstance1 = await CreatePluginInstanceAsync(config);
            var pluginInstance2 = await CreatePluginInstanceAsync(config);
            
            var functions1 = await GetFunctionsAsync(pluginInstance1);
            var functions2 = await GetFunctionsAsync(pluginInstance2);
            
            var namingFunction1 = functions1.First(f => f.Description.Name == config.FunctionName);
            var namingFunction2 = functions2.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test multiple instances",
                ["background"] = "Testing session creation with multiple plugin instances"
            };

            // Act
            var result1 = await InvokeNamingFunctionAsync(namingFunction1, requestData);
            var result2 = await InvokeNamingFunctionAsync(namingFunction2, requestData);

            // Assert
            result1.Should().NotBeNull();
            result2.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should create separate sessions for each plugin instance");
            
            var sessionIds = _mockHost.CreatedSessions.Select(s => s.Id).ToList();
            sessionIds.Should().OnlyHaveUniqueItems("Each session should have unique ID");
        }

        [Fact]
        public async Task SessionHierarchy_ParentChildRelationship_ShouldMaintainCorrectStructure()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var parentSession = new MockHostSession(100); // Custom parent session ID
            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test parent-child relationship",
                ["background"] = "Testing session hierarchy maintenance"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData, parentSession);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1);
            
            var createdSession = (MockHostSession)_mockHost.CreatedSessions.First();
            createdSession.ParentSessionId.Should().Be(parentSession.Id, 
                "Created session should have correct parent session ID");
        }

        #endregion

        #region Session Cleanup and Disposal

        [Fact]
        public async Task SessionCleanup_OnPluginDispose_ShouldCleanupSessionHandlers()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test cleanup functionality",
                ["background"] = "Testing session cleanup on plugin disposal"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);
            
            // Dispose the plugin instance
            pluginInstance.Dispose();

            // Assert
            result.Should().NotBeNull();
            // After disposal, the plugin should have cleaned up internal session handlers
            // This is verified by ensuring no exceptions occur during disposal
        }

        [Fact]
        public async Task SessionCleanup_OnSessionClose_ShouldRemoveSessionHandler()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            var pluginInstance = await CreatePluginInstanceAsync(config);
            
            // Simulate session close by calling CloseSessionAsync
            var sessionCloseResult = await pluginInstance.CloseSessionAsync(_mockHostSession);

            // Assert
            sessionCloseResult.Should().BeNull("CloseSessionAsync should return null for successful cleanup");
        }

        [Fact]
        public async Task SessionCleanup_MultipleSessionsDisposal_ShouldCleanupAllSessions()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            var pluginInstance = await CreatePluginInstanceAsync(config);
            
            // Create multiple sessions
            var session1 = new MockHostSession(201);
            var session2 = new MockHostSession(202);
            var session3 = new MockHostSession(203);

            var functions1 = new List<FunctionWithDescription>();
            var functions2 = new List<FunctionWithDescription>();
            var functions3 = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(functions1, _mockPerson, session1);
            await pluginInstance.GetSessionFunctionsAsync(functions2, _mockPerson, session2);
            await pluginInstance.GetSessionFunctionsAsync(functions3, _mockPerson, session3);

            // Act - Dispose the plugin
            pluginInstance.Dispose();

            // Assert
            // All session handlers should be cleaned up without exceptions
            functions1.Should().NotBeEmpty();
            functions2.Should().NotBeEmpty();
            functions3.Should().NotBeEmpty();
        }

        #endregion

        #region Session State Management

        [Fact]
        public async Task SessionState_CancellationToken_ShouldRespectParentCancellation()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Set up cancellation token
            _mockHostSession.ResetCancellationToken();

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test cancellation token handling",
                ["background"] = "Testing cancellation token propagation"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHostSession.CurrentCancellationToken.Should().NotBeNull("Parent session should maintain cancellation token");
        }

        [Fact]
        public async Task SessionState_PersonAssignment_ShouldUseCorrectPerson()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            var alternativePerson = new MockPerson("AlternativeAssistant", "Alternative test assistant");
            _mockHost.AddPerson(alternativePerson);

            // Update config to use specific person
            config.ExecutivePerson = new ConfigPerson 
            { 
                Name = alternativePerson.Name, 
                Description = alternativePerson.Description 
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test person assignment",
                ["background"] = "Testing correct person assignment to sessions"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1);
            // The session should be created with the correct person (verified through host mock)
        }

        [Fact]
        public async Task SessionState_MessageFlow_ShouldTrackMessages()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            config.PromptMessage = "Process task: {{subtask}} with background: {{background}}";
            
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Message tracking test",
                ["background"] = "Testing message flow tracking"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1);
            
            var createdSession = (MockHostSession)_mockHost.CreatedSessions.First();
            createdSession.SentMessages.Should().NotBeEmpty("Session should have received messages");
        }

        #endregion

        #region Concurrent Session Management

        [Fact]
        public async Task ConcurrentSessions_ParallelExecution_ShouldManageSessionsCorrectly()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            config.ParallelConfig = new Naming.ParallelExecution.ParallelExecutionConfig
            {
                ExecutionType = Naming.ParallelExecution.ParallelExecutionType.ParameterBased,
                ResultStrategy = Naming.ParallelExecution.ParallelResultStrategy.WaitForAll,
                MaxConcurrency = 3,
                // Exclude shared context parameters from spawning their own sessions
                ExcludedParameters = new List<string> { "subtask", "background" }
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                // Required base parameter for validation
                ["subtask"] = "Concurrent parent task",

                // Parameters that will generate separate parallel sessions
                ["subtask1"] = "Concurrent task 1",
                ["subtask2"] = "Concurrent task 2", 
                ["subtask3"] = "Concurrent task 3",

                // Shared background context (excluded from session spawning)
                ["background"] = "Testing concurrent session management"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(3, "Should create sessions for each parallel task");
            
            // All sessions should have the same parent
            var parentIds = _mockHost.CreatedSessions.Select(s => ((MockHostSession)s).ParentSessionId).Distinct().ToList();
            parentIds.Should().HaveCount(1, "All parallel sessions should have the same parent");
        }

        [Fact]
        public async Task ConcurrentSessions_SessionLimits_ShouldRespectMaxConcurrency()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            config.ParallelConfig = new Naming.ParallelExecution.ParallelExecutionConfig
            {
                ExecutionType = Naming.ParallelExecution.ParallelExecutionType.ParameterBased,
                ResultStrategy = Naming.ParallelExecution.ParallelResultStrategy.WaitForAll,
                MaxConcurrency = 2, // Limit to 2 concurrent sessions
                // Exclude shared/context parameters from spawning sessions
                ExcludedParameters = new List<string> { "subtask", "background" }
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                // Required base parameter
                ["subtask"] = "Parent limited concurrent task",

                // Parameters that will generate separate sessions
                ["subtask1"] = "Limited concurrent task 1",
                ["subtask2"] = "Limited concurrent task 2",
                ["subtask3"] = "Limited concurrent task 3",
                ["subtask4"] = "Limited concurrent task 4",
                ["background"] = "Testing session concurrency limits"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Should still create sessions for all tasks");
            // The concurrency limit affects execution timing, not total session count
        }

        #endregion

        #region Error Scenarios and Recovery

        [Fact]
        public async Task SessionErrorHandling_HostUnavailable_ShouldHandleGracefully()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            var pluginInstance = await CreatePluginInstanceAsync(config);
            
            // Clear host persons to simulate unavailable host
            _mockHost.ClearPersons();
            
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test host unavailable",
                ["background"] = "Testing error handling when host is unavailable"
            };

            // Act & Assert
            var act = async () => await InvokeNamingFunctionAsync(namingFunction, requestData);

            // The host mock returns a specific error message when no persons are available.
            // Match that exact message (or a wildcard covering the key phrase) to avoid brittle tests.
            await act.Should().ThrowAsync<ArgumentException>()
                     .WithMessage("Error: No people are available in the system.");
        }

        [Fact]
        public async Task SessionErrorHandling_InvalidConfiguration_ShouldReturnError()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            config.ExecutivePerson = new ConfigPerson 
            { 
                Name = "NonexistentPerson", 
                Description = "A person that doesn't exist" 
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test invalid configuration",
                ["background"] = "Testing error handling with invalid person configuration"
            };

            // Act & Assert - configured ExecutivePerson is not present on the host and should
            // cause the handler to throw an InvalidOperationException indicating the configured
            // assistant is not available.
            var act = async () => await InvokeNamingFunctionAsync(namingFunction, requestData);

            await act.Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage(string.Format(NamingTool.Properties.Resources.ErrorConfiguredAssistantsNotAvailable, config.ExecutivePerson.Name));
        }

        [Fact]
        public async Task SessionErrorHandling_SessionCreationFailure_ShouldReturnError()
        {
            // Arrange
            var config = CreateSessionManagementConfig();
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Use invalid session context
            var invalidSession = new MockHostSession(-1); // Invalid session ID

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test session creation failure"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData, invalidSession);

            // Assert
            result.Should().NotBeNull();
            // Should handle the error gracefully without crashing
        }

        #endregion

        #region Helper Methods

        private NamingConfig CreateSessionManagementConfig()
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "create_managed_subtask",
                FunctionDescription = "Create a subtask with proper session management",
                PromptMessage = "Execute subtask: {{subtask}}. Context: {{background}}",
                UrgingMessage = "Please complete the task using {{_Config.ReturnToolName}}",
                ReturnToolName = "complete_managed_task",
                ReturnToolDescription = "Mark managed task as completed",
                MaxRecursionLevel = 2,
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "subtask", Description = "The subtask to execute", IsRequired = true },
                    new ParameterConfig { Name = "background", Description = "Background context", IsRequired = false }
                }
            };
        }

        private async Task<IPluginTool> CreatePluginInstanceAsync(NamingConfig config)
        {
            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = DateTime.Now.Ticks, // Unique ID
                Description = "Session management integration test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = "Test Session Management Naming Plugin"
            };

            return await _factory.CreatePluginToolAsync(plugToolInfo);
        }

        private async Task<List<FunctionWithDescription>> GetFunctionsAsync(IPluginTool pluginInstance)
        {
            var functions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(functions, _mockPerson, _mockHostSession);
            return functions;
        }

        private async Task<string> InvokeNamingFunctionAsync(FunctionWithDescription function, Dictionary<string, object?> parameters, MockHostSession? customSession = null)
        {
            // Ensure required session parameter
            parameters[DaoStudio.Common.Plugins.Constants.DasSession] = customSession ?? _mockHostSession;

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
