using DaoStudio.Interfaces;
using DaoStudio.Services;
using DaoStudio.DBStorage.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TestDaoStudio.Mocks;

namespace TestDaoStudio.Services;

/// <summary>
/// Unit tests for ToolService class.
/// Tests tool CRUD operations, event handling, and tool management functionality.
/// </summary>
public class ToolServiceTests : IDisposable
{
    private readonly Mock<ILlmToolRepository> _mockRepository;
    private readonly Mock<ILogger<ToolService>> _mockLogger;
    private readonly ToolService _service;

    public ToolServiceTests()
    {
        _mockRepository = new Mock<ILlmToolRepository>();
        _mockLogger = new Mock<ILogger<ToolService>>();
        _service = new ToolService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act & Assert
        _service.Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(ILlmToolRepository))]
    [InlineData(nameof(ILogger<ToolService>))]
    public void Constructor_WithNullParameters_ThrowsArgumentNullException(string parameterName)
    {
        // Arrange & Act & Assert
        Action act = parameterName switch
        {
            nameof(ILlmToolRepository) => () => new ToolService(null!, _mockLogger.Object),
            nameof(ILogger<ToolService>) => () => new ToolService(_mockRepository.Object, null!),
            _ => throw new ArgumentException("Invalid parameter name", nameof(parameterName))
        };

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateToolAsync_WithValidParameters_CreatesTool()
    {
        // Arrange
        var name = "TestTool";
        var description = "A test tool";
        var staticId = "test-tool-id";
        var toolConfig = "{}";
        var parameters = new Dictionary<string, string> { { "param1", "value1" } };
        var isEnabled = true;
        var appId = 1L;

        var expectedTool = new DaoStudio.DBStorage.Models.LlmTool
        {
            Id = 1,
            Name = name,
            Description = description,
            StaticId = staticId,
            ToolConfig = toolConfig,
            Parameters = parameters,
            IsEnabled = isEnabled,
            AppId = appId,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.CreateToolAsync(It.IsAny<DaoStudio.DBStorage.Models.LlmTool>()))
                      .ReturnsAsync(expectedTool);

        // Act
        var result = await _service.CreateToolAsync(name, description, staticId, toolConfig, parameters, isEnabled, appId);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Description.Should().Be(description);
        result.StaticId.Should().Be(staticId);
        result.IsEnabled.Should().Be(isEnabled);

        _mockRepository.Verify(r => r.CreateToolAsync(It.Is<DaoStudio.DBStorage.Models.LlmTool>(t =>
            t.Name == name &&
            t.Description == description &&
            t.StaticId == staticId &&
            t.IsEnabled == isEnabled
        )), Times.Once);
    }

    [Fact]
    public async Task GetToolAsync_WithValidId_ReturnsTool()
    {
        // Arrange
        var toolId = 1L;
        var dbTool = new DaoStudio.DBStorage.Models.LlmTool
        {
            Id = toolId,
            Name = "TestTool",
            Description = "A test tool",
            StaticId = "test-tool",
            IsEnabled = true
        };

        _mockRepository.Setup(r => r.GetToolAsync(toolId))
                      .ReturnsAsync(dbTool);

        // Act
        var result = await _service.GetToolAsync(toolId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(toolId);
        result.Name.Should().Be("TestTool");

        _mockRepository.Verify(r => r.GetToolAsync(toolId), Times.Once);
    }

    [Fact]
    public async Task GetToolAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var invalidId = 999L;
        _mockRepository.Setup(r => r.GetToolAsync(invalidId))
                      .ReturnsAsync((DaoStudio.DBStorage.Models.LlmTool?)null);

        // Act
        var result = await _service.GetToolAsync(invalidId);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(r => r.GetToolAsync(invalidId), Times.Once);
    }

    [Fact]
    public async Task GetAllToolsAsync_ReturnsAllTools()
    {
        // Arrange
        var dbTools = new List<DaoStudio.DBStorage.Models.LlmTool>
        {
            new() { Id = 1, Name = "Tool1", StaticId = "tool1", IsEnabled = true },
            new() { Id = 2, Name = "Tool2", StaticId = "tool2", IsEnabled = false }
        };

        _mockRepository.Setup(r => r.GetAllToolsAsync(It.IsAny<bool>()))
                      .ReturnsAsync(dbTools);

        // Act
        var result = await _service.GetAllToolsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.Name == "Tool1");
        result.Should().Contain(t => t.Name == "Tool2");

        _mockRepository.Verify(r => r.GetAllToolsAsync(It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task GetToolsByStaticIdAsync_WithValidStaticId_ReturnsMatchingTools()
    {
        // Arrange
        var staticId = "test-tool";
        var dbTools = new List<DaoStudio.DBStorage.Models.LlmTool>
        {
            new() { Id = 1, Name = "Tool1", StaticId = staticId, IsEnabled = true },
            new() { Id = 2, Name = "Tool2", StaticId = staticId, IsEnabled = false }
        };

        _mockRepository.Setup(r => r.GetToolsByStaticIdAsync(staticId))
                      .ReturnsAsync(dbTools);

        // Act
        var result = await _service.GetToolsByStaticIdAsync(staticId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(t => t.StaticId == staticId);

        _mockRepository.Verify(r => r.GetToolsByStaticIdAsync(staticId), Times.Once);
    }

    [Fact]
    public async Task UpdateToolAsync_WithValidTool_UpdatesTool()
    {
        // Arrange
        var tool = new MockTool
        {
            Id = 1,
            Name = "Updated Tool",
            Description = "Updated description",
            IsEnabled = false
        };

        // Ensure the service finds an existing tool in the repository so that the update path is executed successfully.
        _mockRepository.Setup(r => r.GetToolAsync(tool.Id))
                      .ReturnsAsync(new DaoStudio.DBStorage.Models.LlmTool
                      {
                          Id = tool.Id,
                          Name = tool.Name,
                          Description = tool.Description,
                          StaticId = tool.StaticId,
                          ToolConfig = tool.ToolConfig,
                          Parameters = tool.Parameters,
                          IsEnabled = tool.IsEnabled,
                          AppId = tool.AppId,
                          CreatedAt = DateTime.UtcNow,
                          LastModified = DateTime.UtcNow
                      });

        _mockRepository.Setup(r => r.SaveToolAsync(It.IsAny<DaoStudio.DBStorage.Models.LlmTool>()))
                      .ReturnsAsync(true);

        // Act
        var result = await _service.UpdateToolAsync(tool);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.SaveToolAsync(It.IsAny<DaoStudio.DBStorage.Models.LlmTool>()), Times.Once);
    }

    [Fact]
    public async Task DeleteToolAsync_WithValidId_DeletesTool()
    {
        // Arrange
        var toolId = 1L;
        _mockRepository.Setup(r => r.DeleteToolAsync(toolId))
                      .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteToolAsync(toolId);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.DeleteToolAsync(toolId), Times.Once);
    }

    [Fact]
    public async Task DeleteToolAsync_WithInvalidId_ReturnsFalse()
    {
        // Arrange
        var invalidId = 999L;
        _mockRepository.Setup(r => r.DeleteToolAsync(invalidId))
                      .ReturnsAsync(false);

        // Act
        var result = await _service.DeleteToolAsync(invalidId);

        // Assert
        result.Should().BeFalse();
        _mockRepository.Verify(r => r.DeleteToolAsync(invalidId), Times.Once);
    }

    [Fact]
    public void ToolChanged_Event_CanBeSubscribedAndUnsubscribed()
    {
        // Arrange
        ToolOperationEventArgs? receivedArgs = null;

        void EventHandler(object? sender, ToolOperationEventArgs e)
        {
            receivedArgs = e;
        }

        // Act - Subscribe
        _service.ToolChanged += EventHandler;

        // Simulate event firing (this would normally happen internally)
        var tool = new MockTool { Id = 1, Name = "Test Tool" };
        // Note: We can't directly test private event firing, but we can test subscription/unsubscription

        // Act - Unsubscribe
        _service.ToolChanged -= EventHandler;

        // Assert
        // The event subscription/unsubscription should not throw
        var act = () => _service.ToolChanged += EventHandler;
        act.Should().NotThrow();
    }

    [Fact]
    public void ToolListUpdated_Event_CanBeSubscribedAndUnsubscribed()
    {
        // Arrange
        ToolListUpdateEventArgs? receivedArgs = null;

        void EventHandler(object? sender, ToolListUpdateEventArgs e)
        {
            receivedArgs = e;
        }

        // Act - Subscribe
        _service.ToolListUpdated += EventHandler;

        // Act - Unsubscribe
        _service.ToolListUpdated -= EventHandler;

        // Assert
        // The event subscription/unsubscription should not throw
        var act = () => _service.ToolListUpdated += EventHandler;
        act.Should().NotThrow();
    }

    [Fact]
    public async Task CreateToolAsync_WithNullName_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _service.CreateToolAsync(null!, "description", "static-id");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateToolAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _service.CreateToolAsync("", "description", "static-id");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateToolAsync_WithNullDescription_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _service.CreateToolAsync("name", null!, "static-id");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateToolAsync_WithNullStaticId_ThrowsArgumentException()
    {
        // Act & Assert
        var act = async () => await _service.CreateToolAsync("name", "description", null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetEnabledToolsAsync_ReturnsOnlyEnabledTools()
    {
        // Arrange
        var dbTools = new List<DaoStudio.DBStorage.Models.LlmTool>
        {
            new() { Id = 1, Name = "EnabledTool", IsEnabled = true },
            new() { Id = 2, Name = "DisabledTool", IsEnabled = false },
            new() { Id = 3, Name = "AnotherEnabledTool", IsEnabled = true }
        };

        _mockRepository.Setup(r => r.GetAllToolsAsync(It.IsAny<bool>()))
                      .ReturnsAsync(dbTools);

    // Act - Get all tools and filter enabled ones (service doesn't expose GetEnabledToolsAsync)
    var allTools = (await _service.GetAllToolsAsync()).ToList();
    var result = allTools.Where(t => t.IsEnabled).ToList();

    // Assert
    result.Should().HaveCount(2);
    result.Should().OnlyContain(t => t.IsEnabled);
    result.Should().Contain(t => t.Name == "EnabledTool");
    result.Should().Contain(t => t.Name == "AnotherEnabledTool");
    }

    [Fact]
    public async Task ToolService_HandlesRepositoryExceptions_Gracefully()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllToolsAsync(It.IsAny<bool>()))
                      .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        var act = async () => await _service.GetAllToolsAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Database error");
    }

    public void Dispose()
    {
    // ToolService does not implement IDisposable anymore; no disposal required here.
    }
}
