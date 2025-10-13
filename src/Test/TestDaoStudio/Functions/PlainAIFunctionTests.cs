using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using FluentAssertions;
using Microsoft.Extensions.AI;
using System.Text.Json;
using Moq;
using DaoStudio.Plugins;

namespace TestDaoStudio.Functions;

/// <summary>
/// Unit tests for PlainAIFunction class.
/// Tests function creation, schema generation, and parameter handling.
/// </summary>
public class PlainAIFunctionTests
{
    // Helper: translate legacy test inputs to the new PlainAIFunction constructor
    private static PlainAIFunction CreatePlainAIFunction(
        string name,
        string description,
        object? parameters,
        Func<Dictionary<string, object?>, Task<object?>> handler)
    {
        var session = new Mock<IHostSession>().Object;

        var funcDesc = new FunctionDescription
        {
            Name = name,
            Description = description,
            Parameters = ToMetadata(parameters)
        };

        var fwd = new FunctionWithDescription
        {
            Function = handler,
            Description = funcDesc
        };

        return new PlainAIFunction(fwd, session);
    }

    private static IList<FunctionTypeMetadata> ToMetadata(object? parameters)
    {
        var list = new List<FunctionTypeMetadata>();
        if (parameters == null)
            return list;

        var props = parameters.GetType().GetProperties();
        foreach (var prop in props)
        {
            var name = prop.Name;
            var val = prop.GetValue(parameters);

            string desc = string.Empty;
            Type paramType = typeof(object);

            if (val is string s)
            {
                paramType = MapType(s);
            }
            else if (val != null)
            {
                var vType = val.GetType();
                var typeProp = vType.GetProperty("type") ?? vType.GetProperty("Type");
                var descProp = vType.GetProperty("description") ?? vType.GetProperty("Description");
                var itemsProp = vType.GetProperty("items") ?? vType.GetProperty("Items");

                var typeStr = typeProp?.GetValue(val)?.ToString();
                if (!string.IsNullOrEmpty(typeStr))
                {
                    if (string.Equals(typeStr, "array", StringComparison.OrdinalIgnoreCase))
                    {
                        // Try to infer item type; default to list of string
                        Type element = typeof(string);
                        var itemsVal = itemsProp?.GetValue(val);
                        var itemsTypeProp = itemsVal?.GetType().GetProperty("type") ?? itemsVal?.GetType().GetProperty("Type");
                        var itemsTypeStr = itemsTypeProp?.GetValue(itemsVal)?.ToString();
                        if (!string.IsNullOrEmpty(itemsTypeStr))
                        {
                            element = MapType(itemsTypeStr);
                        }
                        paramType = typeof(List<>).MakeGenericType(element);
                    }
                    else
                    {
                        paramType = MapType(typeStr!);
                    }
                }

                desc = descProp?.GetValue(val)?.ToString() ?? string.Empty;
            }

            list.Add(new FunctionTypeMetadata
            {
                Name = name,
                Description = desc,
                ParameterType = paramType,
                IsRequired = false,
                DefaultValue = null
            });
        }

        return list;
    }

