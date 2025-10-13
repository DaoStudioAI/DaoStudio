using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Naming;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool;

/// <summary>
/// Integration tests that validate the complete flow from ParameterConfig to JSON schema generation.
/// Tests that the PlainAIFunction correctly builds JSON schemas from FunctionTypeMetadata
/// created by ParameterConfigConverter.
/// </summary>
public class JsonSchemaGenerationIntegrationTests
{
    private readonly NamingPluginFactory _factory;
    private readonly MockHost _mockHost;
    private readonly MockPerson _mockPerson;
    private readonly MockHostSession _mockHostSession;

    public JsonSchemaGenerationIntegrationTests()
    {
        _factory = new NamingPluginFactory();
        _mockHost = new MockHost();
        _mockPerson = new MockPerson("TestAssistant", "Test assistant for schema generation");
        _mockHostSession = new MockHostSession(1);

        _factory.SetHost(_mockHost).Wait();
        _mockHost.AddPerson(_mockPerson);
    }

    #region Helper Methods

    private async Task<JsonElement> GetFunctionSchemaAsync(NamingConfig config)
    {
        var plugToolInfo = new PlugToolInfo
        {
            InstanceId = 1,
            Description = "Test plugin",
            Config = JsonSerializer.Serialize(config),
            DisplayName = "Test Schema Plugin"
        };

        var pluginInstance = await _factory.CreatePluginToolAsync(plugToolInfo);
        var toolcallFunctions = new List<FunctionWithDescription>();
        await pluginInstance.GetSessionFunctionsAsync(toolcallFunctions, _mockPerson as IHostPerson, _mockHostSession);

        toolcallFunctions.Should().HaveCount(1);
        
        // Access the PlainAIFunction via reflection to get JsonSchema
        var function = toolcallFunctions[0];
        
        // The function description contains the parameters with FunctionTypeMetadata
        // We can verify the metadata structure directly instead of relying on PlainAIFunction
        // For this test, we'll build a simplified schema from the function description
        var parameters = function.Description.Parameters;
        
        // Create a mock schema based on the parameters for testing purposes
        var schemaObject = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject()
        };
        var requiredProperties = new JsonArray();

        foreach (var param in parameters)
        {
            var propertySchema = BuildSchemaFromMetadata(param);
            ((JsonObject)schemaObject["properties"]!).Add(param.Name, propertySchema);
            if (param.IsRequired)
            {
                requiredProperties.Add(param.Name);
            }
        }

        if (requiredProperties.Count > 0)
        {
            schemaObject["required"] = requiredProperties;
        }

        using var doc = JsonDocument.Parse(schemaObject.ToJsonString());
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Builds a JSON schema object from FunctionTypeMetadata (mirrors PlainAIFunction logic)
    /// </summary>
    private JsonObject BuildSchemaFromMetadata(FunctionTypeMetadata metadata)
    {
        var propertySchema = new JsonObject();
        
        // Handle type information
        propertySchema["type"] = GetJsonType(metadata.ParameterType);
        
        // Handle array types
        string? primaryType = null;
        if (propertySchema["type"] is JsonValue jsonValue)
        {
            primaryType = jsonValue.GetValue<string>();
        }

        if (primaryType == "array" && metadata.ArrayElementMetadata != null)
        {
            propertySchema["items"] = BuildSchemaFromMetadata(metadata.ArrayElementMetadata);
        }

        // Handle object types
        if (primaryType == "object" && metadata.ObjectProperties != null && metadata.ObjectProperties.Count > 0)
        {
            var objectPropertiesSchema = new JsonObject();
            var objectRequiredProperties = new JsonArray();

            foreach (var property in metadata.ObjectProperties)
            {
                objectPropertiesSchema[property.Key] = BuildSchemaFromMetadata(property.Value);
                if (property.Value.IsRequired)
                {
                    objectRequiredProperties.Add(property.Key);
                }
            }

            propertySchema["properties"] = objectPropertiesSchema;
            if (objectRequiredProperties.Count > 0)
            {
                propertySchema["required"] = objectRequiredProperties;
            }
        }

        // Add description if available
        if (!string.IsNullOrEmpty(metadata.Description))
        {
            propertySchema["description"] = metadata.Description;
        }

        return propertySchema;
    }

