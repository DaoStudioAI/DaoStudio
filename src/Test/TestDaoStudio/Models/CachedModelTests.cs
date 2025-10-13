using System;
using System.Collections.Generic;
using DaoStudio.DBStorage.Models;
using FluentAssertions;
using Xunit;

namespace TestDaoStudio.Models;

/// <summary>
/// Unit tests for DBStorage.Models.CachedModel class.
/// Tests have been adjusted to exercise the actual properties present on the model.
/// </summary>
public class CachedModelTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var model = new CachedModel();

        // Assert - verify actual properties and their defaults
        model.Id.Should().Be(0);
        model.ApiProviderId.Should().Be(0);
        model.Name.Should().Be(string.Empty);
        model.ModelId.Should().Be(string.Empty);
        model.ProviderType.Should().Be(0);
        model.Catalog.Should().Be(string.Empty);
        model.Parameters.Should().NotBeNull();
        model.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Id_PropertySetterAndGetter_WorkCorrectly()
    {
        var model = new CachedModel();
        var expected = 123L;

        model.Id = expected;

        model.Id.Should().Be(expected);
    }

    [Fact]
    public void ApiProviderId_PropertySetterAndGetter_WorkCorrectly()
    {
        var model = new CachedModel();
        var expected = 42L;

        model.ApiProviderId = expected;

        model.ApiProviderId.Should().Be(expected);
    }

    [Fact]
    public void NameAndModelId_Properties_WorkCorrectly()
    {
        var model = new CachedModel();
        model.Name = "OpenAI";
        model.ModelId = "gpt-4";

        model.Name.Should().Be("OpenAI");
        model.ModelId.Should().Be("gpt-4");
    }

    [Fact]
    public void ProviderTypeAndCatalog_Properties_WorkCorrectly()
    {
        var model = new CachedModel();
        model.ProviderType = 2;
        model.Catalog = "public";

        model.ProviderType.Should().Be(2);
        model.Catalog.Should().Be("public");
    }

    [Fact]
    public void Parameters_PropertySetterAndGetter_WorkCorrectly()
    {
        var model = new CachedModel();
        var expected = new Dictionary<string, string>
        {
            { "temperature", "0.7" },
            { "max_tokens", "150" }
        };

        model.Parameters = expected;

        model.Parameters.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Parameters_CanBeReplacedWithEmptyDictionary()
    {
        var model = new CachedModel();
        model.Parameters = new Dictionary<string, string>();

        model.Parameters.Should().NotBeNull();
        model.Parameters.Should().BeEmpty();
    }
}