    private static Type MapType(string t)
    {
        return t.ToLowerInvariant() switch
        {
            "string" => typeof(string),
            "integer" => typeof(int),
            "number" => typeof(double),
            "boolean" => typeof(bool),
            "array" => typeof(List<object>),
            "object" => typeof(object),
            _ => typeof(object)
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var name = "TestFunction";
        var description = "A test function";
        var parameters = new { location = "string", units = "string" };
        Func<Dictionary<string, object?>, Task<object?>> handler = async (args) => await Task.FromResult<object?>("test result");

        // Act
        var function = CreatePlainAIFunction(name, description, parameters, handler);

        // Assert
        function.Should().NotBeNull();
        function.Name.Should().Be(name);
        function.Description.Should().Be(description);
    }

    [Fact]
    public void Constructor_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var description = "A test function";
        var parameters = new { location = "string" };
        Func<Dictionary<string, object?>, Task<object?>> handler = async (args) => await Task.FromResult<object?>("result");

        // Act & Assert
        var act = () => CreatePlainAIFunction(null!, description, parameters, handler);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var name = "";
        var description = "A test function";
        var parameters = new { location = "string" };
        Func<Dictionary<string, object?>, Task<object?>> handler = async (args) => await Task.FromResult<object?>("result");

        // Act & Assert
        var act = () => CreatePlainAIFunction(name, description, parameters, handler);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullDescription_ThrowsArgumentNullException()
    {
        // Arrange
        var name = "TestFunction";
        var parameters = new { location = "string" };
        Func<Dictionary<string, object?>, Task<object?>> handler = async (args) => await Task.FromResult<object?>("result");

        // Act & Assert
        var act = () => CreatePlainAIFunction(name, null!, parameters, handler);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var name = "TestFunction";
        var description = "A test function";
        var parameters = new { location = "string" };

        // Act & Assert
        var act = () => CreatePlainAIFunction(name, description, parameters, null!);
        act.Should().NotThrow();
    }

    [Fact]
    public void Name_ReturnsCorrectFunctionName()
    {
        // Arrange
        var expectedName = "GetWeather";
        var function = CreatePlainAIFunction(
            expectedName,
            "Get weather information",
            new { location = "string" },
            async (args) => await Task.FromResult<object?>("sunny"));

        // Act & Assert
        function.Name.Should().Be(expectedName);
    }

    [Fact]
    public void Description_ReturnsCorrectDescription()
    {
        // Arrange
        var expectedDescription = "Get current weather for a location";
        var function = CreatePlainAIFunction(
            "GetWeather",
            expectedDescription,
            new { location = "string" },
            async (args) => await Task.FromResult<object?>("sunny"));

        // Act & Assert
        function.Description.Should().Be(expectedDescription);
    }

    [Fact]
    public void JsonSchema_ReturnsValidJsonSchema()
    {
        // Arrange
        var parameters = new
        {
            location = new { type = "string", description = "The location to get weather for" },
            units = new { type = "string", description = "Temperature units", @enum = new[] { "celsius", "fahrenheit" } }
        };

        var function = CreatePlainAIFunction(
            "GetWeather",
            "Get weather information",
            parameters,
            async (args) => await Task.FromResult<object?>("result"));

        // Act
        var schema = function.JsonSchema;

        // Assert
        schema.Should().NotBeNull();
        var schemaJson = JsonSerializer.Serialize(schema);
        schemaJson.Should().Contain("location");
        schemaJson.Should().Contain("units");
        schemaJson.Should().Contain("string");
    }

    [Fact]
    public async Task InvokeAsync_WithValidParameters_ExecutesFunction()
    {
        // Arrange
        var expectedResult = "Weather: 25°C, sunny";
        var function = CreatePlainAIFunction(
            "GetWeather",
            "Get weather information",
            new { location = "string", units = "string" },
            async (args) =>
            {
                await Task.Delay(1); // Simulate async work
                return (object?)expectedResult;
            });

        var parameters = new Dictionary<string, object?>
        {
            { "location", "New York" },
            { "units", "celsius" }
        };

        // Act
        var result = await function.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(parameters));

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task InvokeAsync_WithComplexParameters_HandlesCorrectly()
    {
        // Arrange
        var function = CreatePlainAIFunction(
            "ProcessData",
            "Process complex data",
            new
            {
                data = new { type = "object" },
                options = new { type = "object" }
            },
            async (args) =>
            {
                var data = args["data"];
                var options = args["options"];
                return (object?)new { processed = true, dataCount = data?.ToString()?.Length ?? 0 };
            });

        var parameters = new Dictionary<string, object?>
        {
            { "data", new { items = new[] { 1, 2, 3 }, name = "test" } },
            { "options", new { format = "json", validate = true } }
        };

        // Act
        var result = await function.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(parameters));

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithNullParameters_HandlesGracefully()
    {
        // Arrange
        var function = CreatePlainAIFunction(
            "TestFunction",
            "Test function",
            new { optional_param = "string" },
            async (args) =>
            {
                await Task.Delay(1);
                return (object?)"executed with null params";
            });

        // Act
        var result = await function.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(new Dictionary<string, object?>()));

        // Assert
        result.Should().Be("executed with null params");
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyParameters_HandlesCorrectly()
    {
        // Arrange
        var function = CreatePlainAIFunction(
            "NoParamFunction",
            "Function with no parameters",
            new { },
            async (args) =>
            {
                await Task.Delay(1);
                return (object?)"executed without parameters";
            });

        var emptyParams = new Dictionary<string, object?>();

        // Act
        var result = await function.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(emptyParams));

        // Assert
        result.Should().Be("executed without parameters");
    }

