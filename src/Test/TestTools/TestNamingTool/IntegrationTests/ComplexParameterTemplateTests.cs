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
    /// Integration tests for complex parameter types (arrays, objects, array of objects) 
    /// with template access validation. Tests that Scriban templates can access nested 
    /// properties using syntax like {{_Parameter.Value.propertyName}}.
    /// </summary>
    public class ComplexParameterTemplateTests : IDisposable
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public ComplexParameterTemplateTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("ComplexParamTestAssistant", "Test assistant for complex parameter types");
            _mockHostSession = new MockHostSession(1);

            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        #region Object Parameter Tests

        [Fact]
        public async Task ObjectParameter_AccessNestedProperties_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "userConfig",
                    Type = ParameterType.Object,
                    Description = "User configuration object",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "username", Type = ParameterType.String, Description = "Username" },
                        new ParameterConfig { Name = "email", Type = ParameterType.String, Description = "Email address" },
                        new ParameterConfig { Name = "age", Type = ParameterType.Number, Description = "User age" }
                    }
                }
            };
            
            config.PromptMessage = @"
Processing parameter: {{_Parameter.Name}}
Username: {{_Parameter.Value.username}}
Email: {{_Parameter.Value.email}}
Age: {{_Parameter.Value.age}}

Please validate and process this user configuration.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var userConfigObject = new 
            { 
                username = "john_doe", 
                email = "john@example.com", 
                age = 30 
            };

            var requestData = new Dictionary<string, object?>
            {
                ["userConfig"] = userConfigObject
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should create one session for the object parameter");
        }

        [Fact]
        public async Task ObjectParameter_DeepNestedAccess_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "apiRequest",
                    Type = ParameterType.Object,
                    Description = "API request configuration",
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
                        },
                        new ParameterConfig { Name = "timeout", Type = ParameterType.Number }
                    }
                }
            };
            
            config.PromptMessage = @"
API Configuration:
- Endpoint: {{_Parameter.Value.endpoint}}
- Authorization: {{_Parameter.Value.headers.authorization}}
- Content-Type: {{_Parameter.Value.headers.contentType}}
- Timeout: {{_Parameter.Value.timeout}}ms

Execute API request with the specified configuration.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var apiRequestObject = new 
            { 
                endpoint = "/api/v1/users",
                headers = new 
                {
                    authorization = "Bearer token123",
                    contentType = "application/json"
                },
                timeout = 5000
            };

            var requestData = new Dictionary<string, object?>
            {
                ["apiRequest"] = apiRequestObject
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should create one session for deep nested object");
        }

        [Fact]
        public async Task ObjectParameter_ConditionalOnProperties_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "itemToProcess",
                    Type = ParameterType.Object,
                    Description = "Item processing configuration",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "itemId", Type = ParameterType.String },
                        new ParameterConfig { Name = "priority", Type = ParameterType.String },
                        new ParameterConfig { Name = "requiresApproval", Type = ParameterType.Bool }
                    }
                }
            };
            
            config.PromptMessage = @"
Processing Item: {{_Parameter.Value.itemId}}

{{ if (_Parameter.Value.priority == 'high') }}
⚠️ HIGH PRIORITY - Immediate action required!
{{ elsif (_Parameter.Value.priority == 'medium') }}
📋 MEDIUM PRIORITY - Process within 24 hours
{{ else }}
📝 NORMAL PRIORITY - Standard processing queue
{{ end }}

{{ if _Parameter.Value.requiresApproval }}
✅ Approval Required: Yes - Escalate to manager
{{ else }}
✅ Approval Required: No - Auto-process
{{ end }}

Item ID: {{_Parameter.Value.itemId}}
Priority: {{_Parameter.Value.priority}}
Process according to the above classification.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var itemObject = new 
            { 
                itemId = "ITM-12345",
                priority = "high",
                requiresApproval = true
            };

            var requestData = new Dictionary<string, object?>
            {
                ["itemToProcess"] = itemObject
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should create one session with conditional logic on object properties");
        }

        #endregion

        #region Array Parameter Tests

        [Fact]
        public async Task ArrayParameter_SimpleStringArray_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateListBasedConfig("tags");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "tags",
                    Type = ParameterType.Array,
                    Description = "List of tags to process",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig 
                    { 
                        Name = "tag", 
                        Type = ParameterType.String, 
                        Description = "Individual tag" 
                    }
                }
            };
            
            config.PromptMessage = @"
Processing tag from array: {{_Parameter.Name}}
Current tag value: {{_Parameter.Value}}
Tag type: {{_Parameter.Value | type_of}}

