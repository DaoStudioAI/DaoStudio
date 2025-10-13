using System.Text.Json;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool.IntegrationTests
{
    /// <summary>
    /// Integration tests for recursion level limits and nested session scenarios.
    /// Tests the complete flow of recursion level validation and enforcement.
    /// </summary>
    public class RecursionLimitIntegrationTests : IDisposable
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public RecursionLimitIntegrationTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("TestAssistant", "Test assistant for recursion testing");
            _mockHostSession = new MockHostSession(1);

            // Setup the factory with host
            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        #region Basic Recursion Level Tests

        [Fact]
        public async Task RecursionLevel_Zero_ShouldAllowExecution()
        {
            // Arrange
            var config = CreateNamingConfigWithRecursionLimit(1); // Allow 1 level
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);

            // Act & Assert
            functions.Should().NotBeEmpty("Should have functions available at recursion level 0");
            
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);
            namingFunction.Should().NotBeNull("Should have naming function available at level 0");
        }

        [Fact]
        public async Task RecursionLevel_AtMaxLevel_ShouldNotProvideFunctions()
        {
            // Arrange
            var config = CreateNamingConfigWithRecursionLimit(1); // Max level 1

            // Create a nested session structure to simulate being at max recursion level
            var parentSession = new MockHostSession(1);
            var childSession = new MockHostSession(2, parentSession.Id); // This simulates level 1
            
            // Add naming level metadata to simulate we're at the max level
            childSession.SetNamingLevel(1); // At max level

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(functions, _mockPerson, childSession);

            // Act & Assert
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);
            namingFunction.Should().BeNull("Should not provide naming function at max recursion level");
        }

        [Fact]
        public async Task RecursionLevel_BelowMaxLevel_ShouldProvideFunctions()
        {
            // Arrange
            var config = CreateNamingConfigWithRecursionLimit(3); // Allow 3 levels
            
            // Create a session at level 1 (below max)
            var parentSession = new MockHostSession(1);
            var childSession = new MockHostSession(2, parentSession.Id);
            childSession.SetNamingLevel(1); // Below max level

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(functions, _mockPerson, childSession);

            // Act & Assert
            var namingFunction = functions.FirstOrDefault(f => f.Description.Name == config.FunctionName);
            namingFunction.Should().NotBeNull("Should provide naming function below max recursion level");
        }

        #endregion

        #region Recursion Level Validation Tests

        [Fact]
        public async Task RecursionLevelValidation_WithInvalidConfig_ShouldReturnError()
        {
            // Arrange
            var config = CreateNamingConfigWithRecursionLimit(-1); // Invalid negative level
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test invalid recursion level",
                ["background"] = "Testing negative recursion level handling"
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await InvokeNamingFunctionAsync(namingFunction, requestData));
        }

        [Fact]
        public async Task RecursionLevelValidation_AtExactLimit_ShouldBeRejected()
        {
            // Arrange
            var config = CreateNamingConfigWithRecursionLimit(2); // Max level 2
            
            // Create a session exactly at the limit using the mock host so that all sessions are
            // registered and discoverable by NamingLevelCalculator via OpenHostSession.
            var level0Session = (MockHostSession)await _mockHost.StartNewHostSessionAsync(null);
            level0Session.SetNamingLevel(0);

            var level1Session = (MockHostSession)await _mockHost.StartNewHostSessionAsync(level0Session);
            level1Session.SetNamingLevel(1);

            var level2Session = (MockHostSession)await _mockHost.StartNewHostSessionAsync(level1Session);
            level2Session.SetNamingLevel(2); // Exactly at limit

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(functions, _mockPerson, level2Session);
            
            // If functions are provided at max level, the validation should catch it during execution
            if (functions.Any(f => f.Description.Name == config.FunctionName))
            {
                var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);
                var requestData = new Dictionary<string, object?>
                {
                    ["subtask"] = "Test at exact recursion limit"
                };

                // Act
                var result = await InvokeNamingFunctionAsync(namingFunction, requestData, level2Session);

                // Assert
                result.Should().NotBeNull();
                result.Should().Contain("recursion");
            }
            else
            {
                // Functions should not be available at max level
                functions.Should().NotContain(f => f.Description.Name == config.FunctionName);
            }
        }

        [Fact]
        public async Task RecursionLevelValidation_MultipleRecursionLevels_ShouldRespectHierarchy()
        {
            // Test with different recursion limits
            var testCases = new[] { 1, 2, 3, 5 };

            foreach (var maxLevel in testCases)
            {
                // Arrange
                var config = CreateNamingConfigWithRecursionLimit(maxLevel);
                var pluginInstance = await CreatePluginInstanceAsync(config);

                // Test at each level up to and including the max
                for (int currentLevel = 0; currentLevel <= maxLevel + 1; currentLevel++)
                {
                    var session = CreateSessionAtLevel(currentLevel);
                    var functions = new List<FunctionWithDescription>();
                    await pluginInstance.GetSessionFunctionsAsync(functions, _mockPerson, session);

                    if (currentLevel < maxLevel)
                    {
                        // Should have functions below max level
                        functions.Should().Contain(f => f.Description.Name == config.FunctionName,
                            $"Should have functions at level {currentLevel} when max is {maxLevel}");
                    }
                    else
                    {
                        // Should not have functions at or above max level
                        functions.Should().NotContain(f => f.Description.Name == config.FunctionName,
                            $"Should not have functions at level {currentLevel} when max is {maxLevel}");
                    }
                }

                // Reset for next iteration
                _mockHost.ClearSessions();
            }
        }

        #endregion

        #region Nested Session Scenarios

        [Fact]
        public async Task NestedSessions_WithValidRecursion_ShouldCreateChildSessions()
        {
            // Arrange
            var config = CreateNamingConfigWithRecursionLimit(3); // Allow deep nesting
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Parent task that creates child tasks",
                ["background"] = "Testing nested session creation"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().NotBeEmpty("Should create child sessions");
        }

        [Fact]
        public async Task NestedSessions_WithParallelExecution_ShouldRespectRecursionLimits()
        {
            // Arrange
            var config = CreateNamingConfigWithRecursionLimit(2);
            config.ParallelConfig = new Naming.ParallelExecution.ParallelExecutionConfig
            {
                ExecutionType = Naming.ParallelExecution.ParallelExecutionType.ParameterBased,
                ResultStrategy = Naming.ParallelExecution.ParallelResultStrategy.WaitForAll,
                MaxConcurrency = 2
            };

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Main parallel task",
                ["subtask1"] = "Parallel task 1",
                ["subtask2"] = "Parallel task 2",
                ["background"] = "Testing parallel execution with recursion limits"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            // Multiple sessions should be created for parallel execution
            _mockHost.CreatedSessions.Should().HaveCountGreaterThan(1);
        }

        #endregion

        #region Error Handling in Recursion

        [Fact]
        public async Task RecursionLevelCalculation_WithBrokenSessionHierarchy_ShouldHandleGracefully()
        {
            // Arrange - Create a session with invalid parent reference
            var config = CreateNamingConfigWithRecursionLimit(2);
            var brokenSession = new MockHostSession(999, parentSessionId: 888); // Non-existent parent
            brokenSession.SetNamingLevel(0); // Assume level 0 for testing

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(functions, _mockPerson, brokenSession);

            // Act & Assert
            // Should not crash and should handle the error gracefully
            functions.Should().NotBeNull("Should handle broken session hierarchy gracefully");
        }

        [Fact]
        public async Task RecursionLevelCalculation_WithNullSession_ShouldThrowArgumentNull()
        {
            // Arrange
            var config = CreateNamingConfigWithRecursionLimit(2);
            var pluginInstance = await CreatePluginInstanceAsync(config);

            // Act & Assert - Passing null session should throw
            var functions = new List<FunctionWithDescription>();
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await pluginInstance.GetSessionFunctionsAsync(functions, _mockPerson, null));
        }

        #endregion

        #region Configuration Edge Cases

        [Fact]
        public async Task MaxRecursionLevel_SetToZero_ShouldPreventAllExecution()
        {
            // Arrange
            var config = CreateNamingConfigWithRecursionLimit(0); // No recursion allowed
            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);

            // Act & Assert
            functions.Should().NotContain(f => f.Description.Name == config.FunctionName,
                "Should not provide naming function when max recursion level is 0");
        }

        [Fact]
        public async Task MaxRecursionLevel_VeryHighValue_ShouldAllowDeepNesting()
        {
            // Arrange
            var config = CreateNamingConfigWithRecursionLimit(100); // Very high limit
            var pluginInstance = await CreatePluginInstanceAsync(config);
            
            // Create a session at a moderate level (well below the high limit)
            var session = CreateSessionAtLevel(10);
            var functions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(functions, _mockPerson, session);

            // Act & Assert
            functions.Should().Contain(f => f.Description.Name == config.FunctionName,
                "Should allow execution at moderate levels when limit is very high");
        }

        #endregion

        #region Helper Methods

        private NamingConfig CreateNamingConfigWithRecursionLimit(int maxRecursionLevel)
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "create_recursive_subtask",
                FunctionDescription = "Create a subtask with recursion level validation",
                PromptMessage = "Complete this recursive subtask: {{subtask}}",
                UrgingMessage = "Please use {{_Config.ReturnToolName}} to complete the recursive task",
                ReturnToolName = "complete_recursive_task",
                ReturnToolDescription = "Mark recursive task as completed",
                MaxRecursionLevel = maxRecursionLevel,
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "subtask", Description = "The subtask to complete", IsRequired = true },
                    new ParameterConfig { Name = "background", Description = "Background information", IsRequired = false }
                }
            };
        }

        private MockHostSession CreateSessionAtLevel(int level)
        {
            // Always create sessions through the mock host so that they are properly tracked and
            // accessible via IHost.OpenHostSession, which is required for accurate recursion-level
            // calculation.
            var rootSession = (MockHostSession)_mockHost.StartNewHostSessionAsync(null).GetAwaiter().GetResult();
            rootSession.SetNamingLevel(0);

            if (level == 0)
            {
                return rootSession;
            }

            MockHostSession currentSession = rootSession;
            for (int i = 1; i <= level; i++)
            {
                var childSession = (MockHostSession)_mockHost.StartNewHostSessionAsync(currentSession).GetAwaiter().GetResult();
                childSession.SetNamingLevel(i);
                currentSession = childSession;
            }

            return currentSession;
        }

        private async Task<IPluginTool> CreatePluginInstanceAsync(NamingConfig config)
        {
            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = DateTime.Now.Ticks, // Unique ID
                Description = "Recursion limit integration test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = "Test Recursion Limit Naming Plugin"
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

    /// <summary>
    /// Extension methods for MockHostSession to support recursion level testing
    /// </summary>
    public static class MockHostSessionExtensions
    {
        private static readonly Dictionary<long, int> _sessionLevels = new Dictionary<long, int>();

        public static void SetNamingLevel(this MockHostSession session, int level)
        {
            _sessionLevels[session.Id] = level;
        }

        public static int GetNamingLevel(this MockHostSession session)
        {
            return _sessionLevels.TryGetValue(session.Id, out var level) ? level : 0;
        }
    }
}
