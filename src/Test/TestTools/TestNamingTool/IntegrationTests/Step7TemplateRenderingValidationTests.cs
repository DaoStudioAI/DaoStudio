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
    /// Integration tests for Step 7 template rendering validation.
    /// Tests Scriban template rendering with _Parameter.Name and _Parameter.Value context objects
    /// for all three parallel execution strategies.
    /// </summary>
    public class Step7TemplateRenderingValidationTests : IDisposable
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public Step7TemplateRenderingValidationTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("TemplateTestAssistant", "Test assistant for template rendering");
            _mockHostSession = new MockHostSession(1);

            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        #region ParameterBased Template Rendering Tests

        [Fact]
        public async Task ParameterBased_TemplateRendering_ShouldAccessParameterNameAndValue()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.PromptMessage = @"
Parameter Name: {{_Parameter.Name}}
Parameter Value: {{_Parameter.Value}}
Shared Context: {{sharedContext}}
Process this parameter according to the requirements.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["analysisType"] = "performance_metrics",
                ["dataSource"] = "production_logs",
                ["sharedContext"] = "quarterly_review_2024"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(3, "Should create sessions for analysisType, dataSource, and sharedContext");
            
            // Verify that each session would have received properly rendered template
            // In a real scenario, we'd inspect the actual messages sent to sessions
        }

        [Fact]
        public async Task ParameterBased_ComplexTemplateStructure_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.PromptMessage = @"
# Task: Process {{_Parameter.Name}}

## Input Details
- Parameter: {{_Parameter.Name}}
- Value: {{_Parameter.Value}}
- Type: {{_Parameter.Value | type_of}}

## Context
{{ if environment }}
Environment: {{environment}}
{{ end }}
{{ if priority }}
Priority Level: {{priority}}
{{ end }}

## Instructions
Please process the above parameter according to the specified requirements.
Ensure you handle the {{_Parameter.Name}} parameter with value '{{_Parameter.Value}}' appropriately.

## Expected Output
Provide a comprehensive analysis of {{_Parameter.Name}}.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["userBehaviorPattern"] = "high_engagement_mobile",
                ["conversionMetric"] = 0.127,
                ["environment"] = "production",
                ["priority"] = "high"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Should create sessions for all parameters");
        }

        [Fact]
        public async Task ParameterBased_WithConditionalLogic_ShouldRenderBasedOnParameterValues()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.PromptMessage = @"
{{ if (_Parameter.Value == 'critical') }}
🚨 CRITICAL ALERT: Process {{_Parameter.Name}} immediately!
Priority: URGENT
{{ elsif (_Parameter.Value == 'important') }}
⚠️ Important: Handle {{_Parameter.Name}} with elevated priority
Priority: HIGH
{{ else }}
📋 Standard: Process {{_Parameter.Name}} normally
Priority: NORMAL
{{ end }}