Analyze and categorize this tag.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var tagsArray = new List<string> { "performance", "security", "scalability", "maintainability" };

            var requestData = new Dictionary<string, object?>
            {
                ["tags"] = tagsArray
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(4, "Should create one session per tag");
        }

        [Fact]
        public async Task ArrayParameter_NumericArray_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateListBasedConfig("metrics");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "metrics",
                    Type = ParameterType.Array,
                    Description = "Performance metrics to analyze",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig 
                    { 
                        Name = "metric", 
                        Type = ParameterType.Number, 
                        Description = "Individual metric value" 
                    }
                }
            };
            
            config.PromptMessage = @"
Analyzing metric from: {{_Parameter.Name}}
Metric value: {{_Parameter.Value}}

{{ if (_Parameter.Value > 90) }}
✅ EXCELLENT - Metric exceeds target threshold
{{ elsif (_Parameter.Value > 70) }}
📊 GOOD - Metric meets acceptable range
{{ elsif (_Parameter.Value > 50) }}
⚠️ WARNING - Metric below optimal level
{{ else }}
❌ CRITICAL - Immediate attention required
{{ end }}

Provide detailed analysis for this metric value: {{_Parameter.Value}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var metricsArray = new List<double> { 95.5, 78.2, 45.8, 92.1, 88.7 };

            var requestData = new Dictionary<string, object?>
            {
                ["metrics"] = metricsArray
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(5, "Should create one session per metric");
        }

        #endregion

        #region Array of Objects Tests

        [Fact]
        public async Task ArrayOfObjects_AccessItemProperties_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateListBasedConfig("tasks");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "tasks",
                    Type = ParameterType.Array,
                    Description = "List of tasks to process",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "task",
                        Type = ParameterType.Object,
                        Description = "Individual task object",
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "taskId", Type = ParameterType.String },
                            new ParameterConfig { Name = "title", Type = ParameterType.String },
                            new ParameterConfig { Name = "priority", Type = ParameterType.Number },
                            new ParameterConfig { Name = "assignee", Type = ParameterType.String }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Processing task from list: {{_Parameter.Name}}

Task Details:
- Task ID: {{_Parameter.Value.taskId}}
- Title: {{_Parameter.Value.title}}
- Priority: {{_Parameter.Value.priority}}
- Assignee: {{_Parameter.Value.assignee}}

Execute this task according to the specified parameters.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var tasksArray = new List<object>
            {
                new { taskId = "TSK-001", title = "Implement user authentication", priority = 1, assignee = "Alice" },
                new { taskId = "TSK-002", title = "Fix payment gateway bug", priority = 2, assignee = "Bob" },
                new { taskId = "TSK-003", title = "Update documentation", priority = 3, assignee = "Charlie" }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["tasks"] = tasksArray
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(3, "Should create one session per task object");
        }

        [Fact]
        public async Task ArrayOfObjects_ComplexNestedStructure_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateListBasedConfig("orders");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "orders",
                    Type = ParameterType.Array,
                    Description = "Customer orders to process",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "order",
                        Type = ParameterType.Object,
                        Description = "Individual order",
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "orderId", Type = ParameterType.String },
                            new ParameterConfig { 
                                Name = "customer", 
                                Type = ParameterType.Object,
                                ObjectProperties = new List<ParameterConfig> {
                                    new ParameterConfig { Name = "name", Type = ParameterType.String },
                                    new ParameterConfig { Name = "email", Type = ParameterType.String },
                                    new ParameterConfig { Name = "vipStatus", Type = ParameterType.Bool }
                                }
                            },
                            new ParameterConfig { Name = "total", Type = ParameterType.Number },
                            new ParameterConfig { 
                                Name = "items", 
                                Type = ParameterType.Array,
                                ArrayElementConfig = new ParameterConfig 
                                { 
                                    Name = "item", 
                                    Type = ParameterType.String 
                                }
                            }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Processing order: {{_Parameter.Value.orderId}}

Customer Information:
- Name: {{_Parameter.Value.customer.name}}
- Email: {{_Parameter.Value.customer.email}}
- VIP Status: {{_Parameter.Value.customer.vipStatus}}

Order Details:
- Total Amount: ${{_Parameter.Value.total}}
- Number of Items: {{_Parameter.Value.items | array.size}}

{{ if _Parameter.Value.customer.vipStatus }}
🌟 VIP Customer - Apply priority processing and special discounts
{{ end }}

{{ if (_Parameter.Value.total > 1000) }}
💰 High-Value Order - Require manager approval
{{ end }}

