using Naming.ParallelExecution;

namespace TestNamingTool.UnitTests.Core
{
    public class ParallelExecutionTypesTests
    {
        [Fact]
        public void ParallelExecutionConfig_DefaultConstructor_SetsDefaultValues()
        {
            // Arrange & Act
            var config = new ParallelExecutionConfig();

            // Assert
            config.ExecutionType.Should().Be(ParallelExecutionType.None);
            config.MaxConcurrency.Should().Be(Environment.ProcessorCount);
            config.ResultStrategy.Should().Be(ParallelResultStrategy.WaitForAll);
            config.ListParameterName.Should().BeNull();
            config.ExternalList.Should().NotBeNull().And.BeEmpty();
            config.ExcludedParameters.Should().NotBeNull().And.BeEmpty();
            config.SessionTimeoutMs.Should().Be(30 * 60 * 1000); // 30 minutes
        }

        [Theory]
        [InlineData(ParallelExecutionType.None)]
        [InlineData(ParallelExecutionType.ParameterBased)]
        [InlineData(ParallelExecutionType.ListBased)]
        [InlineData(ParallelExecutionType.ExternalList)]
        public void ParallelExecutionConfig_ExecutionType_CanBeSet(ParallelExecutionType executionType)
        {
            // Arrange
            var config = new ParallelExecutionConfig();

            // Act
            config.ExecutionType = executionType;

            // Assert
            config.ExecutionType.Should().Be(executionType);
        }

