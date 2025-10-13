using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using DaoStudio.Common.Plugins;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using BrowserTool.Properties;
using DaoStudio.Interfaces;

namespace BrowserTool;

public partial class BrowserConfigWindow : Window, INotifyPropertyChanged
{
    private TaskCompletionSource<PlugToolInfo>? _resultSource;
    private BrowserToolConfig _config = new();
    private string _displayName = string.Empty;
    private Button _browseButton = null!;
    private Button _saveButton = null!;
    private Button _cancelButton = null!;
    private ComboBox _browserToolTypeComboBox = null!;
    private ComboBox _browserTypeComboBox = null!;
    private TextBox _browserPathTextBox = null!;
    private TextBox _displayNameTextBox = null!;
    private TextBlock _browserTypeLabel = null!;
    private TextBlock _browserPathLabel = null!;
    private CheckBox _enableSessionAwareCheckBox = null!;

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

    public new event PropertyChangedEventHandler? PropertyChanged;

    public BrowserConfigWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        InitializeControls();
        if (!Design.IsDesignMode)
        {
            _config = new BrowserToolConfig();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }    private void InitializeControls()
    {
        _displayNameTextBox = this.FindControl<TextBox>("DisplayNameTextBox") ?? 
            throw new InvalidOperationException("Could not find DisplayNameTextBox");
        _enableSessionAwareCheckBox = this.FindControl<CheckBox>("EnableSessionAwareCheckBox") ?? 
            throw new InvalidOperationException("Could not find EnableSessionAwareCheckBox");
        _browserToolTypeComboBox = this.FindControl<ComboBox>("BrowserToolTypeComboBox") ?? 
            throw new InvalidOperationException("Could not find BrowserToolTypeComboBox");
        _browserTypeComboBox = this.FindControl<ComboBox>("BrowserTypeComboBox") ?? 
            throw new InvalidOperationException("Could not find BrowserTypeComboBox");
        _browserTypeLabel = this.FindControl<TextBlock>("BrowserTypeLabel") ?? 
            throw new InvalidOperationException("Could not find BrowserTypeLabel");
        _browserPathLabel = this.FindControl<TextBlock>("BrowserPathLabel") ?? 
            throw new InvalidOperationException("Could not find BrowserPathLabel");
        _browserPathTextBox = this.FindControl<TextBox>("BrowserPathTextBox") ?? 
            throw new InvalidOperationException("Could not find BrowserPathTextBox");
        _browseButton = this.FindControl<Button>("BrowseButton") ?? 
            throw new InvalidOperationException("Could not find BrowseButton");
        _saveButton = this.FindControl<Button>("SaveButton") ?? 
            throw new InvalidOperationException("Could not find SaveButton");
        _cancelButton = this.FindControl<Button>("CancelButton") ?? 
            throw new InvalidOperationException("Could not find CancelButton");

        // Add ComboBox items based on platform
#if WINDOWS
        _browserToolTypeComboBox.Items.Add(Properties.Resources.Embedded);
#endif
        _browserToolTypeComboBox.Items.Add(Properties.Resources.PlaywrightLocal);
        _browserToolTypeComboBox.Items.Add(Properties.Resources.PlaywrightOS);

        _browseButton.Click += BrowseButton_Click;
        _saveButton.Click += SaveButton_Click;
        _cancelButton.Click += CancelButton_Click;
        
        // Set DataContext for binding
        DataContext = this;

        UpdateControlsEnabledState();
    }

