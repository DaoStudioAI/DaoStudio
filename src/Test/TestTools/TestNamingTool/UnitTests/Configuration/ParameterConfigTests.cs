using Naming;

namespace TestNamingTool.UnitTests.Configuration
{
    public class ParameterConfigTests
    {
        [Fact]
        public void ParameterConfig_DefaultConstructor_SetsDefaultValues()
        {
            // Arrange & Act
            var parameter = new ParameterConfig();

            // Assert
            parameter.Name.Should().Be(string.Empty);
            parameter.Description.Should().Be(string.Empty);
            parameter.IsRequired.Should().BeTrue();
            parameter.Type.Should().Be(ParameterType.String);
            parameter.ArrayElementConfig.Should().BeNull();
            parameter.ObjectProperties.Should().BeNull();
        }

        [Theory]
        [InlineData((int)ParameterType.String, true, false, false)]
        [InlineData((int)ParameterType.Number, true, false, false)]
        [InlineData((int)ParameterType.Bool, true, false, false)]
        [InlineData((int)ParameterType.Array, false, true, false)]
        [InlineData((int)ParameterType.Object, false, false, true)]
        public void ParameterConfig_HelperProperties_ReturnCorrectValues(
            int typeValue, bool expectedIsPrimitive, bool expectedIsArray, bool expectedIsObject)
        {
            // Arrange
            var parameter = new ParameterConfig { Type = (ParameterType)typeValue };

            // Act & Assert
            parameter.IsPrimitive.Should().Be(expectedIsPrimitive);
            parameter.IsArray.Should().Be(expectedIsArray);
            parameter.IsObject.Should().Be(expectedIsObject);
        }

        [Fact]
        public void ParameterConfig_StringParameter_ConfigurationIsValid()
        {
            // Arrange & Act
            var parameter = new ParameterConfig
            {
                Name = "stringParam",
                Description = "A string parameter",
                IsRequired = true,
                Type = ParameterType.String
            };

            // Assert
            parameter.Name.Should().Be("stringParam");
            parameter.Description.Should().Be("A string parameter");
            parameter.IsRequired.Should().BeTrue();
            parameter.Type.Should().Be(ParameterType.String);
            parameter.IsPrimitive.Should().BeTrue();
            parameter.IsArray.Should().BeFalse();
            parameter.IsObject.Should().BeFalse();
        }

        [Fact]
        public void ParameterConfig_ArrayParameter_CanHaveElementConfiguration()
        {
            // Arrange & Act
            var arrayParameter = new ParameterConfig
            {
                Name = "arrayParam",
                Description = "An array parameter",
                IsRequired = false,
                Type = ParameterType.Array,
                ArrayElementConfig = new ParameterConfig
                {
                    Name = "element",
                    Description = "Array element",
                    Type = ParameterType.String,
                    IsRequired = true
                }
            };

            // Assert
            arrayParameter.IsArray.Should().BeTrue();
            arrayParameter.ArrayElementConfig.Should().NotBeNull();
            arrayParameter.ArrayElementConfig!.Name.Should().Be("element");
            arrayParameter.ArrayElementConfig.Type.Should().Be(ParameterType.String);
            arrayParameter.ArrayElementConfig.IsRequired.Should().BeTrue();
        }

        [Fact]
        public void ParameterConfig_ObjectParameter_CanHavePropertiesConfiguration()
        {
            // Arrange & Act
            var objectParameter = new ParameterConfig
            {
                Name = "objectParam",
                Description = "An object parameter",
                IsRequired = true,
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig>
                {
                    new ParameterConfig
                    {
                        Name = "property1",
                        Description = "First property",
                        Type = ParameterType.String,
                        IsRequired = true
                    },
                    new ParameterConfig
                    {
                        Name = "property2",
                        Description = "Second property",
                        Type = ParameterType.Number,
                        IsRequired = false
                    }
                }
            };

            // Assert
            objectParameter.IsObject.Should().BeTrue();
            objectParameter.ObjectProperties.Should().NotBeNull();
            objectParameter.ObjectProperties.Should().HaveCount(2);
            objectParameter.ObjectProperties.Should().Contain(p => p.Name == "property1");
            objectParameter.ObjectProperties.Should().Contain(p => p.Name == "property2");
            
            var property1 = objectParameter.ObjectProperties!.First(p => p.Name == "property1");
            property1.Name.Should().Be("property1");
            property1.Type.Should().Be(ParameterType.String);
            property1.IsRequired.Should().BeTrue();
            
            var property2 = objectParameter.ObjectProperties.First(p => p.Name == "property2");
            property2.Name.Should().Be("property2");
            property2.Type.Should().Be(ParameterType.Number);
            property2.IsRequired.Should().BeFalse();
        }

        [Fact]
        public void ParameterConfig_NestedArrayOfObjects_ConfigurationIsValid()
        {
            // Arrange & Act
            var nestedParameter = new ParameterConfig
            {
                Name = "complexArray",
                Description = "Array of objects",
                Type = ParameterType.Array,
                ArrayElementConfig = new ParameterConfig
                {
                    Name = "arrayElement",
                    Description = "Object in array",
                    Type = ParameterType.Object,
                    ObjectProperties = new List<ParameterConfig>
                    {
                        new ParameterConfig
                        {
                            Name = "nestedProperty",
                            Type = ParameterType.String,
                            IsRequired = true
                        }
                    }
                }
            };

            // Assert
            nestedParameter.IsArray.Should().BeTrue();
            nestedParameter.ArrayElementConfig.Should().NotBeNull();
            nestedParameter.ArrayElementConfig!.IsObject.Should().BeTrue();
            nestedParameter.ArrayElementConfig.ObjectProperties.Should().HaveCount(1);
            nestedParameter.ArrayElementConfig.ObjectProperties!.First(p => p.Name == "nestedProperty").Type.Should().Be(ParameterType.String);
        }

        [Fact]
        public void ParameterConfig_AllParameterTypes_AreHandledByHelperProperties()
        {
            // Arrange
            var allTypes = Enum.GetValues<ParameterType>();

            // Act & Assert
            foreach (var paramType in allTypes)
            {
                var parameter = new ParameterConfig { Type = paramType };
                
                // Each parameter should be exactly one of: primitive, array, or object
                var typeCount = (parameter.IsPrimitive ? 1 : 0) + 
                               (parameter.IsArray ? 1 : 0) + 
                               (parameter.IsObject ? 1 : 0);
                
                typeCount.Should().Be(1, $"Parameter type {paramType} should be exactly one classification");
            }
        }
    }
}
