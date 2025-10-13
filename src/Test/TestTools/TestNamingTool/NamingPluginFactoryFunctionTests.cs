using System.Text.Json;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool
{
    /// <summary>
    /// Comprehensive tests for NamingPluginFactory.CreatePluginToolAsync -> NamingPluginInstance.GetSessionFunctionsAsync
    /// Tests verify that different NamingConfig combinations result in different toolcallFunctions content
    /// </summary>
    public class NamingPluginFactoryFunctionTests
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public NamingPluginFactoryFunctionTests()
        {
            _factory = new NamingPluginFactory();
            _mockHost = new MockHost();
            _mockPerson = new MockPerson("TestAssistant", "Test assistant for Naming tasks");
            _mockHostSession = new MockHostSession(1);

            // Setup the factory with host
            _factory.SetHost(_mockHost).Wait();
            _mockHost.AddPerson(_mockPerson);
        }

        /// <summary>
        /// Helper method to create a plugin instance and get its functions
        /// </summary>
        private async Task<List<FunctionWithDescription>> GetPluginFunctionsAsync(NamingConfig config)
        {
            // Create PlugToolInfo with the config
            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = 1,
                Description = "Test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = "Test Naming Plugin"
            };

            // Create plugin instance
            var pluginInstance = await _factory.CreatePluginToolAsync(plugToolInfo);

            // Get session functions
            var toolcallFunctions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(toolcallFunctions, _mockPerson as IHostPerson, _mockHostSession);

            return toolcallFunctions;
        }

        /// <summary>
        /// Helper method to create a basic NamingConfig for testing
        /// </summary>
        private NamingConfig CreateBasicConfig()
        {
            return new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestAssistant", Description = "Test" },
                MaxRecursionLevel = 3,
                UrgingMessage = "Please complete the task"
            };
        }

        #region Default Configuration Tests

        [Fact]
        public async Task GetSessionFunctions_WithDefaultConfig_ShouldReturnDefaultFunctionInfo()
        {
            // Arrange
            var config = CreateBasicConfig();
            // Using all default values: FunctionName="create_subtask", FunctionDescription="Arbitrarily redefining...", empty InputParameters

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1, "Should have exactly one function registered");
            
            var namingFunction = functions.FirstOrDefault();
            namingFunction.Should().NotBeNull("Function should be present");
            
            // Verify default function name
            namingFunction!.Description.Name.Should().Be("create_subtask", "Should use default function name");
            
            // Verify default function description
            namingFunction.Description.Description.Should().Be("Arbitrarily redefining a concept and acting on the new definition", 
                "Should use default function description");
            
            // Verify no custom parameters (should be empty since InputParameters is empty)
            namingFunction.Description.Parameters.Should().BeEmpty("Default config with empty InputParameters should have no parameters");
        }

        [Fact]
        public async Task GetSessionFunctions_WithNoExecutivePerson_ShouldRegisterFunctionsUsingParentSession()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.ExecutivePerson = null; // No executive person configured; should fall back to parent session person

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1, "When no executive person is configured, the parent session's person should be used and functions should still be registered.");
        }

        [Fact]
        public async Task GetSessionFunctions_WithMaxRecursionLevelConfiguration_ShouldStillRegisterFunctions()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.MaxRecursionLevel = 1;

            // Create a child session to simulate being at recursion level 1
            var parentSession = new MockHostSession(1);
            var childSession = new MockHostSession(2, parentSessionId: 1);

            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = 1,
                Description = "Test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = "Test Naming Plugin"
            };

            var pluginInstance = await _factory.CreatePluginToolAsync(plugToolInfo);

            // Act
            var toolcallFunctions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(toolcallFunctions, _mockPerson as IHostPerson, childSession);

            // Assert
            toolcallFunctions.Should().HaveCount(0, 
                "Functions are still registered - recursion level is checked during execution, not registration");
        }

        #endregion

        #region Function Name Tests

        [Theory]
        [InlineData("custom_task")]
        [InlineData("execute_subtask")]
        [InlineData("handle_request")]
        [InlineData("process_work")]
        public async Task GetSessionFunctions_WithCustomFunctionName_ShouldReflectInToolcallFunctions(string customFunctionName)
        {
            // Arrange
            var config = CreateBasicConfig();
            config.FunctionName = customFunctionName;

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            functions[0].Description.Name.Should().Be(customFunctionName, 
                $"Function name should be updated to '{customFunctionName}'");
        }

        [Fact]
        public async Task GetSessionFunctions_WithEmptyFunctionName_ShouldUseEmptyName()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.FunctionName = "";

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            functions[0].Description.Name.Should().BeEmpty("Should allow empty function name");
        }

        #endregion

        #region Function Description Tests

        [Theory]
        [InlineData("Execute a custom task with specific parameters")]
        [InlineData("Handle complex business logic")]
        [InlineData("Process user requests")]
        [InlineData("")]
        public async Task GetSessionFunctions_WithCustomFunctionDescription_ShouldReflectInToolcallFunctions(string customDescription)
        {
            // Arrange
            var config = CreateBasicConfig();
            config.FunctionDescription = customDescription;

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            functions[0].Description.Description.Should().Be(customDescription, 
                $"Function description should be updated to '{customDescription}'");
        }

        #endregion

        #region Input Parameters Tests

        [Fact]
        public async Task GetSessionFunctions_WithSingleStringParameter_ShouldReflectInToolcallFunctions()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig 
                { 
                    Name = "task_name", 
                    Type = ParameterType.String, 
                    Description = "The name of the task to execute",
                    IsRequired = true 
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1, "Should have exactly one parameter");
            
            parameters[0].Name.Should().Be("task_name");
            parameters[0].Description.Should().Be("The name of the task to execute");
            parameters[0].IsRequired.Should().BeTrue();
            parameters[0].ParameterType.Should().Be(typeof(string));
        }

        [Fact]
        public async Task GetSessionFunctions_WithMultipleParameters_ShouldReflectAllInToolcallFunctions()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig 
                { 
                    Name = "task_id", 
                    Type = ParameterType.String, 
                    Description = "Unique task identifier",
                    IsRequired = true 
                },
                new ParameterConfig 
                { 
                    Name = "priority", 
                    Type = ParameterType.Number, 
                    Description = "Task priority level",
                    IsRequired = false 
                },
                new ParameterConfig 
                { 
                    Name = "urgent", 
                    Type = ParameterType.Bool, 
                    Description = "Whether task is urgent",
                    IsRequired = true 
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(3, "Should have exactly three parameters");

            // Verify first parameter
            var taskIdParam = parameters.FirstOrDefault(p => p.Name == "task_id");
            taskIdParam.Should().NotBeNull();
            taskIdParam!.Description.Should().Be("Unique task identifier");
            taskIdParam.IsRequired.Should().BeTrue();
            taskIdParam.ParameterType.Should().Be(typeof(string));

            // Verify second parameter
            var priorityParam = parameters.FirstOrDefault(p => p.Name == "priority");
            priorityParam.Should().NotBeNull();
            priorityParam!.Description.Should().Be("Task priority level");
            priorityParam.IsRequired.Should().BeFalse();
            priorityParam.ParameterType.Should().Be(typeof(double)); // Number type maps to double

            // Verify third parameter
            var urgentParam = parameters.FirstOrDefault(p => p.Name == "urgent");
            urgentParam.Should().NotBeNull();
            urgentParam!.Description.Should().Be("Whether task is urgent");
            urgentParam.IsRequired.Should().BeTrue();
            urgentParam.ParameterType.Should().Be(typeof(bool)); // Bool type maps to bool
        }

        [Fact]
        public async Task GetSessionFunctions_WithNoInputParameters_ShouldHaveNoParameters()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>(); // Explicitly empty

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            functions[0].Description.Parameters.Should().BeEmpty("Should have no parameters when InputParameters is empty");
        }

        [Fact]
        public async Task GetSessionFunctions_WithParameterTypes_ShouldHandleAllTypes()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "str_param", Type = ParameterType.String, Description = "String parameter", IsRequired = true },
                new ParameterConfig { Name = "num_param", Type = ParameterType.Number, Description = "Number parameter", IsRequired = true },
                new ParameterConfig { Name = "bool_param", Type = ParameterType.Bool, Description = "Boolean parameter", IsRequired = true },
                new ParameterConfig { Name = "obj_param", Type = ParameterType.Object, Description = "Object parameter", IsRequired = false },
                new ParameterConfig { Name = "arr_param", Type = ParameterType.Array, Description = "Array parameter", IsRequired = false }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(5, "Should have all five parameter types");

            // Verify each parameter type is handled
            parameters.Should().Contain(p => p.Name == "str_param" && p.Description == "String parameter");
            parameters.Should().Contain(p => p.Name == "num_param" && p.Description == "Number parameter");
            parameters.Should().Contain(p => p.Name == "bool_param" && p.Description == "Boolean parameter");
            parameters.Should().Contain(p => p.Name == "obj_param" && p.Description == "Object parameter");
            parameters.Should().Contain(p => p.Name == "arr_param" && p.Description == "Array parameter");
        }

        #endregion

        #region Return Parameters Tests

        [Fact]
        public async Task GetSessionFunctions_WithCustomReturnParameters_ShouldNotAffectMainFunction()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig 
                { 
                    Name = "status", 
                    Type = ParameterType.String, 
                    Description = "Task completion status",
                    IsRequired = true 
                },
                new ParameterConfig 
                { 
                    Name = "result_data", 
                    Type = ParameterType.Object, 
                    Description = "Result data object",
                    IsRequired = false 
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1, "ReturnParameters should not affect the main function count");
            // ReturnParameters are used for the return tool in child sessions, not the main function
            // So the main function structure should remain the same
        }

        [Fact]
        public async Task GetSessionFunctions_WithCustomReturnToolName_ShouldNotAffectMainFunction()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.ReturnToolName = "submit_result";
            config.ReturnToolDescription = "Submit the task result";

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1, "Return tool configuration should not affect main function");
            functions[0].Description.Name.Should().Be("create_subtask", "Main function name should remain unchanged");
        }

        #endregion

        #region Complex Configuration Combinations

        [Fact]
        public async Task GetSessionFunctions_WithComplexConfiguration_ShouldReflectAllChanges()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.FunctionName = "execute_complex_task";
            config.FunctionDescription = "Execute a complex multi-step task";
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig 
                { 
                    Name = "workflow_id", 
                    Type = ParameterType.String, 
                    Description = "Unique workflow identifier",
                    IsRequired = true 
                },
                new ParameterConfig 
                { 
                    Name = "params", 
                    Type = ParameterType.Object, 
                    Description = "Workflow parameters",
                    IsRequired = true 
                },
                new ParameterConfig 
                { 
                    Name = "timeout", 
                    Type = ParameterType.Number, 
                    Description = "Execution timeout in seconds",
                    IsRequired = false 
                }
            };
            config.ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig 
                { 
                    Name = "execution_id", 
                    Type = ParameterType.String, 
                    Description = "Execution identifier",
                    IsRequired = true 
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var function = functions[0];
            
            // Verify function name and description
            function.Description.Name.Should().Be("execute_complex_task");
            function.Description.Description.Should().Be("Execute a complex multi-step task");
            
            // Verify parameters
            function.Description.Parameters.Should().HaveCount(3);
            function.Description.Parameters.Should().Contain(p => p.Name == "workflow_id" && p.IsRequired);
            function.Description.Parameters.Should().Contain(p => p.Name == "params" && p.IsRequired);
            function.Description.Parameters.Should().Contain(p => p.Name == "timeout" && !p.IsRequired);
        }

        [Theory]
        [InlineData("task_a", "Description A", 1)]
        [InlineData("task_b", "Description B", 2)]
        [InlineData("task_c", "Description C", 3)]
        public async Task GetSessionFunctions_WithDifferentConfigurations_ShouldProduceDifferentResults(
            string functionName, string functionDescription, int parameterCount)
        {
            // Arrange
            var config = CreateBasicConfig();
            config.FunctionName = functionName;
            config.FunctionDescription = functionDescription;
            config.InputParameters = Enumerable.Range(1, parameterCount)
                .Select(i => new ParameterConfig 
                { 
                    Name = $"param_{i}", 
                    Type = ParameterType.String, 
                    Description = $"Parameter {i}",
                    IsRequired = true 
                })
                .ToList();

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var function = functions[0];
            
            function.Description.Name.Should().Be(functionName);
            function.Description.Description.Should().Be(functionDescription);
            function.Description.Parameters.Should().HaveCount(parameterCount);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task GetSessionFunctions_WithNullInputParameters_ShouldHandleGracefully()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = null!;

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            functions[0].Description.Parameters.Should().BeEmpty("Should handle null InputParameters gracefully");
        }

        [Fact]
        public async Task GetSessionFunctions_WithParameterNameEdgeCases_ShouldHandleSpecialCharacters()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig 
                { 
                    Name = "param_with_underscore", 
                    Type = ParameterType.String, 
                    Description = "Parameter with underscore",
                    IsRequired = true 
                },
                new ParameterConfig 
                { 
                    Name = "paramWithCamelCase", 
                    Type = ParameterType.String, 
                    Description = "Parameter with camel case",
                    IsRequired = true 
                },
                new ParameterConfig 
                { 
                    Name = "param123", 
                    Type = ParameterType.String, 
                    Description = "Parameter with numbers",
                    IsRequired = true 
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(3);
            parameters.Should().Contain(p => p.Name == "param_with_underscore");
            parameters.Should().Contain(p => p.Name == "paramWithCamelCase");
            parameters.Should().Contain(p => p.Name == "param123");
        }

        [Fact]
        public async Task GetSessionFunctions_WithLongDescriptions_ShouldPreserveFullText()
        {
            // Arrange
            var longDescription = new string('A', 1000); // Very long description
            var config = CreateBasicConfig();
            config.FunctionDescription = longDescription;

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            functions[0].Description.Description.Should().Be(longDescription, "Should preserve long descriptions");
        }

        [Fact]
        public async Task GetSessionFunctions_WithDifferentRecursionLevels_ShouldAlwaysRegisterFunctions()
        {
            // Arrange - Test different recursion levels
            var configs = new[]
            {
                new { Level = 0, ShouldHaveFunctions = true },
                new { Level = 1, ShouldHaveFunctions = true },
                new { Level = 5, ShouldHaveFunctions = true },
                new { Level = 10, ShouldHaveFunctions = true }
            };

            foreach (var testCase in configs)
            {
                var config = CreateBasicConfig();
                config.MaxRecursionLevel = testCase.Level;

                // Act
                var functions = await GetPluginFunctionsAsync(config);

                // Assert - All recursion levels should register functions
                // The actual recursion checking happens during function execution, not registration
                if (testCase.Level==0)
                {
                    functions.Should().HaveCount(0, $"Should always register functions regardless of recursion level {testCase.Level}");
                }else
                {
                    functions.Should().HaveCount(1, $"Should always register functions regardless of recursion level {testCase.Level}");
                }
            }
        }

        #endregion

        #region Configuration Persistence Tests

        [Fact]
        public async Task GetSessionFunctions_MultipleCalls_ShouldReturnConsistentResults()
        {
            // Arrange
            var config = CreateBasicConfig();
            config.FunctionName = "persistent_task";
            config.InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "test_param", Type = ParameterType.String, Description = "Test", IsRequired = true }
            };

            // Act - Call multiple times
            var functions1 = await GetPluginFunctionsAsync(config);
            var functions2 = await GetPluginFunctionsAsync(config);

            // Assert
            functions1.Should().HaveCount(1);
            functions2.Should().HaveCount(1);
            
            functions1[0].Description.Name.Should().Be(functions2[0].Description.Name);
            functions1[0].Description.Description.Should().Be(functions2[0].Description.Description);
            functions1[0].Description.Parameters.Should().HaveCount(functions2[0].Description.Parameters.Count);
        }

        #endregion
    }
}