Process this order with appropriate business rules.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var ordersArray = new List<object>
            {
                new 
                { 
                    orderId = "ORD-2024-001",
                    customer = new { name = "Alice Johnson", email = "alice@example.com", vipStatus = true },
                    total = 1250.50,
                    items = new[] { "Laptop", "Mouse", "Keyboard" }
                },
                new 
                { 
                    orderId = "ORD-2024-002",
                    customer = new { name = "Bob Smith", email = "bob@example.com", vipStatus = false },
                    total = 89.99,
                    items = new[] { "USB Cable" }
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["orders"] = ordersArray
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should create one session per order");
        }

        [Fact]
        public async Task ArrayOfObjects_WithArrayPropertyAccess_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateListBasedConfig("projects");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "projects",
                    Type = ParameterType.Array,
                    Description = "Software projects",
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
                            },
                            new ParameterConfig { Name = "teamSize", Type = ParameterType.Number }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Analyzing project: {{_Parameter.Value.projectName}}

Technology Stack ({{_Parameter.Value.technologies | array.size}} technologies):
{{ for tech in _Parameter.Value.technologies }}
  - {{tech}}
{{ end }}

Team Size: {{_Parameter.Value.teamSize}} developers

{{ if (_Parameter.Value.teamSize > 10) }}
📊 Large Team - Implement additional coordination processes
{{ elsif (_Parameter.Value.teamSize > 5) }}
👥 Medium Team - Standard agile practices
{{ else }}
🔧 Small Team - Streamlined workflows
{{ end }}

Provide project analysis and recommendations.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var projectsArray = new List<object>
            {
                new 
                { 
                    projectName = "E-Commerce Platform",
                    technologies = new[] { "React", "Node.js", "PostgreSQL", "Redis" },
                    teamSize = 8
                },
                new 
                { 
                    projectName = "Mobile Banking App",
                    technologies = new[] { "Flutter", "Firebase", "GraphQL" },
                    teamSize = 5
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["projects"] = projectsArray
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should create one session per project");
        }

        #endregion

        #region Mixed Complex Parameters Tests

        [Fact]
        public async Task MixedComplexParameters_ObjectAndArrayTogether_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.ParallelConfig!.ExcludedParameters = new List<string> { "context" };
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "userProfile",
                    Type = ParameterType.Object,
                    Description = "User profile information",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "userId", Type = ParameterType.String },
                        new ParameterConfig { Name = "name", Type = ParameterType.String },
                        new ParameterConfig { 
                            Name = "roles", 
                            Type = ParameterType.Array,
                            ArrayElementConfig = new ParameterConfig { Name = "role", Type = ParameterType.String }
                        }
                    }
                },
                new ParameterConfig
                {
                    Name = "permissions",
                    Type = ParameterType.Array,
                    Description = "User permissions",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig { Name = "permission", Type = ParameterType.String }
                },
                new ParameterConfig
                {
                    Name = "context",
                    Type = ParameterType.String,
                    Description = "Execution context",
                    IsRequired = false
                }
            };
            
            config.PromptMessage = @"
Parameter Type: {{_Parameter.Name}}
Parameter Value: {{_Parameter.Value}}

{{ if (_Parameter.Name == 'userProfile') }}
User Analysis:
- User ID: {{_Parameter.Value.userId}}
- Name: {{_Parameter.Value.name}}
- Roles: {{ _Parameter.Value.roles | array.join ', ' }}
{{ elsif (_Parameter.Name == 'permissions') }}
Permissions Array Analysis:
- Total Permissions: {{ _Parameter.Value | array.size }}
- First Permission: {{ _Parameter.Value | array.first }}
{{ end }}

Execution Context: {{context}}
Process this parameter according to its type.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var userProfileObject = new 
            { 
                userId = "USR-789",
                name = "Jane Developer",
                roles = new[] { "developer", "reviewer", "admin" }
            };

            var permissionsArray = new List<string> { "read", "write", "delete", "admin" };

            var requestData = new Dictionary<string, object?>
            {
                ["userProfile"] = userProfileObject,
                ["permissions"] = permissionsArray,
                ["context"] = "security_audit_2024"
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            // Should create 2 sessions: userProfile and permissions (context is excluded)
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should create sessions for non-excluded complex parameters");
        }

        [Fact]
        public async Task ComplexParameters_WithScribanHelperFunctions_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateListBasedConfig("dataItems");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "dataItems",
                    Type = ParameterType.Array,
                    Description = "Data items with metadata",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "dataItem",
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "itemId", Type = ParameterType.String },
                            new ParameterConfig { Name = "category", Type = ParameterType.String },
                            new ParameterConfig { Name = "value", Type = ParameterType.Number },
                            new ParameterConfig { 
                                Name = "tags", 
                                Type = ParameterType.Array,
                                ArrayElementConfig = new ParameterConfig { Name = "tag", Type = ParameterType.String }
                            }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Data Item Analysis: {{_Parameter.Value.itemId}}

