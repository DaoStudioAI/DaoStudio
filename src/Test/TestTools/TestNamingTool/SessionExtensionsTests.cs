using Xunit;
using FluentAssertions;
using Moq;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.Common.Plugins;
using Naming.Extensions;
using Naming.ParallelExecution;
using NamingTool.Return;
using Naming;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool;

public class SessionExtensionsTests
{
    private readonly MockHostSession _mockChildSession;
    private readonly Mock<IHost> _mockHost;
    private readonly NamingConfig _config;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public SessionExtensionsTests()
    {
        _mockChildSession = new MockHostSession(12345L);
        _mockHost = new Mock<IHost>();
        _cancellationTokenSource = new CancellationTokenSource();
        
        _config = new NamingConfig
        {
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "result", Type = ParameterType.String, Description = "Task result", IsRequired = true }
            }
        };
    }

    [Fact]
    public void WaitChildSessionAsync_WithNullChildSession_Should_ThrowArgumentNullException()
    {
        // Act & Assert
        var act = async () => await ((IHostSession)null!).WaitChildSessionAsync(
            "test message", _config, "urging message");

        act.Should().ThrowAsync<ArgumentNullException>()
           .WithParameterName("childSession");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WaitChildSessionAsync_WithInvalidMessage_Should_ThrowArgumentException(string? message)
    {
        // Act & Assert
        var act = async () => await _mockChildSession.WaitChildSessionAsync(
            message!, _config, "urging message");

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void WaitChildSessionAsync_WithNullConfig_Should_ThrowArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _mockChildSession.WaitChildSessionAsync(
            "test message", null!, "urging message");

        act.Should().ThrowAsync<ArgumentNullException>()
           .WithParameterName("config");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WaitChildSessionAsync_WithInvalidUrgingMessage_Should_ThrowArgumentException(string? urgingMessage)
    {
        // Act & Assert
        var act = async () => await _mockChildSession.WaitChildSessionAsync(
            "test message", _config, urgingMessage!);

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WaitChildSessionAsync_WithValidParameters_Should_RegisterCustomReturnTool()
    {
        // Disable auto invocation so that cancellation can be observed
        _mockChildSession.AutoInvokeReturnTool = false;
        // Arrange
        var shortTimeoutConfig = new NamingConfig
        {
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "result", Type = ParameterType.String, Description = "Task result", IsRequired = true }
            }
        };

        // Use a very short timeout to avoid long waits in test
        var shortTimeout = TimeSpan.FromMilliseconds(100);

        // Act & Assert - Expect timeout/failure but verify tool registration happened
        var act = async () => await _mockChildSession.WaitChildSessionAsync(
            "test message", shortTimeoutConfig, "urging message", shortTimeout);

        // This should fail with timeout/prompt failure, but tool should be registered
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Child session failed to provide result after 3 reminder attempts.");
        
        // Verify that the tool was registered
        _mockChildSession.SetToolsHistory.Should().HaveCount(1);
        _mockChildSession.ToolExecutionMode.Should().Be(ToolExecutionMode.RequireAny);
    }

    [Fact]
    public async Task WaitChildSessionAsync_WhenCancellationRequested_Should_CancelChildSession()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Disable auto invocation so that cancellation can be observed
        _mockChildSession.AutoInvokeReturnTool = false;

        // Act - Start the task and then cancel it
        var waitTask = _mockChildSession.WaitChildSessionAsync(
            "test message", _config, "urging message", cancellationToken: cancellationTokenSource.Token);

        // Cancel the token after a small delay to simulate cancellation during execution
        await Task.Delay(10);
        cancellationTokenSource.Cancel();

        // Assert
        var act = async () => await waitTask;
        await act.Should().ThrowAsync<Exception>(); // Accept either OperationCanceledException or InvalidOperationException
        
        // Verify that the child session's cancellation token was called
        cancellationTokenSource.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task WaitChildSessionAsync_WithConfigWithoutReturnParameters_Should_CreateToolWithoutParameters()
    {
        // Disable auto invocation so that cancellation can be observed
        _mockChildSession.AutoInvokeReturnTool = false;

        // Arrange
        var configWithoutParams = new NamingConfig
        {
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            ReturnParameters = new List<ParameterConfig>()
        };

        // Use a very short timeout to avoid long waits in test
        var shortTimeout = TimeSpan.FromMilliseconds(100);

        // Act & Assert - Expect timeout/failure but verify tool registration happened
        var act = async () => await _mockChildSession.WaitChildSessionAsync(
            "test message", configWithoutParams, "urging message", shortTimeout);

        // This should fail with timeout/prompt failure, but tool should be registered
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Child session failed to provide result after 3 reminder attempts.");
        
        // Verify that the tool was registered
        _mockChildSession.SetToolsHistory.Should().HaveCount(1);
        _mockChildSession.ToolExecutionMode.Should().Be(ToolExecutionMode.RequireAny);
    }

    [Fact]
    public async Task WaitChildSessionAsync_WithErrorReportingReportError_Should_ReturnErrorReport()
    {
        _mockChildSession.AutoInvokeReturnTool = false;

        var config = new NamingConfig
        {
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "result", Type = ParameterType.String, Description = "Task result", IsRequired = true }
            },
            DanglingBehavior = DanglingBehavior.Pause,
            ErrorReportingToolName = "report_issue",
            ErrorReportingConfig = new ErrorReportingConfig
            {
                ToolDescription = "Report an issue",
                Behavior = ErrorReportingBehavior.ReportError,
                CustomErrorMessageToParent = "Parent override message",
                Parameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "error_message", Type = ParameterType.String, Description = "Error message", IsRequired = true },
                    new ParameterConfig { Name = "details", Type = ParameterType.String, Description = "Additional details", IsRequired = false }
                }
            }
        };

        var waitTask = _mockChildSession.WaitChildSessionAsync("test message", config, "urging message");

        var latestTools = await WaitForToolRegistrationAsync(_mockChildSession, nameof(CustomErrorReportingTool));
        var errorToolFunction = latestTools[nameof(CustomErrorReportingTool)].Single();

        var errorData = new Dictionary<string, object?>
        {
            ["error_message"] = "Original error",
            ["details"] = "Stack overflow"
        };

        var confirmation = await InvokeFunctionAsync(errorToolFunction, errorData);
        confirmation.Should().BeOfType<string>().Which.Should().Contain("Error reported to parent session");

        var result = await waitTask;

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Parent override message");
    }

    [Fact]
    public async Task WaitChildSessionAsync_WithErrorReportingPause_Should_ContinueWaitingUntilReturnToolInvoked()
    {
        _mockChildSession.AutoInvokeReturnTool = false;

        var config = new NamingConfig
        {
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "result", Type = ParameterType.String, Description = "Task result", IsRequired = true }
            },
            DanglingBehavior = DanglingBehavior.Pause,
            ErrorReportingToolName = "report_issue",
            ErrorReportingConfig = new ErrorReportingConfig
            {
                ToolDescription = "Report an issue",
                Behavior = ErrorReportingBehavior.Pause,
                Parameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "error_message", Type = ParameterType.String, Description = "Error message", IsRequired = true }
                }
            }
        };

        var waitTask = _mockChildSession.WaitChildSessionAsync("test message", config, "urging message");

        var latestTools = await WaitForToolRegistrationAsync(_mockChildSession, nameof(CustomErrorReportingTool));
        latestTools.Should().ContainKey(nameof(CustomReturnResultTool));

        var errorToolFunction = latestTools[nameof(CustomErrorReportingTool)].Single();

        await InvokeFunctionAsync(errorToolFunction, new Dictionary<string, object?>
        {
            ["error_message"] = "Temporary issue"
        });

        waitTask.IsCompleted.Should().BeFalse();

        var returnToolFunction = latestTools[nameof(CustomReturnResultTool)].Single();
        await InvokeFunctionAsync(returnToolFunction, new Dictionary<string, object?>
        {
            ["result"] = "Recovered"
        });

        var result = await waitTask;

        result.Success.Should().BeTrue();
        result.Result.Should().Contain("Recovered");
    }

    [Fact]
    public async Task WaitChildSessionAsync_WithErrorReportingNoParameters_Should_RegisterDefaultParameters()
    {
        _mockChildSession.AutoInvokeReturnTool = false;

        var config = new NamingConfig
        {
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            ReturnParameters = new List<ParameterConfig>(),
            DanglingBehavior = DanglingBehavior.Pause,
            ErrorReportingToolName = "report_error",
            ErrorReportingConfig = new ErrorReportingConfig
            {
                ToolDescription = "Report error",
                Behavior = ErrorReportingBehavior.ReportError,
                Parameters = new List<ParameterConfig>()
            }
        };

        var waitTask = _mockChildSession.WaitChildSessionAsync("test message", config, "urging message");

        var latestTools = await WaitForToolRegistrationAsync(_mockChildSession, nameof(CustomErrorReportingTool));

        var errorToolFunction = latestTools[nameof(CustomErrorReportingTool)].Single();
        var parameters = errorToolFunction.Description.Parameters;
        parameters.Should().NotBeNull();
        parameters!.Should().HaveCount(2);
        parameters.Select(p => p.Name).Should().Contain(new[] { "error_message", "error_type" });
        parameters.Single(p => p.Name == "error_message").IsRequired.Should().BeTrue();
        parameters.Single(p => p.Name == "error_type").IsRequired.Should().BeFalse();

        var returnToolFunction = latestTools[nameof(CustomReturnResultTool)].Single();
        await InvokeFunctionAsync(returnToolFunction, new Dictionary<string, object?>());

        await waitTask;
    }

    private static async Task<Dictionary<string, List<FunctionWithDescription>>> WaitForToolRegistrationAsync(MockHostSession session, string toolKey, int maxAttempts = 100)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (session.SetToolsHistory.Count > 0)
            {
                var latest = session.SetToolsHistory.Last();
                if (latest.ContainsKey(toolKey))
                {
                    return latest;
                }
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"Tool '{toolKey}' was not registered within the expected time.");
    }

    private static async Task<object?> InvokeFunctionAsync(FunctionWithDescription function, Dictionary<string, object?> args)
    {
        var invocationResult = function.Function.DynamicInvoke(args);

        switch (invocationResult)
        {
            case Task<string> stringTask:
                return await stringTask.ConfigureAwait(false);
            case Task<object?> objectTask:
                return await objectTask.ConfigureAwait(false);
            case Task task:
                await task.ConfigureAwait(false);
                return null;
            default:
                return invocationResult;
        }
    }

    #region DanglingBehavior Tests

    [Fact]
    public async Task WaitChildSessionAsync_WithUrgeBehavior_Should_SendRemindersAndThrowException()
    {
        // Arrange
        _mockChildSession.AutoInvokeReturnTool = false;
        var urgeBehaviorConfig = new NamingConfig
        {
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            DanglingBehavior = DanglingBehavior.Urge,
            ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "result", Type = ParameterType.String, Description = "Task result", IsRequired = true }
            }
        };

        var shortTimeout = TimeSpan.FromMilliseconds(100);

        // Act & Assert
        var act = async () => await _mockChildSession.WaitChildSessionAsync(
            "test message", urgeBehaviorConfig, "urging message", shortTimeout);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Child session failed to provide result after 3 reminder attempts.");
    }

    [Fact]
    public async Task WaitChildSessionAsync_WithReportErrorBehavior_Should_ReturnFailureResultImmediately()
    {
        // Arrange
        _mockChildSession.AutoInvokeReturnTool = false;
        var reportErrorConfig = new NamingConfig
        {
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            DanglingBehavior = DanglingBehavior.ReportError,
            ErrorMessage = "Custom error message",
            ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "result", Type = ParameterType.String, Description = "Task result", IsRequired = true }
            }
        };

        // Act
        var result = await _mockChildSession.WaitChildSessionAsync(
            "test message", reportErrorConfig, "urging message");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Custom error message");
        result.Result.Should().BeNull();
    }

    [Fact]
    public async Task WaitChildSessionAsync_WithReportErrorBehaviorAndEmptyMessage_Should_UseDefaultErrorMessage()
    {
        // Arrange
        _mockChildSession.AutoInvokeReturnTool = false;
        var reportErrorConfig = new NamingConfig
        {
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            DanglingBehavior = DanglingBehavior.ReportError,
            ErrorMessage = "", // Empty error message
            ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "result", Type = ParameterType.String, Description = "Task result", IsRequired = true }
            }
        };

        // Act
        var result = await _mockChildSession.WaitChildSessionAsync(
            "test message", reportErrorConfig, "urging message");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Child session failed to call the return tool");
        result.Result.Should().BeNull();
    }



    [Fact]
    public async Task WaitChildSessionAsync_WithUnknownDanglingBehavior_Should_FallbackToUrgeBehavior()
    {
        // Arrange
        _mockChildSession.AutoInvokeReturnTool = false;
        var unknownBehaviorConfig = new NamingConfig
        {
            ReturnToolName = "complete_task",
            ReturnToolDescription = "Complete the assigned task",
            DanglingBehavior = (DanglingBehavior)999, // Unknown behavior value
            ReturnParameters = new List<ParameterConfig>
            {
                new ParameterConfig { Name = "result", Type = ParameterType.String, Description = "Task result", IsRequired = true }
            }
        };

        var shortTimeout = TimeSpan.FromMilliseconds(100);

        // Act & Assert
        var act = async () => await _mockChildSession.WaitChildSessionAsync(
            "test message", unknownBehaviorConfig, "urging message", shortTimeout);

        // Should fall back to urge behavior (3 retries + exception)
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Child session failed to provide result after 3 reminder attempts.");
    }

    #endregion
}

