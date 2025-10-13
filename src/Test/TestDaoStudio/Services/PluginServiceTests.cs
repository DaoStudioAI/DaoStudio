using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Mocks;

namespace TestDaoStudio.Services;

/// <summary>
/// Unit tests for PluginService class.
/// Tests plugin loading, initialization, and management functionality.
/// </summary>
public class PluginServiceTests : IDisposable
{
    private readonly Mock<ILogger<PluginService>> _mockLogger;
    private readonly Mock<IToolService> _mockToolService;
    private readonly Mock<IHost> _mockHost;
    private readonly PluginService _pluginService;

    public PluginServiceTests()
    {
        _mockLogger = new Mock<ILogger<PluginService>>();
        _mockToolService = new Mock<IToolService>();
        _mockHost = new Mock<IHost>();
        _pluginService = new PluginService(_mockLogger.Object, _mockToolService.Object, _mockHost.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act & Assert
        _pluginService.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new PluginService(null!, _mockToolService.Object, _mockHost.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullToolService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new PluginService(_mockLogger.Object, null!, _mockHost.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("toolService");
    }

    [Fact]
    public void Constructor_WithNullHost_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new PluginService(_mockLogger.Object, _mockToolService.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("DaoStudio");
    }

    [Fact]
    public async Task LoadPluginsAsync_WithValidPath_LoadsValidPlugins()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            await _pluginService.LoadPluginsAsync(tempDir);

            // Assert - Should not throw exceptions for valid empty directory
            _pluginService.Should().NotBeNull();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task LoadPluginsAsync_WithInvalidPath_HandlesGracefully()
    {
        // Arrange
        var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nonexistent");

        // Act & Assert
        var act = async () => await _pluginService.LoadPluginsAsync(invalidPath);
        
        // Should handle gracefully without throwing
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAsync_InitializesLoadedPlugins()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            await _pluginService.LoadPluginsAsync(tempDir);

            // Act
            await _pluginService.InitializeAsync();

            // Assert - Should complete without exceptions
            _pluginService.Should().NotBeNull();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void PluginFactories_ReturnsLoadedFactories()
    {
        // Act
        var factories = _pluginService.PluginFactories;

        // Assert
        factories.Should().BeNull(); // Initially null before loading
    }

    [Fact]
    public void Dispose_DisposesAllPlugins()
    {
        // Act & Assert - Should not throw
        var act = () => _pluginService.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task LoadPluginsAsync_WithNullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _pluginService.LoadPluginsAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task LoadPluginsAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _pluginService.LoadPluginsAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void PluginService_ImplementsIDisposable()
    {
        // Assert
        _pluginService.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public void PlugToolInfo_HasMultipleInstancesField_ExistsAndDefaultsToFalse()
    {
        // Arrange & Act
        var plugToolInfo = new PlugToolInfo();
        
        // Assert
        plugToolInfo.Should().NotBeNull();
        plugToolInfo.HasMultipleInstances.Should().BeFalse();
        
        // Test that we can set it to true
        plugToolInfo.HasMultipleInstances = true;
        plugToolInfo.HasMultipleInstances.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Act & Assert
        _pluginService.Dispose();
        var act = () => _pluginService.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task InitializeAsync_WithMultipleToolsWithSameStaticId_SetsHasMultipleInstancesCorrectly()
    {
        // Arrange
        var mockPluginFactory = new Mock<IPluginFactory>();
        var pluginInfo = new PluginInfo
        {
            StaticId = "TestPlugin",
            DisplayName = "Test Plugin",
            Version = "1.0"
        };
        mockPluginFactory.Setup(f => f.GetPluginInfo()).Returns(pluginInfo);
        mockPluginFactory.Setup(f => f.SetHost(It.IsAny<IHost>())).Returns(Task.CompletedTask);
        mockPluginFactory.Setup(f => f.CreatePluginToolAsync(It.IsAny<PlugToolInfo>()))
            .ReturnsAsync(new Mock<IPluginTool>().Object);

        // Create multiple tools with the same StaticId
        var tool1 = new MockTool { Id = 1, StaticId = "TestPlugin", IsEnabled = true, Name = "Tool1", Description = "First tool" };
        var tool2 = new MockTool { Id = 2, StaticId = "TestPlugin", IsEnabled = true, Name = "Tool2", Description = "Second tool" };
        var tool3 = new MockTool { Id = 3, StaticId = "DifferentPlugin", IsEnabled = true, Name = "Tool3", Description = "Different plugin tool" };

        var allTools = new List<ITool> { tool1, tool2, tool3 };

        _mockToolService.Setup(t => t.GetAllToolsAsync()).ReturnsAsync(allTools);

        // Set the plugin factories manually
        var pluginFactoriesProperty = typeof(PluginService).GetProperty("PluginFactories");
        pluginFactoriesProperty?.SetValue(_pluginService, new List<IPluginFactory> { mockPluginFactory.Object });

        // Act
        await _pluginService.InitializeAsync();

        // Assert - Verify that CreatePluginToolAsync was called with correct HasMultipleInstances values
        mockPluginFactory.Verify(f => f.CreatePluginToolAsync(It.Is<PlugToolInfo>(info => 
            info.InstanceId == 1 && info.HasMultipleInstances == true)), Times.Once);
        mockPluginFactory.Verify(f => f.CreatePluginToolAsync(It.Is<PlugToolInfo>(info => 
            info.InstanceId == 2 && info.HasMultipleInstances == true)), Times.Once);

        // Tool3 should not be called since we only have factory for "TestPlugin"
        mockPluginFactory.Verify(f => f.CreatePluginToolAsync(It.Is<PlugToolInfo>(info => 
            info.InstanceId == 3)), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_WithSingleToolInstance_SetsHasMultipleInstancesFalse()
    {
        // Arrange
        var mockPluginFactory = new Mock<IPluginFactory>();
        var pluginInfo = new PluginInfo
        {
            StaticId = "SinglePlugin",
            DisplayName = "Single Plugin",
            Version = "1.0"
        };
        mockPluginFactory.Setup(f => f.GetPluginInfo()).Returns(pluginInfo);
        mockPluginFactory.Setup(f => f.SetHost(It.IsAny<IHost>())).Returns(Task.CompletedTask);
        mockPluginFactory.Setup(f => f.CreatePluginToolAsync(It.IsAny<PlugToolInfo>()))
            .ReturnsAsync(new Mock<IPluginTool>().Object);

        // Create single tool
        var tool1 = new MockTool { Id = 1, StaticId = "SinglePlugin", IsEnabled = true, Name = "SingleTool", Description = "Only tool" };
        var allTools = new List<ITool> { tool1 };

        _mockToolService.Setup(t => t.GetAllToolsAsync()).ReturnsAsync(allTools);

        // Set the plugin factories manually
        var pluginFactoriesProperty = typeof(PluginService).GetProperty("PluginFactories");
        pluginFactoriesProperty?.SetValue(_pluginService, new List<IPluginFactory> { mockPluginFactory.Object });

        // Act
        await _pluginService.InitializeAsync();

        // Assert - Verify that CreatePluginToolAsync was called with HasMultipleInstances = false
        mockPluginFactory.Verify(f => f.CreatePluginToolAsync(It.Is<PlugToolInfo>(info => 
            info.InstanceId == 1 && info.HasMultipleInstances == false)), Times.Once);
    }

    [Fact]
    public async Task AddPluginToolAsync_WithMultipleToolsWithSameStaticId_SetsHasMultipleInstancesCorrectly()
    {
        // Arrange
        var mockPluginFactory = new Mock<IPluginFactory>();
        var pluginInfo = new PluginInfo
        {
            StaticId = "TestPlugin",
            DisplayName = "Test Plugin",
            Version = "1.0"
        };
        mockPluginFactory.Setup(f => f.GetPluginInfo()).Returns(pluginInfo);
        mockPluginFactory.Setup(f => f.CreatePluginToolAsync(It.IsAny<PlugToolInfo>()))
            .ReturnsAsync(new Mock<IPluginTool>().Object);

        var tool1 = new MockTool { Id = 1, StaticId = "TestPlugin", IsEnabled = true, Name = "Tool1", Description = "First tool" };
        var tool2 = new MockTool { Id = 2, StaticId = "TestPlugin", IsEnabled = true, Name = "Tool2", Description = "Second tool" };
        
        // Setup GetAllToolsAsync to return multiple tools with the same StaticId
        var allTools = new List<ITool> { tool1, tool2 };
        _mockToolService.Setup(t => t.GetAllToolsAsync()).ReturnsAsync(allTools);

        // Set up plugin factories and tools manually
        var pluginFactoriesProperty = typeof(PluginService).GetProperty("PluginFactories");
        pluginFactoriesProperty?.SetValue(_pluginService, new List<IPluginFactory> { mockPluginFactory.Object });
        
        var pluginToolsProperty = typeof(PluginService).GetProperty("PluginTools");
        pluginToolsProperty?.SetValue(_pluginService, new Dictionary<long, IPluginTool>());

        // Act - Use reflection to call the private AddPluginToolAsync method
        var addPluginToolAsyncMethod = typeof(PluginService).GetMethod("AddPluginToolAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (addPluginToolAsyncMethod != null)
        {
            var task = addPluginToolAsyncMethod.Invoke(_pluginService, new object[] { tool1, _mockLogger.Object });
            if (task is Task taskResult)
            {
                await taskResult;
            }
        }

        // Assert - Verify that CreatePluginToolAsync was called with HasMultipleInstances = true
        mockPluginFactory.Verify(f => f.CreatePluginToolAsync(It.Is<PlugToolInfo>(info => 
            info.InstanceId == 1 && info.HasMultipleInstances == true)), Times.Once);
    }

    public void Dispose()
    {
        _pluginService?.Dispose();
    }
}
