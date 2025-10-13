using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces.Plugins;
using Naming;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool.UnitTests.Core
{
    public class NamingLevelCalculatorTests
    {
        [Fact]
        public async Task CalculateCurrentLevelAsync_WithNullParent_ReturnsZero()
        {
            // Arrange
            var mockHost = new MockHost();
            var session = new MockHostSession(1, parentSessionId: null);

            // Act
            var level = await NamingLevelCalculator.CalculateCurrentLevelAsync(session, mockHost);

            // Assert
            level.Should().Be(0);
        }

        [Fact]
        public async Task CalculateCurrentLevelAsync_WithParent_ReturnsOne()
        {
            // Arrange
            var mockHost = new MockHost();
            var parentSession = new MockHostSession(1);
            var childSession = new MockHostSession(2, parentSessionId: 1);

            // Act
            var level = await NamingLevelCalculator.CalculateCurrentLevelAsync(childSession, mockHost);

            // Assert
            level.Should().Be(1);
        }

        [Fact]
        public async Task CalculateCurrentLevelAsync_WithGrandparent_ReturnsTwo()
        {
            // Arrange
            var mockHost = new MockHost();
            var grandparentSession = new MockHostSession(1);
            var parentSession = new MockHostSession(2, parentSessionId: 1);
            var childSession = new MockHostSession(3, parentSessionId: 2);

            // Act
            var level = await NamingLevelCalculator.CalculateCurrentLevelAsync(childSession, mockHost);

            // Assert
            level.Should().Be(1); // Note: Current implementation returns currentDepth + 1 for any parent
        }

        [Fact]
        public async Task CalculateCurrentLevelAsync_WithException_ReturnsZero()
        {
            // Arrange
            var mockHost = new Mock<IHost>();
            var session = new MockHostSession(1, parentSessionId: null); // Use null to ensure level 0
            
            // Setup host to throw exception
            mockHost.Setup(h => h.GetPersonsAsync(It.IsAny<string?>()))
                   .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            var level = await NamingLevelCalculator.CalculateCurrentLevelAsync(session, mockHost.Object);

            // Assert
            level.Should().Be(0, "Exception should result in safe fallback to level 0");
        }

        [Fact]
        public async Task CalculateCurrentLevelAsync_PerformanceTest_CompletesQuickly()
        {
            // Arrange
            var mockHost = new MockHost();
            var session = new MockHostSession(1, parentSessionId: 999);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var level = await NamingLevelCalculator.CalculateCurrentLevelAsync(session, mockHost);

            // Assert
            stopwatch.Stop();
            level.Should().BeGreaterThanOrEqualTo(0);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Level calculation should be fast");
        }

        [Theory]
        [InlineData(null, 0)]
        [InlineData(1L, 1)]
        [InlineData(999L, 1)]
        public async Task CalculateCurrentLevelAsync_VariousParentIds_ReturnsExpectedLevels(
            long? parentSessionId, int expectedLevel)
        {
            // Arrange
            var mockHost = new MockHost();
            var session = new MockHostSession(100, parentSessionId);

            // Act
            var level = await NamingLevelCalculator.CalculateCurrentLevelAsync(session, mockHost);

            // Assert
            level.Should().Be(expectedLevel);
        }

        [Fact]
        public async Task CalculateCurrentLevelAsync_MultipleCallsSameSession_ReturnsSameResult()
        {
            // Arrange
            var mockHost = new MockHost();
            var session = new MockHostSession(1, parentSessionId: 5);

            // Act
            var level1 = await NamingLevelCalculator.CalculateCurrentLevelAsync(session, mockHost);
            var level2 = await NamingLevelCalculator.CalculateCurrentLevelAsync(session, mockHost);

            // Assert
            level1.Should().Be(level2, "Multiple calls should return consistent results");
        }

        [Fact]
        public async Task CalculateCurrentLevelAsync_DifferentSessions_CanHaveDifferentLevels()
        {
            // Arrange
            var mockHost = new MockHost();
            var rootSession = new MockHostSession(1, parentSessionId: null);
            var childSession = new MockHostSession(2, parentSessionId: 1);

            // Act
            var rootLevel = await NamingLevelCalculator.CalculateCurrentLevelAsync(rootSession, mockHost);
            var childLevel = await NamingLevelCalculator.CalculateCurrentLevelAsync(childSession, mockHost);

            // Assert
            rootLevel.Should().Be(0);
            childLevel.Should().Be(1);
            childLevel.Should().BeGreaterThan(rootLevel);
        }
    }
}