Category: {{_Parameter.Value.category | string.upcase}}
Value: {{_Parameter.Value.value | math.round 2}}

Tags ({{_Parameter.Value.tags | array.size}}):
{{ for tag in _Parameter.Value.tags }}
  - {{tag | string.capitalize}}
{{ end }}

{{ if (_Parameter.Value.category | string.contains 'premium') }}
⭐ Premium Category - Apply special handling
{{ end }}

{{ if (_Parameter.Value.tags | array.size > 3) }}
🏷️ Multi-tagged item - Requires cross-reference validation
{{ end }}

Process this data item with the specified business rules.";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var dataItemsArray = new List<object>
            {
                new 
                { 
                    itemId = "ITEM-001",
                    category = "premium_electronics",
                    value = 1299.99,
                    tags = new[] { "high-value", "electronics", "featured", "trending", "warranty" }
                },
                new 
                { 
                    itemId = "ITEM-002",
                    category = "standard_books",
                    value = 29.95,
                    tags = new[] { "books", "education" }
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["dataItems"] = dataItemsArray
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should create one session per data item");
        }

        #endregion

        #region Cascade Member Access Tests

        [Fact]
        public async Task CascadeMemberAccess_TwoLevelNesting_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateListBasedConfig("itemsToProcess");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "itemsToProcess",
                    Type = ParameterType.Array,
                    Description = "Items that need to be processed",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "item",
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { 
                                Name = "itemToBeProcessed", 
                                Type = ParameterType.String,
                                Description = "The actual item to process" 
                            },
                            new ParameterConfig { Name = "priority", Type = ParameterType.String },
                            new ParameterConfig { Name = "metadata", Type = ParameterType.String }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Processing item from cascade: {{_Parameter.Value.itemToBeProcessed}}
Priority: {{_Parameter.Value.priority}}
Metadata: {{_Parameter.Value.metadata}}

Execute processing for: {{_Parameter.Value.itemToBeProcessed}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var itemsArray = new List<object>
            {
                new { itemToBeProcessed = "Document_A.pdf", priority = "high", metadata = "financial" },
                new { itemToBeProcessed = "Document_B.docx", priority = "medium", metadata = "legal" },
                new { itemToBeProcessed = "Document_C.xlsx", priority = "low", metadata = "report" }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["itemsToProcess"] = itemsArray
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(3, "Should create one session per item with cascade member access");
        }

        [Fact]
        public async Task CascadeMemberAccess_ThreeLevelNesting_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateListBasedConfig("workItems");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "workItems",
                    Type = ParameterType.Array,
                    Description = "Work items with nested payload",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "workItem",
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { 
                                Name = "payload", 
                                Type = ParameterType.Object,
                                ObjectProperties = new List<ParameterConfig> {
                                    new ParameterConfig { 
                                        Name = "itemToBeProcessed", 
                                        Type = ParameterType.String 
                                    },
                                    new ParameterConfig { 
                                        Name = "processingHints", 
                                        Type = ParameterType.String 
                                    }
                                }
                            },
                            new ParameterConfig { Name = "workItemId", Type = ParameterType.String },
                            new ParameterConfig { Name = "timestamp", Type = ParameterType.String }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Work Item: {{_Parameter.Value.workItemId}}
Timestamp: {{_Parameter.Value.timestamp}}

Target Item (3-level cascade): {{_Parameter.Value.payload.itemToBeProcessed}}
Processing Hints: {{_Parameter.Value.payload.processingHints}}

Execute the task on: {{_Parameter.Value.payload.itemToBeProcessed}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var workItemsArray = new List<object>
            {
                new 
                { 
                    workItemId = "WI-001",
                    timestamp = "2024-10-04T10:30:00Z",
                    payload = new 
                    { 
                        itemToBeProcessed = "customer_data.csv",
                        processingHints = "validate emails and phone numbers"
                    }
                },
                new 
                { 
                    workItemId = "WI-002",
                    timestamp = "2024-10-04T11:15:00Z",
                    payload = new 
                    { 
                        itemToBeProcessed = "transaction_log.json",
                        processingHints = "check for anomalies"
                    }
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["workItems"] = workItemsArray
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should create sessions with 3-level cascade member access");
        }

        [Fact]
        public async Task CascadeMemberAccess_MixedWithArrays_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateListBasedConfig("processingQueue");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "processingQueue",
                    Type = ParameterType.Array,
                    Description = "Queue of items with nested data and arrays",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "queueItem",
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { 
                                Name = "task", 
                                Type = ParameterType.Object,
                                ObjectProperties = new List<ParameterConfig> {
                                    new ParameterConfig { 
                                        Name = "itemToBeProcessed", 
                                        Type = ParameterType.String 
                                    },
                                    new ParameterConfig { 
                                        Name = "dependencies", 
                                        Type = ParameterType.Array,
                                        ArrayElementConfig = new ParameterConfig 
                                        { 
                                            Name = "dependency", 
                                            Type = ParameterType.String 
                                        }
                                    }
                                }
                            },
                            new ParameterConfig { Name = "queueId", Type = ParameterType.String }
                        }
                    }
                }
            };
            
            config.PromptMessage = @"
