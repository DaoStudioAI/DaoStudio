using DaoStudio.Interfaces.Plugins;
using Naming;
using Naming.Types;
using FluentAssertions;

namespace TestNamingTool;

/// <summary>
/// Comprehensive tests for ParameterConfigConverter utility class.
/// Tests conversion from ParameterConfig to FunctionTypeMetadata with full cascade information.
/// </summary>
public class ParameterConfigConverterTests
{
    #region Simple Type Conversion Tests

    [Fact]
    public void ConvertToMetadata_StringParameter_ConvertsCorrectly()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "message",
            Description = "A message string",
            Type = ParameterType.String,
            IsRequired = true
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.Name.Should().Be("message");
        metadata.Description.Should().Be("A message string");
        metadata.ParameterType.Should().Be(typeof(string));
        metadata.IsRequired.Should().BeTrue();
        metadata.ArrayElementMetadata.Should().BeNull();
        metadata.ObjectProperties.Should().BeNull();
    }

    [Fact]
    public void ConvertToMetadata_NumberParameter_ConvertsToDouble()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "score",
            Description = "A numeric score",
            Type = ParameterType.Number,
            IsRequired = false
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.Name.Should().Be("score");
        metadata.Description.Should().Be("A numeric score");
        metadata.ParameterType.Should().Be(typeof(double));
        metadata.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void ConvertToMetadata_BoolParameter_ConvertsCorrectly()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "isActive",
            Description = "Active status",
            Type = ParameterType.Bool,
            IsRequired = true
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.Name.Should().Be("isActive");
        metadata.Description.Should().Be("Active status");
        metadata.ParameterType.Should().Be(typeof(bool));
        metadata.IsRequired.Should().BeTrue();
    }

    #endregion

    #region Simple Array Conversion Tests

    [Fact]
    public void ConvertToMetadata_SimpleStringArray_ConvertsWithElementMetadata()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "tags",
            Description = "List of tags",
            Type = ParameterType.Array,
            IsRequired = true,
            ArrayElementConfig = new ParameterConfig
            {
                Name = "tag",
                Description = "Individual tag",
                Type = ParameterType.String,
                IsRequired = true
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.Name.Should().Be("tags");
        metadata.Description.Should().Be("List of tags");
        metadata.IsRequired.Should().BeTrue();
        
        // Check array element metadata
        metadata.ArrayElementMetadata.Should().NotBeNull();
        metadata.ArrayElementMetadata!.Name.Should().Be("tag");
        metadata.ArrayElementMetadata.Description.Should().Be("Individual tag");
        metadata.ArrayElementMetadata.ParameterType.Should().Be(typeof(string));
    }

    [Fact]
    public void ConvertToMetadata_NumberArray_ConvertsWithElementMetadata()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "scores",
            Description = "List of scores",
            Type = ParameterType.Array,
            IsRequired = true,
            ArrayElementConfig = new ParameterConfig
            {
                Name = "score",
                Description = "Individual score",
                Type = ParameterType.Number,
                IsRequired = true
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.ArrayElementMetadata.Should().NotBeNull();
        metadata.ArrayElementMetadata!.ParameterType.Should().Be(typeof(double));
        metadata.ArrayElementMetadata.Description.Should().Be("Individual score");
    }

    [Fact]
    public void ConvertToMetadata_ArrayWithoutElementConfig_CreatesObjectArray()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "items",
            Description = "Generic items",
            Type = ParameterType.Array,
            IsRequired = true,
            ArrayElementConfig = null
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.Name.Should().Be("items");
        metadata.ParameterType.Should().NotBeNull();
        // ArrayElementMetadata should be null when no element config is provided
        metadata.ArrayElementMetadata.Should().BeNull();
    }

    #endregion

    #region Simple Object Conversion Tests

    [Fact]
    public void ConvertToMetadata_SimpleObject_ConvertsWithProperties()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "user",
            Description = "User information",
            Type = ParameterType.Object,
            IsRequired = true,
            ObjectProperties = new List<ParameterConfig> {
                new ParameterConfig {
                    Name = "name",
                    Description = "User name",
                    Type = ParameterType.String,
                    IsRequired = true
                },
                new ParameterConfig {
                    Name = "age",
                    Description = "User age",
                    Type = ParameterType.Number,
                    IsRequired = false
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.Name.Should().Be("user");
        metadata.Description.Should().Be("User information");
        metadata.IsRequired.Should().BeTrue();
        
        metadata.ObjectProperties.Should().NotBeNull();
        metadata.ObjectProperties.Should().HaveCount(2);
        
        // Verify name property
        metadata.ObjectProperties.Should().ContainKey("name");
        metadata.ObjectProperties!["name"].Name.Should().Be("name");
        metadata.ObjectProperties["name"].ParameterType.Should().Be(typeof(string));
        metadata.ObjectProperties["name"].IsRequired.Should().BeTrue();
        
        // Verify age property
        metadata.ObjectProperties.Should().ContainKey("age");
        metadata.ObjectProperties["age"].Name.Should().Be("age");
        metadata.ObjectProperties["age"].ParameterType.Should().Be(typeof(double));
        metadata.ObjectProperties["age"].IsRequired.Should().BeFalse();
    }

    [Fact]
    public void ConvertToMetadata_ObjectWithoutProperties_HandlesGracefully()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "emptyObject",
            Description = "Empty object",
            Type = ParameterType.Object,
            IsRequired = true,
            ObjectProperties = new List<ParameterConfig>()
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.Name.Should().Be("emptyObject");
        metadata.ObjectProperties.Should().NotBeNull();
        metadata.ObjectProperties.Should().BeEmpty();
    }

    [Fact]
    public void ConvertToMetadata_ObjectWithNullProperties_HandlesGracefully()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "nullPropsObject",
            Description = "Object with null properties",
            Type = ParameterType.Object,
            IsRequired = true,
            ObjectProperties = null
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.Name.Should().Be("nullPropsObject");
        metadata.ObjectProperties.Should().BeNull();
    }

    #endregion

    #region Nested Object Conversion Tests

    [Fact]
    public void ConvertToMetadata_NestedObject_ConvertsRecursively()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "profile",
            Description = "User profile",
            Type = ParameterType.Object,
            IsRequired = true,
            ObjectProperties = new List<ParameterConfig> {
                new ParameterConfig {
                    Name = "name",
                    Type = ParameterType.String,
                    IsRequired = true
                },
                new ParameterConfig {
                    Name = "address",
                    Description = "Address information",
                    Type = ParameterType.Object,
                    IsRequired = true,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig {
                            Name = "street",
                            Type = ParameterType.String,
                            IsRequired = true
                        },
                        new ParameterConfig {
                            Name = "city",
                            Type = ParameterType.String,
                            IsRequired = true
                        },
                        new ParameterConfig {
                            Name = "zipCode",
                            Type = ParameterType.String,
                            IsRequired = false
                        }
                    }
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.ObjectProperties.Should().ContainKey("address");
        var addressMetadata = metadata.ObjectProperties!["address"];
        
        addressMetadata.ObjectProperties.Should().NotBeNull();
        addressMetadata.ObjectProperties.Should().HaveCount(3);
        addressMetadata.ObjectProperties.Should().ContainKey("street");
        addressMetadata.ObjectProperties.Should().ContainKey("city");
        addressMetadata.ObjectProperties.Should().ContainKey("zipCode");
        
        addressMetadata.ObjectProperties!["street"].ParameterType.Should().Be(typeof(string));
        addressMetadata.ObjectProperties["city"].ParameterType.Should().Be(typeof(string));
        addressMetadata.ObjectProperties["zipCode"].IsRequired.Should().BeFalse();
    }

    [Fact]
    public void ConvertToMetadata_DeeplyNestedObject_ConvertsAllLevels()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "config",
            Description = "System configuration",
            Type = ParameterType.Object,
            IsRequired = true,
            ObjectProperties = new List<ParameterConfig> {
                new ParameterConfig {
                    Name = "database",
                    Type = ParameterType.Object,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig {
                            Name = "connection",
                            Type = ParameterType.Object,
                            ObjectProperties = new List<ParameterConfig> {
                                new ParameterConfig {
                                    Name = "host",
                                    Type = ParameterType.String,
                                    IsRequired = true
                                },
                                new ParameterConfig {
                                    Name = "port",
                                    Type = ParameterType.Number,
                                    IsRequired = true
                                }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        var dbMetadata = metadata.ObjectProperties!["database"];
        var connMetadata = dbMetadata.ObjectProperties!["connection"];
        
        connMetadata.ObjectProperties.Should().HaveCount(2);
        connMetadata.ObjectProperties!["host"].ParameterType.Should().Be(typeof(string));
        connMetadata.ObjectProperties["port"].ParameterType.Should().Be(typeof(double));
    }

    #endregion

    #region Array of Objects Conversion Tests

    [Fact]
    public void ConvertToMetadata_ArrayOfSimpleObjects_ConvertsCorrectly()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "users",
            Description = "List of users",
            Type = ParameterType.Array,
            IsRequired = true,
            ArrayElementConfig = new ParameterConfig
            {
                Name = "user",
                Description = "Individual user",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig {
                        Name = "id",
                        Type = ParameterType.String,
                        IsRequired = true
                    },
                    new ParameterConfig {
                        Name = "name",
                        Type = ParameterType.String,
                        IsRequired = true
                    },
                    new ParameterConfig {
                        Name = "active",
                        Type = ParameterType.Bool,
                        IsRequired = false
                    }
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.ArrayElementMetadata.Should().NotBeNull();
        metadata.ArrayElementMetadata!.ObjectProperties.Should().NotBeNull();
        metadata.ArrayElementMetadata.ObjectProperties.Should().HaveCount(3);
        
        metadata.ArrayElementMetadata.ObjectProperties!["id"].ParameterType.Should().Be(typeof(string));
        metadata.ArrayElementMetadata.ObjectProperties["name"].ParameterType.Should().Be(typeof(string));
        metadata.ArrayElementMetadata.ObjectProperties["active"].ParameterType.Should().Be(typeof(bool));
        metadata.ArrayElementMetadata.ObjectProperties["active"].IsRequired.Should().BeFalse();
    }

    [Fact]
    public void ConvertToMetadata_ArrayOfComplexObjects_ConvertsNestedStructures()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "orders",
            Description = "Customer orders",
            Type = ParameterType.Array,
            IsRequired = true,
            ArrayElementConfig = new ParameterConfig
            {
                Name = "order",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig {
                        Name = "orderId",
                        Type = ParameterType.String,
                        IsRequired = true
                    },
                    new ParameterConfig {
                        Name = "customer",
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig {
                                Name = "customerId",
                                Type = ParameterType.String,
                                IsRequired = true
                            },
                            new ParameterConfig {
                                Name = "name",
                                Type = ParameterType.String,
                                IsRequired = true
                            }
                        }
                    },
                    new ParameterConfig {
                        Name = "total",
                        Type = ParameterType.Number,
                        IsRequired = true
                    }
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        var elementMetadata = metadata.ArrayElementMetadata!;
        elementMetadata.ObjectProperties.Should().ContainKey("customer");
        
        var customerMetadata = elementMetadata.ObjectProperties!["customer"];
        customerMetadata.ObjectProperties.Should().HaveCount(2);
        customerMetadata.ObjectProperties!["customerId"].ParameterType.Should().Be(typeof(string));
        customerMetadata.ObjectProperties["name"].ParameterType.Should().Be(typeof(string));
    }

    #endregion

    #region Object with Array Properties Conversion Tests

    [Fact]
    public void ConvertToMetadata_ObjectWithArrayProperty_ConvertsCorrectly()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "project",
            Description = "Project information",
            Type = ParameterType.Object,
            IsRequired = true,
            ObjectProperties = new List<ParameterConfig> {
                new ParameterConfig {
                    Name = "name",
                    Type = ParameterType.String,
                    IsRequired = true
                },
                new ParameterConfig {
                    Name = "technologies",
                    Description = "Technologies used",
                    Type = ParameterType.Array,
                    IsRequired = true,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "technology",
                        Type = ParameterType.String
                    }
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.ObjectProperties.Should().ContainKey("technologies");
        var techMetadata = metadata.ObjectProperties!["technologies"];
        
        techMetadata.ArrayElementMetadata.Should().NotBeNull();
        techMetadata.ArrayElementMetadata!.ParameterType.Should().Be(typeof(string));
    }

    [Fact]
    public void ConvertToMetadata_ObjectWithMultipleArrayProperties_ConvertsAll()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "item",
            Description = "Item with multiple arrays",
            Type = ParameterType.Object,
            IsRequired = true,
            ObjectProperties = new List<ParameterConfig> {
                new ParameterConfig {
                    Name = "tags",
                    Type = ParameterType.Array,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "tag",
                        Type = ParameterType.String
                    }
                },
                new ParameterConfig {
                    Name = "scores",
                    Type = ParameterType.Array,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "score",
                        Type = ParameterType.Number
                    }
                },
                new ParameterConfig {
                    Name = "flags",
                    Type = ParameterType.Array,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "flag",
                        Type = ParameterType.Bool
                    }
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.ObjectProperties!["tags"].ArrayElementMetadata!.ParameterType.Should().Be(typeof(string));
        metadata.ObjectProperties["scores"].ArrayElementMetadata!.ParameterType.Should().Be(typeof(double));
        metadata.ObjectProperties["flags"].ArrayElementMetadata!.ParameterType.Should().Be(typeof(bool));
    }

    #endregion

    #region Nested Arrays Conversion Tests

    [Fact]
    public void ConvertToMetadata_ArrayOfArrays_ConvertsRecursively()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "matrix",
            Description = "2D matrix",
            Type = ParameterType.Array,
            IsRequired = true,
            ArrayElementConfig = new ParameterConfig
            {
                Name = "row",
                Type = ParameterType.Array,
                ArrayElementConfig = new ParameterConfig
                {
                    Name = "cell",
                    Type = ParameterType.Number
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.ArrayElementMetadata.Should().NotBeNull();
        metadata.ArrayElementMetadata!.ArrayElementMetadata.Should().NotBeNull();
        metadata.ArrayElementMetadata.ArrayElementMetadata!.ParameterType.Should().Be(typeof(double));
    }

    [Fact]
    public void ConvertToMetadata_ArrayOfObjectsWithArrays_ConvertsComplexNesting()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "projects",
            Description = "Projects with tags",
            Type = ParameterType.Array,
            IsRequired = true,
            ArrayElementConfig = new ParameterConfig
            {
                Name = "project",
                Type = ParameterType.Object,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig {
                        Name = "name",
                        Type = ParameterType.String,
                        IsRequired = true
                    },
                    new ParameterConfig {
                        Name = "tags",
                        Type = ParameterType.Array,
                        ArrayElementConfig = new ParameterConfig
                        {
                            Name = "tag",
                            Type = ParameterType.String
                        }
                    }
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        var projectMetadata = metadata.ArrayElementMetadata!;
        projectMetadata.ObjectProperties.Should().ContainKey("tags");
        
        var tagsMetadata = projectMetadata.ObjectProperties!["tags"];
        tagsMetadata.ArrayElementMetadata.Should().NotBeNull();
        tagsMetadata.ArrayElementMetadata!.ParameterType.Should().Be(typeof(string));
    }

    #endregion

    #region Mixed Complex Types Conversion Tests

    [Fact]
    public void ConvertToMetadata_ComplexMixedStructure_ConvertsAllLevels()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "complexData",
            Description = "Complex nested structure",
            Type = ParameterType.Object,
            IsRequired = true,
            ObjectProperties = new List<ParameterConfig> {
                new ParameterConfig {
                    Name = "simpleString",
                    Type = ParameterType.String,
                    IsRequired = true
                },
                new ParameterConfig {
                    Name = "simpleNumber",
                    Type = ParameterType.Number,
                    IsRequired = true
                },
                new ParameterConfig {
                    Name = "arrayOfStrings",
                    Type = ParameterType.Array,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "item",
                        Type = ParameterType.String
                    }
                },
                new ParameterConfig {
                    Name = "nestedObject",
                    Type = ParameterType.Object,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "field1", Type = ParameterType.String },
                        new ParameterConfig { Name = "field2", Type = ParameterType.Bool }
                    }
                },
                new ParameterConfig {
                    Name = "arrayOfObjects",
                    Type = ParameterType.Array,
                    ArrayElementConfig = new ParameterConfig
                    {
                        Name = "obj",
                        Type = ParameterType.Object,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "id", Type = ParameterType.String },
                            new ParameterConfig { Name = "value", Type = ParameterType.Number }
                        }
                    }
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.ObjectProperties.Should().HaveCount(5);
        
        // Verify simple properties
        metadata.ObjectProperties!["simpleString"].ParameterType.Should().Be(typeof(string));
        metadata.ObjectProperties["simpleNumber"].ParameterType.Should().Be(typeof(double));
        
        // Verify array of strings
        metadata.ObjectProperties["arrayOfStrings"].ArrayElementMetadata.Should().NotBeNull();
        metadata.ObjectProperties["arrayOfStrings"].ArrayElementMetadata!.ParameterType.Should().Be(typeof(string));
        
        // Verify nested object
        metadata.ObjectProperties["nestedObject"].ObjectProperties.Should().HaveCount(2);
        
        // Verify array of objects
        var arrayOfObjsMetadata = metadata.ObjectProperties["arrayOfObjects"];
        arrayOfObjsMetadata.ArrayElementMetadata.Should().NotBeNull();
        arrayOfObjsMetadata.ArrayElementMetadata!.ObjectProperties.Should().HaveCount(2);
    }

    #endregion

    #region Requirement Propagation Tests

    [Fact]
    public void ConvertToMetadata_RequiredFlags_PreservedThroughNesting()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "data",
            Type = ParameterType.Object,
            IsRequired = true,
            ObjectProperties = new List<ParameterConfig> {
                new ParameterConfig {
                    Name = "requiredField",
                    Type = ParameterType.String,
                    IsRequired = true
                },
                new ParameterConfig {
                    Name = "optionalField",
                    Type = ParameterType.String,
                    IsRequired = false
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.IsRequired.Should().BeTrue();
        metadata.ObjectProperties!["requiredField"].IsRequired.Should().BeTrue();
        metadata.ObjectProperties["optionalField"].IsRequired.Should().BeFalse();
    }

    #endregion

    #region Description Propagation Tests

    [Fact]
    public void ConvertToMetadata_Descriptions_PreservedThroughNesting()
    {
        // Arrange
        var paramConfig = new ParameterConfig
        {
            Name = "config",
            Description = "Top level config",
            Type = ParameterType.Object,
            IsRequired = true,
            ObjectProperties = new List<ParameterConfig> {
                new ParameterConfig {
                    Name = "nested",
                    Description = "Nested config",
                    Type = ParameterType.Object,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig {
                            Name = "value",
                            Description = "Deep value",
                            Type = ParameterType.String
                        }
                    }
                }
            }
        };

        // Act
        var metadata = ParameterConfigConverter.ConvertToMetadata(paramConfig);

        // Assert
        metadata.Description.Should().Be("Top level config");
        metadata.ObjectProperties!["nested"].Description.Should().Be("Nested config");
        metadata.ObjectProperties["nested"].ObjectProperties!["value"].Description.Should().Be("Deep value");
    }

    #endregion
}
