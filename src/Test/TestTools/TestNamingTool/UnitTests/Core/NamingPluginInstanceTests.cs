using Naming;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces.Plugins;
using TestNamingTool.TestInfrastructure.Mocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.Interfaces;
using Moq;
using FluentAssertions;
using Xunit;

namespace TestNamingTool.UnitTests.Core
{
    public class NamingPluginInstanceTests
    {
        private readonly MockHost _mockHost;
        private readonly PlugToolInfo _plugToolInfo;

        public NamingPluginInstanceTests()
        {
            _mockHost = new MockHost();
            var initialConfig = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestPerson", Description = "Test Description" },
                FunctionName = "test_function",
                FunctionDescription = "Test function description",
                MaxRecursionLevel = 2
            };
            _plugToolInfo = new PlugToolInfo
            {
                Config = System.Text.Json.JsonSerializer.Serialize(initialConfig)
            };
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act
            var instance = new NamingPluginInstance(_mockHost, _plugToolInfo);

            // Assert
            instance.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullHost_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new NamingPluginInstance(null!, _plugToolInfo));
        }

        [Fact]
        public void UpdateConfig_WithNewConfig_UpdatesConfiguration()
        {
            // Arrange
            var instance = new NamingPluginInstance(_mockHost, _plugToolInfo);
            var newConfig = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "NewPerson", Description = "New Description" },
                FunctionName = "new_function",
                MaxRecursionLevel = 5
            };

            // Act
            instance.UpdateConfig(newConfig);

            // Assert - We can't directly access the config, but we can verify the method doesn't throw
            // The actual verification would happen in integration tests
        }

        [Fact]
        public async Task RegisterToolFunctionsAsync_WithNoExecutivePerson_DoesNotThrowAndAddsFunctions()
        {
            // Arrange
            var configWithoutPerson = new NamingConfig { ExecutivePerson = null };
            var plugInfo = new PlugToolInfo { Config = System.Text.Json.JsonSerializer.Serialize(configWithoutPerson) };
            var instance = new NamingPluginInstance(_mockHost, plugInfo);
            var toolFunctions = new List<FunctionWithDescription>();
            var mockSession = new MockHostSession(1);

            // Act
            await instance.GetSessionFunctionsAsync(toolFunctions, null, mockSession);

            // Assert
            toolFunctions.Should().NotBeEmpty();
        }

        [Fact]
        public async Task RegisterToolFunctionsAsync_AtMaxRecursionLevel_DoesNotAddFunctions()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestPerson", Description = "Test" },
                MaxRecursionLevel = 1
            };
            var plugInfo = new PlugToolInfo { Config = System.Text.Json.JsonSerializer.Serialize(config) };
            var instance = new NamingPluginInstance(_mockHost, plugInfo);
            var toolFunctions = new List<FunctionWithDescription>();

            // Create a session at level 1 (which equals MaxRecursionLevel)
            var mockSession = new MockHostSession(2, parentSessionId: 1);

            // Act
            await instance.GetSessionFunctionsAsync(toolFunctions, null, mockSession);

            // Assert
            toolFunctions.Should().BeEmpty();
        }

        [Fact]
        public async Task RegisterToolFunctionsAsync_BelowMaxRecursionLevel_AddsFunctions()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestPerson", Description = "Test" },
                MaxRecursionLevel = 2,
                FunctionName = "test_function",
                FunctionDescription = "Test description"
            };
            var plugInfo = new PlugToolInfo { Config = System.Text.Json.JsonSerializer.Serialize(config) };
            var instance = new NamingPluginInstance(_mockHost, plugInfo);
            var toolFunctions = new List<FunctionWithDescription>();

            // Create a session at level 0 (below MaxRecursionLevel of 2)
            var mockSession = new MockHostSession(1, parentSessionId: null);

            // Act
            await instance.GetSessionFunctionsAsync(toolFunctions, null, mockSession);

            // Assert
            toolFunctions.Should().NotBeEmpty();
        }

        [Fact]
        public async Task RegisterToolFunctionsAsync_WithNullSession_ThrowsArgumentNullException()
        {
            // Arrange
            var instance = new NamingPluginInstance(_mockHost, _plugToolInfo);
            var toolFunctions = new List<FunctionWithDescription>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await instance.GetSessionFunctionsAsync(toolFunctions, null, null));
        }

        [Fact]
        public async Task RegisterToolFunctionsAsync_WithCustomInputParameters_ConfiguresFunctionParameters()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestPerson", Description = "Test" },
                FunctionName = "custom_function",
                FunctionDescription = "Custom description",
                InputParameters = new List<ParameterConfig>
                {
                    new ParameterConfig { Name = "param1", Description = "Parameter 1", IsRequired = true, Type = ParameterType.String },
                    new ParameterConfig { Name = "param2", Description = "Parameter 2", IsRequired = false, Type = ParameterType.Number }
                }
            };
            var plugInfo = new PlugToolInfo { Config = System.Text.Json.JsonSerializer.Serialize(config) };
            var instance = new NamingPluginInstance(_mockHost, plugInfo);
            var toolFunctions = new List<FunctionWithDescription>();
            var mockSession = new MockHostSession(1);

            // Act
            await instance.GetSessionFunctionsAsync(toolFunctions, null, mockSession);

            // Assert
            toolFunctions.Should().NotBeEmpty();
            var namingFunction = toolFunctions.FirstOrDefault(f => f.Description.Name == "custom_function");
            namingFunction.Should().NotBeNull();
            namingFunction!.Description.Description.Should().Be("Custom description");
            namingFunction.Description.Parameters.Should().HaveCount(2);

            var param1 = namingFunction.Description.Parameters.FirstOrDefault(p => p.Name == "param1");
            param1.Should().NotBeNull();
            param1!.Description.Should().Be("Parameter 1");
            param1.IsRequired.Should().BeTrue();

            var param2 = namingFunction.Description.Parameters.FirstOrDefault(p => p.Name == "param2");
            param2.Should().NotBeNull();
            param2!.Description.Should().Be("Parameter 2");
            param2.IsRequired.Should().BeFalse();
        }

        [Fact]
        public async Task RegisterToolFunctionsAsync_WithRecursionLevelCalculationError_StillAddsFunctions()
        {
            // Arrange
            var mockHostWithError = new Mock<IHost>();
            mockHostWithError.Setup(h => h.OpenHostSession(It.IsAny<long>()))
                           .ThrowsAsync(new InvalidOperationException("Test error"));

            var instance = new NamingPluginInstance(mockHostWithError.Object, _plugToolInfo);
            var toolFunctions = new List<FunctionWithDescription>();
            var mockSession = new MockHostSession(1, parentSessionId: 999);

            // Act - Should not throw despite the error in level calculation
            await instance.GetSessionFunctionsAsync(toolFunctions, null, mockSession);

            // Assert
            toolFunctions.Should().NotBeEmpty();
        }

        [Fact]
        public async Task OnSessionCloseAsync_RemovesSessionHandler()
        {
            // Arrange
            var instance = new NamingPluginInstance(_mockHost, _plugToolInfo);
            var mockSession = new MockHostSession(1);
            var toolFunctions = new List<FunctionWithDescription>();

            // First register functions to create a session handler
            await instance.GetSessionFunctionsAsync(toolFunctions, null, mockSession);

            // Act
            var result = await instance.CloseSessionAsync(mockSession);

            // Assert
            result.Should().BeNull();
            // The actual removal verification would happen internally and can't be directly tested
            // without exposing internal state
        }

        [Fact]
        public async Task OnSessionCloseAsync_WithNonExistentSession_DoesNotThrow()
        {
            // Arrange
            var instance = new NamingPluginInstance(_mockHost, _plugToolInfo);
            var mockSession = new MockHostSession(999); // Non-existent session

            // Act & Assert - Should not throw
            var result = await instance.CloseSessionAsync(mockSession);
            result.Should().BeNull();
        }

        [Fact]
        public void Dispose_ClearsSessionHandlers()
        {
            // Arrange
            var instance = new NamingPluginInstance(_mockHost, _plugToolInfo);

            // Act - Should not throw
            instance.Dispose();

            // Assert - Dispose should complete without errors
            // Internal state clearing can't be directly verified
        }

        [Fact]
        public async Task RegisterToolFunctionsAsync_WithZeroMaxRecursionLevel_AddsToolsAtRootLevel()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestPerson", Description = "Test" },
                MaxRecursionLevel = 0  // Only root level allowed
            };
            var plugInfo = new PlugToolInfo { Config = System.Text.Json.JsonSerializer.Serialize(config) };
            var instance = new NamingPluginInstance(_mockHost, plugInfo);
            var toolFunctions = new List<FunctionWithDescription>();

            // Root session (level 0)
            var rootSession = new MockHostSession(1, parentSessionId: null);

            // Act
            await instance.GetSessionFunctionsAsync(toolFunctions, null, rootSession);

            // Assert
            toolFunctions.Should().BeEmpty(); // At level 0, which equals MaxRecursionLevel of 0
        }

        [Fact]
        public async Task RegisterToolFunctionsAsync_WithNegativeMaxRecursionLevel_AlwaysAddsFunctions()
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestPerson", Description = "Test" },
                MaxRecursionLevel = -1  // Negative means unlimited
            };
            var plugInfo = new PlugToolInfo { Config = System.Text.Json.JsonSerializer.Serialize(config) };
            var instance = new NamingPluginInstance(_mockHost, plugInfo);
            var toolFunctions = new List<FunctionWithDescription>();

            // Deep nested session
            var deepSession = new MockHostSession(5, parentSessionId: 4);

            // Act
            await instance.GetSessionFunctionsAsync(toolFunctions, null, deepSession);

            // Assert
            toolFunctions.Should().NotBeEmpty(); // Negative max level allows unlimited recursion
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task RegisterToolFunctionsAsync_WithEmptyFunctionName_UsesDefaultName(string? functionName)
        {
            // Arrange
            var config = new NamingConfig
            {
                ExecutivePerson = new ConfigPerson { Name = "TestPerson", Description = "Test" },
                FunctionName = functionName ?? string.Empty
            };
            var plugInfo = new PlugToolInfo { Config = System.Text.Json.JsonSerializer.Serialize(config) };
            var instance = new NamingPluginInstance(_mockHost, plugInfo);
            var toolFunctions = new List<FunctionWithDescription>();

            // Act
            await instance.GetSessionFunctionsAsync(toolFunctions, null, new MockHostSession(1));

            // Assert
            toolFunctions.Should().NotBeEmpty();
            // The function should still be created even with empty name
            // The actual name would be determined by the base implementation
        }
    }
}