Queue Item: {{_Parameter.Value.queueId}}

Target for Processing: {{_Parameter.Value.task.itemToBeProcessed}}

Dependencies ({{_Parameter.Value.task.dependencies | array.size}}):
{{ for dep in _Parameter.Value.task.dependencies }}
  - {{dep}}
{{ end }}

{{ if (_Parameter.Value.task.dependencies | array.size > 0) }}
⚠️ Process dependencies before executing: {{_Parameter.Value.task.itemToBeProcessed}}
{{ else }}
✅ No dependencies - proceed with: {{_Parameter.Value.task.itemToBeProcessed}}
{{ end }}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var queueArray = new List<object>
            {
                new 
                { 
                    queueId = "Q-100",
                    task = new 
                    { 
                        itemToBeProcessed = "build_project.sh",
                        dependencies = new[] { "install_deps.sh", "run_tests.sh" }
                    }
                },
                new 
                { 
                    queueId = "Q-101",
                    task = new 
                    { 
                        itemToBeProcessed = "deploy_app.sh",
                        dependencies = new string[] { }
                    }
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["processingQueue"] = queueArray
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should handle cascade access with nested arrays");
        }

        [Fact]
        public async Task CascadeMemberAccess_DeepNestingWithConditionals_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "dataPackage",
                    Type = ParameterType.Object,
                    Description = "Deeply nested data package",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { 
                            Name = "envelope", 
                            Type = ParameterType.Object,
                            ObjectProperties = new List<ParameterConfig> {
                                new ParameterConfig { 
                                    Name = "content", 
                                    Type = ParameterType.Object,
                                    ObjectProperties = new List<ParameterConfig> {
                                        new ParameterConfig { 
                                            Name = "itemToBeProcessed", 
                                            Type = ParameterType.String 
                                        },
                                        new ParameterConfig { 
                                            Name = "format", 
                                            Type = ParameterType.String 
                                        }
                                    }
                                }
                            }
                        },
                        new ParameterConfig { Name = "packageId", Type = ParameterType.String }
                    }
                }
            };
            
            config.PromptMessage = @"
Package ID: {{_Parameter.Value.packageId}}

Deep Cascade Item (4 levels): {{_Parameter.Value.envelope.content.itemToBeProcessed}}
Format: {{_Parameter.Value.envelope.content.format}}

{{ if (_Parameter.Value.envelope.content.format == 'json') }}
📋 JSON Format - Parse as structured data
{{ elsif (_Parameter.Value.envelope.content.format == 'xml') }}
📄 XML Format - Use XML parser
{{ else }}
📝 Plain Format - Process as text
{{ end }}

Process deeply nested item: {{_Parameter.Value.envelope.content.itemToBeProcessed}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var dataPackageObject = new 
            { 
                packageId = "PKG-XYZ-789",
                envelope = new 
                {
                    content = new 
                    {
                        itemToBeProcessed = "api_response.json",
                        format = "json"
                    }
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["dataPackage"] = dataPackageObject
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should handle deep cascade member access (4 levels)");
        }

        [Fact]
        public async Task CascadeMemberAccess_ArrayOfObjectsWithItemToBeProcessed_ShouldRenderCorrectly()
        {
            // Arrange - This is the exact pattern shown in the README example
            var config = CreateListBasedConfig("batchItems");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "batchItems",
                    Type = ParameterType.Array,
                    Description = "Batch processing items",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "batchItem",
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { 
                                Name = "itemToBeProcessed", 
                                Type = ParameterType.String,
                                Description = "The specific item to be processed"
                            },
                            new ParameterConfig { Name = "batchId", Type = ParameterType.String },
                            new ParameterConfig { Name = "status", Type = ParameterType.String }
                        }
                    }
                }
            };
            
            // Using the exact template pattern from the README
            config.PromptMessage = @"
Process the following {{_Parameter.Name}}: {{_Parameter.Value}} {{_Parameter.Value.itemToBeProcessed}}

Batch ID: {{_Parameter.Value.batchId}}
Status: {{_Parameter.Value.status}}
Item: {{_Parameter.Value.itemToBeProcessed}}

