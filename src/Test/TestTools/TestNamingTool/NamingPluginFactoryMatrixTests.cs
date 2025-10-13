using System.Text.Json;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool
{
    /// <summary>
    /// Matrix tests for comprehensive combinations of NamingConfig parameters
    /// Tests exhaustive combinations to ensure all parameter interactions work correctly
    /// </summary>
    public class NamingPluginFactoryMatrixTests
    {
        private readonly NamingPluginFactory _factory;
        private readonly MockHost _mockHost;
        private readonly MockPerson _mockPerson;
        private readonly MockHostSession _mockHostSession;

        public NamingPluginFactoryMatrixTests()
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
            var plugToolInfo = new PlugToolInfo
            {
                InstanceId = 1,
                Description = "Test plugin",
                Config = JsonSerializer.Serialize(config),
                DisplayName = "Test Naming Plugin"
            };

            var pluginInstance = await _factory.CreatePluginToolAsync(plugToolInfo);
            var toolcallFunctions = new List<FunctionWithDescription>();
            await pluginInstance.GetSessionFunctionsAsync(toolcallFunctions, _mockPerson as IHostPerson, _mockHostSession);

            return toolcallFunctions;
        }

        #region Function Name and Description Matrix Tests

        [Theory]
        [InlineData("create_task", "Create a new task", true)]
        [InlineData("execute_action", "Execute an action", false)]
        [InlineData("", "", true)]
        [InlineData("very_long_function_name_with_many_characters", "A very long description that explains what this function does in great detail", false)]
        public async Task GetSessionFunctions_FunctionNameDescriptionMatrix_ShouldReflectCorrectly(
            string functionName, string functionDescription, bool hasExecutivePerson)
        {
            // Arrange
            var config = new NamingConfig
            {
                FunctionName = functionName,
                FunctionDescription = functionDescription,
                ExecutivePerson = hasExecutivePerson ? new ConfigPerson { Name = "TestAssistant", Description = "Test" } : null,
                MaxRecursionLevel = 3,
                UrgingMessage = "Complete the task"
            };

            // Act & Assert
            var functions = await GetPluginFunctionsAsync(config);
            if (hasExecutivePerson)
            {
                functions.Should().HaveCount(1);
                functions[0].Description.Name.Should().Be(functionName);
                functions[0].Description.Description.Should().Be(functionDescription);
            }
            else
            {
                // When no ExecutivePerson is configured, we should still register functions using the parent session's person
                functions.Should().HaveCount(1);
                functions[0].Description.Name.Should().Be(functionName);
                functions[0].Description.Description.Should().Be(functionDescription);
            }
        }

        #endregion

        #region Parameter Type Matrix Tests

        [Theory]
        [MemberData(nameof(GetParameterTypeTestData))]
        internal async Task GetSessionFunctions_ParameterTypeMatrix_ShouldHandleAllTypes(
            int parameterTypeValue, string parameterName, string description, bool isRequired)
        {
            // Arrange
            var parameterType = (ParameterType)parameterTypeValue;
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestAssistant", Description = "Test" },
                FunctionName = "test_function",
                FunctionDescription = "Test function",
                MaxRecursionLevel = 3,
                UrgingMessage = "Complete the task",
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        Name = parameterName,
                        Type = parameterType,
                        Description = description,
                        IsRequired = isRequired
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);
            
            var parameter = parameters[0];
            parameter.Name.Should().Be(parameterName);
            parameter.Description.Should().Be(description);
            parameter.IsRequired.Should().Be(isRequired);
            
            // Verify that parameter types are correctly mapped from ParameterType enum to System.Type
            var expectedType = parameterType switch
            {
                ParameterType.String => typeof(string),
                ParameterType.Number => typeof(double),
                ParameterType.Bool => typeof(bool),
                ParameterType.Object => typeof(object),
                ParameterType.Array => typeof(object[]), // Default array type when no element config
                _ => typeof(string)
            };
            parameter.ParameterType.Should().Be(expectedType);
        }

        public static IEnumerable<object[]> GetParameterTypeTestData()
        {
            var parameterTypes = Enum.GetValues<ParameterType>();
            var requirements = new[] { true, false };
            
            foreach (var paramType in parameterTypes)
            {
                foreach (var isRequired in requirements)
                {
                    yield return new object[]
                    {
                        (int)paramType,
                        $"param_{paramType.ToString().ToLower()}",
                        $"Parameter of type {paramType}",
                        isRequired
                    };
                }
            }
        }

        #endregion

        #region Multiple Parameters Matrix Tests

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(10)]
        public async Task GetSessionFunctions_MultipleParametersMatrix_ShouldHandleVariousCounts(int parameterCount)
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestAssistant", Description = "Test" },
                FunctionName = "multi_param_function",
                FunctionDescription = "Function with multiple parameters",
                MaxRecursionLevel = 3,
                UrgingMessage = "Complete the task",
                InputParameters = Enumerable.Range(1, parameterCount)
                    .Select(i => new ParameterConfig
                    {
                        Name = $"param_{i}",
                        Type = (ParameterType)(i % Enum.GetValues<ParameterType>().Length),
                        Description = $"Parameter number {i}",
                        IsRequired = i % 2 == 1 // Alternate between required/optional
                    })
                    .ToList()
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            functions[0].Description.Parameters.Should().HaveCount(parameterCount);
            
            if (parameterCount > 0)
            {
                // Verify parameter ordering is preserved
                for (int i = 0; i < parameterCount; i++)
                {
                    functions[0].Description.Parameters[i].Name.Should().Be($"param_{i + 1}");
                }
            }
        }

        #endregion

        #region Return Parameters Matrix Tests

        [Theory]
        [InlineData(0, "default_return")]
        [InlineData(1, "single_return")]
        [InlineData(3, "multiple_returns")]
        [InlineData(5, "many_returns")]
        public async Task GetSessionFunctions_ReturnParametersMatrix_ShouldNotAffectMainFunction(
            int returnParameterCount, string returnToolName)
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestAssistant", Description = "Test" },
                FunctionName = "test_function",
                FunctionDescription = "Test function",
                MaxRecursionLevel = 3,
                UrgingMessage = "Complete the task",
                ReturnToolName = returnToolName,
                ReturnParameters = Enumerable.Range(1, returnParameterCount)
                    .Select(i => new ParameterConfig
                    {
                        Name = $"return_param_{i}",
                        Type = ParameterType.String,
                        Description = $"Return parameter {i}",
                        IsRequired = true
                    })
                    .ToList()
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1, "ReturnParameters should not affect main function count");
            functions[0].Description.Name.Should().Be("test_function", "Main function name should not be affected by return parameters");
        }

        #endregion

        #region Complete Configuration Matrix Tests

        [Theory]
        [MemberData(nameof(GetCompleteConfigurationTestData))]
        public async Task GetSessionFunctions_CompleteConfigurationMatrix_ShouldHandleAllCombinations(
            string functionName,
            string functionDescription,
            int inputParamCount,
            int returnParamCount,
            int maxRecursionLevel)
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestAssistant", Description = "Test" },
                FunctionName = functionName,
                FunctionDescription = functionDescription,
                MaxRecursionLevel = maxRecursionLevel,
                UrgingMessage = "Complete the task",
                InputParameters = Enumerable.Range(1, inputParamCount)
                    .Select(i => new ParameterConfig
                    {
                        Name = $"input_{i}",
                        Type = ParameterType.String,
                        Description = $"Input parameter {i}",
                        IsRequired = true
                    })
                    .ToList(),
                ReturnParameters = Enumerable.Range(1, returnParamCount)
                    .Select(i => new ParameterConfig
                    {
                        Name = $"return_{i}",
                        Type = ParameterType.String,
                        Description = $"Return parameter {i}",
                        IsRequired = true
                    })
                    .ToList()
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            
            var function = functions[0];
            function.Description.Name.Should().Be(functionName);
            function.Description.Description.Should().Be(functionDescription);
            function.Description.Parameters.Should().HaveCount(inputParamCount);
        }

        public static IEnumerable<object[]> GetCompleteConfigurationTestData()
        {
            var functionNames = new[] { "task_a", "task_b", "execute" };
            var descriptions = new[] { "Description A", "Description B" };
            var inputParamCounts = new[] { 0, 1, 3 };
            var returnParamCounts = new[] { 0, 2 };
            var recursionLevels = new[] { 1, 5 };

            foreach (var functionName in functionNames)
            {
                foreach (var description in descriptions)
                {
                    foreach (var inputCount in inputParamCounts)
                    {
                        foreach (var returnCount in returnParamCounts)
                        {
                            foreach (var recursionLevel in recursionLevels)
                            {
                                yield return new object[]
                                {
                                    functionName,
                                    description,
                                    inputCount,
                                    returnCount,
                                    recursionLevel
                                };
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Edge Case Matrix Tests

        [Theory]
        [InlineData(null, "Valid Description")]
        [InlineData("Valid Name", null)]
        [InlineData("", "")]
        [InlineData("   ", "   ")]
        public async Task GetSessionFunctions_EdgeCaseStringMatrix_ShouldHandleNullAndEmpty(
            string? functionName, string? functionDescription)
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestAssistant", Description = "Test" },
                FunctionName = functionName ?? "default_name",
                FunctionDescription = functionDescription ?? "default_description",
                MaxRecursionLevel = 3,
                UrgingMessage = "Complete the task"
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            functions[0].Description.Name.Should().Be(functionName ?? "default_name");
            functions[0].Description.Description.Should().Be(functionDescription ?? "default_description");
        }

        [Theory]
        [InlineData("param_with_special_chars", "Parameter with special characters: !@#$%^&*()")]
        [InlineData("param123", "Parameter with numbers 123456789")]
        [InlineData("PARAM_UPPERCASE", "PARAMETER WITH UPPERCASE")]
        [InlineData("paramCamelCase", "Parameter with camelCase")]
        [InlineData("param_with_very_long_name_that_exceeds_normal_length", "Parameter with extremely long description that goes on and on and provides very detailed information about what this parameter does and how it should be used in the context of the function")]
        public async Task GetSessionFunctions_ParameterNameDescriptionEdgeCases_ShouldHandleSpecialCases(
            string parameterName, string parameterDescription)
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestAssistant", Description = "Test" },
                FunctionName = "test_function",
                FunctionDescription = "Test function",
                MaxRecursionLevel = 3,
                UrgingMessage = "Complete the task",
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        Name = parameterName,
                        Type = ParameterType.String,
                        Description = parameterDescription,
                        IsRequired = true
                    }
                }
            };

            // Act
            var functions = await GetPluginFunctionsAsync(config);

            // Assert
            functions.Should().HaveCount(1);
            var parameters = functions[0].Description.Parameters;
            parameters.Should().HaveCount(1);
            parameters[0].Name.Should().Be(parameterName);
            parameters[0].Description.Should().Be(parameterDescription);
        }

        #endregion

        #region Consistency and Persistence Tests

        [Fact]
        public async Task GetSessionFunctions_SameConfigurationMultipleCalls_ShouldReturnIdenticalResults()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestAssistant", Description = "Test" },
                FunctionName = "consistent_function",
                FunctionDescription = "Function for consistency testing",
                MaxRecursionLevel = 3,
                UrgingMessage = "Complete the task",
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        Name = "test_param",
                        Type = ParameterType.String,
                        Description = "Test parameter",
                        IsRequired = true
                    }
                }
            };

            // Act - Call multiple times
            var functions1 = await GetPluginFunctionsAsync(config);
            var functions2 = await GetPluginFunctionsAsync(config);
            var functions3 = await GetPluginFunctionsAsync(config);

            // Assert
            functions1.Should().HaveCount(1);
            functions2.Should().HaveCount(1);
            functions3.Should().HaveCount(1);

            // Verify all results are identical
            for (int i = 0; i < 3; i++)
            {
                var functionSets = new[] { functions1, functions2, functions3 };
                var current = functionSets[i][0];
                
                current.Description.Name.Should().Be("consistent_function");
                current.Description.Description.Should().Be("Function for consistency testing");
                current.Description.Parameters.Should().HaveCount(1);
                current.Description.Parameters[0].Name.Should().Be("test_param");
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public async Task GetSessionFunctions_MultipleInstancesWithSameConfig_ShouldReturnIdenticalResults(int instanceCount)
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestAssistant", Description = "Test" },
                FunctionName = "multi_instance_function",
                FunctionDescription = "Function for multi-instance testing",
                MaxRecursionLevel = 3,
                UrgingMessage = "Complete the task"
            };

            // Act - Create multiple instances
            var allFunctions = new List<List<FunctionWithDescription>>();
            for (int i = 0; i < instanceCount; i++)
            {
                var functions = await GetPluginFunctionsAsync(config);
                allFunctions.Add(functions);
            }

            // Assert
            allFunctions.Should().HaveCount(instanceCount);
            
            // Verify all instances return identical results
            for (int i = 0; i < instanceCount; i++)
            {
                allFunctions[i].Should().HaveCount(1);
                allFunctions[i][0].Description.Name.Should().Be("multi_instance_function");
                allFunctions[i][0].Description.Description.Should().Be("Function for multi-instance testing");
            }
        }

        #endregion
    }
}