    private void BrowserToolTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_browserToolTypeComboBox.SelectedIndex >= 0)
        {
#if !WINDOWS
            // For non-Windows platforms, adjust the index since "Embedded" is not in the list
            _config.BrowserToolType = (BrowserToolType)(_browserToolTypeComboBox.SelectedIndex + 1);
#else
            _config.BrowserToolType = (BrowserToolType)_browserToolTypeComboBox.SelectedIndex;
#endif
        }
        UpdateControlsEnabledState();
    }

    private void UpdateControlsEnabledState()
    {
        var isPlaywright = _config.BrowserToolType == BrowserToolType.PlaywrightLocal || 
                          _config.BrowserToolType == BrowserToolType.PlaywrightOs;
        
        _browserTypeLabel.IsEnabled = isPlaywright;
        _browserPathLabel.IsEnabled = _config.BrowserToolType == BrowserToolType.PlaywrightLocal;
        
#if !WINDOWS
        // Ensure we can't select Embedded on non-Windows platforms
        if (_config.BrowserToolType == BrowserToolType.Embeded)
        {
            _config.BrowserToolType = BrowserToolType.PlaywrightLocal;
            _browserToolTypeComboBox.SelectedIndex = 0; // First item (PlaywrightLocal)
        }
#endif
    }

    private void LoadConfig(string config)
    {
        if (!string.IsNullOrEmpty(config))
        {
            try
            {
                _config = JsonSerializer.Deserialize<BrowserToolConfig>(config) ?? new BrowserToolConfig();
            }
            catch
            {
                _config = new BrowserToolConfig();
            }
        }

#if !WINDOWS
        // If we're not on Windows and config is set to Embedded, change it to PlaywrightLocal
        if (_config.BrowserToolType == BrowserToolType.Embeded)
        {
            _config.BrowserToolType = BrowserToolType.PlaywrightLocal;
        }
#endif

        // Set ComboBox selections and checkbox
        _browserToolTypeComboBox.SelectedIndex = (int)_config.BrowserToolType;
        _browserTypeComboBox.SelectedIndex = (int)_config.BrowserType;
        _browserPathTextBox.Text = _config.BrowserPath;
        _enableSessionAwareCheckBox.IsChecked = _config.EnableSessionAware;

        UpdateControlsEnabledState();
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        var options = new FilePickerOpenOptions
        {
            Title = Properties.Resources.SelectBrowserExecutable,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(Properties.Resources.ExecutableFiles)
                {
                    Patterns = new[] { "*.exe" },
                    MimeTypes = new[] { "application/x-msdownload" }
                },
                new FilePickerFileType(Properties.Resources.AllFiles)
                {
                    Patterns = new[] { "*.*" }
                }
            }
        };

        var result = await StorageProvider.OpenFilePickerAsync(options);
        if (result.Count > 0)
        {
            _browserPathTextBox.Text = result[0].Path.LocalPath;
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        _config.BrowserToolType = (BrowserToolType)_browserToolTypeComboBox.SelectedIndex;
        _config.BrowserType = (BrowserType)_browserTypeComboBox.SelectedIndex;
        _config.BrowserPath = _browserPathTextBox.Text ?? string.Empty;
        _config.EnableSessionAware = _enableSessionAwareCheckBox.IsChecked ?? true;

        var instanceInfo = new PlugToolInfo
        {
            DisplayName = DisplayName,
            Description = Properties.Resources.BrowserToolDescription,
            Config = JsonSerializer.Serialize(_config),
            SupportConfigWindow = true
        };

        _resultSource?.TrySetResult(instanceInfo);
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        _resultSource?.TrySetResult(new PlugToolInfo
        {
            Description = Properties.Resources.BrowserToolDescription,
            Config = JsonSerializer.Serialize(_config), // Return original config
            SupportConfigWindow = true
        });
        Close();
    }

    // Static helper used by BrowserToolPlugin to open this window and obtain an updated PlugInstanceInfo
    public static async Task<PlugToolInfo> ShowDialog(Window owner, string config)
    {
        return await ShowDialog(owner, new PlugToolInfo { Config = config });
    }

    // Updated static helper that takes full PlugToolInfo
    public static async Task<PlugToolInfo> ShowDialog(Window owner, PlugToolInfo plugToolInfo)
    {
        var window = new BrowserConfigWindow();
        window._resultSource = new TaskCompletionSource<PlugToolInfo>();
        window.LoadConfig(plugToolInfo.Config ?? string.Empty);
        window.DisplayName = plugToolInfo.DisplayName ?? string.Empty;

        if (owner != null)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            window.Show();
        }

        return await window._resultSource.Task;
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