{{ if (_Parameter.Value.status == 'pending') }}
🔄 Status: Pending - Begin processing
{{ elsif (_Parameter.Value.status == 'retry') }}
🔁 Status: Retry - Reprocess with caution
{{ else }}
✅ Status: {{_Parameter.Value.status}}
{{ end }}

Execute processing for item: {{_Parameter.Value.itemToBeProcessed}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var batchItemsArray = new List<object>
            {
                new { itemToBeProcessed = "invoice_2024_001.pdf", batchId = "BATCH-A", status = "pending" },
                new { itemToBeProcessed = "invoice_2024_002.pdf", batchId = "BATCH-A", status = "retry" },
                new { itemToBeProcessed = "invoice_2024_003.pdf", batchId = "BATCH-B", status = "pending" }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["batchItems"] = batchItemsArray
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(3, "Should create one session per batch item using README pattern");
        }

        [Fact]
        public async Task CascadeMemberAccess_DictionaryWithNestedDictionaries_ShouldRenderCorrectly()
        {
            // Arrange - Test Dictionary<string, object> with nested dictionaries
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "configData",
                    Type = ParameterType.Object,
                    Description = "Configuration data as dictionary",
                    IsRequired = true
                }
            };
            
            config.PromptMessage = @"
Configuration Access via Dictionary:
Server: {{_Parameter.Value.server.host}}:{{_Parameter.Value.server.port}}
Database: {{_Parameter.Value.database.name}}
Connection: {{_Parameter.Value.database.connectionString}}

{{ if (_Parameter.Value.server.ssl == true) }}
🔒 SSL Enabled - Secure connection
{{ else }}
⚠️ SSL Disabled - Insecure connection
{{ end }}

Process with server: {{_Parameter.Value.server.host}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Using Dictionary<string, object> instead of anonymous objects
            var configDataDict = new Dictionary<string, object?>
            {
                ["server"] = new Dictionary<string, object?>
                {
                    ["host"] = "api.example.com",
                    ["port"] = 443,
                    ["ssl"] = true
                },
                ["database"] = new Dictionary<string, object?>
                {
                    ["name"] = "production_db",
                    ["connectionString"] = "Server=db.example.com;Database=prod;"
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["configData"] = configDataDict
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should handle nested dictionaries with cascade access");
        }

        [Fact]
        public async Task CascadeMemberAccess_ListOfDictionaries_ShouldRenderCorrectly()
        {
            // Arrange - Test array of dictionaries
            var config = CreateListBasedConfig("records");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "records",
                    Type = ParameterType.Array,
                    Description = "Records as list of dictionaries",
                    IsRequired = true
                }
            };
            
            config.PromptMessage = @"
Record Processing:
ID: {{_Parameter.Value.id}}
User: {{_Parameter.Value.user.name}} ({{_Parameter.Value.user.email}})
Action: {{_Parameter.Value.action}}
Metadata: {{_Parameter.Value.metadata.timestamp}}

{{ if (_Parameter.Value.metadata.critical == true) }}
🚨 CRITICAL - Immediate action required
{{ end }}

Process record: {{_Parameter.Value.id}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Using List of Dictionary<string, object>
            var recordsList = new List<Dictionary<string, object?>>
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "REC-001",
                    ["action"] = "update",
                    ["user"] = new Dictionary<string, object?>
                    {
                        ["name"] = "Alice",
                        ["email"] = "alice@example.com"
                    },
                    ["metadata"] = new Dictionary<string, object?>
                    {
                        ["timestamp"] = "2024-10-04T10:30:00Z",
                        ["critical"] = true
                    }
                },
                new Dictionary<string, object?>
                {
                    ["id"] = "REC-002",
                    ["action"] = "delete",
                    ["user"] = new Dictionary<string, object?>
                    {
                        ["name"] = "Bob",
                        ["email"] = "bob@example.com"
                    },
                    ["metadata"] = new Dictionary<string, object?>
                    {
                        ["timestamp"] = "2024-10-04T11:00:00Z",
                        ["critical"] = false
                    }
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["records"] = recordsList
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should handle list of dictionaries with nested cascade access");
        }

        [Fact]
        public async Task CascadeMemberAccess_DeepNestedDictionaries_ShouldRenderCorrectly()
        {
            // Arrange - Test deep nesting with dictionaries (4+ levels)
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "deepConfig",
                    Type = ParameterType.Object,
                    Description = "Deeply nested dictionary configuration",
                    IsRequired = true
                }
            };
            
            config.PromptMessage = @"
Deep Dictionary Cascade Access:

