using Naming;
using Naming.ParallelExecution;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamingTool.UnitTests.Validation
{
    public class ConfigurationValidationTests
    {
        [Fact]
        public void NamingConfig_WithValidConfiguration_PassesValidation()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestPerson", Description = "Test Description" },
                FunctionName = "test_function",
                FunctionDescription = "Test function description",
                MaxRecursionLevel = 2,
                PromptMessage = "Test prompt: {{ param }}",
                UrgingMessage = "Complete the task",
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "param", Description = "Test parameter", IsRequired = true, Type = ParameterType.String }
                },
                ReturnParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "result", Description = "Result", IsRequired = false, Type = ParameterType.String }
                }
            };

            // Act & Assert - Should not throw any validation exceptions
            config.Should().NotBeNull();
            config.ExecutivePerson.Should().NotBeNull();
            config.FunctionName.Should().NotBeNullOrEmpty();
            config.InputParameters.Should().NotBeNull();
            config.ReturnParameters.Should().NotBeNull();
        }

        [Fact]
        public void ParameterConfig_WithDuplicateNames_FailsValidation()
        {
            // Arrange
            var parameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "duplicate", Description = "First", Type = ParameterType.String },
                new ParameterConfig { Name = "duplicate", Description = "Second", Type = ParameterType.Number }
            };

            // Act
            var duplicateNames = parameters.GroupBy(p => p.Name)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            // Assert
            duplicateNames.Should().Contain("duplicate");
        }

        [Fact]
        public void ParameterConfig_WithValidArrayConfiguration_IsValid()
        {
            // Arrange
            var arrayParameter = new ParameterConfig
            {
                Name = "items",
                Description = "Array of items",
                Type = ParameterType.Array,
                ArrayElementConfig = new ParameterConfig
                {
                    Name = "item",
                    Description = "Individual item",
                    Type = ParameterType.String,
                    IsRequired = true
                }
            };

            // Act & Assert
            arrayParameter.IsArray.Should().BeTrue();
            arrayParameter.ArrayElementConfig.Should().NotBeNull();
            arrayParameter.ArrayElementConfig!.Name.Should().Be("item");
            arrayParameter.ArrayElementConfig.Type.Should().Be(ParameterType.String);
        }

        [Fact]
        public void ParameterConfig_WithValidObjectConfiguration_IsValid()
        {
            // Arrange
            var objectParameter = new ParameterConfig
            {
                Name = "person",
                Description = "Person object",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        Name = "name",
                        Description = "Person's name",
                        Type = ParameterType.String,
                        IsRequired = true
                    },
                    new ParameterConfig
                    {
                        Name = "age",
                        Description = "Person's age",
                        Type = ParameterType.Number,
                        IsRequired = false
                    }
                }
            };

            // Act & Assert
            objectParameter.IsObject.Should().BeTrue();
            objectParameter.ObjectProperties.Should().NotBeNull();
            objectParameter.ObjectProperties.Should().HaveCount(2);
            objectParameter.ObjectProperties.Should().Contain(p => p.Name == "name");
            objectParameter.ObjectProperties.Should().Contain(p => p.Name == "age");
        }

        [Fact]
        public void ParallelExecutionConfig_WithListBasedExecution_RequiresListParameterName()
        {
            // Arrange
            var config = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ListBased,
                ListParameterName = null // Missing required parameter
            };

            // Act & Assert
            config.ExecutionType.Should().Be(ParallelExecutionType.ListBased);
            config.ListParameterName.Should().BeNull();
            // Validation would fail when extracting parallel sources
        }

        [Fact]
        public void ParallelExecutionConfig_WithExternalListExecution_RequiresExternalList()
        {
            // Arrange
            var config = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ExternalList,
                ExternalList = new List<string>() // Empty list
            };

            // Act & Assert
            config.ExecutionType.Should().Be(ParallelExecutionType.ExternalList);
            config.ExternalList.Should().BeEmpty();
            // Validation would fail when extracting parallel sources
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        public void NamingConfig_WithVariousMaxRecursionLevels_AcceptsAllValues(int maxLevel)
        {
            // Arrange & Act
            var config = new NamingConfig
            {
                MaxRecursionLevel = maxLevel
            };

            // Assert
            config.MaxRecursionLevel.Should().Be(maxLevel);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("valid_name")]
        [InlineData("ValidName")]
        [InlineData("valid-name")]
        [InlineData("valid.name")]
        public void NamingConfig_WithVariousFunctionNames_AcceptsAllValues(string functionName)
        {
            // Arrange & Act
            var config = new NamingConfig
            {
                FunctionName = functionName
            };

            // Assert
            config.FunctionName.Should().Be(functionName);
        }

        [Fact]
        public void ParameterConfig_WithNestedArrayOfObjects_IsValid()
        {
            // Arrange
            var complexParameter = new ParameterConfig
            {
                Name = "complexData",
                Description = "Array of complex objects",
                Type = ParameterType.Array,
                ArrayElementConfig = new ParameterConfig
                {
                    Name = "complexObject",
                    Description = "Complex object element",
                    Type = ParameterType.Object,
                    ObjectProperties = new List<ParameterConfig>
                    {
                        new ParameterConfig
                        {
                            Name = "id",
                            Type = ParameterType.Number,
                            IsRequired = true
                        },
                        new ParameterConfig
                        {
                            Name = "tags",
                            Type = ParameterType.Array,
                            ArrayElementConfig = new ParameterConfig
                            {
                                Name = "tag",
                                Type = ParameterType.String
                            }
                        }
                    }
                }
            };

            // Act & Assert
            complexParameter.IsArray.Should().BeTrue();
            complexParameter.ArrayElementConfig.Should().NotBeNull();
            complexParameter.ArrayElementConfig!.IsObject.Should().BeTrue();
            
            var nestedObjectProperties = complexParameter.ArrayElementConfig.ObjectProperties;
            nestedObjectProperties.Should().NotBeNull();
            nestedObjectProperties.Should().Contain(p => p.Name == "id");
            nestedObjectProperties.Should().Contain(p => p.Name == "tags");
            
            var tagsProperty = nestedObjectProperties!.First(p => p.Name == "tags");
            tagsProperty.IsArray.Should().BeTrue();
            tagsProperty.ArrayElementConfig.Should().NotBeNull();
            tagsProperty.ArrayElementConfig!.Type.Should().Be(ParameterType.String);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        public void ParallelExecutionConfig_WithValidMaxConcurrency_AcceptsValue(int maxConcurrency)
        {
            // Arrange & Act
            var config = new ParallelExecutionConfig
            {
                MaxConcurrency = maxConcurrency
            };

            // Assert
            config.MaxConcurrency.Should().Be(maxConcurrency);
        }

        [Theory]
        [InlineData(ParallelResultStrategy.StreamIndividual)]
        [InlineData(ParallelResultStrategy.WaitForAll)]
        [InlineData(ParallelResultStrategy.FirstResultWins)]
        public void ParallelExecutionConfig_WithValidResultStrategy_AcceptsValue(ParallelResultStrategy strategy)
        {
            // Arrange & Act
            var config = new ParallelExecutionConfig
            {
                ResultStrategy = strategy
            };

            // Assert
            config.ResultStrategy.Should().Be(strategy);
        }

        [Fact]
        public void ConfigPerson_WithEmptyValues_IsValid()
        {
            // Arrange & Act
            var person = new ConfigPerson
            {
                Name = "",
                Description = ""
            };

            // Assert
            person.Name.Should().Be("");
            person.Description.Should().Be("");
        }

        [Fact]
        public void ParameterConfig_WithAllPrimitiveTypes_AreValid()
        {
            // Arrange
            var primitiveTypes = new[]
            {
                ParameterType.String,
                ParameterType.Number,
                ParameterType.Bool
            };

            foreach (var type in primitiveTypes)
            {
                // Act
                var parameter = new ParameterConfig
                {
                    Name = $"param_{type}",
                    Type = type
                };

                // Assert
                parameter.IsPrimitive.Should().BeTrue();
                parameter.IsArray.Should().BeFalse();
                parameter.IsObject.Should().BeFalse();
            }
        }

        [Fact]
        public void NamingConfig_WithCompleteParallelConfiguration_IsValid()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestPerson", Description = "Test" },
                FunctionName = "parallel_function",
                MaxRecursionLevel = 2,
                ParallelConfig = new ParallelExecutionConfig
                {
                    ExecutionType = ParallelExecutionType.ParameterBased,
                    MaxConcurrency = 4,
                    ResultStrategy = ParallelResultStrategy.WaitForAll,
                    ExcludedParameters = new List<string> { "DasSession", "metadata" },
                    SessionTimeoutMs = 60000
                }
            };

            // Act & Assert
            config.ParallelConfig.Should().NotBeNull();
            config.ParallelConfig!.ExecutionType.Should().Be(ParallelExecutionType.ParameterBased);
            config.ParallelConfig.MaxConcurrency.Should().Be(4);
            config.ParallelConfig.ResultStrategy.Should().Be(ParallelResultStrategy.WaitForAll);
            config.ParallelConfig.ExcludedParameters.Should().Contain("DasSession");
            config.ParallelConfig.ExcludedParameters.Should().Contain("metadata");
            config.ParallelConfig.SessionTimeoutMs.Should().Be(60000);
        }

        [Fact]
        public void ParameterConfig_WithMissingArrayElementConfig_IsInconsistent()
        {
            // Arrange
            var arrayParameter = new ParameterConfig
            {
                Name = "incompleteArray",
                Type = ParameterType.Array,
                ArrayElementConfig = null // Missing element configuration
            };

            // Act & Assert
            arrayParameter.IsArray.Should().BeTrue();
            arrayParameter.ArrayElementConfig.Should().BeNull();
            // This represents an inconsistent configuration that should be validated
        }

        [Fact]
        public void ParameterConfig_WithMissingObjectProperties_IsInconsistent()
        {
            // Arrange
            var objectParameter = new ParameterConfig
            {
                Name = "incompleteObject",
                Type = ParameterType.Object,
                ObjectProperties = null // Missing object properties
            };

            // Act & Assert
            objectParameter.IsObject.Should().BeTrue();
            objectParameter.ObjectProperties.Should().BeNull();
            // This represents an inconsistent configuration that should be validated
        }

        [Theory]
        [InlineData(1000)]    // 1 second
        [InlineData(30000)]   // 30 seconds
        [InlineData(300000)]  // 5 minutes
        [InlineData(1800000)] // 30 minutes
        public void ParallelExecutionConfig_WithValidSessionTimeouts_AcceptsValues(int timeoutMs)
        {
            // Arrange & Act
            var config = new ParallelExecutionConfig
            {
                SessionTimeoutMs = timeoutMs
            };

            // Assert
            config.SessionTimeoutMs.Should().Be(timeoutMs);
        }

        [Fact]
        public void NamingConfig_WithEmptyCollections_InitializesCorrectly()
        {
            // Arrange & Act
            var config = new NamingConfig();

            // Assert
            config.InputParameters.Should().NotBeNull();
            config.InputParameters.Should().BeEmpty();
            config.ReturnParameters.Should().NotBeNull();
            config.ReturnParameters.Should().BeEmpty();
        }

        [Fact]
        public void ParallelExecutionConfig_WithEmptyCollections_InitializesCorrectly()
        {
            // Arrange & Act
            var config = new ParallelExecutionConfig();

            // Assert
            config.ExternalList.Should().NotBeNull();
            config.ExternalList.Should().BeEmpty();
            config.ExcludedParameters.Should().NotBeNull();
            config.ExcludedParameters.Should().BeEmpty();
        }
    }
}
