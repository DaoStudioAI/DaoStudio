using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Naming;
using Naming.Types;
using FluentAssertions;
using Xunit;

namespace TestNamingTool.UnitTests
{
    /// <summary>
    /// Unit tests for ObjectTypeDescriptor functionality and composite object type handling
    /// </summary>
    public class ObjectTypeDescriptorTests
    {
        [Fact]
        public void ObjectTypeDescriptor_WithSimpleProperties_CreatesCorrectStructure()
        {
            // Arrange
            var properties = new Dictionary<string, PropertyDescriptor>
            {
                ["name"] = new PropertyDescriptor("name", "Person's name", typeof(string), true),
                ["age"] = new PropertyDescriptor("age", "Person's age", typeof(double), false)
            };

            // Act
            var descriptor = new ObjectTypeDescriptor("Person", "A person object", properties);

            // Assert
            descriptor.TypeName.Should().Be("Person");
            descriptor.Description.Should().Be("A person object");
            descriptor.Properties.Should().HaveCount(2);
            descriptor.RequiredProperties.Should().Contain("name");
            descriptor.RequiredProperties.Should().NotContain("age");
        }

        [Fact]
        public void ObjectTypeDescriptor_FromParameterConfig_SimpleObject_CreatesCorrectDescriptor()
        {
            // Arrange
            var paramConfig = new ParameterConfig
            {
                Name = "user",
                Description = "User object",
                Type = ParameterType.Object,
                IsRequired = true,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig { Name = "username", Type = ParameterType.String, IsRequired = true },
                    new ParameterConfig { Name = "score", Type = ParameterType.Number, IsRequired = false },
                    new ParameterConfig { Name = "active", Type = ParameterType.Bool, IsRequired = true }
                }
            };

            // Act
            var descriptor = ObjectTypeDescriptor.FromParameterConfig(paramConfig);

            // Assert
            descriptor.TypeName.Should().Be("user");
            descriptor.Description.Should().Be("User object");
            descriptor.Properties.Should().HaveCount(3);
            
            descriptor.Properties["username"].PropertyType.Should().Be(typeof(string));
            descriptor.Properties["username"].IsRequired.Should().BeTrue();
            
            descriptor.Properties["score"].PropertyType.Should().Be(typeof(double));
            descriptor.Properties["score"].IsRequired.Should().BeFalse();
            
            descriptor.Properties["active"].PropertyType.Should().Be(typeof(bool));
            descriptor.Properties["active"].IsRequired.Should().BeTrue();
            
            descriptor.RequiredProperties.Should().Contain("username");
            descriptor.RequiredProperties.Should().Contain("active");
            descriptor.RequiredProperties.Should().NotContain("score");
        }

