using DaoStudio.Interfaces.Plugins;
using Moq;
using System.Text.Json;

namespace TestDaoStudio.Infrastructure;

/// <summary>
/// Factory for creating test plugins and plugin-related objects for testing purposes.
/// Provides pre-configured mock plugins with predictable behaviors.
/// Note: Updated to match actual IPluginTool interface which only has GetSessionFunctionsAsync and CloseSessionAsync methods.
/// </summary>
public static class TestPluginFactory
{
    /// <summary>
    /// Creates a basic mock plugin tool with standard configuration.
    /// </summary>
    public static IPluginTool CreateMockPluginTool(string name, string description, bool isEnabled = true)
    {
        var mockPlugin = new Mock<IPluginTool>();

        mockPlugin.Setup(p => p.GetSessionFunctionsAsync(
                    It.IsAny<List<FunctionWithDescription>>(),
                    It.IsAny<IHostPerson?>(),
                    It.IsAny<IHostSession?>()))
                  .Returns(Task.CompletedTask);

        mockPlugin.Setup(p => p.CloseSessionAsync(It.IsAny<IHostSession>()))
                  .ReturnsAsync((byte[]?)null);

        return mockPlugin.Object;
    }

    /// <summary>
    /// Creates a mock calculator plugin for mathematical operations.
    /// </summary>
    public static IPluginTool CreateCalculatorPlugin()
    {
        var mockPlugin = new Mock<IPluginTool>();

        // IPluginTool interface only has GetSessionFunctionsAsync and CloseSessionAsync methods

        // IPluginTool interface only has GetSessionFunctionsAsync and CloseSessionAsync methods
        mockPlugin.Setup(p => p.GetSessionFunctionsAsync(
                    It.IsAny<List<FunctionWithDescription>>(),
                    It.IsAny<IHostPerson?>(),
                    It.IsAny<IHostSession?>()))
                  .Returns(Task.CompletedTask);

        mockPlugin.Setup(p => p.CloseSessionAsync(It.IsAny<IHostSession>()))
                  .ReturnsAsync((byte[]?)null);

        // Calculator functionality would be implemented through GetSessionFunctionsAsync

        return mockPlugin.Object;
    }

    /// <summary>
    /// Creates a mock weather plugin for weather information.
    /// </summary>
    public static IPluginTool CreateWeatherPlugin()
    {
        var mockPlugin = new Mock<IPluginTool>();

        mockPlugin.Setup(p => p.GetSessionFunctionsAsync(
                    It.IsAny<List<FunctionWithDescription>>(),
                    It.IsAny<IHostPerson?>(),
                    It.IsAny<IHostSession?>()))
                  .Returns(Task.CompletedTask);

        mockPlugin.Setup(p => p.CloseSessionAsync(It.IsAny<IHostSession>()))
                  .ReturnsAsync((byte[]?)null);

        return mockPlugin.Object;
    }

    /// <summary>
    /// Creates a mock file operations plugin.
    /// </summary>
    public static IPluginTool CreateFilePlugin()
    {
        var mockPlugin = new Mock<IPluginTool>();

        mockPlugin.Setup(p => p.GetSessionFunctionsAsync(
                    It.IsAny<List<FunctionWithDescription>>(),
                    It.IsAny<IHostPerson?>(),
                    It.IsAny<IHostSession?>()))
                  .Returns(Task.CompletedTask);

        mockPlugin.Setup(p => p.CloseSessionAsync(It.IsAny<IHostSession>()))
                  .ReturnsAsync((byte[]?)null);

        return mockPlugin.Object;
    }

    /// <summary>
    /// Creates a mock plugin that throws exceptions for error testing.
    /// </summary>
    public static IPluginTool CreateErrorPlugin(Exception exceptionToThrow)
    {
        var mockPlugin = new Mock<IPluginTool>();

        mockPlugin.Setup(p => p.GetSessionFunctionsAsync(
                    It.IsAny<List<FunctionWithDescription>>(),
                    It.IsAny<IHostPerson?>(),
                    It.IsAny<IHostSession?>()))
                  .ThrowsAsync(exceptionToThrow);

        mockPlugin.Setup(p => p.CloseSessionAsync(It.IsAny<IHostSession>()))
                  .ThrowsAsync(exceptionToThrow);

        return mockPlugin.Object;
    }

    /// <summary>
    /// Creates a mock plugin with custom behavior setup.
    /// </summary>
    public static Mock<IPluginTool> CreateCustomMockPlugin(string name, string description)
    {
        var mockPlugin = new Mock<IPluginTool>();

        // IPluginTool interface only has GetSessionFunctionsAsync and CloseSessionAsync methods

        return mockPlugin;
    }

    /// <summary>
    /// Creates a mock plugin that simulates slow execution for performance testing.
    /// </summary>
    public static IPluginTool CreateSlowPlugin(int delayMs = 2000)
    {
        var mockPlugin = new Mock<IPluginTool>();

        mockPlugin.Setup(p => p.GetSessionFunctionsAsync(
                    It.IsAny<List<FunctionWithDescription>>(),
                    It.IsAny<IHostPerson?>(),
                    It.IsAny<IHostSession?>()))
                  .Returns(async (List<FunctionWithDescription> functions, IHostPerson? person, IHostSession? session) =>
                  {
                      await Task.Delay(delayMs);
                  });

        mockPlugin.Setup(p => p.CloseSessionAsync(It.IsAny<IHostSession>()))
                  .ReturnsAsync((byte[]?)null);

        return mockPlugin.Object;
    }

    /// <summary>
    /// Creates a collection of different mock plugins for comprehensive testing.
    /// </summary>
    public static Dictionary<string, IPluginTool> CreatePluginCollection()
    {
        return new Dictionary<string, IPluginTool>
        {
            { "Calculator", CreateCalculatorPlugin() },
            { "Weather", CreateWeatherPlugin() },
            { "FileOperations", CreateFilePlugin() },
            { "BasicPlugin", CreateMockPluginTool("BasicPlugin", "Basic test plugin") },
            { "DisabledPlugin", CreateMockPluginTool("DisabledPlugin", "Disabled test plugin", false) },
            { "SlowPlugin", CreateSlowPlugin(1000) }
        };
    }

    /// <summary>
    /// Creates a mock plugin with validation logic.
    /// </summary>
    public static IPluginTool CreateValidatingPlugin()
    {
        var mockPlugin = new Mock<IPluginTool>();

        mockPlugin.Setup(p => p.GetSessionFunctionsAsync(
                    It.IsAny<List<FunctionWithDescription>>(),
                    It.IsAny<IHostPerson?>(),
                    It.IsAny<IHostSession?>()))
                  .Returns(Task.CompletedTask);

        mockPlugin.Setup(p => p.CloseSessionAsync(It.IsAny<IHostSession>()))
                  .ReturnsAsync((byte[]?)null);

        return mockPlugin.Object;
    }

    /// <summary>
    /// Creates a mock plugin that supports cancellation.
    /// </summary>
    public static IPluginTool CreateCancellablePlugin()
    {
        var mockPlugin = new Mock<IPluginTool>();

        mockPlugin.Setup(p => p.GetSessionFunctionsAsync(
                    It.IsAny<List<FunctionWithDescription>>(),
                    It.IsAny<IHostPerson?>(),
                    It.IsAny<IHostSession?>()))
                  .Returns(Task.CompletedTask);

        mockPlugin.Setup(p => p.CloseSessionAsync(It.IsAny<IHostSession>()))
                  .ReturnsAsync((byte[]?)null);

        return mockPlugin.Object;
    }

    // Schema helper methods removed as they're not needed for the current IPluginTool interface
}
