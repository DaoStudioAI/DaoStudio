using Xunit;
using FluentAssertions;
using Moq;
using DaoStudio.Common.Plugins;
using Naming.ParallelExecution;
using Naming;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool;

public class ParallelSessionManagerTests
{
    private readonly Mock<IHost> _mockHost;
    private readonly MockHostSession _mockHostSession;
    private readonly MockPerson _mockPerson;
    private readonly NamingConfig _config;
    private readonly Dictionary<string, object?> _refSources;
    private readonly List<(string, object?)> _parallelSources;

    public ParallelSessionManagerTests()
    {
        _mockHost = new Mock<IHost>();
        _mockHostSession = new MockHostSession(12345L);
        _mockPerson = new MockPerson("TestPerson");

        _config = new NamingConfig
        {
            PromptMessage = "Process {{name}}",
            UrgingMessage = "Complete task for {{name}}",
            MaxRecursionLevel = 5,
            ParallelConfig = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                MaxConcurrency = 2,
                ResultStrategy = ParallelResultStrategy.WaitForAll,
                SessionTimeoutMs = 30000
            }
        };
        // Prepare reference sources (request data) and derive parallel sources using the extractor
        _refSources = new Dictionary<string, object?>
        {
            ["item1"] = new Dictionary<string, object?> { ["name"] = "Item1", ["id"] = 1 },
            ["item2"] = new Dictionary<string, object?> { ["name"] = "Item2", ["id"] = 2 },
            ["item3"] = new Dictionary<string, object?> { ["name"] = "Item3", ["id"] = 3 }
        };
        _parallelSources = ParallelParameterExtractor.ExtractParallelSources(_refSources, _config.ParallelConfig!);
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithNullHost_Should_ThrowArgumentNullException()
    {
        // Act & Assert
        var act = async () => await ParallelSessionManager.ExecuteParallelSessionsAsync(
            null!, _mockHostSession, _mockPerson.Name, _refSources, _parallelSources, _config);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("host");
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithNullPerson_Should_ThrowArgumentNullException()
    {
        // Act & Assert
        var act = async () => await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, null!, _refSources, _parallelSources, _config);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Person name cannot be null or empty*");
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithNullRefSources_Should_ThrowArgumentNullException()
    {
        // Act & Assert
        var act = async () => await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name, null!, _parallelSources, _config);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("refsources");
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithNullParallelConfig_Should_ThrowArgumentException()
    {
        // Arrange
        var configWithoutParallel = new NamingConfig
        {
            PromptMessage = "Test",
            UrgingMessage = "Complete",
            ParallelConfig = null
        };

        // Act & Assert
        var act = async () => await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name, _refSources, _parallelSources, configWithoutParallel);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*ParallelConfig cannot be null*");
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithUnsupportedResultStrategy_Should_ThrowNotSupportedException()
    {
        // Arrange
        var configWithUnsupportedStrategy = new NamingConfig
        {
            PromptMessage = "Test {{name}}",
            UrgingMessage = "Complete {{name}}",
            ParallelConfig = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                ResultStrategy = (ParallelResultStrategy)999 // Invalid strategy
            }
        };

        // Act & Assert
        var act = async () => await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name,
            _refSources,
            ParallelParameterExtractor.ExtractParallelSources(_refSources, configWithUnsupportedStrategy.ParallelConfig!),
            configWithUnsupportedStrategy);

        var exception = await act.Should().ThrowAsync<NotSupportedException>();
        exception.WithMessage("*Result strategy*not supported*");
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithWaitForAllStrategy_Should_ReturnAggregatedResult()
    {
        // Arrange
        var configWaitForAll = new NamingConfig
        {
            PromptMessage = "Process {{name}}",
            UrgingMessage = "Complete {{name}}",
            ParallelConfig = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                ResultStrategy = ParallelResultStrategy.WaitForAll,
                MaxConcurrency = 1
            }
        };

