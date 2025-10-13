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
    /// Tests that verify the ACTUAL RENDERED OUTPUT of Scriban templates with complex parameter types.
    /// These tests inspect the LastReceivedPrompt property of MockHostSession to validate that
    /// template syntax like {{_Parameter.Value.propertyName}} produces the expected output.
    /// </summary>
    public class TemplateRenderingVerificationTests : IDisposable
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public TemplateRenderingVerificationTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("RenderTestAssistant", "Test assistant for render verification");
            _mockHostSession = new MockHostSession(1);

            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
            
            // Disable auto-invoke so we can capture the prompt before the session completes
            _mockHost.AutoInvokeReturnToolForNewSessions = false;
        }

        #region Object Property Access Verification

        [Fact]
        public async Task ObjectParameter_SimplePropertyAccess_ShouldRenderActualValues()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "userInfo",
                    Type = ParameterType.Object,
                    Description = "User information",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "username", Type = ParameterType.String },
                        new ParameterConfig { Name = "email", Type = ParameterType.String },
                        new ParameterConfig { Name = "age", Type = ParameterType.Number }
                    }
                }
            };
            
            config.PromptMessage = @"
Username: {{_Parameter.Value.username}}
Email: {{_Parameter.Value.email}}
Age: {{_Parameter.Value.age}}
Parameter Name: {{_Parameter.Name}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var userInfo = new 
            { 
                username = "john_doe", 
                email = "john@example.com", 
                age = 30 
            };

            var requestData = new Dictionary<string, object?>
            {
                ["userInfo"] = userInfo
            };

            // Act
            await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should create one session for the object parameter");
            
            var childSession = _mockHost.CreatedSessions.First() as MockHostSession;
            childSession.Should().NotBeNull();
            
            var renderedPrompt = childSession!.LastReceivedPrompt;
            renderedPrompt.Should().Contain("Username: john_doe", "Template should render username property");
            renderedPrompt.Should().Contain("Email: john@example.com", "Template should render email property");
            renderedPrompt.Should().Contain("Age: 30", "Template should render age property");
            renderedPrompt.Should().Contain("Parameter Name: userInfo", "Template should render parameter name");
        }

        [Fact]
        public async Task ObjectParameter_NestedPropertyAccess_ShouldRenderActualValues()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "apiConfig",
                    Type = ParameterType.Object,
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "endpoint", Type = ParameterType.String },
                        new ParameterConfig {
                            Name = "headers",
                            Type = ParameterType.Object,
                            ObjectProperties = new List<ParameterConfig> {
                                new ParameterConfig { Name = "authorization", Type = ParameterType.String },
                                new ParameterConfig { Name = "contentType", Type = ParameterType.String }
                            }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Endpoint: {{_Parameter.Value.endpoint}}
Authorization: {{_Parameter.Value.headers.authorization}}
Content-Type: {{_Parameter.Value.headers.contentType}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var apiConfig = new 
            { 
                endpoint = "/api/v1/users",
                headers = new 
                {
                    authorization = "Bearer token123",
                    contentType = "application/json"
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["apiConfig"] = apiConfig
            };

            // Act
            await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            var childSession = _mockHost.CreatedSessions.First() as MockHostSession;
            var renderedPrompt = childSession!.LastReceivedPrompt;
            
            renderedPrompt.Should().Contain("Endpoint: /api/v1/users", "Template should render endpoint");
            renderedPrompt.Should().Contain("Authorization: Bearer token123", "Template should render nested authorization");
            renderedPrompt.Should().Contain("Content-Type: application/json", "Template should render nested contentType");
        }

        [Fact]
        public async Task ObjectParameter_ConditionalOnProperty_ShouldRenderCorrectBranch()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "task",
                    Type = ParameterType.Object,
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "taskId", Type = ParameterType.String },
                        new ParameterConfig { Name = "priority", Type = ParameterType.String }
                    }
                }
            };
            
            config.PromptMessage = @"
