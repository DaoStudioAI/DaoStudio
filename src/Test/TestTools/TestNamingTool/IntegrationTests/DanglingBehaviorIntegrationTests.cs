using Xunit;
using FluentAssertions;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool.IntegrationTests
{
    /// <summary>
    /// Integration tests for the new DanglingBehavior functionality.
    /// Tests the complete flow for each behavior type: Urge, ReportError, and Pause.
    /// </summary>
    public class DanglingBehaviorIntegrationTests : IDisposable
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public DanglingBehaviorIntegrationTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("TestAssistant", "Test assistant for dangling behavior testing");
            _mockHostSession = new MockHostSession(1);

            // Setup the factory with host
            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        #region Urge Behavior Tests

        [Fact]
        public async Task UrgeBehavior_WhenReturnToolNotCalled_Should_FailAfterRetries()
        {
            // Arrange
            var config = CreateDanglingBehaviorConfig(DanglingBehavior.Urge);
            var functions = await GetPluginFunctionsAsync(config);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Disable auto invocation to simulate return tool not being called
            SetupChildSessionToNotCallReturnTool();

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test urge behavior failure",
                ["background"] = "Testing urge behavior when return tool is not called"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Failed", "Urge behavior should report failure after retries");
            
            // Verify child session was created
            _mockHost.CreatedSessions.Should().HaveCount(1);
        }

        [Fact]
        public async Task UrgeBehavior_WhenReturnToolCalled_Should_Succeed()
        {
            // Arrange
            var config = CreateDanglingBehaviorConfig(DanglingBehavior.Urge);
            var functions = await GetPluginFunctionsAsync(config);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Enable auto invocation to simulate return tool being called
            SetupChildSessionToCallReturnTool(true);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test urge behavior success",
                ["background"] = "Testing urge behavior when return tool is called"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded", "Urge behavior should succeed when return tool is called");
        }

        #endregion

        #region ReportError Behavior Tests

        [Fact]
        public async Task ReportErrorBehavior_WhenReturnToolNotCalled_Should_ReportErrorImmediately()
        {
            // Arrange
            var config = CreateDanglingBehaviorConfig(DanglingBehavior.ReportError, "Custom error: Return tool was not called");
            var functions = await GetPluginFunctionsAsync(config);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Disable auto invocation to simulate return tool not being called
            SetupChildSessionToNotCallReturnTool();

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test report error behavior",
                ["background"] = "Testing report error behavior when return tool is not called"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Failed", "ReportError behavior should report failure");
            result.Should().Contain("Custom error", "Should use the custom error message");
        }

        [Fact]
        public async Task ReportErrorBehavior_WithEmptyErrorMessage_Should_UseDefaultMessage()
        {
            // Arrange
            var config = CreateDanglingBehaviorConfig(DanglingBehavior.ReportError, ""); // Empty error message
            var functions = await GetPluginFunctionsAsync(config);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Disable auto invocation to simulate return tool not being called
            SetupChildSessionToNotCallReturnTool();

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test report error with default message",
                ["background"] = "Testing report error behavior with default message"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Failed", "ReportError behavior should report failure");
            result.Should().Contain("Child session failed to call the return tool", "Should use default error message");
        }

        [Fact]
        public async Task ReportErrorBehavior_WhenReturnToolCalled_Should_Succeed()
        {
            // Arrange
            var config = CreateDanglingBehaviorConfig(DanglingBehavior.ReportError, "This error should not appear");
            var functions = await GetPluginFunctionsAsync(config);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Enable auto invocation to simulate return tool being called
            SetupChildSessionToCallReturnTool(true);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test report error behavior success",
                ["background"] = "Testing report error behavior when return tool is called"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded", "Should succeed when return tool is called");
            result.Should().NotContain("This error should not appear", "Error message should not be used when successful");
        }

        #endregion

        #region Pause Behavior Tests


        [Fact]
        public async Task PauseBehavior_WhenReturnToolCalled_Should_Succeed()
        {
            // Arrange
            var config = CreateDanglingBehaviorConfig(DanglingBehavior.Pause);
            var functions = await GetPluginFunctionsAsync(config);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Enable auto invocation to simulate return tool being called
            SetupChildSessionToCallReturnTool(true);

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test pause behavior success",
                ["background"] = "Testing pause behavior when return tool is called"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Succeeded", "Pause behavior should succeed when return tool is called");
        }

        #endregion

        #region Configuration Edge Cases

        [Fact]
        public async Task DanglingBehavior_WithUnknownValue_Should_FallbackToUrge()
        {
            // Arrange
            var config = CreateDanglingBehaviorConfig((DanglingBehavior)999); // Unknown value
            var functions = await GetPluginFunctionsAsync(config);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Disable auto invocation to simulate return tool not being called
            SetupChildSessionToNotCallReturnTool();

            var requestData = new Dictionary<string, object?>
            {
                ["subtask"] = "Test unknown behavior fallback",
                ["background"] = "Testing fallback behavior for unknown DanglingBehavior value"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("Failed", "Should fall back to Urge behavior and fail after retries");
        }

        [Fact]
        public async Task DanglingBehavior_ConfigurationSerialization_Should_PreserveSettings()
        {
            // Arrange
            var originalConfig = CreateDanglingBehaviorConfig(DanglingBehavior.ReportError, "Test serialization error");
            
            // Act - Serialize and deserialize
            var json = JsonSerializer.Serialize(originalConfig);
            var deserializedConfig = JsonSerializer.Deserialize<NamingConfig>(json);

            // Assert
            deserializedConfig.Should().NotBeNull();
            deserializedConfig!.DanglingBehavior.Should().Be(DanglingBehavior.ReportError);
            deserializedConfig.ErrorMessage.Should().Be("Test serialization error");
        }

        #endregion

        #region Helper Methods

        private NamingConfig CreateDanglingBehaviorConfig(DanglingBehavior behavior, string? errorMessage = null)
        {
            return new NamingConfig
            {
                FunctionName = "test_dangling_behavior",
                FunctionDescription = $"Test function for {behavior} behavior",
                DanglingBehavior = behavior,
                ErrorMessage = errorMessage ?? string.Empty,
                PromptMessage = "Execute the task: {{subtask}}. Context: {{background}}",
                UrgingMessage = "Please complete the task using the return tool.",
                ReturnToolName = "complete_task",
                ReturnToolDescription = "Complete the assigned task",
                ReturnParameters = new List<ParameterConfig>
                {
                    new ParameterConfig 
                    { 
                        Name = "result", 
                        Type = ParameterType.String, 
                        Description = "Task result", 
                        IsRequired = true 
                    }
                },
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig 
                    { 
                        Name = "subtask", 
                        Type = ParameterType.String, 
                        Description = "The subtask to execute", 
                        IsRequired = true 
                    },
                    new ParameterConfig 
                    { 
                        Name = "background", 
                        Type = ParameterType.String, 
                        Description = "Background context", 
                        IsRequired = false 
                    }
                },
                ExecutivePerson = new ConfigPerson 
                { 
                    Name = _mockPerson.Name, 
                    Description = _mockPerson.Description 
                }
            };
        }

        private async Task<List<FunctionWithDescription>> GetPluginFunctionsAsync(NamingConfig config)
        {
            // Create PlugToolInfo with the config
            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = 1,
                Description = "Test dangling behavior plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = "Test Dangling Behavior Plugin"
            };

            // Create plugin instance
            var pluginInstance = await _factory.CreatePluginToolAsync(plugToolInfo);

            // Get session functions
            var toolcallFunctions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(toolcallFunctions, _mockPerson as IHostPerson, _mockHostSession);

            return toolcallFunctions;
        }

        private async Task<string> InvokeNamingFunctionAsync(FunctionWithDescription function, Dictionary<string, object?> requestData)
        {
            var invocationResult = function.Function.DynamicInvoke(BuildInvocationArguments(function, requestData));
            return await ExtractResultAsStringAsync(invocationResult);
        }

        private async Task<string> InvokeNamingFunctionWithCancellationAsync(FunctionWithDescription function, Dictionary<string, object?> requestData, CancellationToken cancellationToken)
        {
            var invocationResult = function.Function.DynamicInvoke(BuildInvocationArguments(function, requestData));
            return await ExtractResultAsStringAsync(invocationResult);
        }

        private static object?[] BuildInvocationArguments(FunctionWithDescription function, Dictionary<string, object?> requestData)
        {
            var parameters = function.Function.Method.GetParameters();

            if (parameters.Length == 0)
            {
                return Array.Empty<object?>();
            }

            if (parameters.Length == 1)
            {
                var parameterType = parameters[0].ParameterType;
                if (parameterType.IsAssignableFrom(typeof(Dictionary<string, object?>)) ||
                    parameterType.IsAssignableFrom(typeof(IDictionary<string, object?>)))
                {
                    return new object?[] { requestData };
                }
            }

            var args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                object? value = null;

                if (!string.IsNullOrEmpty(parameter.Name) && requestData.TryGetValue(parameter.Name, out var requestValue))
                {
                    value = ConvertIfNeeded(requestValue, parameter.ParameterType);
                }
                else if (parameter.HasDefaultValue)
                {
                    value = parameter.DefaultValue;
                }
                else
                {
                    value = ConvertIfNeeded(null, parameter.ParameterType);
                }

                args[i] = value;
            }

            return args;
        }

        private static object? ConvertIfNeeded(object? value, Type targetType)
        {
            if (value == null)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                {
                    return Activator.CreateInstance(targetType);
                }
                return null;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            try
            {
                return Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType);
            }
            catch
            {
                return value;
            }
        }

        private static string FormatFailureFromException(Exception ex)
        {
            var message = ex.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Failed";
            }

            // Always ensure a consistent "Failed" prefix for assertions
            return $"Failed: {message}";
        }

        private static async Task<string> ExtractResultAsStringAsync(object? invocationResult)
        {
            switch (invocationResult)
            {
                case null:
                    return string.Empty;
                case string str:
                    return str;
                case Task<string> stringTask:
                    try
                    {
                        return await stringTask.ConfigureAwait(false) ?? string.Empty;
                    }
                    catch (InvalidOperationException ex)
                    {
                        return FormatFailureFromException(ex);
                    }
                case ValueTask<string> valueTaskString:
                    try
                    {
                        return await valueTaskString.ConfigureAwait(false) ?? string.Empty;
                    }
                    catch (InvalidOperationException ex)
                    {
                        return FormatFailureFromException(ex);
                    }
                case Task task:
                    try
                    {
                        await task.ConfigureAwait(false);
                    }
                    catch (InvalidOperationException ex)
                    {
                        return FormatFailureFromException(ex);
                    }

                    var resultProperty = task.GetType().GetProperty("Result");
                    var taskResult = resultProperty?.GetValue(task);
                    return taskResult?.ToString() ?? string.Empty;
                case ValueTask valueTask:
                    try
                    {
                        await valueTask.ConfigureAwait(false);
                        return string.Empty;
                    }
                    catch (InvalidOperationException ex)
                    {
                        return FormatFailureFromException(ex);
                    }
                default:
                    return invocationResult.ToString() ?? string.Empty;
            }
        }

        private void SetupChildSessionToNotCallReturnTool()
        {
            _mockHostSession.AutoInvokeReturnTool = false;
            _mockHostSession.InvokeReturnToolOnPrompt = false;
            _mockHost.AutoInvokeReturnToolForNewSessions = false;
            _mockHost.InvokeReturnToolOnPromptForNewSessions = false;
        }

        private void SetupChildSessionToCallReturnTool(bool success)
        {
            _mockHostSession.AutoInvokeReturnTool = true;
            _mockHostSession.AutoInvokeSuccess = success;
            _mockHostSession.InvokeReturnToolOnPrompt = true;
            _mockHost.AutoInvokeReturnToolForNewSessions = true;
            _mockHost.AutoInvokeSuccessForNewSessions = success;
            _mockHost.InvokeReturnToolOnPromptForNewSessions = true;
        }

        public void Dispose()
        {
            _mockHost?.Dispose();
        }

        #endregion
    }
}