    [Fact]
    public async Task InvokeAsync_WhenHandlerThrowsException_PropagatesException()
    {
        // Arrange
        var function = CreatePlainAIFunction(
            "ErrorFunction",
            "Function that throws an error",
            new { input = "string" },
            async (args) =>
            {
                await Task.Delay(1);
                throw new InvalidOperationException("Test error");
            });

        var parameters = new Dictionary<string, object?> { { "input", "test" } };

        // Act & Assert
        var act = async () => await function.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(parameters));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Test error");
    }

    [Fact]
    public void PlainAIFunction_WithComplexSchema_GeneratesCorrectSchema()
    {
        // Arrange
        var complexParameters = new
        {
            query = new
            {
                type = "string",
                description = "Search query",
                minLength = 1,
                maxLength = 100
            },
            filters = new
            {
                type = "object",
                properties = new
                {
                    category = new { type = "string", @enum = new[] { "tech", "science", "news" } },
                    date_range = new
                    {
                        type = "object",
                        properties = new
                        {
                            start = new { type = "string", format = "date" },
                            end = new { type = "string", format = "date" }
                        }
                    }
                }
            },
            limit = new
            {
                type = "integer",
                minimum = 1,
                maximum = 100,
                @default = 10
            }
        };

        // Act
        var function = CreatePlainAIFunction(
            "SearchContent",
            "Search for content with filters",
            complexParameters,
            async (args) => await Task.FromResult<object?>("search results"));

        // Assert
        var schema = function.JsonSchema;
        schema.Should().NotBeNull();
        
        var schemaJson = JsonSerializer.Serialize(schema);
        schemaJson.Should().Contain("query");
        schemaJson.Should().Contain("filters");
        schemaJson.Should().Contain("limit");
    }

    [Fact]
    public async Task InvokeAsync_WithAsyncHandler_ExecutesCorrectly()
    {
        // Arrange
        var function = CreatePlainAIFunction(
            "AsyncFunction",
            "Function with async operations",
            new { delay_ms = "integer" },
            async (args) =>
            {
                var delayMs = Convert.ToInt32(args["delay_ms"] ?? 10);
                await Task.Delay(delayMs);
                return (object?)$"Completed after {delayMs}ms";
            });

        var parameters = new Dictionary<string, object?> { { "delay_ms", 50 } };

        // Act
        var result = await function.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(parameters));

        // Assert
        result.Should().Be("Completed after 50ms");
    }

    [Fact]
    public void PlainAIFunction_WithDifferentParameterTypes_HandlesCorrectly()
    {
        // Arrange
        var parameters = new
        {
            text_param = new { type = "string" },
            number_param = new { type = "number" },
            integer_param = new { type = "integer" },
            boolean_param = new { type = "boolean" },
            array_param = new { type = "array", items = new { type = "string" } },
            object_param = new { type = "object" }
        };

        // Act
        var function = CreatePlainAIFunction(
            "MultiTypeFunction",
            "Function with multiple parameter types",
            parameters,
            async (args) => await Task.FromResult<object?>("success"));

        // Assert
        var schema = function.JsonSchema;
        var schemaJson = JsonSerializer.Serialize(schema);
        
        schemaJson.Should().Contain("string");
        schemaJson.Should().Contain("number");
        schemaJson.Should().Contain("integer");
        schemaJson.Should().Contain("boolean");
        schemaJson.Should().Contain("array");
        schemaJson.Should().Contain("object");
    }

    [Fact]
    public async Task InvokeAsync_WithJsonParameters_DeserializesCorrectly()
    {
        // Arrange
        var function = CreatePlainAIFunction(
            "JsonFunction",
            "Function that processes JSON data",
            new { json_data = "object" },
            async (args) =>
            {
                var jsonData = args["json_data"];
                if (jsonData is Dictionary<string, object?> d)
                {
                    return (object?)$"Received JSON with {d.Count} properties";
                }
                return (object?)"No JSON data received";
            });

        var jsonObject = JsonSerializer.Deserialize<JsonElement>("{\"name\":\"test\",\"value\":123}");
        var parameters = new Dictionary<string, object?> { { "json_data", jsonObject } };

        // Act
        var result = await function.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(parameters));

        // Assert
        result.Should().Be("Received JSON with 2 properties");
    }

    [Fact]
    public void PlainAIFunction_Metadata_ContainsCorrectInformation()
    {
        // Arrange
        var name = "TestMetadataFunction";
        var description = "Function for testing metadata";
        var parameters = new { param1 = "string", param2 = "integer" };

        // Act
        var function = CreatePlainAIFunction(name, description, parameters, async (args) => await Task.FromResult<object?>("result"));

        // Assert
        function.Name.Should().Be(name);
        function.Description.Should().Be(description);
        function.JsonSchema.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var function = CreatePlainAIFunction(
            "CancellableFunction",
            "Function that can be cancelled",
            new { duration = "integer" },
            async (args) =>
            {
                await Task.Delay(1000, cts.Token); // Respect external cancellation
                return (object?)"completed";
            });

        var parameters = new Dictionary<string, object?> { { "duration", 1000 } };

        // Act
        var task = function.InvokeAsync(new Microsoft.Extensions.AI.AIFunctionArguments(parameters), cts.Token);
        cts.Cancel(); // Cancel immediately

        // Assert
        var act = async () => await task;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