Task: {{_Parameter.Value.taskId}}
{{ if (_Parameter.Value.priority == 'high') }}
PRIORITY: HIGH - Immediate action required!
{{ else if (_Parameter.Value.priority == 'medium') }}
PRIORITY: MEDIUM - Handle within 24 hours
{{ else }}
PRIORITY: LOW - Standard queue
{{ end }}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var taskHigh = new { taskId = "TSK-001", priority = "high" };

            var requestData = new Dictionary<string, object?>
            {
                ["task"] = taskHigh
            };

            // Act
            await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            var childSession = _mockHost.CreatedSessions.First() as MockHostSession;
            var renderedPrompt = childSession!.LastReceivedPrompt;
            
            renderedPrompt.Should().Contain("Task: TSK-001");
            renderedPrompt.Should().Contain("PRIORITY: HIGH - Immediate action required!");
            renderedPrompt.Should().NotContain("PRIORITY: MEDIUM");
            renderedPrompt.Should().NotContain("PRIORITY: LOW");
        }

        #endregion

        #region Array of Objects Verification

        [Fact]
        public async Task ArrayOfObjects_PropertyAccess_ShouldRenderActualValues()
        {
            // Arrange
            var config = CreateListBasedConfig("tasks");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "tasks",
                    Type = ParameterType.Array,
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "task",
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "taskId", Type = ParameterType.String },
                            new ParameterConfig { Name = "title", Type = ParameterType.String },
                            new ParameterConfig { Name = "assignee", Type = ParameterType.String }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Processing Task: {{_Parameter.Value.taskId}}
Title: {{_Parameter.Value.title}}
Assignee: {{_Parameter.Value.assignee}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var tasks = new List<object>
            {
                new { taskId = "TSK-001", title = "Implement feature", assignee = "Alice" },
                new { taskId = "TSK-002", title = "Fix bug", assignee = "Bob" }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["tasks"] = tasks
            };

            // Act
            await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should create one session per task");
            
            var session1 = _mockHost.CreatedSessions[0] as MockHostSession;
            var session2 = _mockHost.CreatedSessions[1] as MockHostSession;
            
            // Verify first task rendered correctly
            session1!.LastReceivedPrompt.Should().Contain("Processing Task: TSK-001");
            session1.LastReceivedPrompt.Should().Contain("Title: Implement feature");
            session1.LastReceivedPrompt.Should().Contain("Assignee: Alice");
            
            // Verify second task rendered correctly
            session2!.LastReceivedPrompt.Should().Contain("Processing Task: TSK-002");
            session2.LastReceivedPrompt.Should().Contain("Title: Fix bug");
            session2.LastReceivedPrompt.Should().Contain("Assignee: Bob");
        }

        [Fact]
        public async Task ArrayOfObjects_ComplexNestedStructure_ShouldRenderActualValues()
        {
            // Arrange
            var config = CreateListBasedConfig("orders");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "orders",
                    Type = ParameterType.Array,
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "order",
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "orderId", Type = ParameterType.String },
                            new ParameterConfig {
                                Name = "customer",
                                Type = ParameterType.Object,
                                ObjectProperties = new List<ParameterConfig> {
                                    new ParameterConfig { Name = "name", Type = ParameterType.String },
                                    new ParameterConfig { Name = "vipStatus", Type = ParameterType.Bool }
                                }
                            },
                            new ParameterConfig { Name = "total", Type = ParameterType.Number }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Order ID: {{_Parameter.Value.orderId}}