Level 1: {{_Parameter.Value.application.name}}
Level 2: {{_Parameter.Value.application.settings.environment}}
Level 3: {{_Parameter.Value.application.settings.features.authentication.provider}}
Level 4: {{_Parameter.Value.application.settings.features.authentication.options.timeout}}

{{ if (_Parameter.Value.application.settings.features.authentication.provider == 'oauth2') }}
🔐 OAuth2 Authentication configured
Timeout: {{_Parameter.Value.application.settings.features.authentication.options.timeout}}s
{{ end }}

Application: {{_Parameter.Value.application.name}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Deep nested Dictionary<string, object>
            var deepConfigDict = new Dictionary<string, object?>
            {
                ["application"] = new Dictionary<string, object?>
                {
                    ["name"] = "MyApp",
                    ["settings"] = new Dictionary<string, object?>
                    {
                        ["environment"] = "production",
                        ["features"] = new Dictionary<string, object?>
                        {
                            ["authentication"] = new Dictionary<string, object?>
                            {
                                ["provider"] = "oauth2",
                                ["options"] = new Dictionary<string, object?>
                                {
                                    ["timeout"] = 3600,
                                    ["refreshToken"] = true
                                }
                            }
                        }
                    }
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["deepConfig"] = deepConfigDict
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should handle 4+ level deep dictionary nesting");
        }

        [Fact]
        public async Task CascadeMemberAccess_MixedDictionariesAndObjects_ShouldRenderCorrectly()
        {
            // Arrange - Test mixing dictionaries with anonymous objects
            var config = CreateListBasedConfig("mixedData");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "mixedData",
                    Type = ParameterType.Array,
                    Description = "Mixed dictionaries and objects",
                    IsRequired = true
                }
            };
            
            config.PromptMessage = @"
Mixed Data Structure:
Item: {{_Parameter.Value.itemToBeProcessed}}
Category: {{_Parameter.Value.category}}
Config Host: {{_Parameter.Value.config.host}}
Config Port: {{_Parameter.Value.config.port}}

Process: {{_Parameter.Value.itemToBeProcessed}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Mixing dictionaries and objects
            var mixedDataList = new List<object>
            {
                new 
                { 
                    itemToBeProcessed = "file1.txt",
                    category = "text",
                    config = new Dictionary<string, object?>
                    {
                        ["host"] = "server1.com",
                        ["port"] = 8080
                    }
                },
                new Dictionary<string, object?>
                {
                    ["itemToBeProcessed"] = "file2.pdf",
                    ["category"] = "document",
                    ["config"] = new 
                    {
                        host = "server2.com",
                        port = 9090
                    }
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["mixedData"] = mixedDataList
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(2, "Should handle mixed dictionaries and objects seamlessly");
        }

        [Fact]
        public async Task CascadeMemberAccess_DictionaryWithArrayValues_ShouldRenderCorrectly()
        {
            // Arrange - Dictionary containing arrays
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "teamData",
                    Type = ParameterType.Object,
                    Description = "Team data with dictionary containing arrays",
                    IsRequired = true
                }
            };
            
            config.PromptMessage = @"
Team: {{_Parameter.Value.team.name}}
Lead: {{_Parameter.Value.team.lead}}

Members ({{_Parameter.Value.team.members | array.size}}):
{{ for member in _Parameter.Value.team.members }}
  - {{member}}
{{ end }}

Technologies ({{_Parameter.Value.tech.stack | array.size}}):
{{ for tech in _Parameter.Value.tech.stack }}
  - {{tech}}
{{ end }}

Process team: {{_Parameter.Value.team.name}}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            // Dictionary with array values
            var teamDataDict = new Dictionary<string, object?>
            {
                ["team"] = new Dictionary<string, object?>
                {
                    ["name"] = "Backend Team",
                    ["lead"] = "Sarah",
                    ["members"] = new List<string> { "Alice", "Bob", "Charlie", "Diana" }
                },
                ["tech"] = new Dictionary<string, object?>
                {
                    ["stack"] = new List<string> { "C#", ".NET", "PostgreSQL", "Redis" }
                }
            };

            var requestData = new Dictionary<string, object?>
            {
                ["teamData"] = teamDataDict
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should handle dictionaries with array values and cascade access");
        }

        #endregion

        #region Edge Cases and Validation Tests

        [Fact]
        public async Task ComplexParameters_NullObjectProperties_ShouldHandleGracefully()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "optionalConfig",
                    Type = ParameterType.Object,
                    Description = "Optional configuration",
                    IsRequired = false,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "setting1", Type = ParameterType.String, IsRequired = false },
                        new ParameterConfig { Name = "setting2", Type = ParameterType.Number, IsRequired = false }
                    }
                }
            };
            
            config.PromptMessage = @"
