using System;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;


#if DEBUG
using Avalonia.Diagnostics;
#endif

namespace DaoStudio.Plugins.KVStore;

public partial class KeyValueStoreConfigWindow : Window, INotifyPropertyChanged
{
    private KeyValueStoreConfig _config;
    private bool _isCaseSensitive;
    private string _displayName = string.Empty;
    private bool _useInstanceName;
    private string _instanceName = string.Empty;
    private TextBox? _displayNameTextBox;
    private ToggleSwitch? _caseSensitivityToggle;
    private ToggleSwitch? _useInstanceNameToggle;
    private TextBox? _instanceNameTextBox;
    private Button? _saveButton;
    private Button? _cancelButton;

    public string? Result { get; private set; }
    public string? DisplayNameResult { get; private set; }

    public bool IsCaseSensitive
    {
        get => _isCaseSensitive;
        set
        {
            if (SetProperty(ref _isCaseSensitive, value))
            {
                OnPropertyChanged();
            }
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
            {
                OnPropertyChanged();
            }
        }
    }

    public bool UseInstanceName
    {
        get => _useInstanceName;
        set
        {
            if (SetProperty(ref _useInstanceName, value))
            {
                OnPropertyChanged();
            }
        }
    }

    public string InstanceName
    {
        get => _instanceName;
        set
        {
            if (SetProperty(ref _instanceName, value))
            {
                OnPropertyChanged();
            }
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    // Indicates whether multiple instances are supported. Bind this from XAML.
    private bool _hasMultipleInstances;
    public bool HasMultipleInstances
    {
        get => _hasMultipleInstances;
        set => SetProperty(ref _hasMultipleInstances, value);
    }

    public KeyValueStoreConfigWindow() : this(string.Empty)
    {
    }

    public KeyValueStoreConfigWindow(string currentConfig) : this(new PlugToolInfo { Config = currentConfig })
    {
    }

    public KeyValueStoreConfigWindow(PlugToolInfo plugToolInfo)
    {
        // Do not cache PlugToolInfo - read needed values from the parameter
        _config = ParseConfig(plugToolInfo?.Config ?? string.Empty);
        IsCaseSensitive = _config.IsCaseSensitive;
        DisplayName = plugToolInfo?.DisplayName ?? string.Empty;
        HasMultipleInstances = true;// plugToolInfo?.HasMultipleInstances ?? false;
        UseInstanceName = !string.IsNullOrEmpty(_config.InstanceName);
        InstanceName = _config.InstanceName ?? string.Empty;

        DataContext = this;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Find controls
        _displayNameTextBox = this.FindControl<TextBox>("DisplayNameTextBox");
        _caseSensitivityToggle = this.FindControl<ToggleSwitch>("CaseSensitivityToggle");
        _useInstanceNameToggle = this.FindControl<ToggleSwitch>("UseInstanceNameToggle");
        _instanceNameTextBox = this.FindControl<TextBox>("InstanceNameTextBox");
        _saveButton = this.FindControl<Button>("SaveButton");
        _cancelButton = this.FindControl<Button>("CancelButton");
    }

    private KeyValueStoreConfig ParseConfig(string configJson)
    {
        if (!string.IsNullOrEmpty(configJson))
        {
            try
            {
                return JsonSerializer.Deserialize<KeyValueStoreConfig>(configJson)
                    ?? new KeyValueStoreConfig();
            }
            catch
            {
                // Fall through to return default
            }
        }

        return new KeyValueStoreConfig();
    }

    private void OnSaveButtonClick(object? sender, RoutedEventArgs e)
    {
        _config.IsCaseSensitive = IsCaseSensitive;
        _config.InstanceName = UseInstanceName && !string.IsNullOrWhiteSpace(InstanceName) ? InstanceName : null;
        Result = JsonSerializer.Serialize(_config);
        DisplayNameResult = DisplayName;
        Close(true);
    }

    private void OnCancelButtonClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
