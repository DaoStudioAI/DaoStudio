using System;
using DaoStudio.DBStorage.Models;
using FluentAssertions;
using Xunit;

namespace TestDaoStudio.Models;

/// <summary>
/// Unit tests for DBStorage.Models.Settings class.
/// Tests focus on the actual properties present on the model: ApplicationName, Version,
/// Properties (dictionary), LastModified and Theme.
/// </summary>
public class SettingsTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var settings = new Settings();

        // Assert
        settings.ApplicationName.Should().Be(DaoStudio.Common.Constants.AppName);
        settings.Version.Should().Be(1);
        settings.Properties.Should().NotBeNull();
        settings.Properties.Should().BeEmpty();
        settings.LastModified.Should().NotBe(default);
        settings.Theme.Should().Be(2);
    }

    [Fact]
    public void Version_PropertySetterAndGetter_Works()
    {
        var settings = new Settings();
        settings.Version = 5;
        settings.Version.Should().Be(5);
    }

    [Fact]
    public void ApplicationName_PropertySetterAndGetter_Works()
    {
        var settings = new Settings();
        settings.ApplicationName = "CustomApp";
        settings.ApplicationName.Should().Be("CustomApp");
    }

    [Fact]
    public void Properties_Dictionary_BehavesLikeDictionary()
    {
        var settings = new Settings();
        settings.Properties["k1"] = "v1";
        settings.Properties["k2"] = "v2";

        settings.Properties.Should().HaveCount(2);
        settings.Properties.Should().ContainKey("k1");
        settings.Properties["k1"].Should().Be("v1");
    }

    [Fact]
    public void LastModified_CanBeSet()
    {
        var settings = new Settings();
        var dt = DateTime.UtcNow;
        settings.LastModified = dt;
        settings.LastModified.Should().Be(dt);
    }

    [Fact]
    public void Theme_PropertySetterAndGetter_Works()
    {
        var settings = new Settings();
        settings.Theme = 0;
        settings.Theme.Should().Be(0);
    }
}