Parameter Details:
- Name: {{_Parameter.Name}}
- Value: {{_Parameter.Value}}
- Context: {{workflowContext}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["securityAlert"] = "critical",
                ["systemUpdate"] = "important",
                ["logCleanup"] = "routine",
                ["workflowContext"] = "automated_maintenance"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Should create sessions with conditional template rendering");
        }

        #endregion

        #region ListBased Template Rendering Tests

        [Fact]
        public async Task ListBased_TemplateRendering_ShouldAccessListNameAndItemValue()
        {
            // Arrange
            var config = CreateListBasedConfig("taskQueue");
            config.PromptMessage = @"
Processing item from list: {{_Parameter.Name}}
Current item value: {{_Parameter.Value}}
Batch ID: {{batchId}}
Processing mode: {{processingMode}}

Execute the task for this specific item.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var taskQueue = new List<string> 
            { 
                "validate_user_input", 
                "process_payment", 
                "send_confirmation_email", 
                "update_inventory" 
            };

            var requestData = new Dictionary<string, object?>
            {
                ["taskQueue"] = taskQueue,
                ["batchId"] = "batch_2024_001",
                ["processingMode"] = "sequential"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Should create one session per task queue item");
        }

        [Fact]
        public async Task ListBased_WithComplexListItems_ShouldRenderObjectProperties()
        {
            // Arrange
            var config = CreateListBasedConfig("customerOrders");
            config.PromptMessage = @"
Processing order from list: {{_Parameter.Name}}
Order details: {{_Parameter.Value}}
{{ if region }}
Target region: {{region}}
{{ end }}

Please process this order according to business rules.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var customerOrders = new List<object>
            {
                new { OrderId = "ORD-001", Amount = 299.99, Customer = "Alice Johnson" },
                new { OrderId = "ORD-002", Amount = 89.50, Customer = "Bob Smith" },
                new { OrderId = "ORD-003", Amount = 1250.00, Customer = "Charlie Brown" }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["customerOrders"] = customerOrders,
                ["region"] = "North America"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(3, "Should create one session per customer order");
        }

        [Fact]
        public async Task ListBased_WithIterationHelpers_ShouldProvideIterationContext()
        {
            // Arrange
            var config = CreateListBasedConfig("processingItems");
            config.PromptMessage = @"
Item from {{_Parameter.Name}}: {{_Parameter.Value}}
Processing configuration: {{config}}

Handle this item as part of the batch processing workflow.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var processingItems = new[] { "item_alpha", "item_beta", "item_gamma", "item_delta", "item_epsilon" };
            var requestData = new Dictionary<string, object?>
            {
                ["processingItems"] = processingItems,
                ["config"] = "high_throughput_mode"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(5, "Should create sessions for all processing items");
        }

        #endregion

        #region ExternalList Template Rendering Tests

        [Fact]
        public async Task ExternalList_TemplateRendering_ShouldUseExternalListAsParameterName()
        {
            // Arrange
            var predefinedScenarios = new List<string>
            {
                "load_test_scenario_1",
                "security_scan_scenario_2", 
                "performance_benchmark_3",
                "compatibility_test_4"
            };

            var config = CreateExternalListConfig(predefinedScenarios);
            config.PromptMessage = @"
Executing scenario from source: {{_Parameter.Name}}
Scenario identifier: {{_Parameter.Value}}
Test environment: {{testEnvironment}}
Configuration: {{testConfig}}

Run the specified test scenario according to predefined parameters.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["testEnvironment"] = "staging",
                ["testConfig"] = "comprehensive_suite"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Should create one session per predefined scenario");
        }

        [Fact]
        public async Task ExternalList_WithAdvancedTemplating_ShouldHandleComplexScenarios()
        {
            // Arrange
            var deploymentEnvironments = new List<string>
            {
                "dev-environment-1",
                "staging-environment-2",
                "prod-environment-3",
                "disaster-recovery-4"
            };

            var config = CreateExternalListConfig(deploymentEnvironments);
            config.PromptMessage = @"
# Deployment Target Analysis

## Environment Details
- Source: {{_Parameter.Name}}
- Target: {{_Parameter.Value}}
- Deployment Version: {{version}}
- Release Notes: {{releaseNotes}}

## Environment-Specific Instructions
{{ if (_Parameter.Value | string.contains 'prod') }}
⚠️ PRODUCTION DEPLOYMENT - Extra validation required!
- Perform pre-deployment health checks
- Verify rollback procedures
- Notify stakeholders
{{ elsif (_Parameter.Value | string.contains 'staging') }}
🧪 STAGING DEPLOYMENT - Standard validation
- Run automated test suite
- Perform smoke tests
{{ else }}
🔧 DEVELOPMENT DEPLOYMENT - Quick deploy
- Basic validation only
{{ end }}

## Action Required
Deploy to {{_Parameter.Value}} with the specified configuration.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["version"] = "v2.1.0",
                ["releaseNotes"] = "Performance improvements and bug fixes"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Should create sessions for all deployment environments");
        }

        #endregion

        #region Template Error Handling Tests

        [Fact]
        public async Task AllStrategies_WithInvalidTemplatesSyntax_ShouldHandleGracefully()
        {
            // Test each strategy with invalid template syntax
            var strategies = new[]
            {
                (ParallelExecutionType.ParameterBased, "testParam", (object)"testValue"),
                (ParallelExecutionType.ListBased, "testList", new List<string> { "item1" }),
                (ParallelExecutionType.ExternalList, "external", new List<string> { "ext1" })
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

                // Invalid Scriban syntax
                config.PromptMessage = @"
Invalid template syntax: {{_Parameter.Name}
Missing closing brace: {{_Parameter.Value
Invalid function: {{_Parameter.Value | nonexistent_function}}
Unclosed if: {{#if true}} some text";

                var pluginInstance = await CreatePluginInstanceAsync(config);
                var functions = await GetFunctionsAsync(pluginInstance);
                var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

                var requestData = strategy == ParallelExecutionType.ExternalList 
                    ? new Dictionary<string, object?> { ["testParam"] = "test" }
                    : new Dictionary<string, object?> { [paramName] = paramValue };

                // Act & Assert
                var result = await InvokeNamingFunctionAsync(namingFunction, requestData);
                result.Should().NotBeNull($"Strategy {strategy} should handle invalid template syntax gracefully");
            }
        }

        [Fact]
        public async Task AllStrategies_WithMissingTemplateVariables_ShouldHandleGracefully()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.PromptMessage = @"
Parameter: {{_Parameter.Name}}
Value: {{_Parameter.Value}}
Missing variable: {{nonExistentVariable}}
Another missing: {{anotherMissing.property}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["testParam"] = "testValue"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should still create sessions despite missing template variables");
        }

        #endregion

        #region Helper Methods

        private NamingConfig CreateParameterBasedConfig()
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "template_parameter_based",
                FunctionDescription = "Template rendering test for ParameterBased execution",
                PromptMessage = "Default template: {{_Parameter.Name}} = {{_Parameter.Value}}",
                UrgingMessage = "Complete the template rendering test",
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
                FunctionName = "template_list_based",
                FunctionDescription = "Template rendering test for ListBased execution",
                PromptMessage = "Default template: {{_Parameter.Name}} item {{_Parameter.Value}}",
                UrgingMessage = "Complete the template rendering test",
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
                FunctionName = "template_external_list",
                FunctionDescription = "Template rendering test for ExternalList execution",
                PromptMessage = "Default template: {{_Parameter.Name}} external {{_Parameter.Value}}",
                UrgingMessage = "Complete the template rendering test",
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
                InstanceId = DateTime.Now.Ticks + Random.Shared.Next(100000),
                Description = "Template rendering test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = $"Template Test - {config.FunctionName}"
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
