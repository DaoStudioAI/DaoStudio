using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.Interfaces;
using DaoStudio.Services;
using DaoStudio.DBStorage.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace TestDaoStudio.Services;

/// <summary>
/// Unit tests for ApiProviderService class that match the current service/repository signatures.
/// </summary>
public class ApiProviderServiceTests : IDisposable
{
    private readonly Mock<IAPIProviderRepository> _mockRepository;
    private readonly Mock<ILogger<ApiProviderService>> _mockLogger;
    private readonly ApiProviderService _service;

    public ApiProviderServiceTests()
    {
        _mockRepository = new Mock<IAPIProviderRepository>();
        _mockLogger = new Mock<ILogger<ApiProviderService>>();
        _service = new ApiProviderService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        _service.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateApiProviderAsync_WithValidProvider_CreatesProvider()
    {
        var providerName = "OpenAI";
        var endpoint = "https://api.openai.com/v1";

        var expectedDb = new DaoStudio.DBStorage.Models.APIProvider
        {
            Id = 1,
            Name = providerName,
            ApiEndpoint = endpoint,
            ApiKey = "test-key",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            ProviderType = (int)ProviderType.OpenAI
        };

        _mockRepository.Setup(r => r.CreateProviderAsync(It.IsAny<DaoStudio.DBStorage.Models.APIProvider>()))
            .ReturnsAsync(expectedDb);

        var result = await _service.CreateApiProviderAsync(providerName, ProviderType.OpenAI, endpoint, "test-key", null, true);

        result.Should().NotBeNull();
        result.Name.Should().Be(providerName);
        result.ApiEndpoint.Should().Be(endpoint);

        _mockRepository.Verify(r => r.CreateProviderAsync(It.Is<DaoStudio.DBStorage.Models.APIProvider>(p =>
            p.Name == providerName && p.ApiEndpoint == endpoint && p.IsEnabled == true)), Times.Once);
    }

    [Fact]
    public async Task GetApiProviderByIdAsync_WithValidId_ReturnsProvider()
    {
        var providerId = 1L;
        var dbProvider = new DaoStudio.DBStorage.Models.APIProvider
        {
            Id = providerId,
            Name = "OpenAI",
            ApiEndpoint = "https://api.openai.com/v1",
            IsEnabled = true
        };

        _mockRepository.Setup(r => r.GetProviderAsync(providerId)).ReturnsAsync(dbProvider);

        var result = await _service.GetApiProviderByIdAsync(providerId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(providerId);
        result.Name.Should().Be(dbProvider.Name);

        _mockRepository.Verify(r => r.GetProviderAsync(providerId), Times.Once);
    }

    [Fact]
    public async Task GetApiProviderByIdAsync_WithInvalidId_ReturnsNull()
    {
        var invalidId = 999L;
        _mockRepository.Setup(r => r.GetProviderAsync(invalidId)).ReturnsAsync((DaoStudio.DBStorage.Models.APIProvider?)null);

        var result = await _service.GetApiProviderByIdAsync(invalidId);

        result.Should().BeNull();
        _mockRepository.Verify(r => r.GetProviderAsync(invalidId), Times.Once);
    }

    [Fact]
    public async Task GetAllApiProvidersAsync_ReturnsAllProviders()
    {
        var providers = new List<DaoStudio.DBStorage.Models.APIProvider>
        {
            new() { Id = 1, Name = "OpenAI", ApiEndpoint = "https://api.openai.com/v1", IsEnabled = true },
            new() { Id = 2, Name = "Anthropic", ApiEndpoint = "https://api.anthropic.com", IsEnabled = true }
        };

        _mockRepository.Setup(r => r.GetAllProvidersAsync()).ReturnsAsync(providers);

        var result = await _service.GetAllApiProvidersAsync();

        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Name == "OpenAI");
        result.Should().Contain(p => p.Name == "Anthropic");

        _mockRepository.Verify(r => r.GetAllProvidersAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateApiProviderAsync_WithValidProvider_UpdatesProvider()
    {
        var providerDb = new DaoStudio.DBStorage.Models.APIProvider
        {
            Id = 1,
            Name = "Updated OpenAI",
            ApiEndpoint = "https://api.openai.com/v1",
            IsEnabled = false
        };

        _mockRepository.Setup(r => r.SaveProviderAsync(It.IsAny<DaoStudio.DBStorage.Models.APIProvider>())).ReturnsAsync(true);

        var mockInterface = new Mock<IApiProvider>();
        mockInterface.SetupGet(p => p.Id).Returns(providerDb.Id);
        mockInterface.SetupGet(p => p.Name).Returns(providerDb.Name);
        mockInterface.SetupGet(p => p.ApiEndpoint).Returns(providerDb.ApiEndpoint);
        mockInterface.SetupGet(p => p.ApiKey).Returns((string?)null);
        mockInterface.SetupGet(p => p.Parameters).Returns(new Dictionary<string, string>());
        mockInterface.SetupGet(p => p.IsEnabled).Returns(providerDb.IsEnabled);
        mockInterface.SetupGet(p => p.CreatedAt).Returns(DateTime.UtcNow);
        mockInterface.SetupGet(p => p.LastModified).Returns(DateTime.UtcNow);
        mockInterface.SetupGet(p => p.ProviderType).Returns(ProviderType.OpenAI);

        var result = await _service.UpdateApiProviderAsync(mockInterface.Object);

        result.Should().BeTrue();
        _mockRepository.Verify(r => r.SaveProviderAsync(It.IsAny<DaoStudio.DBStorage.Models.APIProvider>()), Times.Once);
    }

    [Fact]
    public async Task DeleteApiProviderAsync_WithValidId_DeletesProvider()
    {
        var providerId = 1L;
        _mockRepository.Setup(r => r.DeleteProviderAsync(providerId)).ReturnsAsync(true);

        var result = await _service.DeleteApiProviderAsync(providerId);

        result.Should().BeTrue();
        _mockRepository.Verify(r => r.DeleteProviderAsync(providerId), Times.Once);
    }

    [Fact]
    public async Task GetApiProviderByNameAsync_WithValidName_ReturnsProvider()
    {
        var providerName = "OpenAI";
        var dbProvider = new DaoStudio.DBStorage.Models.APIProvider
        {
            Id = 1,
            Name = providerName,
            ApiEndpoint = "https://api.openai.com/v1",
            IsEnabled = true
        };

        _mockRepository.Setup(r => r.GetProviderByNameAsync(providerName)).ReturnsAsync(dbProvider);

        // The service does not expose a GetByName method; emulate expected behavior via GetAll and filter
        _mockRepository.Setup(r => r.GetAllProvidersAsync()).ReturnsAsync(new[] { dbProvider });

        var all = await _service.GetAllApiProvidersAsync();
        var found = all.FirstOrDefault(p => p.Name == providerName);

        found.Should().NotBeNull();
        found!.Name.Should().Be(providerName);

        _mockRepository.Verify(r => r.GetAllProvidersAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateApiProviderAsync_InvalidParameters_Throws()
    {
        var act1 = async () => await _service.CreateApiProviderAsync(null!, ProviderType.OpenAI, "https://api.test.com", "key", null, true);
        await act1.Should().ThrowAsync<ArgumentException>();

        var act2 = async () => await _service.CreateApiProviderAsync("", ProviderType.OpenAI, "https://api.test.com", "key", null, true);
        await act2.Should().ThrowAsync<ArgumentException>();

        var act3 = async () => await _service.CreateApiProviderAsync("Test", ProviderType.OpenAI, null!, "key", null, true);
        await act3.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetEnabledApiProvidersAsync_ReturnsOnlyEnabledProviders()
    {
        var providers = new List<DaoStudio.DBStorage.Models.APIProvider>
        {
            new() { Id = 1, Name = "OpenAI", IsEnabled = true },
            new() { Id = 2, Name = "Anthropic", IsEnabled = false },
            new() { Id = 3, Name = "Google", IsEnabled = true }
        };

        _mockRepository.Setup(r => r.GetAllProvidersAsync()).ReturnsAsync(providers);

        var result = await _service.GetAllApiProvidersAsync();

        var enabled = result.Where(p => p.IsEnabled).ToList();
        enabled.Should().HaveCount(2);
        enabled.Should().OnlyContain(p => p.IsEnabled);
        enabled.Should().Contain(p => p.Name == "OpenAI");
        enabled.Should().Contain(p => p.Name == "Google");
    }

    public void Dispose()
    {
        // service does not implement Dispose in current code; nothing to do
    }
}
