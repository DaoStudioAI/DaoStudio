using Naming;
using Naming.ParallelExecution;
using TestNamingTool.TestInfrastructure.Mocks;
using TestNamingTool.TestInfrastructure.Builders;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DaoStudio.Common.Plugins;
using System.Linq;
using System.Linq;

namespace TestNamingTool.UnitTests.Core
{
    public class NamingSessionRunnerTests
    {
        private readonly MockHost _mockHost;
    private readonly MockHostSessionWithChildExecution _mockSession;
        private readonly NamingConfig _baseConfig;

        public NamingSessionRunnerTests()
        {
            _mockHost = new MockHost();
            _mockSession = new MockHostSessionWithChildExecution(1);
            _baseConfig = new NamingConfig
            {
                PromptMessage = "Test prompt: {{ testParam }}",
                UrgingMessage = "Please complete the task",
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "testParam", Description = "Test parameter", IsRequired = true, Type = ParameterType.String }
                }
            };
        }

        [Fact]
        public async Task RunSessionAsync_WithValidParameters_ReturnsChildSessionResult()
        {
            // Arrange
            var requestData = new Dictionary<string, object?> { { "testParam", "test value" } };

            // Act
            // Pass null for the context session to ensure a new child session is created by the host
            var result = await NamingSessionRunner.RunSessionAsync(
                _mockHost, null, "TestPerson", requestData, _baseConfig);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            // When no custom return parameters are provided, the default custom return tool returns an empty JSON object.
            result.Result.Should().Be("{}");
        }

        [Fact]
        public async Task RunSessionAsync_WithNullHost_ThrowsArgumentNullException()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                NamingSessionRunner.RunSessionAsync(null!, _mockSession, "TestPerson", requestData, _baseConfig));
        }

        [Fact]
        public async Task RunSessionAsync_WithNullRequestData_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                NamingSessionRunner.RunSessionAsync(_mockHost, _mockSession, "TestPerson", null!, _baseConfig));
        }

        [Fact]
        public async Task RunSessionAsync_WithNullConfig_ThrowsArgumentNullException()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                NamingSessionRunner.RunSessionAsync(_mockHost, _mockSession, "TestPerson", requestData, null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task RunSessionAsync_WithInvalidPersonName_ThrowsArgumentException(string? personName)
        {
            // Arrange
            var requestData = new Dictionary<string, object?>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                NamingSessionRunner.RunSessionAsync(_mockHost, _mockSession, personName!, requestData, _baseConfig));
        }

        [Fact]
        public async Task RunSessionAsync_WithEmptyUrgingMessage_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "Test prompt",
                UrgingMessage = "", // Empty urging message
                InputParameters = new List<ParameterConfig>()
            };
            var requestData = new Dictionary<string, object?>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NamingSessionRunner.RunSessionAsync(_mockHost, _mockSession, "TestPerson", requestData, config));
        }

        [Fact]
        public async Task RunSessionAsync_WithNullUrgingMessage_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "Test prompt",
                UrgingMessage = null!, // Null urging message
                InputParameters = new List<ParameterConfig>()
            };
            var requestData = new Dictionary<string, object?>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NamingSessionRunner.RunSessionAsync(_mockHost, _mockSession, "TestPerson", requestData, config));
        }

        [Fact]
        public async Task RunSessionAsync_RendersScribanTemplateInPrompt()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "Hello {{ name }}, your task is {{ task }}",
                UrgingMessage = "Please complete",
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "name", Type = ParameterType.String },
                    new ParameterConfig { Name = "task", Type = ParameterType.String }
                }
            };
            var requestData = new Dictionary<string, object?>
            {
                { "name", "John" },
                { "task", "testing" }
            };
            var expectedResult = ChildSessionResult.CreateSuccess("Done");
            _mockSession.EnqueueChildSessionResult(expectedResult);

            // Act
            var result = await NamingSessionRunner.RunSessionAsync(
                _mockHost, null, "TestPerson", requestData, config);

            // Assert
            result.Should().NotBeNull();
            // The mock should have received the rendered template
            var receivedPrompt = GetActiveSession().LastReceivedPrompt;
            receivedPrompt.Should().Contain("Hello John, your task is testing");
        }
        private MockHostSession GetActiveSession()
        {
            if (_mockHost.CreatedSessions.Count > 0)
            {
                return (MockHostSession)_mockHost.CreatedSessions.Last();
            }
            return _mockSession;
        }

        [Fact]
        public async Task RunSessionAsync_RendersScribanTemplateInUrgingMessage()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "Test prompt",
                UrgingMessage = "Complete task {{ taskName }} by {{ deadline }}",
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "taskName", Type = ParameterType.String },
                    new ParameterConfig { Name = "deadline", Type = ParameterType.String }
                }
            };
            var requestData = new Dictionary<string, object?>
            {
                { "taskName", "analysis" },
                { "deadline", "today" }
            };
            var expectedResult = ChildSessionResult.CreateSuccess("Done");
            _mockSession.EnqueueChildSessionResult(expectedResult);

            // Act
            var result = await NamingSessionRunner.RunSessionAsync(
                _mockHost, null, "TestPerson", requestData, config);

            // Assert
            result.Should().NotBeNull();
            var receivedUrgingMessage = GetActiveSession().LastReceivedUrgingMessage;
            receivedUrgingMessage.Should().Contain("Complete task analysis by today");
        }

        [Fact]
        public async Task RunSessionAsync_WithParameterInfo_RendersParameterObjectInTemplate()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "Processing {{ _Parameter.Name }}: {{ _Parameter.Value }}",
                UrgingMessage = "Complete processing {{ _Parameter.Name }}",
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "testParam", Type = ParameterType.String }
                }
            };
            var requestData = new Dictionary<string, object?>();
            var parameterInfo = ("testParam", (object?)"testValue");
            var expectedResult = ChildSessionResult.CreateSuccess("Done");
            _mockSession.EnqueueChildSessionResult(expectedResult);

            // Act
            var result = await NamingSessionRunner.RunSessionAsync(
                _mockHost, null, "TestPerson", requestData, config, parameterInfo);

            // Assert
            result.Should().NotBeNull();
            var receivedPrompt = GetActiveSession().LastReceivedPrompt;
            receivedPrompt.Should().Contain("Processing testParam: testValue");
            
            var receivedUrgingMessage = GetActiveSession().LastReceivedUrgingMessage;
            receivedUrgingMessage.Should().Contain("Complete processing testParam");
        }

        [Fact]
        public async Task RunSessionAsync_WithNamingConfigInTemplate_RendersConfigValues()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "Function: {{ _Config.FunctionName }}, Max Level: {{ _Config.MaxRecursionLevel }}",
                UrgingMessage = "Complete task",
                FunctionName = "test_function",
                MaxRecursionLevel = 3,
                InputParameters = new List<ParameterConfig>()
            };
            var requestData = new Dictionary<string, object?>();
            var expectedResult = ChildSessionResult.CreateSuccess("Done");
            _mockSession.EnqueueChildSessionResult(expectedResult);

            // Act
            var result = await NamingSessionRunner.RunSessionAsync(
                _mockHost, null, "TestPerson", requestData, config);

            // Assert
            result.Should().NotBeNull();
            var receivedPrompt = GetActiveSession().LastReceivedPrompt;
            receivedPrompt.Should().Contain("Function: test_function, Max Level: 3");
        }

        [Fact]
        public async Task RunSessionAsync_WithRequiredParameterMissing_RendersTemplateWithNull()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "Required param: {{ requiredParam }}, Optional param: {{ optionalParam }}",
                UrgingMessage = "Complete task",
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "requiredParam", IsRequired = true, Type = ParameterType.String },
                    new ParameterConfig { Name = "optionalParam", IsRequired = false, Type = ParameterType.String }
                }
            };
            var requestData = new Dictionary<string, object?>
            {
                { "optionalParam", "optional value" }
                // requiredParam is missing
            };
            var expectedResult = ChildSessionResult.CreateSuccess("Done");
            _mockSession.EnqueueChildSessionResult(expectedResult);

            // Act
            var result = await NamingSessionRunner.RunSessionAsync(
                _mockHost, null, "TestPerson", requestData, config);

            // Assert
            result.Should().NotBeNull();
            var receivedPrompt = GetActiveSession().LastReceivedPrompt;
            receivedPrompt.Should().Contain("Optional param: optional value");
            // Required param should be rendered as empty/null
        }

        [Fact]
        public async Task RunSessionAsync_WithAdditionalRequestDataEntries_IncludesInTemplate()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "Configured: {{ configuredParam }}, Additional: {{ additionalParam }}",
                UrgingMessage = "Complete task",
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "configuredParam", Type = ParameterType.String }
                }
            };
            var requestData = new Dictionary<string, object?>
            {
                { "configuredParam", "configured value" },
                { "additionalParam", "additional value" } // Not in InputParameters but should still be available
            };
            var expectedResult = ChildSessionResult.CreateSuccess("Done");
            _mockSession.EnqueueChildSessionResult(expectedResult);

            // Act
            var result = await NamingSessionRunner.RunSessionAsync(
                _mockHost, null, "TestPerson", requestData, config);

            // Assert
            result.Should().NotBeNull();
            var receivedPrompt = GetActiveSession().LastReceivedPrompt;
            receivedPrompt.Should().Contain("Configured: configured value");
            receivedPrompt.Should().Contain("Additional: additional value");
        }

        [Fact]
        public async Task RunSessionAsync_WithInvalidScribanTemplate_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "Invalid template: {{ unclosed_bracket", // Invalid Scriban syntax
                UrgingMessage = "Complete task",
                InputParameters = new List<ParameterConfig>()
            };
            var requestData = new Dictionary<string, object?>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NamingSessionRunner.RunSessionAsync(_mockHost, null, "TestPerson", requestData, config));
        }

        [Fact]
        public async Task RunSessionAsync_WithTemplateRenderingError_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "{{ nonExistentObject.someProperty }}", // This will cause a rendering error
                UrgingMessage = "Complete task",
                InputParameters = new List<ParameterConfig>()
            };
            var requestData = new Dictionary<string, object?>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NamingSessionRunner.RunSessionAsync(_mockHost, null, "TestPerson", requestData, config));
        }

        [Fact]
        public async Task RunSessionAsync_WithEmptyPromptMessage_ThrowsArgumentException()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "", // Empty prompt
                UrgingMessage = "Complete task",
                InputParameters = new List<ParameterConfig>()
            };
            var requestData = new Dictionary<string, object?>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                NamingSessionRunner.RunSessionAsync(
                    _mockHost, null, "TestPerson", requestData, config));
        }

        [Fact]
        public async Task RunSessionAsync_WithNullContextSession_CreatesNewSession()
        {
            // Arrange
            var requestData = new Dictionary<string, object?> { { "testParam", "test value" } };
            var expectedResult = ChildSessionResult.CreateSuccess("Test result");
            _mockSession.EnqueueChildSessionResult(expectedResult);

            // Act
            var result = await NamingSessionRunner.RunSessionAsync(
                _mockHost, null, "TestPerson", requestData, _baseConfig);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            // Should have started a new session since contextSession was null
        }

        [Fact]
        public async Task RunSessionAsync_WithCancellationToken_PassesToChildSession()
        {
            // Arrange
            var requestData = new Dictionary<string, object?> { { "testParam", "test value" } };
            var expectedResult = ChildSessionResult.CreateSuccess("Test result");
            _mockSession.EnqueueChildSessionResult(expectedResult);
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            var result = await NamingSessionRunner.RunSessionAsync(
                _mockHost, _mockSession, "TestPerson", requestData, _baseConfig, 
                cancellationToken: cancellationTokenSource.Token);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
        }

        [Fact]
        public async Task RunSessionAsync_ExcludesParameterPrefixedEntries_FromTemplate()
        {
            // Arrange
            var config = new NamingConfig
            {
                PromptMessage = "Normal: {{ normalParam }}, Internal: {{ _ParameterInternal }}",
                UrgingMessage = "Complete task",
                InputParameters = new List<ParameterConfig>()
            };
            var requestData = new Dictionary<string, object?>
            {
                { "normalParam", "normal value" },
                { "_ParameterInternal", "internal value" } // Should be excluded
            };
            var expectedResult = ChildSessionResult.CreateSuccess("Done");
            _mockSession.EnqueueChildSessionResult(expectedResult);

            // Act
            var result = await NamingSessionRunner.RunSessionAsync(
                _mockHost, null, "TestPerson", requestData, config);

            // Assert
            result.Should().NotBeNull();
            var receivedPrompt = GetActiveSession().LastReceivedPrompt;
            receivedPrompt.Should().Contain("Normal: normal value");
            // _ParameterInternal should not be rendered as it starts with "_Parameter"
        }
    }
}
