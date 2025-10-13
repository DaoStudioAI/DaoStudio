using System.Text.Json;
using System.Text.Json.Nodes;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Plugins;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace TestDaoStudio.Plugins;

/// <summary>
/// Comprehensive tests for PlainAIFunction class, focusing on JSON schema generation,
/// parameter handling, nested types support, and function invocation.
/// </summary>
public class PlainAIFunctionTests
{
    private readonly Mock<IHostSession> _mockSession;

    public PlainAIFunctionTests()
    {
        _mockSession = new Mock<IHostSession>();
        _mockSession.Setup(s => s.Id).Returns(123);
    }

    #region Helper Methods

    private PlainAIFunction CreateFunction(
        string functionName,
        string description,
        List<FunctionTypeMetadata> parameters,
        Func<Dictionary<string, object?>, Task<object?>> implementation,
        FunctionTypeMetadata? returnParameter = null,
        bool strictMode = false)
    {
        var functionWithDescription = new FunctionWithDescription
        {
            Function = (Dictionary<string, object?> args) => implementation(args),
            Description = new FunctionDescription
            {
                Name = functionName,
                Description = description,
                Parameters = parameters,
                ReturnParameter = returnParameter,
                StrictMode = strictMode
            }
        };

        return new PlainAIFunction(functionWithDescription, _mockSession.Object);
    }

    private PlainAIFunction CreateSimpleFunction(
        string name,
        string description,
        List<FunctionTypeMetadata> parameters)
    {
        return CreateFunction(
            name,
            description,
            parameters,
            async args => await Task.FromResult<object?>("test result"));
    }

    #endregion

    #region Basic Schema Generation Tests

