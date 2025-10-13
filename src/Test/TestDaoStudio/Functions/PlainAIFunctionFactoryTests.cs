using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Plugins;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace TestDaoStudio.Functions
{
    /// <summary>
    /// Unit tests for PlainAIFunctionFactory class.
    /// Tests factory creation and instantiation of PlainAIFunction objects.
    /// </summary>
    public class PlainAIFunctionFactoryTests
    {
        private readonly Mock<IMessageService> _mockMessageService;
        private readonly Mock<ILogger<HostSessionAdapter>> _mockLogger;
        private readonly PlainAIFunctionFactory _factory;

        public PlainAIFunctionFactoryTests()
        {
            _mockMessageService = new Mock<IMessageService>();
            _mockLogger = new Mock<ILogger<HostSessionAdapter>>();
            _factory = new PlainAIFunctionFactory(_mockMessageService.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Act & Assert
            _factory.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullMessageService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new PlainAIFunctionFactory(null!, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("messageService");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new PlainAIFunctionFactory(_mockMessageService.Object, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        [Fact]
        public void Create_WithValidParameters_ReturnsPlainAIFunction()
        {
            // Arrange
            var mockSession = new Mock<ISession>();
            var functionWithDescription = CreateTestFunctionWithDescription();

            // Act
            var result = _factory.Create(functionWithDescription, mockSession.Object);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeOfType<PlainAIFunction>();
            result.Name.Should().Be("TestFunction");
            result.Description.Should().Be("A test function");
        }

        [Fact]
        public void Create_WithNullFunctionWithDescription_ThrowsArgumentNullException()
        {
            // Arrange
            var mockSession = new Mock<ISession>();

            // Act & Assert
            var act = () => _factory.Create(null!, mockSession.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("functionWithDescription");
        }

        [Fact]
        public void Create_WithNullSession_ThrowsArgumentNullException()
        {
            // Arrange
            var functionWithDescription = CreateTestFunctionWithDescription();

            // Act & Assert
            var act = () => _factory.Create(functionWithDescription, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("session");
        }

        private static FunctionWithDescription CreateTestFunctionWithDescription()
        {
            var funcDesc = new FunctionDescription
            {
                Name = "TestFunction",
                Description = "A test function",
                Parameters = new List<FunctionTypeMetadata>
                {
                    new FunctionTypeMetadata
                    {
                        Name = "input",
                        Description = "Test input parameter",
                        ParameterType = typeof(string),
                        IsRequired = true,
                        DefaultValue = null
                    }
                }
            };

            Func<Dictionary<string, object?>, Task<object?>> handler = async (args) => 
                await Task.FromResult<object?>("test result");

            return new FunctionWithDescription
            {
                Function = handler,
                Description = funcDesc
            };
        }
    }
}