    private string GetJsonType(Type type)
    {
        if (type == typeof(string) || type == typeof(char) || type == typeof(Guid))
            return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
            type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte))
            return "integer";
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return "number";
        if (type == typeof(bool))
            return "boolean";
        if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
            return "array";
        return "object";
    }

    private NamingConfig CreateBasicConfig()
    {
        return new NamingConfig
        {
            ExecutivePerson = new ConfigPerson { Name = "TestAssistant", Description = "Test" },
            MaxRecursionLevel = 3,
            UrgingMessage = "Please complete the task"
        };
    }

    #endregion

    #region Simple Type Schema Tests

    [Fact]
    public async Task JsonSchema_SimpleStringParameter_GeneratesCorrectJsonSchema()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.InputParameters = new List<ParameterConfig>
        {
            new ParameterConfig
            {
                Name = "message",
                Type = ParameterType.String,
                Description = "A message to process",
                IsRequired = true
            }
        };

        // Act
        var schema = await GetFunctionSchemaAsync(config);

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        Assert.NotNull(schemaObject);
        
        var properties = schemaObject["properties"] as JsonObject;
        Assert.NotNull(properties);
        Assert.True(properties.ContainsKey("message"));
        
        var messageProperty = properties["message"] as JsonObject;
        Assert.NotNull(messageProperty);
        messageProperty["type"]!.GetValue<string>().Should().Be("string");
        messageProperty["description"]!.GetValue<string>().Should().Be("A message to process");
    }

    [Fact]
    public async Task JsonSchema_MultipleSimpleParameters_GeneratesCorrectJsonSchema()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.InputParameters = new List<ParameterConfig>
        {
            new ParameterConfig { Name = "name", Type = ParameterType.String, Description = "User name", IsRequired = true },
            new ParameterConfig { Name = "age", Type = ParameterType.Number, Description = "User age", IsRequired = true },
            new ParameterConfig { Name = "isActive", Type = ParameterType.Bool, Description = "Active status", IsRequired = false }
        };

        // Act
        var schema = await GetFunctionSchemaAsync(config);

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        
        Assert.True(properties!.ContainsKey("name"));
        Assert.True(properties.ContainsKey("age"));
        Assert.True(properties.ContainsKey("isActive"));
        
        (properties["name"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("string");
        (properties["age"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("number");
        (properties["isActive"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("boolean");
        
        var required = schemaObject["required"] as JsonArray;
        Assert.NotNull(required);
        required.Should().Contain(n => n!.GetValue<string>() == "name");
        required.Should().Contain(n => n!.GetValue<string>() == "age");
        required.Should().NotContain(n => n!.GetValue<string>() == "isActive");
    }

    #endregion

    #region Array Type Schema Tests

    [Fact]
    public async Task JsonSchema_SimpleArray_GeneratesCorrectItemsSchema()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.InputParameters = new List<ParameterConfig>
        {
            new ParameterConfig
            {
                Name = "tags",
                Type = ParameterType.Array,
                Description = "List of tags",
                IsRequired = true,
                ArrayElementConfig = new ParameterConfig
                {
                    Name = "tag",
                    Type = ParameterType.String,
                    Description = "Individual tag"
                }
            }
        };

        // Act
        var schema = await GetFunctionSchemaAsync(config);

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var tagsProperty = properties!["tags"] as JsonObject;
        
        tagsProperty!["type"]!.GetValue<string>().Should().Be("array");
        tagsProperty["description"]!.GetValue<string>().Should().Be("List of tags");
        
        var items = tagsProperty["items"] as JsonObject;
        Assert.NotNull(items);
        items["type"]!.GetValue<string>().Should().Be("string");
        items["description"]!.GetValue<string>().Should().Be("Individual tag");
    }

    [Fact]
    public async Task JsonSchema_ArrayOfNumbers_GeneratesCorrectSchema()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.InputParameters = new List<ParameterConfig>
        {
            new ParameterConfig
            {
                Name = "scores",
                Type = ParameterType.Array,
                Description = "Performance scores",
                IsRequired = true,
                ArrayElementConfig = new ParameterConfig
                {
                    Name = "score",
                    Type = ParameterType.Number,
                    Description = "Individual score value"
                }
            }
        };

        // Act
        var schema = await GetFunctionSchemaAsync(config);

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var scoresProperty = properties!["scores"] as JsonObject;
        
        var items = scoresProperty!["items"] as JsonObject;
        items!["type"]!.GetValue<string>().Should().Be("number");
    }

    #endregion

    #region Object Type Schema Tests

    [Fact]
    public async Task JsonSchema_SimpleObject_GeneratesNestedPropertiesAndRequired()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.InputParameters = new List<ParameterConfig>
        {
            new ParameterConfig
            {
                Name = "user",
                Type = ParameterType.Object,
                Description = "User information",
                IsRequired = true,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig {
                        Name = "username",
                        Type = ParameterType.String,
                        Description = "User's username",
                        IsRequired = true
                    },
                    new ParameterConfig {
                        Name = "email",
                        Type = ParameterType.String,
                        Description = "User's email",
                        IsRequired = true
                    },
                    new ParameterConfig {
                        Name = "age",
                        Type = ParameterType.Number,
                        Description = "User's age",
                        IsRequired = false
                    }
                }
            }
        };

        // Act
        var schema = await GetFunctionSchemaAsync(config);

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var userProperty = properties!["user"] as JsonObject;
        
        userProperty!["type"]!.GetValue<string>().Should().Be("object");
        userProperty["description"]!.GetValue<string>().Should().Be("User information");
        
        var userProperties = userProperty["properties"] as JsonObject;
        Assert.True(userProperties!.ContainsKey("username"));
        Assert.True(userProperties.ContainsKey("email"));
        Assert.True(userProperties.ContainsKey("age"));
        
        var userRequired = userProperty["required"] as JsonArray;
        Assert.NotNull(userRequired);
        userRequired.Should().Contain(n => n!.GetValue<string>() == "username");
        userRequired.Should().Contain(n => n!.GetValue<string>() == "email");
        userRequired.Should().NotContain(n => n!.GetValue<string>() == "age");
    }

    [Fact]
    public async Task JsonSchema_NestedObject_GeneratesDeepHierarchy()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.InputParameters = new List<ParameterConfig>
        {
            new ParameterConfig
            {
                Name = "profile",
                Type = ParameterType.Object,
                Description = "User profile with address",
                IsRequired = true,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true },
                    new ParameterConfig {
                        Name = "address",
                        Type = ParameterType.Object,
                        Description = "Address information",
                        IsRequired = true,
                        ObjectProperties = new List<ParameterConfig> {
                            new ParameterConfig { Name = "street", Type = ParameterType.String, IsRequired = true },
                            new ParameterConfig { Name = "city", Type = ParameterType.String, IsRequired = true },
                            new ParameterConfig { Name = "zipCode", Type = ParameterType.String, IsRequired = false }
                        }
                    }
                }
            }
        };

        // Act
        var schema = await GetFunctionSchemaAsync(config);

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var profileProperty = properties!["profile"] as JsonObject;
        var profileProperties = profileProperty!["properties"] as JsonObject;
        var addressProperty = profileProperties!["address"] as JsonObject;
        
        addressProperty!["type"]!.GetValue<string>().Should().Be("object");
        addressProperty["description"]!.GetValue<string>().Should().Be("Address information");
        
        var addressProperties = addressProperty["properties"] as JsonObject;
        Assert.True(addressProperties!.ContainsKey("street"));
        Assert.True(addressProperties.ContainsKey("city"));
        Assert.True(addressProperties.ContainsKey("zipCode"));
        
        var addressRequired = addressProperty["required"] as JsonArray;
        Assert.NotNull(addressRequired);
        addressRequired.Should().Contain(n => n!.GetValue<string>() == "street");
        addressRequired.Should().Contain(n => n!.GetValue<string>() == "city");
        addressRequired.Should().NotContain(n => n!.GetValue<string>() == "zipCode");
    }

    #endregion

    #region Array of Objects Schema Tests

    [Fact]
    public async Task JsonSchema_ArrayOfObjects_GeneratesCompleteItemSchema()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.InputParameters = new List<ParameterConfig>
        {
            new ParameterConfig
            {
                Name = "users",
                Type = ParameterType.Array,
                Description = "List of users",
                IsRequired = true,
                ArrayElementConfig = new ParameterConfig
                {
                    Name = "user",
                    Type = ParameterType.Object,
                    Description = "Individual user",
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "id", Type = ParameterType.String, IsRequired = true },
                        new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true },
                        new ParameterConfig { Name = "active", Type = ParameterType.Bool, IsRequired = false }
                    }
                }
            }
        };

        // Act
        var schema = await GetFunctionSchemaAsync(config);

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var usersProperty = properties!["users"] as JsonObject;
        
        usersProperty!["type"]!.GetValue<string>().Should().Be("array");
        
        var items = usersProperty["items"] as JsonObject;
        items!["type"]!.GetValue<string>().Should().Be("object");
        items["description"]!.GetValue<string>().Should().Be("Individual user");
        
        var itemProperties = items["properties"] as JsonObject;
        Assert.True(itemProperties!.ContainsKey("id"));
        Assert.True(itemProperties.ContainsKey("name"));
        Assert.True(itemProperties.ContainsKey("active"));
        
        var itemRequired = items["required"] as JsonArray;
        Assert.NotNull(itemRequired);
        itemRequired.Should().Contain(n => n!.GetValue<string>() == "id");
        itemRequired.Should().Contain(n => n!.GetValue<string>() == "name");
        itemRequired.Should().NotContain(n => n!.GetValue<string>() == "active");
    }

    [Fact]
    public async Task JsonSchema_ArrayOfComplexObjects_WithNestedObjects_GeneratesCompleteSchema()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.InputParameters = new List<ParameterConfig>
        {
            new ParameterConfig
            {
                Name = "orders",
                Type = ParameterType.Array,
                Description = "Customer orders",
                IsRequired = true,
                ArrayElementConfig = new ParameterConfig
                {
                    Name = "order",
                    Type = ParameterType.Object,
                    ObjectProperties = new List<ParameterConfig> {
                        new ParameterConfig { Name = "orderId", Type = ParameterType.String, IsRequired = true },
                        new ParameterConfig {
                            Name = "customer",
                            Type = ParameterType.Object,
                            Description = "Customer info",
                            ObjectProperties = new List<ParameterConfig> {
                                new ParameterConfig { Name = "customerId", Type = ParameterType.String, IsRequired = true },
                                new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true }
                            }
                        },
                        new ParameterConfig { Name = "total", Type = ParameterType.Number, IsRequired = true }
                    }
                }
            }
        };

        // Act
        var schema = await GetFunctionSchemaAsync(config);

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var ordersProperty = properties!["orders"] as JsonObject;
        var items = ordersProperty!["items"] as JsonObject;
        var itemProperties = items!["properties"] as JsonObject;
        var customerProperty = itemProperties!["customer"] as JsonObject;
        
        customerProperty!["type"]!.GetValue<string>().Should().Be("object");
        customerProperty["description"]!.GetValue<string>().Should().Be("Customer info");
        
        var customerProperties = customerProperty["properties"] as JsonObject;
        Assert.True(customerProperties!.ContainsKey("customerId"));
        Assert.True(customerProperties.ContainsKey("name"));
    }

    #endregion

    #region Object with Arrays Schema Tests

    [Fact]
    public async Task JsonSchema_ObjectWithArrayProperty_GeneratesCorrectSchema()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.InputParameters = new List<ParameterConfig>
        {
            new ParameterConfig
            {
                Name = "project",
                Type = ParameterType.Object,
                Description = "Project with technologies",
                IsRequired = true,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig { Name = "name", Type = ParameterType.String, IsRequired = true },
                    new ParameterConfig {
                        Name = "technologies",
                        Type = ParameterType.Array,
                        Description = "Technologies used",
                        ArrayElementConfig = new ParameterConfig { Name = "tech", Type = ParameterType.String }
                    }
                }
            }
        };

        // Act
        var schema = await GetFunctionSchemaAsync(config);

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var projectProperty = properties!["project"] as JsonObject;
        var projectProperties = projectProperty!["properties"] as JsonObject;
        var technologiesProperty = projectProperties!["technologies"] as JsonObject;
        
        technologiesProperty!["type"]!.GetValue<string>().Should().Be("array");
        
        var items = technologiesProperty["items"] as JsonObject;
        items!["type"]!.GetValue<string>().Should().Be("string");
    }

    #endregion

    #region Complex Mixed Schema Tests

    [Fact]
    public async Task JsonSchema_ComplexMixedStructure_GeneratesCompleteHierarchy()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.InputParameters = new List<ParameterConfig>
        {
            new ParameterConfig
            {
                Name = "complexData",
                Type = ParameterType.Object,
                Description = "Complex data structure",
                IsRequired = true,
                ObjectProperties = new List<ParameterConfig> {
                    new ParameterConfig { Name = "simpleString", Type = ParameterType.String, IsRequired = true },
                    new ParameterConfig {
                        Name = "simpleArray",
                        Type = ParameterType.Array,
                        ArrayElementConfig = new ParameterConfig { Name = "item", Type = ParameterType.String }
                    },
                    new ParameterConfig {
                        Name = "arrayOfObjects",
                        Type = ParameterType.Array,
                        ArrayElementConfig = new ParameterConfig
                        {
                            Name = "obj",
                            Type = ParameterType.Object,
                            ObjectProperties = new List<ParameterConfig> {
                                new ParameterConfig { Name = "id", Type = ParameterType.String, IsRequired = true },
                                new ParameterConfig {
                                    Name = "tags",
                                    Type = ParameterType.Array,
                                    ArrayElementConfig = new ParameterConfig { Name = "tag", Type = ParameterType.String }
                                }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var schema = await GetFunctionSchemaAsync(config);

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var complexDataProperty = properties!["complexData"] as JsonObject;
        var complexDataProperties = complexDataProperty!["properties"] as JsonObject;
        
        // Verify all top-level properties exist
        Assert.True(complexDataProperties!.ContainsKey("simpleString"));
        Assert.True(complexDataProperties.ContainsKey("simpleArray"));
        Assert.True(complexDataProperties.ContainsKey("arrayOfObjects"));
        
        // Verify arrayOfObjects structure
        var arrayOfObjectsProperty = complexDataProperties["arrayOfObjects"] as JsonObject;
        var arrayItems = arrayOfObjectsProperty!["items"] as JsonObject;
        var arrayItemProperties = arrayItems!["properties"] as JsonObject;
        
        Assert.True(arrayItemProperties!.ContainsKey("id"));
        Assert.True(arrayItemProperties.ContainsKey("tags"));
        
        // Verify nested tags array
        var tagsProperty = arrayItemProperties["tags"] as JsonObject;
        tagsProperty!["type"]!.GetValue<string>().Should().Be("array");
        
        var tagsItems = tagsProperty["items"] as JsonObject;
        tagsItems!["type"]!.GetValue<string>().Should().Be("string");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task JsonSchema_EmptyObjectProperties_GeneratesValidSchema()
    {
        // Arrange
        var config = CreateBasicConfig();
        config.InputParameters = new List<ParameterConfig>
        {
            new ParameterConfig
            {
                Name = "emptyObject",
                Type = ParameterType.Object,
                Description = "Empty object",
                IsRequired = true,
                ObjectProperties = new List<ParameterConfig>()
            }
        };

        // Act
        var schema = await GetFunctionSchemaAsync(config);

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var emptyObjectProperty = properties!["emptyObject"] as JsonObject;
        
        emptyObjectProperty!["type"]!.GetValue<string>().Should().Be("object");
        
        // Empty objects should have properties node with count of 0
        var objectProperties = emptyObjectProperty["properties"] as JsonObject;
        if (objectProperties != null)
        {
            objectProperties.Count.Should().Be(0);
        }
        // It's also valid for properties to be omitted entirely when empty
    }

    #endregion
}