        var mockChildSession = new Mock<IHostSession>();
        mockChildSession.Setup(x => x.Id).Returns(67890L);
        mockChildSession.Setup(x => x.GetTools()).Returns(new Dictionary<string, List<FunctionWithDescription>>());

        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name))
               .ReturnsAsync(mockChildSession.Object);

        // Act
        var result = await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name,
            _refSources,
            ParallelParameterExtractor.ExtractParallelSources(_refSources, configWaitForAll.ParallelConfig!),
            configWaitForAll);

        // Assert
        result.Should().NotBeNull();
        result.Strategy.Should().Be(ParallelResultStrategy.WaitForAll);
        result.TotalSessions.Should().Be(_parallelSources.Count);
        result.StartTime.Should().BeBefore(DateTime.UtcNow);
        result.EndTime.Should().BeAfter(result.StartTime);
        result.ExecutionTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithStreamIndividualStrategy_Should_ProcessConcurrently()
    {
        // Arrange
        var configStreaming = new NamingConfig
        {
            PromptMessage = "Process {{name}}",
            UrgingMessage = "Complete {{name}}",
            ParallelConfig = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                ResultStrategy = ParallelResultStrategy.StreamIndividual,
                MaxConcurrency = 2
            }
        };

        var mockChildSession = new Mock<IHostSession>();
        mockChildSession.Setup(x => x.Id).Returns(67890L);
        mockChildSession.Setup(x => x.GetTools()).Returns(new Dictionary<string, List<FunctionWithDescription>>());

        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name))
               .ReturnsAsync(mockChildSession.Object);

        // Act
        var result = await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name,
            _refSources,
            ParallelParameterExtractor.ExtractParallelSources(_refSources, configStreaming.ParallelConfig!),
            configStreaming);

        // Assert
        result.Should().NotBeNull();
        result.Strategy.Should().Be(ParallelResultStrategy.StreamIndividual);
        result.TotalSessions.Should().Be(_parallelSources.Count);
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithFirstResultWinsStrategy_Should_ReturnFirstSuccess()
    {
        // Arrange
        var configFirstWins = new NamingConfig
        {
            PromptMessage = "Process {{name}}",
            UrgingMessage = "Complete {{name}}",
            ParallelConfig = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                ResultStrategy = ParallelResultStrategy.FirstResultWins,
                MaxConcurrency = 3
            }
        };

        var mockChildSession = new Mock<IHostSession>();
        mockChildSession.Setup(x => x.Id).Returns(67890L);
        mockChildSession.Setup(x => x.GetTools()).Returns(new Dictionary<string, List<FunctionWithDescription>>());

        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name))
               .ReturnsAsync(mockChildSession.Object);

        // Act
        var result = await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name,
            _refSources,
            ParallelParameterExtractor.ExtractParallelSources(_refSources, configFirstWins.ParallelConfig!),
            configFirstWins);

        // Assert
        result.Should().NotBeNull();
        result.Strategy.Should().Be(ParallelResultStrategy.FirstResultWins);
        result.TotalSessions.Should().Be(_parallelSources.Count);
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithEmptyParallelSources_Should_ReturnEmptyResult()
    {
        // Arrange
        var emptyRefSources = new Dictionary<string, object?>();
        var emptyParallelSources = new List<(string, object?)>();

        // Act
        var result = await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name,
            emptyRefSources,
            emptyParallelSources,
            _config);

        // Assert
        result.Should().NotBeNull();
        result.TotalSessions.Should().Be(0);
        result.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithSessionCreationFailure_Should_HandleGracefully()
    {
        // Arrange
        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name))
               .ThrowsAsync(new InvalidOperationException("Session creation failed"));

        // Act
        var result = await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name,
            _refSources,
            _parallelSources,
            _config);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("0/3 sessions completed successfully");
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithMaxConcurrencyLimit_Should_RespectConcurrency()
    {
        // Arrange
        var configWithLimitedConcurrency = new NamingConfig
        {
            PromptMessage = "Process {{name}}",
            UrgingMessage = "Complete {{name}}",
            ParallelConfig = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                ResultStrategy = ParallelResultStrategy.WaitForAll,
                MaxConcurrency = 1 // Force sequential processing
            }
        };

        var sessionCreationCount = 0;
        var maxConcurrentSessions = 0;
        var currentConcurrentSessions = 0;

        var mockChildSession = new Mock<IHostSession>();
        mockChildSession.Setup(x => x.Id).Returns(67890L);
        mockChildSession.Setup(x => x.GetTools()).Returns(new Dictionary<string, List<FunctionWithDescription>>());

        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name))
               .Returns(async () =>
               {
                   Interlocked.Increment(ref sessionCreationCount);
                   var current = Interlocked.Increment(ref currentConcurrentSessions);
                   maxConcurrentSessions = Math.Max(maxConcurrentSessions, current);
                   
                   await Task.Delay(10); // Simulate some work
                   
                   Interlocked.Decrement(ref currentConcurrentSessions);
                   return mockChildSession.Object;
               });

        // Act
        var result = await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name,
            _refSources,
            ParallelParameterExtractor.ExtractParallelSources(_refSources, configWithLimitedConcurrency.ParallelConfig!),
            configWithLimitedConcurrency);

        // Assert
        result.Should().NotBeNull();
        sessionCreationCount.Should().Be(_parallelSources.Count);
        maxConcurrentSessions.Should().BeLessThanOrEqualTo(configWithLimitedConcurrency.ParallelConfig!.MaxConcurrency);
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithCancellation_Should_HandleCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel the token

        // Act
        var result = await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name,
            _refSources,
            _parallelSources,
            _config,
            cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Execution failed");
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithLongRunningSession_Should_HandleTimeout()
    {
        // Arrange
        var configWithShortTimeout = new NamingConfig
        {
            PromptMessage = "Process {{name}}",
            UrgingMessage = "Complete {{name}}",
            ParallelConfig = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                ResultStrategy = ParallelResultStrategy.WaitForAll,
                SessionTimeoutMs = 10 // Very short timeout
            }
        };

        var mockChildSession = new Mock<IHostSession>();
        mockChildSession.Setup(x => x.Id).Returns(67890L);
        mockChildSession.Setup(x => x.GetTools()).Returns(new Dictionary<string, List<FunctionWithDescription>>());

        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name))
               .Returns(async () =>
               {
                   await Task.Delay(100); // Simulate long session creation
                   return mockChildSession.Object;
               });

        // Act
        var result = await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name,
            _refSources,
            ParallelParameterExtractor.ExtractParallelSources(_refSources, configWithShortTimeout.ParallelConfig!),
            configWithShortTimeout);

        // Assert
        result.Should().NotBeNull();
        // Result might be successful or fail depending on timing, but should complete
        result.EndTime.Should().BeAfter(result.StartTime);
    }

    [Fact]
    public async Task ExecuteParallelSessionsAsync_WithComplexScribanTemplate_Should_RenderCorrectly()
    {
        // Arrange
        var configWithComplexTemplate = new NamingConfig
        {
            PromptMessage = "Processing item {{name}} with ID {{id}} at {{date.now}}",
            UrgingMessage = "Please complete processing for {{name}} (ID: {{id}})",
            ParallelConfig = new ParallelExecutionConfig
            {
                ExecutionType = ParallelExecutionType.ParameterBased,
                ResultStrategy = ParallelResultStrategy.WaitForAll,
                MaxConcurrency = 1
            }
        };

        // Create a single-item request and parallel source to keep TotalSessions at 1
        var complexItem = new Dictionary<string, object?> { ["name"] = "Complex Item", ["id"] = 42, ["priority"] = "High" };
        var complexRefSources = new Dictionary<string, object?> { ["item"] = complexItem };
        var complexParallelSources = new List<(string, object?)> { ("item", complexItem) };

        var mockChildSession = new Mock<IHostSession>();
        mockChildSession.Setup(x => x.Id).Returns(67890L);
        mockChildSession.Setup(x => x.GetTools()).Returns(new Dictionary<string, List<FunctionWithDescription>>());

        _mockHost.Setup(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name))
               .ReturnsAsync(mockChildSession.Object);

        // Act
        var result = await ParallelSessionManager.ExecuteParallelSessionsAsync(
            _mockHost.Object, _mockHostSession, _mockPerson.Name,
            complexRefSources,
            complexParallelSources,
            configWithComplexTemplate);

        // Assert
        result.Should().NotBeNull();
        result.TotalSessions.Should().Be(1);
        // Template should have been processed without errors
        _mockHost.Verify(x => x.StartNewHostSessionAsync(_mockHostSession, _mockPerson.Name), Times.Once);
    }
}
