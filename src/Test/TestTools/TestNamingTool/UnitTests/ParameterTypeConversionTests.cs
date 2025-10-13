using System;
using System.Collections.Generic;
using Naming;
using Naming.Types;
using FluentAssertions;
using Xunit;

namespace TestNamingTool.UnitTests
{
    /// <summary>
    /// Unit tests for parameter type conversion functionality in NamingPluginInstance
    /// </summary>
    public class ParameterTypeConversionTests
    {
        [Theory]
        [InlineData(ParameterType.String, typeof(string))]
        [InlineData(ParameterType.Number, typeof(double))]
        [InlineData(ParameterType.Bool, typeof(bool))]
        public void ConvertParameterConfigToType_PrimitiveTypes_ReturnsCorrectType(ParameterType paramType, Type expectedType)
        {
            // Arrange
            var paramConfig = new ParameterConfig { Type = paramType };

            // Act
            var result = ConvertParameterConfigToTypePublic(paramConfig);

            // Assert
            result.Should().Be(expectedType);
        }

        [Fact]
        public void ConvertParameterConfigToType_ObjectWithoutProperties_ReturnsGenericObject()
        {
            // Arrange
            var paramConfig = new ParameterConfig 
            { 
                Type = ParameterType.Object,
                ObjectProperties = null
            };

            // Act
            var result = ConvertParameterConfigToTypePublic(paramConfig);

            // Assert
            result.Should().Be(typeof(object));
        }

        [Fact]
        public void ConvertParameterConfigToType_ObjectWithEmptyProperties_ReturnsGenericObject()
        {
            // Arrange
            var paramConfig = new ParameterConfig 
            { 
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig>()
            };

            // Act
            var result = ConvertParameterConfigToTypePublic(paramConfig);

            // Assert
            result.Should().Be(typeof(object));
        }

        [Fact]
        public void ConvertParameterConfigToType_ObjectWithProperties_ReturnsObjectTypeDescriptor()
        {
            // Arrange
            var paramConfig = new ParameterConfig 
            { 
                Name = "person",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true },
                    new ParameterConfig { Name = "age", Type = ParameterType.Number, IsRequired = false }
                }
            };

            // Act
            var result = ConvertParameterConfigToTypePublic(paramConfig);

            // Assert
            result.Should().BeOfType<ObjectTypeDescriptor>();
            var descriptor = (ObjectTypeDescriptor)result;
            descriptor.TypeName.Should().Be("person");
            descriptor.Properties.Should().HaveCount(2);
            descriptor.Properties.Should().ContainKey("name");
            descriptor.Properties.Should().ContainKey("age");
        }

        [Fact]
        public void ConvertParameterConfigToType_ArrayWithoutElementConfig_ReturnsObjectArray()
        {
            // Arrange
            var paramConfig = new ParameterConfig 
            { 
                Type = ParameterType.Array,
                ArrayElementConfig = null
            };

            // Act
            var result = ConvertParameterConfigToTypePublic(paramConfig);

            // Assert
            result.Should().Be(typeof(object[]));
        }

        [Theory]
        [InlineData(ParameterType.String, typeof(string[]))]
        [InlineData(ParameterType.Number, typeof(double[]))]
        [InlineData(ParameterType.Bool, typeof(bool[]))]
        [InlineData(ParameterType.Object, typeof(object[]))]
        public void ConvertParameterConfigToType_ArrayWithElementConfig_ReturnsTypedArray(ParameterType elementType, Type expectedArrayType)
        {
            // Arrange
            var paramConfig = new ParameterConfig 
            { 
                Type = ParameterType.Array,
                ArrayElementConfig = new ParameterConfig { Type = elementType }
            };

            // Act
            var result = ConvertParameterConfigToTypePublic(paramConfig);

            // Assert
            result.Should().Be(expectedArrayType);
        }

        [Fact]
        public void ConvertParameterConfigToType_NestedArrayOfArrays_ReturnsMultidimensionalArrayType()
        {
            // Arrange - Array of arrays of strings
            var paramConfig = new ParameterConfig 
            { 
                Type = ParameterType.Array,
                ArrayElementConfig = new ParameterConfig
                {
                    Type = ParameterType.Array,
                    ArrayElementConfig = new ParameterConfig { Type = ParameterType.String }
                }
            };

            // Act
            var result = ConvertParameterConfigToTypePublic(paramConfig);

            // Assert
            result.Should().Be(typeof(string[][]));
        }

        [Fact]
        public void ConvertParameterConfigToType_ArrayOfObjects_ReturnsObjectTypeDescriptorArray()
        {
            // Arrange
            var paramConfig = new ParameterConfig 
            { 
                Type = ParameterType.Array,
                ArrayElementConfig = new ParameterConfig 
                { 
                    Name = "person",
                    Type = ParameterType.Object,
                    ObjectProperties = new List<ParameterConfig>
                    {
                        new ParameterConfig { Name = "name", Type = ParameterType.String },
                        new ParameterConfig { Name = "age", Type = ParameterType.Number }
                    }
                }
            };

            // Act
            var result = ConvertParameterConfigToTypePublic(paramConfig);

            // Assert
            result.Should().NotBeNull();
            result.IsArray.Should().BeTrue();
            var elementType = result.GetElementType();
            elementType.Should().BeOfType<ObjectTypeDescriptor>();
            
            var descriptor = (ObjectTypeDescriptor)elementType!;
            descriptor.TypeName.Should().Be("person");
            descriptor.Properties.Should().HaveCount(2);
        }

        [Fact]
        public void ConvertParameterConfigToType_UnknownType_ReturnsStringAsDefault()
        {
            // Arrange
            var paramConfig = new ParameterConfig { Type = (ParameterType)999 }; // Invalid enum value

            // Act
            var result = ConvertParameterConfigToTypePublic(paramConfig);

            // Assert
            result.Should().Be(typeof(string));
        }

        /// <summary>
        /// Helper method to access the private ConvertParameterConfigToType method
        /// </summary>
        private static Type ConvertParameterConfigToTypePublic(ParameterConfig paramConfig)
        {
            // Use reflection to access the private static method
            var method = typeof(Naming.NamingPluginInstance)
                .GetMethod("ConvertParameterConfigToType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            method.Should().NotBeNull("ConvertParameterConfigToType method should exist");
            
            return (Type)method!.Invoke(null, new object[] { paramConfig })!;
        }
    }
}