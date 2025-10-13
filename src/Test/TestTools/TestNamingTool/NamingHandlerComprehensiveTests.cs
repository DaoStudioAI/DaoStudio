using Xunit;
using FluentAssertions;
using Moq;
using System.Text.Json;
using DaoStudio.Common.Plugins;
using Naming;
using Naming.ParallelExecution;
using System.Reflection;
using System;
using System.Collections.Generic;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using System.Threading.Tasks;
using TestNamingTool.TestInfrastructure.Mocks;
using Res = NamingTool.Properties.Resources;

namespace TestNamingTool;

public class NamingHandlerComprehensiveTests
{
    private readonly Mock<IHost> _mockHost;
    private readonly MockHostSession _mockHostSession;
    private readonly MockPerson _mockPerson;
    private readonly NamingConfig _defaultConfig;
    private readonly NamingHandler _namingHandler;

    public NamingHandlerComprehensiveTests()
    {
        _mockHost = new Mock<IHost>();
        _mockHostSession = new MockHostSession(12345L);
        _mockPerson = new MockPerson("TestAssistant");

        _defaultConfig = new NamingConfig
        {
            PromptMessage = "Hello {{name}}, please help with {{task}}",
            UrgingMessage = "Please complete the task for {{task}}",
            MaxRecursionLevel = 5,
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "name", Type = ParameterType.String, Description = "Person name", IsRequired = true },
                new ParameterConfig { Name = "task", Type = ParameterType.String, Description = "Task to complete", IsRequired = true }
            },
            ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "result", Type = ParameterType.String, Description = "Task result", IsRequired = true }
            }
        };
        
        _namingHandler = new NamingHandler(_mockHost.Object, _defaultConfig, _mockHostSession);
    }

    [Fact]
    public void Constructor_WithNullHost_Should_ThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new NamingHandler(null!, _defaultConfig, _mockHostSession);
        act.Should().Throw<ArgumentNullException>().WithParameterName("host");
    }

    [Fact]
    public void Constructor_WithNullConfig_Should_ThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new NamingHandler(_mockHost.Object, null!, _mockHostSession);
        act.Should().Throw<ArgumentNullException>().WithParameterName("config");
    }

    [Fact]
    public void Constructor_WithNullHostSession_Should_ThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new NamingHandler(_mockHost.Object, _defaultConfig, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("contextSession");
    }

    [Fact]
    public async Task Naming_WithMissingRequiredParameters_Should_ReturnValidationError()
    {
        // Arrange
        var incompleteData = new Dictionary<string, object?>
        {
            ["name"] = "John" // Missing required 'task' parameter
        };

        // Act
        var result = await _namingHandler.Naming(incompleteData);

        // Assert
        result.Should().Contain("Missing required parameters");
        result.Should().Contain("task");
    }

    [Fact]
    public async Task Naming_WithAllRequiredParameters_Should_ProceedToExecution()
    {
        // Arrange
        var validData = new Dictionary<string, object?>
        {
            ["name"] = "John",
            ["task"] = "Create a test plan"
        };

        var mockChildSession = new MockHostSession(67890L);

        _mockHost.Setup(x => x.GetHostPersonsAsync(null))
              .ReturnsAsync(new List<IHostPerson> { _mockPerson });

        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name))
               .ReturnsAsync(mockChildSession);

        // Act
        var result = await _namingHandler.Naming(validData);

        // Assert
        result.Should().NotContain("Missing required parameters");
        _mockHost.Verify(x => x.GetHostPersonsAsync(null), Times.Once);
        _mockHost.Verify(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name), Times.Once);
    }

    [Fact]
    public async Task Naming_WithMissingErrorReportingToolName_ReturnsValidationError()
    {
        // Arrange
        var config = CreateConfigWithErrorReporting();
        config.ErrorReportingToolName = string.Empty;

        var handler = new NamingHandler(_mockHost.Object, config, _mockHostSession);

        // Act
        var result = await handler.Naming(CreateValidRequest());

        // Assert
        result.Should().Be(Res.ErrorReporting_Validation_ToolNameRequired);
    }

    [Fact]
    public async Task Naming_WithConflictingErrorReportingToolName_ReturnsValidationError()
    {
        // Arrange
        var config = CreateConfigWithErrorReporting();
        config.ErrorReportingToolName = config.ReturnToolName;

        var handler = new NamingHandler(_mockHost.Object, config, _mockHostSession);

        // Act
        var result = await handler.Naming(CreateValidRequest());

        // Assert
        result.Should().Be(Res.ErrorReporting_Validation_ToolNameConflict);
    }

    [Fact]
    public async Task Naming_WithBlankErrorReportingParameterName_ReturnsValidationError()
    {
        // Arrange
        var config = CreateConfigWithErrorReporting();
        config.ErrorReportingConfig!.Parameters.Add(new ParameterConfig
        {
            Name = string.Empty,
            Type = ParameterType.String,
            IsRequired = true
        });

        var handler = new NamingHandler(_mockHost.Object, config, _mockHostSession);

        // Act
        var result = await handler.Naming(CreateValidRequest());

        // Assert
        result.Should().Be(Res.ErrorReporting_Validation_InvalidParameterName);
    }

    [Fact]
    public async Task Naming_WithDuplicateErrorReportingParameters_ReturnsValidationError()
    {
        // Arrange
        var config = CreateConfigWithErrorReporting();
        config.ErrorReportingConfig!.Parameters = new List<ParameterConfig>
        {
            new ParameterConfig { Name = "error_message", Type = ParameterType.String, IsRequired = true },
            new ParameterConfig { Name = "error_message", Type = ParameterType.String, IsRequired = false }
        };

        var handler = new NamingHandler(_mockHost.Object, config, _mockHostSession);

        // Act
        var result = await handler.Naming(CreateValidRequest());

        // Assert
        var expected = string.Format(Res.ErrorReporting_Validation_DuplicateParameters, "error_message");
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Naming_WithExecutivePersonConfigured_Should_UseSpecifiedPerson()
    {
        // Arrange
        var configWithExecutivePerson = new NamingConfig
        {
            PromptMessage = "Hello {{name}}",
            UrgingMessage = "Please complete",
            MaxRecursionLevel = 5,
            ExecutivePerson = new ConfigPerson { Name = "SpecificAssistant" },
            InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true }
            }
        };

        var specificPerson = new MockPerson("SpecificAssistant");

        var handler = new NamingHandler(_mockHost.Object, configWithExecutivePerson, _mockHostSession);
        var validData = new Dictionary<string, object?> { ["name"] = "John" };

        _mockHost.Setup(x => x.GetHostPersonsAsync(null))
              .ReturnsAsync(new List<IHostPerson> { _mockPerson, specificPerson });

        var mockChildSession = new MockHostSession(67890L);

        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockHostSession, specificPerson.Name))
               .ReturnsAsync(mockChildSession);

        // Act
        var result = await handler.Naming(validData);

        // Assert
        _mockHost.Verify(x => x.StartNewHostSessionAsync(_mockHostSession, specificPerson.Name), Times.Once);
    }

    [Fact]
    public async Task Naming_WithExecutivePersonNotAvailable_Should_ReturnError()
    {
        // Arrange
        var configWithMissingPerson = new NamingConfig
        {
            PromptMessage = "Hello {{name}}",
            UrgingMessage = "Please complete",
            MaxRecursionLevel = 5,
            ExecutivePerson = new ConfigPerson { Name = "NonExistentAssistant" },
            InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true }
            }
        };

        var handler = new NamingHandler(_mockHost.Object, configWithMissingPerson, _mockHostSession);
        var validData = new Dictionary<string, object?> { ["name"] = "John" };

        _mockHost.Setup(x => x.GetHostPersonsAsync(null))
              .ReturnsAsync(new List<IHostPerson> { _mockPerson }); // Only return TestAssistant, not NonExistentAssistant

        // Act
        Func<Task> act = () => handler.Naming(validData);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*configured people*NonExistentAssistant*");
    }

    [Fact]
    public async Task Naming_WithNoAvailableAssistants_Should_ReturnError()
    {
        // Arrange
        var validData = new Dictionary<string, object?>
        {
            ["name"] = "John",
            ["task"] = "Test task"
        };

        _mockHost.Setup(x => x.GetHostPersonsAsync(null))
              .ReturnsAsync(new List<IHostPerson>()); // No assistants available

        // Act
        Func<Task> act = () => _namingHandler.Naming(validData);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
                 .WithMessage("*No people are available*");
    }

    private static Dictionary<string, object?> CreateValidRequest()
    {
        return new Dictionary<string, object?>
        {
            ["name"] = "Casey",
            ["task"] = "Draft a proposal"
        };
    }

    private NamingConfig CreateConfigWithErrorReporting()
    {
        return new NamingConfig
        {
            PromptMessage = "Hello {{name}}, please assist with {{task}}",
            UrgingMessage = "Please complete the assigned work",
            MaxRecursionLevel = 5,
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "name", Type = ParameterType.String, Description = "Person name", IsRequired = true },
                new ParameterConfig { Name = "task", Type = ParameterType.String, Description = "Task to complete", IsRequired = true }
            },
            ErrorReportingToolName = "report_issue",
            ErrorReportingConfig = new ErrorReportingConfig
            {
                ToolDescription = "Report an issue encountered during execution",
                Behavior = ErrorReportingBehavior.Pause,
                CustomErrorMessageToParent = string.Empty,
                Parameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "error_message", Type = ParameterType.String, IsRequired = true }
                }
            }
        };
    }

    [Fact]
    public async Task Naming_WithInvalidRecursionLevel_Should_ReturnError()
    {
        // Arrange
        var invalidConfig = new NamingConfig
        {
            PromptMessage = "Hello {{name}}",
            UrgingMessage = "Please complete",
            MaxRecursionLevel = -1, // Invalid recursion level
            InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true }
            }
        };

        var handler = new NamingHandler(_mockHost.Object, invalidConfig, _mockHostSession);
        var validData = new Dictionary<string, object?> { ["name"] = "John" };

        // Act
        Func<Task> act = () => handler.Naming(validData);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*Invalid recursion level*");
    }


    [Fact]
    public async Task Naming_WithEmptyUrgingMessage_Should_ThrowException()
    {
        // Arrange
        var configWithEmptyUrging = new NamingConfig
        {
            PromptMessage = "Hello {{name}}",
            UrgingMessage = "", // Empty urging message
            MaxRecursionLevel = 5,
            InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true }
            }
        };

        var handler = new NamingHandler(_mockHost.Object, configWithEmptyUrging, _mockHostSession);
        var validData = new Dictionary<string, object?> { ["name"] = "John" };

        var mockChildHostSession = new Mock<IHostSession>();
        mockChildHostSession.Setup(x => x.Id).Returns(67890L);

        _mockHost.Setup(x => x.GetHostPersonsAsync(null))
              .ReturnsAsync(new List<IHostPerson> { _mockPerson });
        
        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name))
               .ReturnsAsync(mockChildHostSession.Object);

        // Act
        Func<Task> act = () => handler.Naming(validData);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*UrgingMessage cannot be empty*");
    }

    [Fact]
    public async Task Naming_WithParallelExecution_Should_ExecuteParallelSessions()
    {
        // Arrange
        var parallelConfig = new NamingConfig
        {
            PromptMessage = "Process {{item}}",
            UrgingMessage = "Complete processing {{item}}",
            MaxRecursionLevel = 5,
            InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "items", Type = ParameterType.Array, IsRequired = true }
            },
            ParallelConfig = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                ListParameterName = "items",
                MaxConcurrency = 2,
                ResultStrategy = ParallelResultStrategy.WaitForAll
            }
        };

        var handler = new NamingHandler(_mockHost.Object, parallelConfig, _mockHostSession);
        var parallelData = new Dictionary<string, object?>
        {
            ["items"] = new List<object> { "Item1", "Item2", "Item3" }
        };

        _mockHost.Setup(x => x.GetHostPersonsAsync(null))
              .ReturnsAsync(new List<IHostPerson> { _mockPerson });

        // Act
        var result = await handler.Naming(parallelData);

        // Assert
        // The actual parallel execution behavior depends on ParallelSessionManager,
        // but we can verify that it attempts parallel execution
        _mockHost.Verify(x => x.GetHostPersonsAsync(null), Times.Once);
    }

    [Fact]
    public async Task Naming_WithNullRequestData_Should_UseEmptyDictionary()
    {
        // Arrange
        var configWithoutRequiredParams = new NamingConfig
        {
            PromptMessage = "Simple task",
            UrgingMessage = "Please complete",
            MaxRecursionLevel = 5,
            InputParameters = new List<ParameterConfig>() // No required parameters
        };

        var handler = new NamingHandler(_mockHost.Object, configWithoutRequiredParams, _mockHostSession);

        var mockChildHostSession = new MockHostSession(67890L);

        _mockHost.Setup(x => x.GetHostPersonsAsync(null))
              .ReturnsAsync(new List<IHostPerson> { _mockPerson });

        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name))
               .ReturnsAsync(mockChildHostSession);

        // Act
        var result = await handler.Naming(null!);

        // Assert
        result.Should().NotContain("Missing required parameters");
        _mockHost.Verify(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name), Times.Once);
    }

    [Fact]
    public async Task Naming_WithExceptionInExecution_Should_ThrowArgumentException()
    {
        // Arrange
        var validData = new Dictionary<string, object?>
        {
            ["name"] = "John",
            ["task"] = "Test task"
        };

        _mockHost.Setup(x => x.GetHostPersonsAsync(null))
              .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        Func<Task> act = () => _namingHandler.Naming(validData);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
                 .WithMessage("*No people are available*");
    }

    [Fact]
    public async Task Naming_WithComplexParameterValidation_Should_HandleCorrectly()
    {
        // Arrange
        var complexConfig = new NamingConfig
        {
            PromptMessage = "Complex task with {{required1}} and {{required2}}",
            UrgingMessage = "Please complete task",
            MaxRecursionLevel = 5,
            InputParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "required1", Type = ParameterType.String, Description = "First required param", IsRequired = true },
                new ParameterConfig { Name = "required2", Type = ParameterType.String, Description = "Second required param", IsRequired = true },
                new ParameterConfig { Name = "optional1", Type = ParameterType.String, Description = "Optional param", IsRequired = false }
            }
        };

        var handler = new NamingHandler(_mockHost.Object, complexConfig, _mockHostSession);

        // Test with missing required parameters
        var incompleteData = new Dictionary<string, object?>
        {
            ["required1"] = "Value1",
            ["optional1"] = "OptionalValue"
            // Missing required2
        };

        // Act
        var result = await handler.Naming(incompleteData);

        // Assert
        result.Should().Contain("Missing required parameters");
        result.Should().Contain("required2");
        result.Should().Contain("Second required param");
    }
}
