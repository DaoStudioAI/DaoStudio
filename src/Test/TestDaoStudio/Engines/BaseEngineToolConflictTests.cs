using DaoStudio.Engines.MEAI;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.DBStorage.Factory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using TestDaoStudio.TestableEngines;
using DaoStudio.Common;
using Xunit;

namespace TestDaoStudio.Engines
{
    /// <summary>
    /// Tests for BaseEngine tool conflict resolution functionality
    /// </summary>
    public class BaseEngineToolConflictTests
    {
        private readonly Mock<IPerson> _mockPerson;
        private readonly Mock<ILogger<BaseEngine>> _mockLogger;
        private readonly Mock<StorageFactory> _mockStorage;
        private readonly Mock<IPlainAIFunctionFactory> _mockPlainAIFunctionFactory;
        private readonly Mock<ISession> _mockSession;
        private readonly Mock<IChatClient> _mockChatClient;
        private readonly Mock<ISettings> _mockSettingsService;
        private readonly TestableBaseEngine _engine;

        public BaseEngineToolConflictTests()
        {
            _mockPerson = new Mock<IPerson>();
            _mockLogger = new Mock<ILogger<BaseEngine>>();
            _mockStorage = new Mock<StorageFactory>("test.db");
            _mockPlainAIFunctionFactory = new Mock<IPlainAIFunctionFactory>();
            _mockSession = new Mock<ISession>();
            _mockChatClient = new Mock<IChatClient>();
            _mockSettingsService = new Mock<ISettings>();

            _mockPerson.Setup(p => p.Parameters).Returns(new Dictionary<string, string>());
            
            // Setup the settings service to return true for AutoResolveToolNameConflicts by default
            var mockSettings = new Mock<ISettings>();
            mockSettings.Setup(s => s.AutoResolveToolNameConflicts).Returns(true);

            _engine = new TestableBaseEngine(
                _mockPerson.Object,
                _mockLogger.Object,
                _mockStorage.Object,
                _mockPlainAIFunctionFactory.Object,
                mockSettings.Object,
                _mockChatClient.Object);
        }