Customer: {{_Parameter.Value.customer.name}}
VIP: {{_Parameter.Value.customer.vipStatus}}
Total: ${{_Parameter.Value.total}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var orders = new List<object>
            {
                new 
                { 
                    orderId = "ORD-001",
                    customer = new { name = "Alice Johnson", vipStatus = true },
                    total = 1250.50
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["orders"] = orders
            };

            // Act
            await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            var childSession = _mockHost.CreatedSessions.First() as MockHostSession;
            var renderedPrompt = childSession!.LastReceivedPrompt;
            
            renderedPrompt.Should().Contain("Order ID: ORD-001");
            renderedPrompt.Should().Contain("Customer: Alice Johnson");
            renderedPrompt.Should().Contain("VIP: true", "Boolean value should be rendered");
            renderedPrompt.Should().Contain("Total: $1250.5", "Numeric value should be rendered");
        }

        [Fact]
        public async Task ArrayOfObjects_WithArrayProperty_ShouldRenderForLoop()
        {
            // Arrange
            var config = CreateListBasedConfig("projects");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "projects",
                    Type = ParameterType.Array,
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "project",
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "projectName", Type = ParameterType.String },
                            new ParameterConfig {
                                Name = "technologies",
                                Type = ParameterType.Array,
                                ArrayElementConfig = new ParameterConfig { Name = "tech", Type = ParameterType.String }
                            }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Project: {{_Parameter.Value.projectName}}
Technologies:
{{ for tech in _Parameter.Value.technologies }}
  - {{tech}}
{{ end }}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var projects = new List<object>
            {
                new 
                { 
                    projectName = "E-Commerce Platform",
                    technologies = new[] { "React", "Node.js", "PostgreSQL" }
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["projects"] = projects
            };

            // Act
            await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            var childSession = _mockHost.CreatedSessions.First() as MockHostSession;
            var renderedPrompt = childSession!.LastReceivedPrompt;
            
            renderedPrompt.Should().Contain("Project: E-Commerce Platform");
            renderedPrompt.Should().Contain("- React", "For-loop should render first technology");
            renderedPrompt.Should().Contain("- Node.js", "For-loop should render second technology");
            renderedPrompt.Should().Contain("- PostgreSQL", "For-loop should render third technology");
        }

        #endregion

        #region Scriban Helper Functions Verification

        [Fact]
        public async Task ScribanHelpers_StringOperations_ShouldRenderTransformed()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "item",
                    Type = ParameterType.Object,
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "category", Type = ParameterType.String },
                        new ParameterConfig { Name = "name", Type = ParameterType.String }
                    }
                }
            };
            
            config.PromptMessage = @"
Category (uppercase): {{_Parameter.Value.category | string.upcase}}
Name (capitalized): {{_Parameter.Value.name | string.capitalize}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var item = new { category = "premium", name = "laptop" };

            var requestData = new Dictionary<string, object?>
            {
                ["item"] = item
            };

            // Act
            await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            var childSession = _mockHost.CreatedSessions.First() as MockHostSession;
            var renderedPrompt = childSession!.LastReceivedPrompt;
            
            renderedPrompt.Should().Contain("Category (uppercase): PREMIUM", "String upcase should work");
            renderedPrompt.Should().Contain("Name (capitalized): Laptop", "String capitalize should work");
        }

        [Fact]
        public async Task ScribanHelpers_MathOperations_ShouldRenderCalculated()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "product",
                    Type = ParameterType.Object,
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "price", Type = ParameterType.Number }
                    }
                }
            };
            
            config.PromptMessage = @"
