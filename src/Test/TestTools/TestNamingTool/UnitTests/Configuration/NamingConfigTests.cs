using Naming;
using System.Collections.Generic;
using System.Text.Json;

namespace TestNamingTool.UnitTests.Configuration
{
    public class NamingConfigTests
    {
        [Fact]
        public void NamingConfig_DefaultConstructor_SetsDefaultValues()
        {
            // Arrange & Act
            var config = new NamingConfig();

            // Assert
            config.Version.Should().Be(1);
            config.MaxRecursionLevel.Should().Be(1);
            config.FunctionName.Should().Be("create_subtask");
            config.FunctionDescription.Should().Be("Arbitrarily redefining a concept and acting on the new definition");
            config.ReturnToolName.Should().Be("set_result");
            config.ReturnToolDescription.Should().Be("Report back with the result after completion");
            config.ExecutivePerson.Should().BeNull();
            config.InputParameters.Should().NotBeNull().And.BeEmpty();
            config.ReturnParameters.Should().NotBeNull().And.BeEmpty();
            config.ParallelConfig.Should().BeNull();
            config.UrgingMessage.Should().NotBeNullOrEmpty();
            config.PromptMessage.Should().BeEmpty();
            config.ErrorReportingConfig.Should().BeNull();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public void NamingConfig_MaxRecursionLevel_CanBeSet(int recursionLevel)
        {
            // Arrange
            var config = new NamingConfig();

            // Act
            config.MaxRecursionLevel = recursionLevel;

            // Assert
            config.MaxRecursionLevel.Should().Be(recursionLevel);
        }

        [Theory]
        [InlineData("custom_function")]
        [InlineData("delegate_task")]
        [InlineData("")]
        public void NamingConfig_FunctionName_CanBeSet(string functionName)
        {
            // Arrange
            var config = new NamingConfig();

            // Act
            config.FunctionName = functionName;

            // Assert
            config.FunctionName.Should().Be(functionName);
        }

        [Fact]
        public void NamingConfig_JsonSerialization_PreservesAllProperties()
        {
            // Arrange
            var originalConfig = new NamingConfig
            {
                Version = 2,
                MaxRecursionLevel = 3,
                FunctionName = "test_function",
                FunctionDescription = "Test description",
                ReturnToolName = "test_return",
                ReturnToolDescription = "Test return description",
                UrgingMessage = "Custom urging message",
                PromptMessage = "Custom prompt message",
                ExecutivePerson = new ConfigPerson { Name = "TestPerson", Description = "Test Description" },
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "param1", Description = "Parameter 1", IsRequired = true, Type = ParameterType.String }
                },
                ReturnParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "result", Description = "Result", IsRequired = false, Type = ParameterType.Bool }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(originalConfig);
            var deserializedConfig = JsonSerializer.Deserialize<NamingConfig>(json);

            // Assert
            deserializedConfig.Should().NotBeNull();
            deserializedConfig!.Version.Should().Be(originalConfig.Version);
            deserializedConfig.MaxRecursionLevel.Should().Be(originalConfig.MaxRecursionLevel);
            deserializedConfig.FunctionName.Should().Be(originalConfig.FunctionName);
            deserializedConfig.FunctionDescription.Should().Be(originalConfig.FunctionDescription);
            deserializedConfig.ReturnToolName.Should().Be(originalConfig.ReturnToolName);
            deserializedConfig.ReturnToolDescription.Should().Be(originalConfig.ReturnToolDescription);
            deserializedConfig.UrgingMessage.Should().Be(originalConfig.UrgingMessage);
            deserializedConfig.PromptMessage.Should().Be(originalConfig.PromptMessage);
            
            deserializedConfig.ExecutivePerson.Should().NotBeNull();
            deserializedConfig.ExecutivePerson!.Name.Should().Be(originalConfig.ExecutivePerson.Name);
            deserializedConfig.ExecutivePerson.Description.Should().Be(originalConfig.ExecutivePerson.Description);
            
            deserializedConfig.InputParameters.Should().HaveCount(1);
            deserializedConfig.InputParameters[0].Name.Should().Be("param1");
            deserializedConfig.InputParameters[0].IsRequired.Should().BeTrue();
            