        [Fact]
        public void ProcessToolsWithConflictResolution_NullTools_ReturnsNull()
        {
            // Arrange & Act
            var result = _engine.TestProcessToolsWithConflictResolution(null, _mockSession.Object);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ProcessToolsWithConflictResolution_EmptyTools_ReturnsNull()
        {
            // Arrange
            var tools = new Dictionary<string, List<FunctionWithDescription>>();

            // Act
            var result = _engine.TestProcessToolsWithConflictResolution(tools, _mockSession.Object);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ProcessToolsWithConflictResolution_NoConflicts_PreservesOriginalNames()
        {
            // Arrange
            var function1 = CreateTestFunction("read");
            var function2 = CreateTestFunction("write");
            var function3 = CreateTestFunction("delete");

            var tools = new Dictionary<string, List<FunctionWithDescription>>
            {
                { "FileSystem", new List<FunctionWithDescription> { function1 } },
                { "Database", new List<FunctionWithDescription> { function2 } },
                { "Network", new List<FunctionWithDescription> { function3 } }
            };

            var mockAIFunction1 = new Mock<AIFunction>();
            var mockAIFunction2 = new Mock<AIFunction>();
            var mockAIFunction3 = new Mock<AIFunction>();

            _mockPlainAIFunctionFactory.SetupSequence(f => f.Create(It.IsAny<FunctionWithDescription>(), _mockSession.Object))
                .Returns(mockAIFunction1.Object)
                .Returns(mockAIFunction2.Object)
                .Returns(mockAIFunction3.Object);

            // Act
            var result = _engine.TestProcessToolsWithConflictResolution(tools, _mockSession.Object);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            
            // Verify the original functions were used (not prefixed versions)
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.Is<FunctionWithDescription>(func => func.Description.Name == "read"), 
                _mockSession.Object), Times.Once);
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.Is<FunctionWithDescription>(func => func.Description.Name == "write"), 
                _mockSession.Object), Times.Once);
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.Is<FunctionWithDescription>(func => func.Description.Name == "delete"), 
                _mockSession.Object), Times.Once);
        }

        [Fact]
        public void ProcessToolsWithConflictResolution_WithConflicts_AddsPrefixes()
        {
            // Arrange
            var function1 = CreateTestFunction("read");
            var function2 = CreateTestFunction("read");
            var function3 = CreateTestFunction("write"); // No conflict

            var tools = new Dictionary<string, List<FunctionWithDescription>>
            {
                { "FileSystem", new List<FunctionWithDescription> { function1 } },
                { "Database", new List<FunctionWithDescription> { function2 } },
                { "Network", new List<FunctionWithDescription> { function3 } }
            };

            var mockAIFunction1 = new Mock<AIFunction>();
            var mockAIFunction2 = new Mock<AIFunction>();
            var mockAIFunction3 = new Mock<AIFunction>();

            _mockPlainAIFunctionFactory.SetupSequence(f => f.Create(It.IsAny<FunctionWithDescription>(), _mockSession.Object))
                .Returns(mockAIFunction1.Object)
                .Returns(mockAIFunction2.Object)
                .Returns(mockAIFunction3.Object);

            // Act
            var result = _engine.TestProcessToolsWithConflictResolution(tools, _mockSession.Object);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);

            // Verify conflicting functions got prefixes
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.Is<FunctionWithDescription>(func => func.Description.Name == "FileSystem_read"), 
                _mockSession.Object), Times.Once);
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.Is<FunctionWithDescription>(func => func.Description.Name == "Database_read"), 
                _mockSession.Object), Times.Once);
            
            // Verify non-conflicting function preserved original name
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.Is<FunctionWithDescription>(func => func.Description.Name == "write"), 
                _mockSession.Object), Times.Once);
        }

        [Fact]
        public void SanitizeKeyForPrefix_ValidCharacters_PreservesString()
        {
            // Act
            var result = _engine.TestSanitizeKeyForPrefix("ValidName_test");

            // Assert
            result.Should().Be("ValidName_test");
        }

        [Fact]
        public void SanitizeKeyForPrefix_InvalidCharacters_RemovesInvalidChars()
        {
            // Act
            var result = _engine.TestSanitizeKeyForPrefix("File-System!@#123");

            // Assert
            result.Should().Be("FileSystem");
        }

        [Fact]
        public void SanitizeKeyForPrefix_EmptyOrNullString_ReturnsDefault()
        {
            // Act
            var result1 = _engine.TestSanitizeKeyForPrefix("");
            var result2 = _engine.TestSanitizeKeyForPrefix(null!);

            // Assert
            result1.Should().Be("tool");
            result2.Should().Be("tool");
        }

        [Fact]
        public void SanitizeKeyForPrefix_OnlyInvalidCharacters_ReturnsDefault()
        {
            // Act
            var result = _engine.TestSanitizeKeyForPrefix("!@#123");

            // Assert
            result.Should().Be("tool");
        }

        [Fact]
        public void MakeUniquePrefix_NoDuplicates_ReturnsOriginal()
        {
            // Arrange
            var usedPrefixes = new Dictionary<string, int>();

            // Act
            var result = _engine.TestMakeUniquePrefix("FileSystem", usedPrefixes);

            // Assert
            result.Should().Be("FileSystem");
        }

        [Fact]
        public void MakeUniquePrefix_WithDuplicates_AppendsNumber()
        {
            // Arrange
            var usedPrefixes = new Dictionary<string, int>
            {
                { "FileSystem", 1 },
                { "FileSystem2", 1 }
            };

            // Act
            var result = _engine.TestMakeUniquePrefix("FileSystem", usedPrefixes);

            // Assert
            result.Should().Be("FileSystem3");
        }

        [Fact]
        public void ProcessToolsWithConflictResolution_MultipleSanitizedPrefixConflicts_GeneratesUniqueNames()
        {
            // Arrange
            var function1 = CreateTestFunction("read");
            var function2 = CreateTestFunction("read");
            var function3 = CreateTestFunction("read");

            var tools = new Dictionary<string, List<FunctionWithDescription>>
            {
                { "File-System", new List<FunctionWithDescription> { function1 } },
                { "File_System", new List<FunctionWithDescription> { function2 } },
                { "FileSystem!", new List<FunctionWithDescription> { function3 } }
            };

            var mockAIFunction1 = new Mock<AIFunction>();
            var mockAIFunction2 = new Mock<AIFunction>();
            var mockAIFunction3 = new Mock<AIFunction>();

            _mockPlainAIFunctionFactory.SetupSequence(f => f.Create(It.IsAny<FunctionWithDescription>(), _mockSession.Object))
                .Returns(mockAIFunction1.Object)
                .Returns(mockAIFunction2.Object)
                .Returns(mockAIFunction3.Object);

            // Act
            var result = _engine.TestProcessToolsWithConflictResolution(tools, _mockSession.Object);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);

            // All three keys sanitize to "FileSystem", so they should get unique suffixes
            // Let's verify the actual calls made to see what names were generated
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.IsAny<FunctionWithDescription>(), 
                _mockSession.Object), Times.Exactly(3));
            
            // Get all the calls to see what function names were actually created
            var calls = _mockPlainAIFunctionFactory.Invocations
                .Where(inv => inv.Method.Name == nameof(IPlainAIFunctionFactory.Create))
                .Select(inv => ((FunctionWithDescription)inv.Arguments[0]).Description.Name)
                .ToList();
            
            // Debug: output the actual names for debugging
            var actualNames = string.Join(", ", calls);
            
            calls.Should().HaveCount(3, $"Expected 3 function calls but got: {actualNames}");
            
            // Expected behavior:
            // "File-System" sanitizes to "FileSystem" -> "FileSystem_read"
            // "File_System" sanitizes to "File_System" (underscore preserved) -> "File_System_read" (no conflict)
            // "FileSystem!" sanitizes to "FileSystem" -> "FileSystem2_read" (conflict with first)
            calls.Should().Contain("FileSystem_read", $"Expected 'FileSystem_read' in: {actualNames}");
            calls.Should().Contain("File_System_read", $"Expected 'File_System_read' in: {actualNames}");
            calls.Should().Contain("FileSystem2_read", $"Expected 'FileSystem2_read' in: {actualNames}");
        }

        [Fact]
        public void CreateFunctionWithPrefixedName_CreatesNewFunctionWithCorrectName()
        {
            // Arrange
            var originalFunction = CreateTestFunction("read");

            // Act
            var result = _engine.TestCreateFunctionWithPrefixedName(originalFunction, "FileSystem");

            // Assert
            result.Should().NotBeNull();
            result.Description.Name.Should().Be("FileSystem_read");
            result.Description.Description.Should().Be(originalFunction.Description.Description);
            result.Description.Parameters.Should().BeSameAs(originalFunction.Description.Parameters);
            result.Description.ReturnParameter.Should().BeSameAs(originalFunction.Description.ReturnParameter);
            result.Description.StrictMode.Should().Be(originalFunction.Description.StrictMode);
            result.Function.Should().BeSameAs(originalFunction.Function);
        }

        [Fact]
        public async Task ProcessToolsWithConflictResolutionAsync_WhenAutoResolveDisabled_ThrowsUIExceptionWithModuleName()
        {
            // Arrange
            var mockSettings = new Mock<ISettings>();
            mockSettings.Setup(s => s.AutoResolveToolNameConflicts).Returns(false);

            var engine = new TestableBaseEngine(
                _mockPerson.Object,
                _mockLogger.Object,
                _mockStorage.Object,
                _mockPlainAIFunctionFactory.Object,
                mockSettings.Object,
                _mockChatClient.Object);

            var function1 = CreateTestFunction("read");
            var function2 = CreateTestFunction("read"); // Same name - conflict

            var tools = new Dictionary<string, List<FunctionWithDescription>>
            {
                { "FileSystem", new List<FunctionWithDescription> { function1 } },
                { "Database", new List<FunctionWithDescription> { function2 } }
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UIException>(
                () => engine.TestProcessToolsWithConflictResolutionAsync(tools, _mockSession.Object));

            exception.Message.Should().Contain("Tool function name conflicts detected for 'read' in module 'FileSystem'");
            exception.Message.Should().Contain("Please rename conflicting functions or enable auto-resolution in settings");
        }

        [Fact]
        public async Task ProcessToolsWithConflictResolutionAsync_WhenAutoResolveEnabled_ResolvesConflictsWithPrefixes()
        {
            // Arrange
            var mockSettings = new Mock<ISettings>();
            mockSettings.Setup(s => s.AutoResolveToolNameConflicts).Returns(true);

            var engine = new TestableBaseEngine(
                _mockPerson.Object,
                _mockLogger.Object,
                _mockStorage.Object,
                _mockPlainAIFunctionFactory.Object,
                mockSettings.Object,
                _mockChatClient.Object);

            var function1 = CreateTestFunction("read");
            var function2 = CreateTestFunction("read"); // Same name - conflict

            var tools = new Dictionary<string, List<FunctionWithDescription>>
            {
                { "FileSystem", new List<FunctionWithDescription> { function1 } },
                { "Database", new List<FunctionWithDescription> { function2 } }
            };

            var mockAIFunction1 = new Mock<AIFunction>();
            var mockAIFunction2 = new Mock<AIFunction>();

            _mockPlainAIFunctionFactory.SetupSequence(f => f.Create(It.IsAny<FunctionWithDescription>(), _mockSession.Object))
                .Returns(mockAIFunction1.Object)
                .Returns(mockAIFunction2.Object);

            // Act
            var result = await engine.TestProcessToolsWithConflictResolutionAsync(tools, _mockSession.Object);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);

            // Verify the functions were created with prefixed names
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.Is<FunctionWithDescription>(func => func.Description.Name == "FileSystem_read"), 
                _mockSession.Object), Times.Once);
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.Is<FunctionWithDescription>(func => func.Description.Name == "Database_read"), 
                _mockSession.Object), Times.Once);
        }

        [Fact]
        public async Task ProcessToolsWithConflictResolutionAsync_WithMultipleConflictingModules_ThrowsExceptionWithFirstModuleName()
        {
            // Arrange
            var mockSettings = new Mock<ISettings>();
            mockSettings.Setup(s => s.AutoResolveToolNameConflicts).Returns(false);

            var engine = new TestableBaseEngine(
                _mockPerson.Object,
                _mockLogger.Object,
                _mockStorage.Object,
                _mockPlainAIFunctionFactory.Object,
                mockSettings.Object,
                _mockChatClient.Object);

            var function1 = CreateTestFunction("execute");
            var function2 = CreateTestFunction("execute");
            var function3 = CreateTestFunction("execute"); // Same name across multiple modules

            var tools = new Dictionary<string, List<FunctionWithDescription>>
            {
                { "ModuleA", new List<FunctionWithDescription> { function1 } },
                { "ModuleB", new List<FunctionWithDescription> { function2 } },
                { "ModuleC", new List<FunctionWithDescription> { function3 } }
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UIException>(
                () => engine.TestProcessToolsWithConflictResolutionAsync(tools, _mockSession.Object));

            // Should mention the first conflicting module (ModuleA)
            exception.Message.Should().Contain("Tool function name conflicts detected for 'execute' in module 'ModuleA'");
        }

        [Fact]
        public async Task ProcessToolsWithConflictResolutionAsync_WithNoConflicts_ReturnsOriginalFunctions()
        {
            // Arrange - AutoResolve setting doesn't matter when there are no conflicts
            var mockSettings = new Mock<ISettings>();
            mockSettings.Setup(s => s.AutoResolveToolNameConflicts).Returns(false);

            var engine = new TestableBaseEngine(
                _mockPerson.Object,
                _mockLogger.Object,
                _mockStorage.Object,
                _mockPlainAIFunctionFactory.Object,
                mockSettings.Object,
                _mockChatClient.Object);

            var function1 = CreateTestFunction("read");
            var function2 = CreateTestFunction("write");
            var function3 = CreateTestFunction("delete");

            var tools = new Dictionary<string, List<FunctionWithDescription>>
            {
                { "FileSystem", new List<FunctionWithDescription> { function1 } },
                { "Database", new List<FunctionWithDescription> { function2 } },
                { "Network", new List<FunctionWithDescription> { function3 } }
            };

            var mockAIFunction1 = new Mock<AIFunction>();
            var mockAIFunction2 = new Mock<AIFunction>();
            var mockAIFunction3 = new Mock<AIFunction>();

            _mockPlainAIFunctionFactory.SetupSequence(f => f.Create(It.IsAny<FunctionWithDescription>(), _mockSession.Object))
                .Returns(mockAIFunction1.Object)
                .Returns(mockAIFunction2.Object)
                .Returns(mockAIFunction3.Object);

            // Act
            var result = await engine.TestProcessToolsWithConflictResolutionAsync(tools, _mockSession.Object);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);

            // Verify the original function names were preserved (no prefixes)
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.Is<FunctionWithDescription>(func => func.Description.Name == "read"), 
                _mockSession.Object), Times.Once);
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.Is<FunctionWithDescription>(func => func.Description.Name == "write"), 
                _mockSession.Object), Times.Once);
            _mockPlainAIFunctionFactory.Verify(f => f.Create(
                It.Is<FunctionWithDescription>(func => func.Description.Name == "delete"), 
                _mockSession.Object), Times.Once);
        }

        [Fact]
        public async Task ProcessToolsWithConflictResolutionAsync_WithNullOrEmptyTools_ReturnsNull()
        {
            // Act & Assert - null tools
            var result1 = await _engine.TestProcessToolsWithConflictResolutionAsync(null, _mockSession.Object);
            result1.Should().BeNull();

            // Act & Assert - empty tools
            var emptyTools = new Dictionary<string, List<FunctionWithDescription>>();
            var result2 = await _engine.TestProcessToolsWithConflictResolutionAsync(emptyTools, _mockSession.Object);
            result2.Should().BeNull();
        }

        private static FunctionWithDescription CreateTestFunction(string name)
        {
            var description = new FunctionDescription
            {
                Name = name,
                Description = $"Test function {name}",
                Parameters = new List<FunctionTypeMetadata>(),
                StrictMode = false
            };

            return new FunctionWithDescription
            {
                Function = new System.Func<Dictionary<string, object?>, Task<object?>>(_ => Task.FromResult<object?>("test result")),
                Description = description,
#pragma warning disable CS0618 // Type or member is obsolete
                ModuleName = "TestModule"
#pragma warning restore CS0618 // Type or member is obsolete
            };
        }
    }
}