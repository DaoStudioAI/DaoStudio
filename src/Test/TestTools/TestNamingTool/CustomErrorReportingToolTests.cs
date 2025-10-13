using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.Common.Plugins;
using FluentAssertions;
using NamingTool.Return;
using Xunit;

namespace TestNamingTool;

public class CustomErrorReportingToolTests
{
    [Fact]
    public async Task ReportError_WithValidData_ShouldSetErrorResult()
    {
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new() { Name = "error_message", Type = typeof(string), Description = "Error message", IsRequired = true },
            new() { Name = "details", Type = typeof(string), Description = "Details", IsRequired = false }
        };

        var tool = new CustomErrorReportingTool(tcs, 456L, parameters, "report_issue", "Report issue");
        var payload = new Dictionary<string, object?>
        {
            ["error_message"] = "Failure occurred",
            ["details"] = "Stack overflow"
        };

        var confirmation = await tool.ReportError(payload);

        confirmation.Should().Contain("Error reported to parent session");
        tcs.Task.IsCompleted.Should().BeTrue();
        var result = await tcs.Task;
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Failure occurred");
    }

    [Fact]
    public async Task ReportError_WithMissingRequiredParameter_ShouldReturnValidationMessage()
    {
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new() { Name = "error_message", Type = typeof(string), Description = "Error message", IsRequired = true }
        };

        var tool = new CustomErrorReportingTool(tcs, 789L, parameters);
        var payload = new Dictionary<string, object?>();

        var response = await tool.ReportError(payload);

        response.Should().Contain("Missing required parameters");
        tcs.Task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ReportError_AfterRepeatedValidationFailures_ShouldFaultTask()
    {
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new() { Name = "error_message", Type = typeof(string), Description = "Error message", IsRequired = true }
        };

        var tool = new CustomErrorReportingTool(tcs, 321L, parameters);
        var payload = new Dictionary<string, object?>();

        for (var i = 0; i < 5; i++)
        {
            var message = await tool.ReportError(payload);
            message.Should().Contain("Validation failed");
        }

        tcs.Task.IsFaulted.Should().BeTrue();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => tcs.Task);
        exception.Message.Should().Contain("Validation failed after 5 attempts");
    }

    [Fact]
    public async Task ReportError_WithTypeMismatch_ShouldReturnValidationMessage()
    {
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new() { Name = "code", Type = typeof(int), Description = "Error code", IsRequired = true }
        };

        var tool = new CustomErrorReportingTool(tcs, 654L, parameters);
        var payload = new Dictionary<string, object?>
        {
            ["code"] = new object()
        };

        var response = await tool.ReportError(payload);

        response.Should().Contain("Type validation errors");
        tcs.Task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ReportError_WithNullPayload_ShouldUseDefaults()
    {
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>();

        var tool = new CustomErrorReportingTool(tcs, 987L, parameters);

        var confirmation = await tool.ReportError(null!);

        confirmation.Should().Contain("Error reported to parent session");
        tcs.Task.IsCompleted.Should().BeTrue();
        var result = await tcs.Task;
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ReportError_WithAdditionalParameters_ShouldFilterToDefinedOnes()
    {
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new() { Name = "error_message", Type = typeof(string), Description = "Error message", IsRequired = true }
        };

        var tool = new CustomErrorReportingTool(tcs, 111L, parameters);
        var payload = new Dictionary<string, object?>
        {
            ["error_message"] = "Test",
            ["extra"] = "ignored"
        };

        await tool.ReportError(payload);

        var result = await tcs.Task;
        result.ErrorMessage.Should().Be("Test");
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithDuplicateNames_ShouldThrow()
    {
        var tcs = new TaskCompletionSource<ChildSessionResult>();
        var parameters = new List<CustomReturnParameter>
        {
            new() { Name = "error_message", Type = typeof(string), Description = "Error message", IsRequired = true },
            new() { Name = "error_message", Type = typeof(string), Description = "Duplicate", IsRequired = false }
        };

        var act = () => new CustomErrorReportingTool(tcs, 1L, parameters);

        act.Should().Throw<ArgumentException>().WithMessage("*Duplicate parameter names*");
    }
}
