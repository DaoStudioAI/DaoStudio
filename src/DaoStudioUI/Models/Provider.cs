using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using DaoStudio.Interfaces;
using DryIoc;
using System.Threading.Tasks;

namespace DaoStudioUI.ViewModels;

// Provider class to replace LlmProviderViewModel
public partial class Provider : ObservableObject
{
    [ObservableProperty]
    private long _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _apiEndpoint = string.Empty;

    [ObservableProperty]
    private string? _apiKey = null;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private DateTime _lastModified = DateTime.UtcNow;

    [ObservableProperty]
    private Dictionary<string, string> _parameters = new();

    [ObservableProperty]
    private bool _hasChanges = false;


    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    [ObservableProperty]
    private ProviderType _providerType = ProviderType.Unknown;


    public IApiProvider? ApiProvider;


    partial void OnNameChanged(string value) => HasChanges = true;
    partial void OnApiEndpointChanged(string value) => HasChanges = true;
    partial void OnApiKeyChanged(string? value) => HasChanges = true;
    partial void OnIsEnabledChanged(bool value) => HasChanges = true;

    // Creates from IApiProvider interface
    public static Provider FromApiProvider(IApiProvider apiProvider)
    {
        return new Provider
        {
            Id = apiProvider.Id,
            Name = apiProvider.Name,
            ApiEndpoint = apiProvider.ApiEndpoint,
            ApiKey = apiProvider.ApiKey,
            Parameters = new Dictionary<string, string>(apiProvider.Parameters),
            IsEnabled = apiProvider.IsEnabled,
            LastModified = apiProvider.LastModified,
            CreatedAt = apiProvider.CreatedAt,
            ProviderType = apiProvider.ProviderType,
            HasChanges = false,
            ApiProvider= apiProvider
        };
    }

}
