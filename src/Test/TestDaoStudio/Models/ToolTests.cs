using DaoStudio.DBStorage.Models;
using DaoStudio.Interfaces;
using FluentAssertions;

namespace TestDaoStudio.Models;

/// <summary>
/// Unit tests for LlmTool class.
/// Tests property getters/setters, validation logic, and serialization.
/// </summary>
public class ToolTests
{

    [Fact]
    public void Id_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedId = 789L;

        // Act
        tool.Id = expectedId;

        // Assert
        tool.Id.Should().Be(expectedId);
    }

    [Fact]
    public void Name_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedName = "WeatherTool";

        // Act
        tool.Name = expectedName;

        // Assert
        tool.Name.Should().Be(expectedName);
    }

    [Fact]
    public void Description_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedDescription = "Get current weather information for a location";

        // Act
        tool.Description = expectedDescription;

        // Assert
        tool.Description.Should().Be(expectedDescription);
    }

    [Fact]
    public void StaticId_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedStaticId = "weather-tool-v1";

        // Act
        tool.StaticId = expectedStaticId;

        // Assert
        tool.StaticId.Should().Be(expectedStaticId);
    }

    [Fact]
    public void ToolConfig_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedConfig = "{\"api_key\": \"${API_KEY}\", \"base_url\": \"https://api.weather.com\"}";

        // Act
        tool.ToolConfig = expectedConfig;

        // Assert
        tool.ToolConfig.Should().Be(expectedConfig);
    }

    [Fact]
    public void Parameters_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedParameters = new Dictionary<string, string>
        {
            { "location", "required" },
            { "units", "metric" }
        };

        // Act
        tool.Parameters = expectedParameters;

        // Assert
        tool.Parameters.Should().BeEquivalentTo(expectedParameters);
    }

    [Fact]
    public void IsEnabled_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();

        // Act & Assert - Default value
        tool.IsEnabled.Should().BeTrue();

        // Act - Set to false
        tool.IsEnabled = false;

        // Assert
        tool.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void AppId_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedAppId = 123L;

        // Act
        tool.AppId = expectedAppId;

        // Assert
        tool.AppId.Should().Be(expectedAppId);
    }

    [Fact]
    public void ToolType_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
    var expectedToolType = (int)ToolType.Normal;

        // Act
        tool.ToolType = expectedToolType;

        // Assert
        tool.ToolType.Should().Be(expectedToolType);
    }

    [Fact]
    public void State_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedState = (int)ToolState.Stateful;

        // Act
        tool.State = expectedState;

        // Assert
        tool.State.Should().Be(expectedState);
    }

    [Fact]
    public void StateData_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedStateData = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        tool.StateData = expectedStateData;

        // Assert
        tool.StateData.Should().BeEquivalentTo(expectedStateData);
    }

    [Fact]
    public void DevMsg_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedDevMsg = "Development notes for this tool";

        // Act
        tool.DevMsg = expectedDevMsg;

        // Assert
        tool.DevMsg.Should().Be(expectedDevMsg);
    }

    [Fact]
    public void CreatedAt_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedCreatedAt = DateTime.UtcNow;

        // Act
        tool.CreatedAt = expectedCreatedAt;

        // Assert
        tool.CreatedAt.Should().Be(expectedCreatedAt);
    }

    [Fact]
    public void LastModified_PropertySetterAndGetter_WorkCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var expectedLastModified = DateTime.UtcNow;

        // Act
        tool.LastModified = expectedLastModified;

        // Assert
        tool.LastModified.Should().Be(expectedLastModified);
    }

    [Fact]
    public void LlmTool_WithCompleteData_InitializesCorrectly()
    {
        // Arrange & Act
        var tool = new LlmTool
        {
            Id = 1,
            Name = "CalculatorTool",
            Description = "Perform mathematical calculations",
            StaticId = "calc-tool-v2",
            ToolConfig = "{\"precision\": 10}",
            Parameters = new Dictionary<string, string>
            {
                { "expression", "required" },
                { "format", "decimal" }
            },
            IsEnabled = true,
            AppId = 100,
            ToolType = (int)ToolType.Normal,
            State = (int)ToolState.Stateless,
            StateData = null,
            DevMsg = "Basic calculator functionality",
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        // Assert
        tool.Id.Should().Be(1);
        tool.Name.Should().Be("CalculatorTool");
        tool.Description.Should().Be("Perform mathematical calculations");
        tool.StaticId.Should().Be("calc-tool-v2");
        tool.ToolConfig.Should().Contain("precision");
        tool.Parameters.Should().ContainKey("expression");
        tool.Parameters.Should().ContainKey("format");
        tool.IsEnabled.Should().BeTrue();
        tool.AppId.Should().Be(100);
        tool.ToolType.Should().Be((int)ToolType.Normal);
        tool.State.Should().Be((int)ToolState.Stateless);
    }

    [Fact]
    public void LlmTool_WithPluginType_InitializesCorrectly()
    {
        // Arrange & Act
        var tool = new LlmTool
        {
            Name = "FileManagerTool",
            Description = "Manage files and directories",
            ToolType = (int)ToolType.Normal,
            State = (int)ToolState.Stateful,
            StateData = new byte[] { 0x01, 0x02, 0x03 },
            Parameters = new Dictionary<string, string>
            {
                { "path", "required" },
                { "operation", "required" }
            }
        };

        // Assert
    tool.ToolType.Should().Be((int)ToolType.Normal);
        tool.State.Should().Be((int)ToolState.Stateful);
        tool.StateData.Should().NotBeNull();
        tool.StateData.Should().HaveCount(3);
    }

    [Fact]
    public void LlmTool_WithComplexConfiguration_HandlesCorrectly()
    {
        // Arrange & Act
        var complexConfig = @"{
            ""api_endpoint"": ""https://api.example.com/v1"",
            ""timeout"": 30000,
            ""retry_count"": 3,
            ""headers"": {
                ""Authorization"": ""Bearer ${TOKEN}"",
                ""Content-Type"": ""application/json""
            }
        }";

        var tool = new LlmTool
        {
            Name = "APITool",
            ToolConfig = complexConfig,
            Parameters = new Dictionary<string, string>
            {
                { "method", "GET" },
                { "endpoint", "required" },
                { "payload", "optional" }
            }
        };

        // Assert
        tool.ToolConfig.Should().Contain("api_endpoint");
        tool.ToolConfig.Should().Contain("Authorization");
        tool.ToolConfig.Should().Contain("Bearer ${TOKEN}");
        tool.Parameters.Should().ContainKey("method");
        tool.Parameters["method"].Should().Be("GET");
    }

    [Fact]
    public void LlmTool_WithStatefulData_HandlesCorrectly()
    {
        // Arrange
        var stateData = System.Text.Encoding.UTF8.GetBytes("{\"session_id\": \"abc123\", \"last_action\": \"query\"}");

        // Act
        var tool = new LlmTool
        {
            Name = "SessionTool",
            State = (int)ToolState.Stateful,
            StateData = stateData
        };

        // Assert
        tool.State.Should().Be((int)ToolState.Stateful);
        tool.StateData.Should().NotBeNull();
        
        var decodedState = System.Text.Encoding.UTF8.GetString(tool.StateData);
        decodedState.Should().Contain("session_id");
        decodedState.Should().Contain("abc123");
    }

    [Fact]
    public void LlmTool_TimestampProperties_HandleDateTimeCorrectly()
    {
        // Arrange
        var tool = new LlmTool();
        var now = DateTime.UtcNow;
        var earlier = now.AddHours(-2);

        // Act
        tool.CreatedAt = earlier;
        tool.LastModified = now;

        // Assert
        tool.CreatedAt.Should().Be(earlier);
        tool.LastModified.Should().Be(now);
        tool.LastModified.Should().BeAfter(tool.CreatedAt);
    }

    [Fact]
    public void LlmTool_NullableProperties_HandleNullCorrectly()
    {
        // Arrange & Act
        var tool = new LlmTool
        {
            Name = string.Empty,
            Description = string.Empty,
            StaticId = string.Empty,
            ToolConfig = string.Empty,
            Parameters = new Dictionary<string, string>(),
            StateData = (byte[]?)null,
            DevMsg = string.Empty
        };

        // Assert
        tool.Name.Should().BeEmpty();
        tool.Description.Should().BeEmpty();
        tool.StaticId.Should().BeEmpty();
        tool.ToolConfig.Should().BeEmpty();
        tool.Parameters.Should().NotBeNull();
        tool.StateData.Should().BeNull();
        tool.DevMsg.Should().BeEmpty();
    }

    [Fact]
    public void LlmTool_WithEmptyParameters_HandlesCorrectly()
    {
        // Arrange & Act
        var tool = new LlmTool
        {
            Name = "NoParamTool",
            Parameters = new Dictionary<string, string>()
        };

        // Assert
        tool.Parameters.Should().NotBeNull();
        tool.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void LlmTool_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange & Act
        var tool = new LlmTool
        {
            Name = "Special-Tool_v1.0",
            Description = "Tool with special chars: !@#$%^&*()",
            StaticId = "special-tool-id_123",
            DevMsg = "Development message with 'quotes' and \"double quotes\""
        };

        // Assert
        tool.Name.Should().Contain("-");
        tool.Name.Should().Contain("_");
        tool.Description.Should().Contain("!@#$%^&*()");
        tool.DevMsg.Should().Contain("'quotes'");
        tool.DevMsg.Should().Contain("\"double quotes\"");
    }

    [Fact]
    public void LlmTool_WithLargeStateData_HandlesCorrectly()
    {
        // Arrange
        var largeStateData = new byte[1024]; // 1KB of data
        for (int i = 0; i < largeStateData.Length; i++)
        {
            largeStateData[i] = (byte)(i % 256);
        }

        // Act
        var tool = new LlmTool
        {
            Name = "LargeStateTool",
            State = (int)ToolState.Stateful,
            StateData = largeStateData
        };

        // Assert
        tool.StateData.Should().HaveCount(1024);
        tool.StateData[0].Should().Be(0);
        tool.StateData[255].Should().Be(255);
        tool.StateData[256].Should().Be(0); // Wrapped around
    }

    [Fact]
    public void LlmTool_MultipleInstances_AreIndependent()
    {
        // Arrange & Act
        var tool1 = new LlmTool { Name = "Tool1", IsEnabled = true };
        var tool2 = new LlmTool { Name = "Tool2", IsEnabled = false };

        // Modify one
        tool1.IsEnabled = false;

        // Assert
        tool1.Name.Should().Be("Tool1");
        tool1.IsEnabled.Should().BeFalse();
        tool2.Name.Should().Be("Tool2");
        tool2.IsEnabled.Should().BeFalse(); // Should remain unchanged
    }

    [Fact]
    public void LlmTool_WithDifferentToolTypes_HandlesCorrectly()
    {
        // Arrange & Act
        var normalTool = new LlmTool { ToolType = (int)ToolType.Normal };
    var pluginTool = new LlmTool { ToolType = (int)ToolType.Normal };

        // Assert
        normalTool.ToolType.Should().Be((int)ToolType.Normal);
    pluginTool.ToolType.Should().Be((int)ToolType.Normal);
    }

    [Fact]
    public void LlmTool_WithDifferentStates_HandlesCorrectly()
    {
        // Arrange & Act
        var statelessTool = new LlmTool { State = (int)ToolState.Stateless };
        var statefulTool = new LlmTool { State = (int)ToolState.Stateful };

        // Assert
        statelessTool.State.Should().Be((int)ToolState.Stateless);
        statefulTool.State.Should().Be((int)ToolState.Stateful);
    }
}