    [Fact]
    public void JsonSchema_SimpleStringParameter_GeneratesCorrectSchema()
    {
        // Arrange
        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "message",
                Description = "A simple string message",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null
            }
        };

        var function = CreateSimpleFunction("sendMessage", "Sends a message", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        schema.ValueKind.Should().Be(JsonValueKind.Object);
        
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        Assert.NotNull(schemaObject);
        schemaObject["type"]!.GetValue<string>().Should().Be("object");
        
        var properties = schemaObject["properties"] as JsonObject;
        Assert.NotNull(properties);
        Assert.True(properties.ContainsKey("message"));
        
        var messageProperty = properties["message"] as JsonObject;
        Assert.NotNull(messageProperty);
        messageProperty["type"]!.GetValue<string>().Should().Be("string");
        messageProperty["description"]!.GetValue<string>().Should().Be("A simple string message");
        
        var required = schemaObject["required"] as JsonArray;
        Assert.NotNull(required);
        required.Should().Contain(n => n!.GetValue<string>() == "message");
    }

    [Fact]
    public void JsonSchema_MultipleSimpleParameters_GeneratesCorrectSchema()
    {
        // Arrange
        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "name",
                Description = "User name",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null
            },
            new FunctionTypeMetadata
            {
                Name = "age",
                Description = "User age",
                ParameterType = typeof(int),
                IsRequired = true,
                DefaultValue = null
            },
            new FunctionTypeMetadata
            {
                Name = "email",
                Description = "User email",
                ParameterType = typeof(string),
                IsRequired = false,
                DefaultValue = null
            }
        };

        var function = CreateSimpleFunction("createUser", "Creates a user", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        
        Assert.True(properties!.ContainsKey("name"));
        Assert.True(properties.ContainsKey("age"));
        Assert.True(properties.ContainsKey("email"));
        
        var required = schemaObject["required"] as JsonArray;
        required.Should().Contain(n => n!.GetValue<string>() == "name");
        required.Should().Contain(n => n!.GetValue<string>() == "age");
        required.Should().NotContain(n => n!.GetValue<string>() == "email");
    }

    [Fact]
    public void JsonSchema_StrictMode_IncludesStrictAndAdditionalProperties()
    {
        // Arrange
        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "field",
                Description = "A field",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null
            }
        };

        var function = CreateFunction(
            "strictFunction",
            "A strict function",
            parameters,
            async args => await Task.FromResult<object?>("result"),
            strictMode: true);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        schemaObject!["strict"]!.GetValue<bool>().Should().BeTrue();
        schemaObject["additionalProperties"]!.GetValue<bool>().Should().BeFalse();
    }

    #endregion

    #region Numeric and Boolean Type Tests

    [Fact]
    public void JsonSchema_NumericTypes_GenerateCorrectSchemas()
    {
        // Arrange
        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "intValue",
                Description = "Integer value",
                ParameterType = typeof(int),
                IsRequired = true,
                DefaultValue = null
            },
            new FunctionTypeMetadata
            {
                Name = "longValue",
                Description = "Long value",
                ParameterType = typeof(long),
                IsRequired = true,
                DefaultValue = null
            },
            new FunctionTypeMetadata
            {
                Name = "doubleValue",
                Description = "Double value",
                ParameterType = typeof(double),
                IsRequired = true,
                DefaultValue = null
            },
            new FunctionTypeMetadata
            {
                Name = "decimalValue",
                Description = "Decimal value",
                ParameterType = typeof(decimal),
                IsRequired = true,
                DefaultValue = null
            }
        };

        var function = CreateSimpleFunction("processNumbers", "Processes numeric values", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        
        (properties!["intValue"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("integer");
        (properties["longValue"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("integer");
        (properties["doubleValue"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("number");
        (properties["decimalValue"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("number");
    }

    [Fact]
    public void JsonSchema_BooleanType_GeneratesCorrectSchema()
    {
        // Arrange
        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "isActive",
                Description = "Whether the item is active",
                ParameterType = typeof(bool),
                IsRequired = true,
                DefaultValue = null
            }
        };

        var function = CreateSimpleFunction("toggleStatus", "Toggles status", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        
        (properties!["isActive"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("boolean");
    }

    #endregion

    #region Nullable Type Tests

    [Fact]
    public void JsonSchema_NullableTypes_GeneratesUnionTypeSchema()
    {
        // Arrange
        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "optionalAge",
                Description = "Optional age",
                ParameterType = typeof(int?),
                IsRequired = false,
                DefaultValue = null
            }
        };

        var function = CreateSimpleFunction("updateAge", "Updates age", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var optionalAgeProperty = properties!["optionalAge"] as JsonObject;
        
        var typeArray = optionalAgeProperty!["type"] as JsonArray;
        typeArray.Should().NotBeNull();
        typeArray.Should().HaveCount(2);
        typeArray.Should().Contain(n => n!.GetValue<string>() == "integer");
        typeArray.Should().Contain(n => n!.GetValue<string>() == "null");
    }

    #endregion

    #region Enum Type Tests

    [Fact]
    public void JsonSchema_EnumType_GeneratesEnumSchema()
    {
        // Arrange
        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "priority",
                Description = "Task priority",
                ParameterType = typeof(TaskPriority),
                IsRequired = true,
                DefaultValue = null
            }
        };

        var function = CreateSimpleFunction("createTask", "Creates a task", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var priorityProperty = properties!["priority"] as JsonObject;
        
        priorityProperty!["type"]!.GetValue<string>().Should().Be("string");
        
        var enumArray = priorityProperty["enum"] as JsonArray;
        enumArray.Should().NotBeNull();
        enumArray.Should().Contain(n => n!.GetValue<string>() == "Low");
        enumArray.Should().Contain(n => n!.GetValue<string>() == "Medium");
        enumArray.Should().Contain(n => n!.GetValue<string>() == "High");
    }

    [Fact]
    public void JsonSchema_EnumTypeFromMetadata_UsesProvidedEnumValues()
    {
        // Arrange
        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "status",
                Description = "Status value",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null,
                EnumValues = new List<string> { "Active", "Inactive", "Pending" }
            }
        };

        var function = CreateSimpleFunction("setStatus", "Sets status", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var statusProperty = properties!["status"] as JsonObject;
        
        var enumArray = statusProperty!["enum"] as JsonArray;
        enumArray.Should().NotBeNull();
        enumArray.Should().HaveCount(3);
        enumArray.Should().Contain(n => n!.GetValue<string>() == "Active");
        enumArray.Should().Contain(n => n!.GetValue<string>() == "Inactive");
        enumArray.Should().Contain(n => n!.GetValue<string>() == "Pending");
    }

    #endregion

    #region Array Type Tests

    [Fact]
    public void JsonSchema_SimpleArrayType_GeneratesCorrectSchema()
    {
        // Arrange
        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "tags",
                Description = "List of tags",
                ParameterType = typeof(string[]),
                IsRequired = true,
                DefaultValue = null
            }
        };

        var function = CreateSimpleFunction("addTags", "Adds tags", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var tagsProperty = properties!["tags"] as JsonObject;
        
        tagsProperty!["type"]!.GetValue<string>().Should().Be("array");
        
        var items = tagsProperty["items"] as JsonObject;
        Assert.NotNull(items);
        items["type"]!.GetValue<string>().Should().Be("string");
    }

    [Fact]
    public void JsonSchema_ArrayWithMetadata_UsesArrayElementMetadata()
    {
        // Arrange
        var elementMetadata = new FunctionTypeMetadata
        {
            Name = "score",
            Description = "Individual score",
            ParameterType = typeof(double),
            IsRequired = true,
            DefaultValue = null
        };

        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "scores",
                Description = "List of scores",
                ParameterType = typeof(double[]),
                IsRequired = true,
                DefaultValue = null,
                ArrayElementMetadata = elementMetadata
            }
        };

        var function = CreateSimpleFunction("processScores", "Processes scores", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var scoresProperty = properties!["scores"] as JsonObject;
        
        scoresProperty!["type"]!.GetValue<string>().Should().Be("array");
        
        var items = scoresProperty["items"] as JsonObject;
        items!["type"]!.GetValue<string>().Should().Be("number");
        items["description"]!.GetValue<string>().Should().Be("Individual score");
    }

    #endregion

    #region Object Type Tests

    [Fact]
    public void JsonSchema_ObjectWithProperties_GeneratesCorrectNestedSchema()
    {
        // Arrange
        var objectProperties = new Dictionary<string, FunctionTypeMetadata>
        {
            ["name"] = new FunctionTypeMetadata
            {
                Name = "name",
                Description = "User name",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null
            },
            ["age"] = new FunctionTypeMetadata
            {
                Name = "age",
                Description = "User age",
                ParameterType = typeof(int),
                IsRequired = false,
                DefaultValue = null
            }
        };

        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "user",
                Description = "User object",
                ParameterType = typeof(object),
                IsRequired = true,
                DefaultValue = null,
                ObjectProperties = objectProperties
            }
        };

        var function = CreateSimpleFunction("saveUser", "Saves a user", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var userProperty = properties!["user"] as JsonObject;
        
        userProperty!["type"]!.GetValue<string>().Should().Be("object");
        
        var userProperties = userProperty["properties"] as JsonObject;
        Assert.True(userProperties!.ContainsKey("name"));
        Assert.True(userProperties.ContainsKey("age"));
        
        (userProperties!["name"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("string");
        (userProperties["age"] as JsonObject)!["type"]!.GetValue<string>().Should().Be("integer");
        
        var requiredProps = userProperty["required"] as JsonArray;
        requiredProps.Should().Contain(n => n!.GetValue<string>() == "name");
        requiredProps.Should().NotContain(n => n!.GetValue<string>() == "age");
    }

    [Fact]
    public void JsonSchema_DeeplyNestedObject_GeneratesCorrectSchema()
    {
        // Arrange
        var addressProperties = new Dictionary<string, FunctionTypeMetadata>
        {
            ["street"] = new FunctionTypeMetadata
            {
                Name = "street",
                Description = "Street address",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null
            },
            ["city"] = new FunctionTypeMetadata
            {
                Name = "city",
                Description = "City",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null
            }
        };

        var userProperties = new Dictionary<string, FunctionTypeMetadata>
        {
            ["name"] = new FunctionTypeMetadata
            {
                Name = "name",
                Description = "User name",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null
            },
            ["address"] = new FunctionTypeMetadata
            {
                Name = "address",
                Description = "User address",
                ParameterType = typeof(object),
                IsRequired = true,
                DefaultValue = null,
                ObjectProperties = addressProperties
            }
        };

        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "user",
                Description = "User with address",
                ParameterType = typeof(object),
                IsRequired = true,
                DefaultValue = null,
                ObjectProperties = userProperties
            }
        };

        var function = CreateSimpleFunction("registerUser", "Registers a user", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var userProperty = properties!["user"] as JsonObject;
        var userProps = userProperty!["properties"] as JsonObject;
        var addressProperty = userProps!["address"] as JsonObject;
        
        addressProperty!["type"]!.GetValue<string>().Should().Be("object");
        
        var addressProps = addressProperty["properties"] as JsonObject;
        Assert.True(addressProps!.ContainsKey("street"));
        Assert.True(addressProps.ContainsKey("city"));
    }

    #endregion

    #region Array of Objects Tests

    [Fact]
    public void JsonSchema_ArrayOfObjects_GeneratesCorrectSchema()
    {
        // Arrange
        var itemProperties = new Dictionary<string, FunctionTypeMetadata>
        {
            ["id"] = new FunctionTypeMetadata
            {
                Name = "id",
                Description = "Item ID",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null
            },
            ["quantity"] = new FunctionTypeMetadata
            {
                Name = "quantity",
                Description = "Quantity",
                ParameterType = typeof(int),
                IsRequired = true,
                DefaultValue = null
            }
        };

        var itemMetadata = new FunctionTypeMetadata
        {
            Name = "item",
            Description = "Individual item",
            ParameterType = typeof(object),
            IsRequired = true,
            DefaultValue = null,
            ObjectProperties = itemProperties
        };

        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "items",
                Description = "List of items",
                ParameterType = typeof(object[]),
                IsRequired = true,
                DefaultValue = null,
                ArrayElementMetadata = itemMetadata
            }
        };

        var function = CreateSimpleFunction("processItems", "Processes items", parameters);

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        var properties = schemaObject!["properties"] as JsonObject;
        var itemsProperty = properties!["items"] as JsonObject;
        
        itemsProperty!["type"]!.GetValue<string>().Should().Be("array");
        
        var itemsSchema = itemsProperty["items"] as JsonObject;
        itemsSchema!["type"]!.GetValue<string>().Should().Be("object");
        
        var itemProps = itemsSchema["properties"] as JsonObject;
        Assert.True(itemProps!.ContainsKey("id"));
        Assert.True(itemProps.ContainsKey("quantity"));
    }

    #endregion

    #region Return Type Schema Tests

    [Fact]
    public void ReturnJsonSchema_SimpleReturnType_GeneratesCorrectSchema()
    {
        // Arrange
        var returnParameter = new FunctionTypeMetadata
        {
            Name = "result",
            Description = "The result message",
            ParameterType = typeof(string),
            IsRequired = true,
            DefaultValue = null
        };

        var function = CreateFunction(
            "getMessage",
            "Gets a message",
            new List<FunctionTypeMetadata>(),
            async args => await Task.FromResult<object?>("Hello"),
            returnParameter: returnParameter);

        // Act
        var returnSchema = function.ReturnJsonSchema;

        // Assert
        returnSchema.Should().NotBeNull();
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(returnSchema!.Value.GetRawText());
        schemaObject!["type"]!.GetValue<string>().Should().Be("string");
        schemaObject["description"]!.GetValue<string>().Should().Be("The result message");
    }

    [Fact]
    public void ReturnJsonSchema_NullReturnParameter_ReturnsNull()
    {
        // Arrange
        var function = CreateFunction(
            "doSomething",
            "Does something",
            new List<FunctionTypeMetadata>(),
            async args => await Task.FromResult<object?>(null),
            returnParameter: null);

        // Act
        var returnSchema = function.ReturnJsonSchema;

        // Assert
        returnSchema.Should().BeNull();
    }

    [Fact]
    public void ReturnJsonSchema_ObjectReturnType_GeneratesCorrectSchema()
    {
        // Arrange
        var returnProperties = new Dictionary<string, FunctionTypeMetadata>
        {
            ["success"] = new FunctionTypeMetadata
            {
                Name = "success",
                Description = "Whether the operation succeeded",
                ParameterType = typeof(bool),
                IsRequired = true,
                DefaultValue = null
            },
            ["message"] = new FunctionTypeMetadata
            {
                Name = "message",
                Description = "Result message",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null
            }
        };

        var returnParameter = new FunctionTypeMetadata
        {
            Name = "result",
            Description = "Operation result",
            ParameterType = typeof(object),
            IsRequired = true,
            DefaultValue = null,
            ObjectProperties = returnProperties
        };

        var function = CreateFunction(
            "performOperation",
            "Performs an operation",
            new List<FunctionTypeMetadata>(),
            async args => await Task.FromResult<object?>(new { success = true, message = "Done" }),
            returnParameter: returnParameter);

        // Act
        var returnSchema = function.ReturnJsonSchema;

        // Assert
        returnSchema.Should().NotBeNull();
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(returnSchema!.Value.GetRawText());
        schemaObject!["type"]!.GetValue<string>().Should().Be("object");
        
        var properties = schemaObject["properties"] as JsonObject;
        Assert.True(properties!.ContainsKey("success"));
        Assert.True(properties.ContainsKey("message"));
    }

    #endregion

    #region Function Invocation Tests

    [Fact]
    public async Task InvokeCoreAsync_SimpleParameters_InvokesCorrectly()
    {
        // Arrange
        string? capturedName = null;
        int capturedAge = 0;

        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "name",
                Description = "Name",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null
            },
            new FunctionTypeMetadata
            {
                Name = "age",
                Description = "Age",
                ParameterType = typeof(int),
                IsRequired = true,
                DefaultValue = null
            }
        };

        var function = CreateFunction(
            "saveData",
            "Saves data",
            parameters,
            async args =>
            {
                capturedName = args["name"] as string;
                capturedAge = Convert.ToInt32(args["age"]);
                return await Task.FromResult<object?>("Saved");
            });

        var arguments = new AIFunctionArguments
        {
            { "name", "John" },
            { "age", 30 }
        };

        // Act
        var result = await function.InvokeAsync(arguments);

        // Assert
        capturedName.Should().Be("John");
        capturedAge.Should().Be(30);
        result.Should().Be("Saved");
    }

    [Fact]
    public async Task InvokeCoreAsync_WithJsonElement_DeserializesCorrectly()
    {
        // Arrange
        object? capturedValue = null;

        var parameters = new List<FunctionTypeMetadata>
        {
            new FunctionTypeMetadata
            {
                Name = "data",
                Description = "Data",
                ParameterType = typeof(string),
                IsRequired = true,
                DefaultValue = null
            }
        };

        var function = CreateFunction(
            "processData",
            "Processes data",
            parameters,
            async args =>
            {
                capturedValue = args["data"];
                return await Task.FromResult<object?>("Processed");
            });

        var jsonElement = JsonSerializer.SerializeToElement("test value");
        var arguments = new AIFunctionArguments
        {
            { "data", jsonElement }
        };

        // Act
        await function.InvokeAsync(arguments);

        // Assert
        capturedValue.Should().Be("test value");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void JsonSchema_EmptyParameters_GeneratesValidSchema()
    {
        // Arrange
        var function = CreateSimpleFunction("noParams", "Function with no parameters", new List<FunctionTypeMetadata>());

        // Act
        var schema = function.JsonSchema;

        // Assert
        var schemaObject = JsonSerializer.Deserialize<JsonObject>(schema.GetRawText());
        schemaObject!["type"]!.GetValue<string>().Should().Be("object");
        
        var properties = schemaObject["properties"] as JsonObject;
        Assert.NotNull(properties);
        properties.Count.Should().Be(0);
    }

    [Fact]
    public void Name_ReturnsCorrectFunctionName()
    {
        // Arrange
        var function = CreateSimpleFunction("testFunction", "Test description", new List<FunctionTypeMetadata>());

        // Act
        var name = function.Name;

        // Assert
        name.Should().Be("testFunction");
    }

    [Fact]
    public void Description_ReturnsCorrectDescription()
    {
        // Arrange
        var function = CreateSimpleFunction("testFunction", "This is a test function", new List<FunctionTypeMetadata>());

        // Act
        var description = function.Description;

        // Assert
        description.Should().Be("This is a test function");
    }

    #endregion
}

/// <summary>
/// Test enum for enum schema generation tests
/// </summary>
public enum TaskPriority
{
    Low,
    Medium,
    High
}