            deserializedConfig.ReturnParameters.Should().HaveCount(1);
            deserializedConfig.ReturnParameters[0].Name.Should().Be("result");
            deserializedConfig.ReturnParameters[0].IsRequired.Should().BeFalse();
        }

        [Fact]
        public void NamingConfig_ErrorReportingConfig_SerializesAndDeserializes()
        {
            // Arrange
            var originalConfig = new NamingConfig
            {
                ReturnToolName = "submit_result",
                ErrorReportingToolName = "report_issue",
                ErrorReportingConfig = new ErrorReportingConfig
                {
                    ToolDescription = "Report an issue to the parent session",
                    Behavior = ErrorReportingBehavior.ReportError,
                    CustomErrorMessageToParent = "A child session failed",
                    Parameters = new List<ParameterConfig>
                    {
                        new ParameterConfig { Name = "error_message", Description = "Details", IsRequired = true, Type = ParameterType.String },
                        new ParameterConfig { Name = "error_type", Description = "Category", IsRequired = false, Type = ParameterType.String }
                    }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(originalConfig);
            var deserializedConfig = JsonSerializer.Deserialize<NamingConfig>(json);

            // Assert
            deserializedConfig.Should().NotBeNull();
            deserializedConfig!.ErrorReportingConfig.Should().NotBeNull();

            var errorConfig = deserializedConfig.ErrorReportingConfig!;
            deserializedConfig.ErrorReportingToolName.Should().Be("report_issue");
            errorConfig.ToolDescription.Should().Be("Report an issue to the parent session");
            errorConfig.Behavior.Should().Be(ErrorReportingBehavior.ReportError);
            errorConfig.CustomErrorMessageToParent.Should().Be("A child session failed");

            errorConfig.Parameters.Should().HaveCount(2);
            errorConfig.Parameters[0].Name.Should().Be("error_message");
            errorConfig.Parameters[0].IsRequired.Should().BeTrue();
            errorConfig.Parameters[1].Name.Should().Be("error_type");
            errorConfig.Parameters[1].IsRequired.Should().BeFalse();
        }

        [Fact]
        public void NamingConfig_EmptyJsonSerialization_CreatesValidConfig()
        {
            // Arrange
            var emptyJson = "{}";

            // Act
            var config = JsonSerializer.Deserialize<NamingConfig>(emptyJson);

            // Assert
            config.Should().NotBeNull();
            config!.Version.Should().Be(1); // Constructor sets default to 1
            config.MaxRecursionLevel.Should().Be(1); // Constructor sets default to 1
            config.FunctionName.Should().Be("create_subtask"); // Constructor sets default
        }

        [Fact]
        public void ConfigPerson_Properties_CanBeSetAndRetrieved()
        {
            // Arrange & Act
            var person = new ConfigPerson
            {
                Name = "Test Person",
                Description = "A test person description"
            };

            // Assert
            person.Name.Should().Be("Test Person");
            person.Description.Should().Be("A test person description");
        }

        [Fact]
        public void ConfigPerson_DefaultConstructor_InitializesEmptyStrings()
        {
            // Arrange & Act
            var person = new ConfigPerson();

            // Assert
            person.Name.Should().Be(string.Empty);
            person.Description.Should().Be(string.Empty);
        }

        [Theory]
        [InlineData(DanglingBehavior.Urge)]
        [InlineData(DanglingBehavior.ReportError)]
        [InlineData(DanglingBehavior.Pause)]
        public void NamingConfig_DanglingBehavior_CanBeSet(DanglingBehavior behavior)
        {
            // Arrange
            var config = new NamingConfig();

            // Act
            config.DanglingBehavior = behavior;

            // Assert
            config.DanglingBehavior.Should().Be(behavior);
        }

        [Fact]
        public void NamingConfig_ErrorMessage_CanBeSet()
        {
            // Arrange
            var config = new NamingConfig();
            var errorMessage = "Custom error message";

            // Act
            config.ErrorMessage = errorMessage;

            // Assert
            config.ErrorMessage.Should().Be(errorMessage);
        }
    }
}
