using CommunityToolkit.Mvvm.ComponentModel;
using DaoStudio;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DryIoc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaoStudioUI.ViewModels;

public partial class PersonItem : ObservableObject
{
    [ObservableProperty]
    private long _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    // Removed ProviderId, using ProviderName instead
    [ObservableProperty]
    private string _providerName = string.Empty; // Use ProviderName directly

    [ObservableProperty]
    private string _modelId = string.Empty; // Added

    [ObservableProperty]
    private byte[]? _image; // Added

    [ObservableProperty]
    private string[] _toolNames = Array.Empty<string>(); // Renamed from ToolIds

    [ObservableProperty]
    private string? _developerMessage;

    [ObservableProperty]
    private double? _presencePenalty;

    [ObservableProperty]
    private double? _frequencyPenalty;

    [ObservableProperty]
    private double? _topP;

    [ObservableProperty]
    private int? _topK;

    [ObservableProperty]
    private double? _temperature;

    private Dictionary<string, string> _parameters = new Dictionary<string, string>();
    public Dictionary<string, string> Parameters
    {
        get => _parameters;
        set
        {
            if (SetProperty(ref _parameters, value))
            {
                OnPropertyChanged(nameof(ParametersList));
            }
        }
    }

    // Observable collection for parameters
    public IEnumerable<KeyValuePair<string, string>> ParametersList => Parameters;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private DateTime _lastModified = DateTime.UtcNow;

    [ObservableProperty]
    private bool _hasChanges = false;

    partial void OnNameChanged(string value) => HasChanges = true;
    partial void OnDescriptionChanged(string value) => HasChanges = true;
    partial void OnProviderNameChanged(string value) => HasChanges = true; // Renamed partial method
    partial void OnModelIdChanged(string value) => HasChanges = true; // Added
    partial void OnImageChanged(byte[]? value) => HasChanges = true; // Added
    partial void OnToolNamesChanged(string[] value) => HasChanges = true; // Renamed partial method
    partial void OnDeveloperMessageChanged(string? value) => HasChanges = true; // Added for DeveloperMessage
    partial void OnPresencePenaltyChanged(double? value) => HasChanges = true;
    partial void OnFrequencyPenaltyChanged(double? value) => HasChanges = true;
    partial void OnTopPChanged(double? value) => HasChanges = true;
    partial void OnTopKChanged(int? value) => HasChanges = true;
    partial void OnTemperatureChanged(double? value) => HasChanges = true;
                                                                               // Parameters changed is handled in the Parameters property setter
    partial void OnIsEnabledChanged(bool value) => HasChanges = true;

    public IPerson? person;
    public static PersonItem FromIPerson(IPerson model)
    {
        return new PersonItem
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.Description,
            ProviderName = model.ProviderName,
            ModelId = model.ModelId,
            Image = model.Image,
            ToolNames = model.ToolNames,
            Parameters = new Dictionary<string, string>(model.Parameters),
            DeveloperMessage = model.DeveloperMessage,
            PresencePenalty = model.PresencePenalty,
            FrequencyPenalty = model.FrequencyPenalty,
            TopP = model.TopP,
            TopK = model.TopK,
            Temperature = model.Temperature,
            IsEnabled = model.IsEnabled,
            LastModified = model.LastModified,
            HasChanges = false,
            person = model
        };
    }

}