        [Fact]
        public void ObjectTypeDescriptor_FromParameterConfig_NestedObject_CreatesCorrectHierarchy()
        {
            // Arrange
            var paramConfig = new ParameterConfig
            {
                Name = "task",
                Description = "Task with nested config",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true },
                    new ParameterConfig {
                        Name = "config",
                        Type = ParameterType.Object,
                        IsRequired = false,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "timeout", Type = ParameterType.Number, IsRequired = true },
                            new ParameterConfig { Name = "retries", Type = ParameterType.Number, IsRequired = false }
                        }
                    }
                }
            };

            // Act
            var descriptor = ObjectTypeDescriptor.FromParameterConfig(paramConfig);

            // Assert
            descriptor.TypeName.Should().Be("task");
            descriptor.Properties.Should().HaveCount(2);
            
            var configProperty = descriptor.Properties["config"];
            configProperty.ObjectTypeDescriptor.Should().NotBeNull();
            configProperty.ObjectTypeDescriptor!.TypeName.Should().Be("config");
            configProperty.ObjectTypeDescriptor.Properties.Should().HaveCount(2);
            
            configProperty.ObjectTypeDescriptor.Properties["timeout"].PropertyType.Should().Be(typeof(double));
            configProperty.ObjectTypeDescriptor.Properties["timeout"].IsRequired.Should().BeTrue();
            
            configProperty.ObjectTypeDescriptor.Properties["retries"].PropertyType.Should().Be(typeof(double));
            configProperty.ObjectTypeDescriptor.Properties["retries"].IsRequired.Should().BeFalse();
        }

        [Fact]
        public void ObjectTypeDescriptor_FromParameterConfig_WithArrayProperty_CreatesCorrectStructure()
        {
            // Arrange
            var paramConfig = new ParameterConfig
            {
                Name = "project",
                Description = "Project with team members",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true },
                    new ParameterConfig {
                        Name = "tags",
                        Type = ParameterType.Array,
                        IsRequired = false,
                        ArrayElementConfig = new ParameterConfig { Name = "tag", Type = ParameterType.String }
                    }
                }
            };

            // Act
            var descriptor = ObjectTypeDescriptor.FromParameterConfig(paramConfig);

            // Assert
            descriptor.Properties["tags"].PropertyType.Should().Be(typeof(string[]));
            descriptor.Properties["tags"].ArrayElementType.Should().NotBeNull();
            descriptor.Properties["tags"].ArrayElementType!.PropertyType.Should().Be(typeof(string));
        }

        [Fact]
        public void ObjectTypeDescriptor_FromParameterConfig_ComplexNestedStructure_CreatesFullHierarchy()
        {
            // Arrange
            var paramConfig = new ParameterConfig
            {
                Name = "company",
                Description = "Company with departments and employees",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true },
                    new ParameterConfig {
                        Name = "departments",
                        Type = ParameterType.Array,
                        IsRequired = true,
                        ArrayElementConfig = new ParameterConfig
                        {
                            Name = "department",
                            Type = ParameterType.Object,
                            ObjectProperties = new List<ParameterConfig> {
                                new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true },
                                new ParameterConfig {
                                    Name = "employees",
                                    Type = ParameterType.Array,
                                    IsRequired = false,
                                    ArrayElementConfig = new ParameterConfig
                                    {
                                        Name = "employee",
                                        Type = ParameterType.Object,
                                        ObjectProperties = new List<ParameterConfig> {
                                            new ParameterConfig { Name = "id", Type = ParameterType.Number, IsRequired = true },
                                            new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true },
                                            new ParameterConfig { Name = "active", Type = ParameterType.Bool, IsRequired = false }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            // Act
            var descriptor = ObjectTypeDescriptor.FromParameterConfig(paramConfig);

            // Assert
            descriptor.Properties.Should().HaveCount(2);
            
            // Check departments array
            var departmentsProperty = descriptor.Properties["departments"];
            departmentsProperty.ArrayElementType.Should().NotBeNull();
            departmentsProperty.ArrayElementType!.ObjectTypeDescriptor.Should().NotBeNull();
            
            // Check department object structure
            var departmentDescriptor = departmentsProperty.ArrayElementType.ObjectTypeDescriptor!;
            departmentDescriptor.Properties.Should().HaveCount(2);
            departmentDescriptor.Properties["name"].PropertyType.Should().Be(typeof(string));
            
            // Check employees array within department
            var employeesProperty = departmentDescriptor.Properties["employees"];
            employeesProperty.ArrayElementType.Should().NotBeNull();
            employeesProperty.ArrayElementType!.ObjectTypeDescriptor.Should().NotBeNull();
            
            // Check employee object structure
            var employeeDescriptor = employeesProperty.ArrayElementType.ObjectTypeDescriptor!;
            employeeDescriptor.Properties.Should().HaveCount(3);
            employeeDescriptor.Properties["id"].PropertyType.Should().Be(typeof(double));
            employeeDescriptor.Properties["name"].PropertyType.Should().Be(typeof(string));
            employeeDescriptor.Properties["active"].PropertyType.Should().Be(typeof(bool));
        }

        [Fact]
        public void ObjectTypeDescriptor_ToJsonSchema_SimpleObject_GeneratesCorrectSchema()
        {
            // Arrange
            var paramConfig = new ParameterConfig
            {
                Name = "user",
                Description = "User information",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig { Name = "name", Description = "User name", Type = ParameterType.String, IsRequired = true },
                    new ParameterConfig { Name = "age", Description = "User age", Type = ParameterType.Number, IsRequired = false }
                }
            };
            var descriptor = ObjectTypeDescriptor.FromParameterConfig(paramConfig);

            // Act
            var schema = descriptor.ToJsonSchema();

            // Assert
            schema["type"]!.GetValue<string>().Should().Be("object");
            schema["description"]!.GetValue<string>().Should().Be("User information");
            
            var properties = schema["properties"]!.AsObject();
            ((IDictionary<string, JsonNode?>)properties).Should().ContainKey("name");
            ((IDictionary<string, JsonNode?>)properties).Should().ContainKey("age");
            
            properties["name"]!["type"]!.GetValue<string>().Should().Be("string");
            properties["name"]!["description"]!.GetValue<string>().Should().Be("User name");
            
            properties["age"]!["type"]!.GetValue<string>().Should().Be("number");
            properties["age"]!["description"]!.GetValue<string>().Should().Be("User age");
            
            var required = schema["required"]!.AsArray();
            required.Should().HaveCount(1);
            required[0]!.GetValue<string>().Should().Be("name");
        }

        [Fact]
        public void ObjectTypeDescriptor_ToJsonSchema_NestedObject_GeneratesCorrectSchema()
        {
            // Arrange
            var paramConfig = new ParameterConfig
            {
                Name = "task",
                Description = "Task with configuration",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true },
                    new ParameterConfig {
                        Name = "config",
                        Type = ParameterType.Object,
                        IsRequired = false,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "timeout", Type = ParameterType.Number, IsRequired = true }
                        }
                    }
                }
            };
            var descriptor = ObjectTypeDescriptor.FromParameterConfig(paramConfig);

            // Act
            var schema = descriptor.ToJsonSchema();

            // Assert
            var properties = schema["properties"]!.AsObject();
            var configProperty = properties["config"]!.AsObject();
            
            configProperty["type"]!.GetValue<string>().Should().Be("object");
            var configProperties = configProperty["properties"]!.AsObject();
            ((IDictionary<string, JsonNode?>)configProperties).Should().ContainKey("timeout");
            configProperties["timeout"]!["type"]!.GetValue<string>().Should().Be("number");
            
            var configRequired = configProperty["required"]!.AsArray();
            configRequired[0]!.GetValue<string>().Should().Be("timeout");
        }

        [Fact]
        public void ObjectTypeDescriptor_ToJsonSchema_WithArrayProperty_GeneratesCorrectSchema()
        {
            // Arrange
            var paramConfig = new ParameterConfig
            {
                Name = "project",
                Description = "Project information",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig {
                        Name = "tags",
                        Description = "Project tags",
                        Type = ParameterType.Array,
                        IsRequired = true,
                        ArrayElementConfig = new ParameterConfig { Name = "tag", Type = ParameterType.String }
                    }
                }
            };
            var descriptor = ObjectTypeDescriptor.FromParameterConfig(paramConfig);

            // Act
            var schema = descriptor.ToJsonSchema();

            // Assert
            var properties = schema["properties"]!.AsObject();
            var tagsProperty = properties["tags"]!.AsObject();
            
            tagsProperty["type"]!.GetValue<string>().Should().Be("array");
            tagsProperty["description"]!.GetValue<string>().Should().Be("Project tags");
            tagsProperty["items"]!["type"]!.GetValue<string>().Should().Be("string");
        }

        [Fact]
        public void PropertyDescriptor_FromParameterConfig_AllTypes_CreatesCorrectDescriptors()
        {
            // Arrange & Act & Assert
            var stringProp = PropertyDescriptor.FromParameterConfig(
                new ParameterConfig { Name = "str", Type = ParameterType.String, IsRequired = true });
            stringProp.PropertyType.Should().Be(typeof(string));
            stringProp.IsRequired.Should().BeTrue();

            var numberProp = PropertyDescriptor.FromParameterConfig(
                new ParameterConfig { Name = "num", Type = ParameterType.Number, IsRequired = false });
            numberProp.PropertyType.Should().Be(typeof(double));
            numberProp.IsRequired.Should().BeFalse();

            var boolProp = PropertyDescriptor.FromParameterConfig(
                new ParameterConfig { Name = "bool", Type = ParameterType.Bool, IsRequired = true });
            boolProp.PropertyType.Should().Be(typeof(bool));
            
            var arrayProp = PropertyDescriptor.FromParameterConfig(
                new ParameterConfig 
                { 
                    Name = "arr", 
                    Type = ParameterType.Array, 
                    ArrayElementConfig = new ParameterConfig { Type = ParameterType.String }
                });
            arrayProp.PropertyType.Should().Be(typeof(string[]));
            arrayProp.ArrayElementType.Should().NotBeNull();
        }

        [Fact]
        public void ObjectTypeDescriptor_FromParameterConfig_NonObjectType_ThrowsArgumentException()
        {
            // Arrange
            var paramConfig = new ParameterConfig { Type = ParameterType.String };

            // Act & Assert
            Action act = () => ObjectTypeDescriptor.FromParameterConfig(paramConfig);
            act.Should().Throw<ArgumentException>().WithMessage("*must be of type Object*");
        }

        [Fact]
        public void ObjectTypeDescriptor_EmptyObjectProperties_CreatesEmptySchema()
        {
            // Arrange
            var paramConfig = new ParameterConfig
            {
                Name = "empty",
                Description = "Empty object",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig>()
            };
            var descriptor = ObjectTypeDescriptor.FromParameterConfig(paramConfig);

            // Act
            var schema = descriptor.ToJsonSchema();

            // Assert
            schema["type"]!.GetValue<string>().Should().Be("object");
            var properties = schema["properties"]!.AsObject();
            ((IDictionary<string, JsonNode?>)properties).Should().BeEmpty();
            ((IDictionary<string, JsonNode?>)schema).Should().NotContainKey("required");
        }
    }
}