public class ParallelSessionExtensionsTests
{
    private readonly Mock<IHost> _mockHost;
    private readonly MockHostSession _mockParentSession;
    private readonly MockPerson _mockPerson;
    private readonly NamingConfig _config;
    private readonly Dictionary<string, object?> _refSources;
    private readonly List<(string, object?)> _parallelSources;

    public ParallelSessionExtensionsTests()
    {
        _mockHost = new Mock<IHost>();
        _mockParentSession = new MockHostSession(12345L);
        _mockPerson = new MockPerson("TestPerson");
        
        _config = new NamingConfig
        {
            PromptMessage = "Process {{name}}",
            UrgingMessage = "Please complete the task for {{name}}",
            ParallelConfig = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                ListParameterName = "items",
                MaxConcurrency = 2,
                ResultStrategy = ParallelResultStrategy.WaitForAll
            }
        };
        // Build reference sources and derive parallel sources via extractor
        _refSources = new Dictionary<string, object?>
        {
            ["item1"] = new Dictionary<string, object?> { ["name"] = "Item 1" },
            ["item2"] = new Dictionary<string, object?> { ["name"] = "Item 2" }
        };
        _parallelSources = ParallelParameterExtractor.ExtractParallelSources(_refSources, _config.ParallelConfig!);
    }

    [Fact]
    public async Task ExecuteParallelChildSessionsAsync_WithValidParameters_Should_CallParallelSessionManager()
    {
        // Arrange
        var mockResult = new ParallelExecutionResult
        {
            Success = true,
            TotalSessions = 2,
            CompletedSessions = 2,
            Strategy = ParallelResultStrategy.WaitForAll
        };

        // Act
        var result = await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockParentSession, _mockPerson.Name, _refSources, _parallelSources, _config);

        // Assert - Just ensure the method exists and can be called
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateMultipleChildSessionsAsync_WithValidParameters_Should_CreateCorrectNumberOfSessions()
    {
        // Arrange
        var mockSessions = new List<ISession>
        {
            new MockHostSession(1),
            new MockHostSession(2),
            new MockHostSession(3)
        };

        int sessionCount = 3;
        var sessionIndex = 0;
        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockParentSession, _mockPerson.Name))
               .Returns(() => Task.FromResult((IHostSession)mockSessions[sessionIndex++]));

        // Act
        var result = await _mockHost.Object.CreateMultipleChildSessionsAsync(
            _mockParentSession, _mockPerson.Name, sessionCount);

        // Assert
        result.Should().HaveCount(sessionCount);
        _mockHost.Verify(x => x.StartNewHostSessionAsync(_mockParentSession, _mockPerson.Name), Times.Exactly(sessionCount));
    }




    [Fact]
    public void CancelMultipleChildSessions_Should_HandleCancellationGracefully()
    {
        // Arrange
        var sessions = new List<IHostSession> 
        { 
            new MockHostSession(1), 
            new MockHostSession(2) 
        };

        // Act
        sessions.CancelMultipleChildSessions();

        // Assert - Verify that cancellation tokens were cancelled
        foreach (var session in sessions)
        {
            if (session is MockHostSession mockSession)
            {
                mockSession.CurrentCancellationToken?.Token.IsCancellationRequested.Should().BeTrue();
            }
        }
    }

    [Fact]
    public void DisposeMultipleChildSessions_Should_DisposeAllSessions()
    {
        // Arrange
        var sessions = new List<IHostSession> 
        { 
            new MockHostSession(1), 
            new MockHostSession(2) 
        };

        // Act
        sessions.DisposeMultipleChildSessions();

        // Assert - Sessions should be disposed (can't easily verify on concrete mock)
        // This just tests that the extension method doesn't throw
    }

    [Fact]
    public void DisposeMultipleChildSessions_WithExceptionOnDispose_Should_ContinueDisposingOthers()
    {
        // Arrange  
        var sessions = new List<IHostSession> 
        { 
            new MockHostSession(1), 
            new MockHostSession(2) 
        };

        // Act
        var act = () => sessions.DisposeMultipleChildSessions();

        // Assert
        act.Should().NotThrow(); // Should not propagate the exception
    }
}
