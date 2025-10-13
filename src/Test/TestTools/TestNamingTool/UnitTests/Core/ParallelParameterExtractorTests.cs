using Naming.ParallelExecution;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;

namespace TestNamingTool.UnitTests.Core
{
    public class ParallelParameterExtractorTests
    {
        [Fact]
        public void ExtractParallelSources_WithNullConfig_ThrowsArgumentNullException()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                ParallelParameterExtractor.ExtractParallelSources(requestData, null!));
        }

        [Fact]
        public void ExtractParallelSources_WithNoneType_ReturnsEmptyList()
        {
            // Arrange
            var requestData = new Dictionary<string, object?> 
            { 
                { "param1", "value1" }, 
                { "param2", "value2" } 
            };
            var config = new ParallelExecutionConfig { ExecutionType = ParallelExecutionType.None };

            // Act
            var result = ParallelParameterExtractor.ExtractParallelSources(requestData, config);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void ExtractParallelSources_WithParameterBased_ReturnsPocoParameters()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>
            {
                { "stringParam", "test" },
                { "intParam", 42 },
                { "boolParam", true },
                { "dasSession", new { /* mock session object */ } }
            };
            var config = new ParallelExecutionConfig { ExecutionType = ParallelExecutionType.ParameterBased };

            // Act
            var result = ParallelParameterExtractor.ExtractParallelSources(requestData, config);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(x => x.Item1 == "stringParam" && object.Equals(x.Item2, "test"));
            result.Should().Contain(x => x.Item1 == "intParam" && object.Equals(x.Item2, 42));
            result.Should().Contain(x => x.Item1 == "boolParam" && object.Equals(x.Item2, true));
            result.Should().NotContain(x => x.Item1 == "dasSession");
        }

        [Fact]
        public void ExtractParallelSources_WithParameterBased_IncludesNullValues()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>
            {
                { "validParam", "test" },
                { "nullParam", null },
                { "anotherValid", 123 }
            };
            var config = new ParallelExecutionConfig { ExecutionType = ParallelExecutionType.ParameterBased };

            // Act
            var result = ParallelParameterExtractor.ExtractParallelSources(requestData, config);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(x => x.Item1 == "validParam" && object.Equals(x.Item2, "test"));
            result.Should().Contain(x => x.Item1 == "anotherValid" && object.Equals(x.Item2, 123));
            result.Should().Contain(x => x.Item1 == "nullParam" && x.Item2 == null);
        }

        [Fact]
        public void ExtractParallelSources_WithParameterBased_RespectsUserExclusions()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>
            {
                { "include1", "value1" },
                { "exclude1", "value2" },
                { "include2", "value3" },
                { "exclude2", "value4" }
            };
            var config = new ParallelExecutionConfig 
            { 
                ExecutionType = ParallelExecutionType.ParameterBased,
                ExcludedParameters = new List<string> { "exclude1", "exclude2" }
            };

            // Act
            var result = ParallelParameterExtractor.ExtractParallelSources(requestData, config);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(x => x.Item1 == "include1");
            result.Should().Contain(x => x.Item1 == "include2");
            result.Should().NotContain(x => x.Item1 == "exclude1");
            result.Should().NotContain(x => x.Item1 == "exclude2");
        }

        [Fact]
        public void ExtractParallelSources_WithListBased_ExtractsListElements()
        {
            // Arrange
            var testList = new List<object?> { "item1", "item2", "item3" };
            var requestData = new Dictionary<string, object?>
            {
                { "myList", testList },
                { "otherParam", "ignored" }
            };
            var config = new ParallelExecutionConfig 
            { 
                ExecutionType = ParallelExecutionType.ListBased,
                ListParameterName = "myList"
            };

            // Act
            var result = ParallelParameterExtractor.ExtractParallelSources(requestData, config);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(x => x.Item1 == "myList" && object.Equals(x.Item2, "item1"));
            result.Should().Contain(x => x.Item1 == "myList" && object.Equals(x.Item2, "item2"));
            result.Should().Contain(x => x.Item1 == "myList" && object.Equals(x.Item2, "item3"));
        }

        [Fact]
        public void ExtractParallelSources_WithListBased_ThrowsWhenListParameterNameEmpty()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>();
            var config = new ParallelExecutionConfig 
            { 
                ExecutionType = ParallelExecutionType.ListBased,
                ListParameterName = ""
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                ParallelParameterExtractor.ExtractParallelSources(requestData, config));
        }

        [Fact]
        public void ExtractParallelSources_WithListBased_ThrowsWhenParameterNotFound()
        {
            // Arrange
            var requestData = new Dictionary<string, object?> 
            { 
                { "someOtherParam", "value" } 
            };
            var config = new ParallelExecutionConfig 
            { 
                ExecutionType = ParallelExecutionType.ListBased,
                ListParameterName = "nonExistentList"
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                ParallelParameterExtractor.ExtractParallelSources(requestData, config));
        }

        [Fact]
        public void ExtractParallelSources_WithListBased_ThrowsWhenParameterNotList()
        {
            // Arrange
            var requestData = new Dictionary<string, object?> 
            { 
                { "notAList", "just a string" } 
            };
            var config = new ParallelExecutionConfig 
            { 
                ExecutionType = ParallelExecutionType.ListBased,
                ListParameterName = "notAList"
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                ParallelParameterExtractor.ExtractParallelSources(requestData, config));
        }

        [Fact]
        public void ExtractParallelSources_WithListBased_ThrowsWhenListEmpty()
        {
            // Arrange
            var requestData = new Dictionary<string, object?> 
            { 
                { "emptyList", new List<object?>() } 
            };
            var config = new ParallelExecutionConfig 
            { 
                ExecutionType = ParallelExecutionType.ListBased,
                ListParameterName = "emptyList"
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                ParallelParameterExtractor.ExtractParallelSources(requestData, config));
        }

        [Fact]
        public void ExtractParallelSources_WithExternalList_ReturnsExternalValues()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>();
            var config = new ParallelExecutionConfig 
            { 
                ExecutionType = ParallelExecutionType.ExternalList,
                ExternalList = new List<string> { "external1", "external2", "external3" }
            };

            // Act
            var result = ParallelParameterExtractor.ExtractParallelSources(requestData, config);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(x => x.Item1 == "ExternalList" && object.Equals(x.Item2, "external1"));
            result.Should().Contain(x => x.Item1 == "ExternalList" && object.Equals(x.Item2, "external2"));
            result.Should().Contain(x => x.Item1 == "ExternalList" && object.Equals(x.Item2, "external3"));
        }

        [Fact]
        public void ExtractParallelSources_WithExternalList_ThrowsWhenListEmpty()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>();
            var config = new ParallelExecutionConfig 
            { 
                ExecutionType = ParallelExecutionType.ExternalList,
                ExternalList = new List<string>()
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                ParallelParameterExtractor.ExtractParallelSources(requestData, config));
        }

        [Fact]
        public void ExtractParallelSources_WithExternalList_ThrowsWhenListNull()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>();
            var config = new ParallelExecutionConfig 
            { 
                ExecutionType = ParallelExecutionType.ExternalList,
                ExternalList = null!
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                ParallelParameterExtractor.ExtractParallelSources(requestData, config));
        }

        [Fact]
        public void ExtractParallelSources_WithInvalidExecutionType_ThrowsArgumentException()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>();
            var config = new ParallelExecutionConfig 
            { 
                ExecutionType = (ParallelExecutionType)999 // Invalid enum value
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                ParallelParameterExtractor.ExtractParallelSources(requestData, config));
        }

        [Theory]
        [InlineData("string", "test")]
        [InlineData("int", 42)]
        [InlineData("bool", true)]
        [InlineData("decimal", 3.14)]
        [InlineData("guid", "550e8400-e29b-41d4-a716-446655440000")]
        public void ExtractParallelSources_WithPrimitiveTypes_IncludesValues(string paramName, object value)
        {
            // Arrange
            var guid = paramName == "guid" ? Guid.Parse(value.ToString()!) : value;
            var actualValue = paramName == "guid" ? guid : value;
            
            var requestData = new Dictionary<string, object?> { { paramName, actualValue } };
            var config = new ParallelExecutionConfig { ExecutionType = ParallelExecutionType.ParameterBased };

            // Act
            var result = ParallelParameterExtractor.ExtractParallelSources(requestData, config);

            // Assert
            result.Should().HaveCount(1);
            var single = result.Single();
            single.Item1.Should().Be(paramName);
            single.Item2.Should().Be(actualValue);
        }

        [Fact]
        public void ExtractParallelSources_WithCollectionTypes_IncludesCollections()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>
            {
                { "list", new List<string> { "a", "b" } },
                { "dictionary", new Dictionary<string, int> { { "key", 1 } } },
                { "array", new[] { 1, 2, 3 } }
            };
            var config = new ParallelExecutionConfig { ExecutionType = ParallelExecutionType.ParameterBased };

            // Act
            var result = ParallelParameterExtractor.ExtractParallelSources(requestData, config);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(x => x.Item1 == "list");
            result.Should().Contain(x => x.Item1 == "dictionary");
            result.Should().Contain(x => x.Item1 == "array");
        }

        [Fact]
        public void ExtractParallelSources_WithSystemTypes_ExcludesFrameworkTypes()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>
            {
                { "validString", "test" },
                { "systemType", typeof(string) }, // System.Type should be excluded
                { "validInt", 42 }
            };
            var config = new ParallelExecutionConfig { ExecutionType = ParallelExecutionType.ParameterBased };

            // Act
            var result = ParallelParameterExtractor.ExtractParallelSources(requestData, config);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(x => x.Item1 == "validString");
            result.Should().Contain(x => x.Item1 == "validInt");
            result.Should().NotContain(x => x.Item1 == "systemType");
        }

        [Fact]
        public void ExtractParallelSources_WithDefaultExcludedParameters_ExcludesKnownSessionTypes()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>
            {
                { "validParam", "test" },
                { "DasSession", new { } },
                { "hostSession", new { } },
                { "session", new { } },
                { "parentSession", new { } },
                { "cancellationToken", new { } }
            };
            var config = new ParallelExecutionConfig { ExecutionType = ParallelExecutionType.ParameterBased };

            // Act
            var result = ParallelParameterExtractor.ExtractParallelSources(requestData, config);

            // Assert
            result.Should().HaveCount(1);
            result.Single().Item1.Should().Be("validParam");
        }

        [Fact]
        public void ExtractParallelSources_WithCaseInsensitiveExclusions_HandlesVariousCasing()
        {
            // Arrange
            var requestData = new Dictionary<string, object?>
            {
                { "validParam", "test" },
                { "DASSESSION", new { } },
                { "HostSession", new { } },
                { "SESSION", new { } }
            };
            var config = new ParallelExecutionConfig { ExecutionType = ParallelExecutionType.ParameterBased };

            // Act
            var result = ParallelParameterExtractor.ExtractParallelSources(requestData, config);

            // Assert
            result.Should().HaveCount(1);
            result.Single().Item1.Should().Be("validParam");
        }
    }
}