        [Theory]
        [InlineData(ParallelResultStrategy.StreamIndividual)]
        [InlineData(ParallelResultStrategy.WaitForAll)]
        [InlineData(ParallelResultStrategy.FirstResultWins)]
        public void ParallelExecutionConfig_ResultStrategy_CanBeSet(ParallelResultStrategy strategy)
        {
            // Arrange
            var config = new ParallelExecutionConfig();

            // Act
            config.ResultStrategy = strategy;

            // Assert
            config.ResultStrategy.Should().Be(strategy);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        public void ParallelExecutionConfig_MaxConcurrency_CanBeSet(int maxConcurrency)
        {
            // Arrange
            var config = new ParallelExecutionConfig();

            // Act
            config.MaxConcurrency = maxConcurrency;

            // Assert
            config.MaxConcurrency.Should().Be(maxConcurrency);
        }

        [Fact]
        public void ParallelExecutionConfig_ExternalStringList_CanBePopulated()
        {
            // Arrange
            var config = new ParallelExecutionConfig();
            var testList = new List<string> { "item1", "item2", "item3" };

            // Act
            config.ExternalList.AddRange(testList);

            // Assert
            config.ExternalList.Should().HaveCount(3);
            config.ExternalList.Should().Contain("item1");
            config.ExternalList.Should().Contain("item2");
            config.ExternalList.Should().Contain("item3");
        }

        [Fact]
        public void ParallelExecutionConfig_ExcludedParameters_CanBePopulated()
        {
            // Arrange
            var config = new ParallelExecutionConfig();
            var excludedParams = new List<string> { "DasSession", "CancellationToken", "Context" };

            // Act
            config.ExcludedParameters.AddRange(excludedParams);

            // Assert
            config.ExcludedParameters.Should().HaveCount(3);
            config.ExcludedParameters.Should().Contain("DasSession");
            config.ExcludedParameters.Should().Contain("CancellationToken");
            config.ExcludedParameters.Should().Contain("Context");
        }

        [Fact]
        public void ParallelExecutionResult_DefaultConstructor_InitializesCollections()
        {
            // Arrange & Act
            var result = new ParallelExecutionResult();

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().BeNull();
            result.Results.Should().NotBeNull().And.BeEmpty();
            result.Strategy.Should().Be(ParallelResultStrategy.StreamIndividual);
            result.TotalSessions.Should().Be(0);
            result.CompletedSessions.Should().Be(0);
            result.FailedSessions.Should().Be(0);
            result.ExecutionTime.Should().Be(TimeSpan.Zero);
            result.StartTime.Should().Be(DateTime.MinValue);
            result.EndTime.Should().Be(DateTime.MinValue);
        }

        [Fact]
        public void ParallelExecutionResult_Properties_CanBeSet()
        {
            // Arrange
            var startTime = DateTime.Now;
            var endTime = startTime.AddMinutes(5);
            var executionTime = endTime - startTime;
            
            var result = new ParallelExecutionResult();

            // Act
            result.Success = true;
            result.ErrorMessage = "Test error";
            result.Strategy = ParallelResultStrategy.WaitForAll;
            result.TotalSessions = 10;
            result.CompletedSessions = 8;
            result.FailedSessions = 2;
            result.ExecutionTime = executionTime;
            result.StartTime = startTime;
            result.EndTime = endTime;

            // Assert
            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().Be("Test error");
            result.Strategy.Should().Be(ParallelResultStrategy.WaitForAll);
            result.TotalSessions.Should().Be(10);
            result.CompletedSessions.Should().Be(8);
            result.FailedSessions.Should().Be(2);
            result.ExecutionTime.Should().Be(executionTime);
            result.StartTime.Should().Be(startTime);
            result.EndTime.Should().Be(endTime);
        }

        [Fact]
        public void ParallelSessionResult_DefaultConstructor_InitializesProperties()
        {
            // Arrange & Act
            var result = new ParallelSessionResult();

            // Assert
            result.ParameterName.Should().BeNull();
            result.ParameterValue.Should().BeNull();
            result.InputParameters.Should().NotBeNull().And.BeEmpty();
            result.ChildResult.Should().BeNull();
            result.StartTime.Should().Be(DateTime.MinValue);
            result.EndTime.Should().Be(DateTime.MinValue);
            result.IsSuccess.Should().BeFalse();
            result.Exception.Should().BeNull();
        }

        [Fact]
        public void ParallelSessionResult_Duration_CalculatesCorrectly()
        {
            // Arrange
            var startTime = DateTime.Now;
            var endTime = startTime.AddMinutes(2);
            var expectedDuration = endTime - startTime;
            
            var result = new ParallelSessionResult
            {
                StartTime = startTime,
                EndTime = endTime
            };

            // Act & Assert
            result.Duration.Should().Be(expectedDuration);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void ParallelSessionResult_IsSuccess_ReflectsChildResultSuccess(bool childSuccess, bool expectedSuccess)
        {
            // Arrange
            var childResult = childSuccess 
                ? DaoStudio.Common.Plugins.ChildSessionResult.CreateSuccess("Test result")
                : DaoStudio.Common.Plugins.ChildSessionResult.CreateError("Test error");
            
            var result = new ParallelSessionResult
            {
                ChildResult = childResult
            };

            // Act & Assert
            result.IsSuccess.Should().Be(expectedSuccess);
        }

        [Fact]
        public void ParallelSessionResult_IsSuccess_FalseWhenChildResultIsNull()
        {
            // Arrange & Act
            var result = new ParallelSessionResult
            {
                ChildResult = null
            };

            // Assert
            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public void ParallelExecutionTypes_AllEnumValuesAreDefined()
        {
            // Arrange & Act
            var executionTypes = Enum.GetValues<ParallelExecutionType>();
            var resultStrategies = Enum.GetValues<ParallelResultStrategy>();

            // Assert
            executionTypes.Should().HaveCount(4);
            executionTypes.Should().Contain(ParallelExecutionType.None);
            executionTypes.Should().Contain(ParallelExecutionType.ParameterBased);
            executionTypes.Should().Contain(ParallelExecutionType.ListBased);
            executionTypes.Should().Contain(ParallelExecutionType.ExternalList);

            resultStrategies.Should().HaveCount(3);
            resultStrategies.Should().Contain(ParallelResultStrategy.StreamIndividual);
            resultStrategies.Should().Contain(ParallelResultStrategy.WaitForAll);
            resultStrategies.Should().Contain(ParallelResultStrategy.FirstResultWins);
        }
    }
}