Original Price: {{_Parameter.Value.price}}
Rounded Price: {{_Parameter.Value.price | math.round 2}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var product = new { price = 1299.9876 };

            var requestData = new Dictionary<string, object?>
            {
                ["product"] = product
            };

            // Act
            await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            var childSession = _mockHost.CreatedSessions.First() as MockHostSession;
            var renderedPrompt = childSession!.LastReceivedPrompt;
            
            renderedPrompt.Should().Contain("Original Price: 1299.9876");
            renderedPrompt.Should().Contain("Rounded Price: 1299.99", "Math round should work");
        }

        [Fact]
        public async Task ScribanHelpers_ArrayOperations_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "data",
                    Type = ParameterType.Object,
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig {
                            Name = "tags",
                            Type = ParameterType.Array,
                            ArrayElementConfig = new ParameterConfig { Name = "tag", Type = ParameterType.String }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Tag Count: {{_Parameter.Value.tags | array.size}}
Tags Joined: {{_Parameter.Value.tags | array.join ', '}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var data = new { tags = new[] { "urgent", "security", "high-priority" } };

            var requestData = new Dictionary<string, object?>
            {
                ["data"] = data
            };

            // Act
            await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            var childSession = _mockHost.CreatedSessions.First() as MockHostSession;
            var renderedPrompt = childSession!.LastReceivedPrompt;
            
            renderedPrompt.Should().Contain("Tag Count: 3", "Array size should work");
            renderedPrompt.Should().Contain("Tags Joined: urgent, security, high-priority", "Array join should work");
        }

        #endregion

        #region Edge Cases and Error Scenarios

        [Fact]
        public async Task NullPropertyAccess_WithCoalescing_ShouldRenderDefault()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "config",
                    Type = ParameterType.Object,
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "requiredField", Type = ParameterType.String },
                        new ParameterConfig { Name = "optionalField", Type = ParameterType.String, IsRequired = false }
                    }
                }
            };
            
            config.PromptMessage = @"
Required: {{_Parameter.Value.requiredField}}
Optional: {{_Parameter.Value.optionalField ?? 'not set'}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var configData = new { requiredField = "present", optionalField = (string?)null };

            var requestData = new Dictionary<string, object?>
            {
                ["config"] = configData
            };

            // Act
            await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            var childSession = _mockHost.CreatedSessions.First() as MockHostSession;
            var renderedPrompt = childSession!.LastReceivedPrompt;
            
            renderedPrompt.Should().Contain("Required: present");
            renderedPrompt.Should().Contain("Optional: not set", "Null coalescing should provide default");
        }

        [Fact]
        public async Task MixedParameters_SharedContext_ShouldAccessBothParameterAndSharedData()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "task",
                    Type = ParameterType.Object,
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "taskId", Type = ParameterType.String }
                    }
                },
                new ParameterConfig
                {
                    Name = "context",
                    Type = ParameterType.String,
                    IsRequired = true
                }
            };
            
            config.PromptMessage = @"
Parameter: {{_Parameter.Name}}
Task ID: {{_Parameter.Value.taskId}}
Shared Context: {{context}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var task = new { taskId = "TSK-999" };

            var requestData = new Dictionary<string, object?>
            {
                ["task"] = task,
                ["context"] = "production_deployment"
            };

            // Act
            await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert - Should create 2 sessions (task and context)
            _mockHost.CreatedSessions.Should().HaveCount(2);
            
            var taskSession = _mockHost.CreatedSessions[0] as MockHostSession;
            var prompt1 = taskSession!.LastReceivedPrompt;
            
            // The task session should render the task object and access shared context
            prompt1.Should().Contain("Parameter: task");
            prompt1.Should().Contain("Task ID: TSK-999");
            prompt1.Should().Contain("Shared Context: production_deployment", "Should access shared parameter");
        }

        #endregion

        #region Helper Methods

        private NamingConfig CreateParameterBasedConfig()
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "render_verify_test",
                FunctionDescription = "Template rendering verification test",
                PromptMessage = "Default template",
                UrgingMessage = "Complete the test",
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
                FunctionName = "render_list_verify_test",
                FunctionDescription = "List rendering verification test",
                PromptMessage = "Default template",
                UrgingMessage = "Complete the test",
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

        private async Task<IPluginTool> CreatePluginInstanceAsync(NamingConfig config)
        {
            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = DateTime.Now.Ticks + Random.Shared.Next(100000),
                Description = "Render verification test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = $"Render Verify Test - {config.FunctionName}"
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
