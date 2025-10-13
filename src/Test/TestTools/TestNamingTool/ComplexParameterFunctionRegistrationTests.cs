using System.Text.Json;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool
{
    /// <summary>
    /// Tests for complex parameter type registration in function schemas.
    /// Validates that Array, Object, and nested complex parameters are properly 
    /// reflected in the generated function descriptions.
    /// </summary>
    public class ComplexParameterFunctionRegistrationTests
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public ComplexParameterFunctionRegistrationTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("TestAssistant", "Test assistant for complex parameter registration");
            _mockHostSession = new MockHostSession(1);

            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        #region Helper Methods

        private async Task<List<FunctionWithDescription>> GetPluginFunctionsAsync(NamingConfig config)
        {
            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = 1,
                Description = "Test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = "Test Complex Parameter Plugin"
            };

            var pluginInstance = await _factory.CreatePluginToolAsync(plugToolInfo);
            var toolcallFunctions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(toolcallFunctions, _mockPerson as IHostPerson, _mockHostSession);

            return toolcallFunctions;
        }

        private NamingConfig CreateBasicConfig()
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestAssistant", Description = "Test" },
                MaxRecursionLevel = 3,
                UrgingMessage = "Please complete the task"
            };
        }

        #endregion

        #region Simple Array Parameter Registration Tests

        [Fact]
        public async Task SimpleArrayParameter_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
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

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);

            var tagsParam = parameters.First(p => p.Name == "tags");
            tagsParam.Should().NotBeNull();
            tagsParam.Description.Should().Be("List of tags to process");
            tagsParam.IsRequired.Should().BeTrue();
            // Array parameters should be registered with a type that indicates collection/list
        }

        [Fact]
        public async Task NumericArrayParameter_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "scores",
                    Type = ParameterType.Array,
                    Description = "Performance scores",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "score",
                        Type = ParameterType.Number,
                        Description = "Individual score value"
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);

            var scoresParam = parameters.First(p => p.Name == "scores");
            scoresParam.Should().NotBeNull();
            scoresParam.Description.Should().Be("Performance scores");
            scoresParam.IsRequired.Should().BeTrue();
        }

        #endregion

        #region Simple Object Parameter Registration Tests

        [Fact]
        public async Task SimpleObjectParameter_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "userConfig",
                    Type = ParameterType.Object,
                    Description = "User configuration object",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { 
                            Name = "username", 
                            Type = ParameterType.String, 
                            Description = "User's username",
                            IsRequired = true 
                        },
                        new ParameterConfig { 
                            Name = "email", 
                            Type = ParameterType.String, 
                            Description = "User's email address",
                            IsRequired = true 
                        },
                        new ParameterConfig { 
                            Name = "age", 
                            Type = ParameterType.Number, 
                            Description = "User's age",
                            IsRequired = false 
                        }
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);

            var userConfigParam = parameters.First(p => p.Name == "userConfig");
            userConfigParam.Should().NotBeNull();
            userConfigParam.Description.Should().Be("User configuration object");
            userConfigParam.IsRequired.Should().BeTrue();
        }

        [Fact]
        public async Task ObjectWithMixedPropertyTypes_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "taskConfig",
                    Type = ParameterType.Object,
                    Description = "Task configuration with various property types",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "taskId", Type = ParameterType.String },
                        new ParameterConfig { Name = "priority", Type = ParameterType.Number },
                        new ParameterConfig { Name = "isUrgent", Type = ParameterType.Bool },
                        new ParameterConfig { Name = "description", Type = ParameterType.String }
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);

            var taskConfigParam = parameters.First(p => p.Name == "taskConfig");
            taskConfigParam.Should().NotBeNull();
            taskConfigParam.Description.Should().Be("Task configuration with various property types");
        }

        #endregion

        #region Nested Object Parameter Registration Tests

        [Fact]
        public async Task NestedObjectParameter_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "apiConfig",
                    Type = ParameterType.Object,
                    Description = "API configuration with nested settings",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "endpoint", Type = ParameterType.String },
                        new ParameterConfig {
                            Name = "authentication",
                            Type = ParameterType.Object,
                            Description = "Authentication settings",
                            ObjectProperties = new List<ParameterConfig> {
                                new ParameterConfig { Name = "type", Type = ParameterType.String },
                                new ParameterConfig { Name = "token", Type = ParameterType.String }
                            }
                        },
                        new ParameterConfig { Name = "timeout", Type = ParameterType.Number }
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);

            var apiConfigParam = parameters.First(p => p.Name == "apiConfig");
            apiConfigParam.Should().NotBeNull();
            apiConfigParam.Description.Should().Be("API configuration with nested settings");
        }

        [Fact]
        public async Task DeeplyNestedObjectParameter_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "systemConfig",
                    Type = ParameterType.Object,
                    Description = "System configuration with deep nesting",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig {
                            Name = "database",
                            Type = ParameterType.Object,
                            ObjectProperties = new List<ParameterConfig> {
                                new ParameterConfig {
                                    Name = "connection",
                                    Type = ParameterType.Object,
                                    ObjectProperties = new List<ParameterConfig> {
                                        new ParameterConfig { Name = "host", Type = ParameterType.String },
                                        new ParameterConfig { Name = "port", Type = ParameterType.Number }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);

            var systemConfigParam = parameters.First(p => p.Name == "systemConfig");
            systemConfigParam.Should().NotBeNull();
        }

        #endregion

        #region Array of Objects Parameter Registration Tests

        [Fact]
        public async Task ArrayOfSimpleObjects_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "users",
                    Type = ParameterType.Array,
                    Description = "List of user objects",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "user",
                        Type = ParameterType.Object,
                        Description = "Individual user",
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "id", Type = ParameterType.String },
                            new ParameterConfig { Name = "name", Type = ParameterType.String },
                            new ParameterConfig { Name = "active", Type = ParameterType.Bool }
                        }
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);

            var usersParam = parameters.First(p => p.Name == "users");
            usersParam.Should().NotBeNull();
            usersParam.Description.Should().Be("List of user objects");
            usersParam.IsRequired.Should().BeTrue();
        }

        [Fact]
        public async Task ArrayOfComplexObjects_WithNestedArrays_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "projects",
                    Type = ParameterType.Array,
                    Description = "List of project configurations",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "project",
                        Type = ParameterType.Object,
                        Description = "Individual project",
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "projectId", Type = ParameterType.String },
                            new ParameterConfig { Name = "name", Type = ParameterType.String },
                            new ParameterConfig {
                                Name = "technologies",
                                Type = ParameterType.Array,
                                Description = "Technologies used in project",
                                ArrayElementConfig = new ParameterConfig
                                {
                                    Name = "technology",
                                    Type = ParameterType.String
                                }
                            },
                            new ParameterConfig { Name = "teamSize", Type = ParameterType.Number }
                        }
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);

            var projectsParam = parameters.First(p => p.Name == "projects");
            projectsParam.Should().NotBeNull();
            projectsParam.Description.Should().Be("List of project configurations");
        }

        [Fact]
        public async Task ArrayOfObjects_WithNestedObjects_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "orders",
                    Type = ParameterType.Array,
                    Description = "Customer orders",
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
                                    new ParameterConfig { Name = "customerId", Type = ParameterType.String },
                                    new ParameterConfig { Name = "name", Type = ParameterType.String }
                                }
                            },
                            new ParameterConfig { Name = "total", Type = ParameterType.Number }
                        }
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);

            var ordersParam = parameters.First(p => p.Name == "orders");
            ordersParam.Should().NotBeNull();
            ordersParam.Description.Should().Be("Customer orders");
        }

        #endregion

        #region Mixed Complex Parameters Registration Tests

        [Fact]
        public async Task MixedComplexParameters_ShouldRegisterAllCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "simpleString",
                    Type = ParameterType.String,
                    Description = "A simple string parameter",
                    IsRequired = true
                },
                new ParameterConfig
                {
                    Name = "simpleArray",
                    Type = ParameterType.Array,
                    Description = "Simple string array",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig { Name = "item", Type = ParameterType.String }
                },
                new ParameterConfig
                {
                    Name = "simpleObject",
                    Type = ParameterType.Object,
                    Description = "Simple object",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "field1", Type = ParameterType.String },
                        new ParameterConfig { Name = "field2", Type = ParameterType.Number }
                    }
                },
                new ParameterConfig
                {
                    Name = "complexArrayOfObjects",
                    Type = ParameterType.Array,
                    Description = "Complex array of objects",
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "complexItem",
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "id", Type = ParameterType.String },
                            new ParameterConfig {
                                Name = "metadata",
                                Type = ParameterType.Object,
                                ObjectProperties = new List<ParameterConfig> {
                                    new ParameterConfig { Name = "createdAt", Type = ParameterType.String },
                                    new ParameterConfig {
                                        Name = "tags",
                                        Type = ParameterType.Array,
                                        ArrayElementConfig = new ParameterConfig { Name = "tag", Type = ParameterType.String }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(4);

            parameters.Should().Contain(p => p.Name == "simpleString");
            parameters.Should().Contain(p => p.Name == "simpleArray");
            parameters.Should().Contain(p => p.Name == "simpleObject");
            parameters.Should().Contain(p => p.Name == "complexArrayOfObjects");

            var complexParam = parameters.First(p => p.Name == "complexArrayOfObjects");
            complexParam.Description.Should().Be("Complex array of objects");
        }

        [Fact]
        public async Task ObjectWithArrayProperties_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "itemToProcess",
                    Type = ParameterType.Object,
                    Description = "Item with multiple array properties",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "itemId", Type = ParameterType.String },
                        new ParameterConfig {
                            Name = "tags",
                            Type = ParameterType.Array,
                            ArrayElementConfig = new ParameterConfig { Name = "tag", Type = ParameterType.String }
                        },
                        new ParameterConfig {
                            Name = "categories",
                            Type = ParameterType.Array,
                            ArrayElementConfig = new ParameterConfig { Name = "category", Type = ParameterType.String }
                        },
                        new ParameterConfig {
                            Name = "scores",
                            Type = ParameterType.Array,
                            ArrayElementConfig = new ParameterConfig { Name = "score", Type = ParameterType.Number }
                        }
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);

            var itemParam = parameters.First(p => p.Name == "itemToProcess");
            itemParam.Should().NotBeNull();
            itemParam.Description.Should().Be("Item with multiple array properties");
        }

        #endregion

        #region Optional vs Required Complex Parameters Tests

        [Fact]
        public async Task ComplexParameters_OptionalArrayAndObject_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "requiredConfig",
                    Type = ParameterType.Object,
                    Description = "Required configuration object",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "setting", Type = ParameterType.String }
                    }
                },
                new ParameterConfig
                {
                    Name = "optionalMetadata",
                    Type = ParameterType.Object,
                    Description = "Optional metadata object",
                    IsRequired = false,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "key", Type = ParameterType.String },
                        new ParameterConfig { Name = "value", Type = ParameterType.String }
                    }
                },
                new ParameterConfig
                {
                    Name = "optionalTags",
                    Type = ParameterType.Array,
                    Description = "Optional tags array",
                    IsRequired = false,
                    ArrayElementConfig = new ParameterConfig { Name = "tag", Type = ParameterType.String }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(3);

            var requiredConfigParam = parameters.First(p => p.Name == "requiredConfig");
            requiredConfigParam.IsRequired.Should().BeTrue();

            var optionalMetadataParam = parameters.First(p => p.Name == "optionalMetadata");
            optionalMetadataParam.IsRequired.Should().BeFalse();

            var optionalTagsParam = parameters.First(p => p.Name == "optionalTags");
            optionalTagsParam.IsRequired.Should().BeFalse();
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public async Task EmptyObjectProperties_ShouldRegisterCorrectly()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "emptyObject",
                    Type = ParameterType.Object,
                    Description = "Object with no defined properties",
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig>()
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);

            var emptyObjectParam = parameters.First(p => p.Name == "emptyObject");
            emptyObjectParam.Should().NotBeNull();
        }

        [Fact]
        public async Task ArrayWithoutElementConfig_ShouldHandleGracefully()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    Name = "arrayWithoutElementConfig",
                    Type = ParameterType.Array,
                    Description = "Array without element configuration",
                    IsRequired = true,
                    ArrayElementConfig = null
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);
        }

        #endregion
    }
}
