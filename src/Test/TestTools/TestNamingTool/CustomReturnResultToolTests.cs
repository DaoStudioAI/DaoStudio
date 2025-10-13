using Xunit;
using FluentAssertions;
using System.Text.Json;
using DaoStudio.Common.Plugins;
using NamingTool.Return;

namespace TestNamingTool;

public class CustomReturnResultToolTests
{
    [Fact]
    public void Constructor_WithValidParameters_Should_CreateTool()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new CustomReturnParameter { Name = "result", Type = typeof(string), Description = "Result value", IsRequired = true }
        };

        // Act
        var tool = new CustomReturnResultTool(tcs, 123L, parameters);

        // Assert
        tool.Should().NotBeNull();
        tool.ToolName.Should().Be("set_custom_result");
        tool.ToolDescription.Should().Be("Report back with the custom result after completion");
        tool.Parameters.Should().HaveCount(1);
        tool.Parameters.First().Name.Should().Be("result");
    }

    [Fact]
    public void Constructor_WithCustomToolName_Should_UseCustomName()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>();
        const string customName = "custom_complete";
        const string customDescription = "Custom completion tool";

        // Act
        var tool = new CustomReturnResultTool(tcs, 123L, parameters, customName, customDescription);

        // Assert
        tool.ToolName.Should().Be(customName);
        tool.ToolDescription.Should().Be(customDescription);
    }

    [Fact]
    public void Constructor_WithNullCompletionSource_Should_ThrowArgumentNullException()
    {
        // Arrange
        var parameters = new List<CustomReturnParameter>();

        // Act & Assert
        var act = () => new CustomReturnResultTool(null!, 123L, parameters);
        act.Should().Throw<ArgumentNullException>().WithParameterName("completionSource");
    }

    [Fact]
    public void Constructor_WithNullParameters_Should_ThrowArgumentNullException()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();

        // Act & Assert
        var act = () => new CustomReturnResultTool(tcs, 123L, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("parameters");
    }

    [Fact]
    public void Constructor_WithDuplicateParameterNames_Should_ThrowArgumentException()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new CustomReturnParameter { Name = "duplicate", Type = typeof(string), Description = "First", IsRequired = true },
            new CustomReturnParameter { Name = "duplicate", Type = typeof(int), Description = "Second", IsRequired = true }
        };

        // Act & Assert
        var act = () => new CustomReturnResultTool(tcs, 123L, parameters);
        act.Should().Throw<ArgumentException>().WithMessage("*Duplicate parameter names found: duplicate*");
    }

    [Fact]
    public async Task SetCustomResult_WithValidData_Should_CompleteTask()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new CustomReturnParameter { Name = "result", Type = typeof(string), Description = "Result value", IsRequired = true },
            new CustomReturnParameter { Name = "status", Type = typeof(string), Description = "Status", IsRequired = false }
        };

        var tool = new CustomReturnResultTool(tcs, 123L, parameters);
        var resultData = new Dictionary<string, object?>
        {
            ["result"] = "Success",
            ["status"] = "Completed"
        };

        // Act
        var message = await tool.SetCustomResult(resultData);

        // Assert
        message.Should().Contain("Custom result set and returned to parent session");
        message.Should().Contain("Session 123 will now close");
        tcs.Task.IsCompleted.Should().BeTrue();
        var completedResult = await tcs.Task;
        completedResult.Success.Should().BeTrue();
        completedResult.Result.Should().Contain("Success");
        completedResult.Result.Should().Contain("Completed");
    }

    [Fact]
    public async Task SetCustomResult_WithMissingRequiredParameter_Should_ReturnValidationError()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new CustomReturnParameter { Name = "required1", Type = typeof(string), Description = "Required param 1", IsRequired = true },
            new CustomReturnParameter { Name = "required2", Type = typeof(string), Description = "Required param 2", IsRequired = true }
        };

        var tool = new CustomReturnResultTool(tcs, 123L, parameters);
        var incompleteData = new Dictionary<string, object?>
        {
            ["required1"] = "Present"
            // Missing required2
        };

        // Act
        var message = await tool.SetCustomResult(incompleteData);

        // Assert
        message.Should().Contain("Validation failed");
        message.Should().Contain("Missing required parameters: required2");
        tcs.Task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task SetCustomResult_WithTypeValidationErrors_Should_ReturnTypeError()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new CustomReturnParameter { Name = "number", Type = typeof(int), Description = "Number value", IsRequired = true }
        };

        var tool = new CustomReturnResultTool(tcs, 123L, parameters);
        var invalidData = new Dictionary<string, object?>
        {
            ["number"] = new List<string>() // Complex type that can't convert to int
        };

        // Act
        var message = await tool.SetCustomResult(invalidData);

        // Assert
        message.Should().Contain("Validation failed");
        message.Should().Contain("Type validation errors");
        tcs.Task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task SetCustomResult_AfterMultipleValidationFailures_Should_SetException()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new CustomReturnParameter { Name = "required", Type = typeof(string), Description = "Required param", IsRequired = true }
        };

        var tool = new CustomReturnResultTool(tcs, 123L, parameters);
        var emptyData = new Dictionary<string, object?>();

        // Act - Call multiple times to exceed retry limit
        for (int i = 0; i < 5; i++)
        {
            await tool.SetCustomResult(emptyData);
        }

        var finalMessage = await tool.SetCustomResult(emptyData);

        // Assert
        finalMessage.Should().Contain("Session 123 will now close due to exceeded retry attempts");
        tcs.Task.IsCompleted.Should().BeTrue();
        tcs.Task.IsFaulted.Should().BeTrue();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => tcs.Task);
        exception.Message.Should().Contain("Validation failed after 5 attempts");
    }

    [Fact]
    public async Task SetCustomResult_CalledTwiceWithValidData_Should_ThrowOnSecondCall()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new CustomReturnParameter { Name = "result", Type = typeof(string), Description = "Result", IsRequired = true }
        };

        var tool = new CustomReturnResultTool(tcs, 123L, parameters);
        var validData = new Dictionary<string, object?> { ["result"] = "Success" };

        // Act
        await tool.SetCustomResult(validData); // First call should succeed

        // Assert
        var act = () => tool.SetCustomResult(validData); // Second call should fail
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*Failed to set custom result for session 123. Result was already set.*");
    }

    [Fact]
    public async Task SetCustomResult_WithNullData_Should_UseEmptyDictionary()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>(); // No required parameters

        var tool = new CustomReturnResultTool(tcs, 123L, parameters);

        // Act
        var message = await tool.SetCustomResult(null!);

        // Assert
        message.Should().Contain("Custom result set and returned to parent session");
        tcs.Task.IsCompleted.Should().BeTrue();
        var result = await tcs.Task;
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SetCustomResult_WithExtraParameters_Should_FilterToDefinedOnly()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new CustomReturnParameter { Name = "allowed", Type = typeof(string), Description = "Allowed param", IsRequired = true }
        };

        var tool = new CustomReturnResultTool(tcs, 123L, parameters);
        var dataWithExtras = new Dictionary<string, object?>
        {
            ["allowed"] = "Valid value",
            ["extra"] = "Should be filtered out",
            ["another_extra"] = 123
        };

        // Act
        var message = await tool.SetCustomResult(dataWithExtras);

        // Assert
        message.Should().Contain("Custom result set and returned to parent session");
        tcs.Task.IsCompleted.Should().BeTrue();
        var result = await tcs.Task;
        result.Success.Should().BeTrue();
        result.Result.Should().Contain("allowed");
        result.Result.Should().Contain("Valid value");
        result.Result.Should().NotContain("extra");
        result.Result.Should().NotContain("another_extra");
    }

    [Fact]
    public async Task SetCustomResult_WithStringToNumericConversion_Should_Succeed()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new CustomReturnParameter { Name = "count", Type = typeof(int), Description = "Count value", IsRequired = true }
        };

        var tool = new CustomReturnResultTool(tcs, 123L, parameters);
        var convertibleData = new Dictionary<string, object?>
        {
            ["count"] = "42" // String that can convert to int
        };

        // Act
        var message = await tool.SetCustomResult(convertibleData);

        // Assert
        message.Should().Contain("Custom result set and returned to parent session");
        tcs.Task.IsCompleted.Should().BeTrue();
        var result = await tcs.Task;
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SetCustomResult_WithNullValueForNullableType_Should_Succeed()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new CustomReturnParameter { Name = "optional_value", Type = typeof(int?), Description = "Optional value", IsRequired = false }
        };

        var tool = new CustomReturnResultTool(tcs, 123L, parameters);
        var nullData = new Dictionary<string, object?>
        {
            ["optional_value"] = null
        };

        // Act
        var message = await tool.SetCustomResult(nullData);

        // Assert
        message.Should().Contain("Custom result set and returned to parent session");
        tcs.Task.IsCompleted.Should().BeTrue();
        var result = await tcs.Task;
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Parameters_Should_ReturnReadOnlyList()
    {
        // Arrange
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new CustomReturnParameter { Name = "param1", Type = typeof(string), Description = "First param", IsRequired = true }
        };

        var tool = new CustomReturnResultTool(tcs, 123L, parameters);

        // Act
        var readOnlyParams = tool.Parameters;

        // Assert
        readOnlyParams.Should().HaveCount(1);
        readOnlyParams.Should().BeAssignableTo<IReadOnlyList<CustomReturnParameter>>();
        readOnlyParams.First().Name.Should().Be("param1");
    }
}

public class CustomReturnParameterTests
{
    [Fact]
    public void CustomReturnParameter_WithRequiredProperties_Should_CreateSuccessfully()
    {
        // Act
        var parameter = new CustomReturnParameter
        {
            Name = "test_param",
            Type = typeof(string),
            Description = "Test parameter"
        };

        // Assert
        parameter.Name.Should().Be("test_param");
        parameter.Type.Should().Be(typeof(string));
        parameter.Description.Should().Be("Test parameter");
        parameter.IsRequired.Should().BeTrue(); // Default value
    }

    [Fact]
    public void CustomReturnParameter_WithIsRequiredSetToFalse_Should_BeOptional()
    {
        // Act
        var parameter = new CustomReturnParameter
        {
            Name = "optional_param",
            Type = typeof(int),
            Description = "Optional parameter",
            IsRequired = false
        };

        // Assert
        parameter.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void CustomReturnParameter_WithComplexType_Should_AcceptAnyType()
    {
        // Act
        var parameter = new CustomReturnParameter
        {
            Name = "complex_param",
            Type = typeof(List<Dictionary<string, object>>),
            Description = "Complex type parameter"
        };

        // Assert
        parameter.Type.Should().Be(typeof(List<Dictionary<string, object>>));
    }
}