Configuration Present: {{ if _Parameter.Value }}true{{ else }}false{{ end }}
{{ if _Parameter.Value }}
  Setting 1: {{ _Parameter.Value.setting1 ?? 'not set' }}
  Setting 2: {{ _Parameter.Value.setting2 ?? 'not set' }}
{{ else }}
  Using default configuration
{{ end }}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var configWithNulls = new 
            { 
                setting1 = (string?)null,
                setting2 = 42
            };

            var requestData = new Dictionary<string, object?>
            {
                ["optionalConfig"] = configWithNulls
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(1, "Should handle null object properties gracefully");
        }

        [Fact]
        public async Task ComplexParameters_EmptyArray_ShouldHandleGracefully()
        {
            // Arrange
            var config = CreateListBasedConfig("emptyList");
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "emptyList",
                    Type = ParameterType.Array,
                    Description = "Potentially empty list",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig { Name = "item", Type = ParameterType.String }
                }
            };
            
            config.PromptMessage = @"
Processing list: {{_Parameter.Name}}
List size: {{ _Parameter.Value | array.size }}
{{ if (_Parameter.Value | array.size == 0) }}
  List is empty - no processing required
{{ else }}
  Current item: {{_Parameter.Value}}
{{ end }}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["emptyList"] = new List<string>()
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(0, "Should not create sessions for empty array");
        }

        [Fact]
        public async Task ComplexParameters_TypeChecksInTemplate_ShouldRenderCorrectly()
        {
            // Arrange
            var config = CreateParameterBasedConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "stringParam", Type = ParameterType.String, IsRequired = true },
                new ParameterConfig { Name = "numberParam", Type = ParameterType.Number, IsRequired = true },
                new ParameterConfig { Name = "boolParam", Type = ParameterType.Bool, IsRequired = true },
                new ParameterConfig 
                { 
                    Name = "objectParam", 
                    Type = ParameterType.Object, 
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "field", Type = ParameterType.String }
                    }
                },
                new ParameterConfig 
                { 
                    Name = "arrayParam", 
                    Type = ParameterType.Array, 
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig { Name = "element", Type = ParameterType.String }
                }
            };
            
            config.PromptMessage = @"
Parameter: {{_Parameter.Name}}
Value Type: {{_Parameter.Value | type_of}}

{{ if (_Parameter.Value | type_of == 'string') }}
  String value: {{_Parameter.Value}}
  Length: {{ _Parameter.Value | string.size }}
{{ elsif (_Parameter.Value | type_of == 'number') || (_Parameter.Value | type_of == 'double') }}
  Numeric value: {{_Parameter.Value}}
  Rounded: {{ _Parameter.Value | math.round }}
{{ elsif (_Parameter.Value | type_of == 'boolean') }}
  Boolean value: {{_Parameter.Value}}
{{ elsif (_Parameter.Value | type_of == 'object') }}
  Object type detected
  Field: {{ _Parameter.Value.field ?? 'not set' }}
{{ elsif (_Parameter.Value | type_of | string.contains 'list') || (_Parameter.Value | type_of | string.contains 'array') }}
  Array/List type detected
  Size: {{ _Parameter.Value | array.size }}
{{ end }}";

            var pluginInstance = await CreatePluginInstanceAsync(config);
            var functions = await GetFunctionsAsync(pluginInstance);
            var namingFunction = functions.First(f => f.Description.Name == config.FunctionName);

            var requestData = new Dictionary<string, object?>
            {
                ["stringParam"] = "test string",
                ["numberParam"] = 42.7,
                ["boolParam"] = true,
                ["objectParam"] = new { field = "value" },
                ["arrayParam"] = new List<string> { "item1", "item2" }
            };

            // Act
            var result = await InvokeNamingFunctionAsync(namingFunction, requestData);

            // Assert
            result.Should().NotBeNull();
            _mockHost.CreatedSessions.Should().HaveCount(5, "Should handle type checks for all parameter types");
        }

        #endregion

        #region Helper Methods

        private NamingConfig CreateParameterBasedConfig()
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = _mockPerson.Name, Description = _mockPerson.Description },
                FunctionName = "complex_param_test",
                FunctionDescription = "Complex parameter template test",
                PromptMessage = "Default template: {{_Parameter.Name}} = {{_Parameter.Value}}",
                UrgingMessage = "Complete the complex parameter test",
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
                FunctionName = "complex_list_test",
                FunctionDescription = "Complex list parameter template test",
                PromptMessage = "Default template: {{_Parameter.Name}} item {{_Parameter.Value}}",
                UrgingMessage = "Complete the complex list test",
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
                Description = "Complex parameter test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = $"Complex Param Test - {config.FunctionName}"
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
