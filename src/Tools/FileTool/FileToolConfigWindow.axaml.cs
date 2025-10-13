using System;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;


#if DEBUG
using Avalonia.Diagnostics;
#endif

namespace FileTool;

public partial class FileToolConfigWindow : Window, INotifyPropertyChanged
{
    private FileToolConfig _config;
    private string _directoryPath = string.Empty;
    private string _displayName = string.Empty;
    private TextBox? _directoryTextBox;
    private TextBox? _displayNameTextBox;
    private Button? _browseButton;
    private Button? _saveButton;
    private Button? _cancelButton;

    public string? Result { get; private set; }
    public string? DisplayNameResult { get; private set; }

    public string DirectoryPath
    {
        get => _directoryPath;
        set
        {
            if (SetProperty(ref _directoryPath, value))
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

    public new event PropertyChangedEventHandler? PropertyChanged;

    public FileToolConfigWindow() : this(string.Empty)
    {
    }

    public FileToolConfigWindow(string currentConfig) : this(new PlugToolInfo { Config = currentConfig })
    {
    }

    public FileToolConfigWindow(PlugToolInfo plugToolInfo)
    {
        _config = ParseConfig(plugToolInfo.Config ?? string.Empty);
        DirectoryPath = string.IsNullOrEmpty(_config.RootDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : _config.RootDirectory;
        DisplayName = plugToolInfo.DisplayName ?? string.Empty;

        DataContext = this;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        // Find controls
        _displayNameTextBox = this.FindControl<TextBox>("DisplayNameTextBox");
        _directoryTextBox = this.FindControl<TextBox>("DirectoryTextBox");
        _browseButton = this.FindControl<Button>("BrowseButton");
        _saveButton = this.FindControl<Button>("SaveButton");
        _cancelButton = this.FindControl<Button>("CancelButton");
    }

    private FileToolConfig ParseConfig(string configJson)
    {
        if (!string.IsNullOrEmpty(configJson))
        {
            try
            {
                return JsonSerializer.Deserialize<FileToolConfig>(configJson)
                    ?? new FileToolConfig();
            }
            catch
            {
                // Fall through to return default
            }
        }
        
        return new FileToolConfig();
    }

    private async void OnBrowseButtonClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var options = new FolderPickerOpenOptions
            {
                Title = Properties.Resources.FileToolConfig_SelectFolder,
                AllowMultiple = false
            };

            if (!string.IsNullOrEmpty(DirectoryPath) && System.IO.Directory.Exists(DirectoryPath))
            {
                try
                {
                    var folder = await storageProvider.TryGetFolderFromPathAsync(DirectoryPath);
                    if (folder != null)
                    {
                        options.SuggestedStartLocation = folder;
                    }
                }
                catch
                {
                    // Continue without suggested location
                }
            }

            var result = await storageProvider.OpenFolderPickerAsync(options);
            
            if (result != null && result.Count > 0)
            {
                var selectedFolder = result[0];
                if (selectedFolder.Path != null)
                {
                    DirectoryPath = selectedFolder.Path.LocalPath;
                }
            }
        }
        catch (Exception ex)
        {
            // Could add proper error handling/logging here
            Console.WriteLine($"Error selecting folder: {ex.Message}");
        }
    }

    private void OnSaveButtonClick(object? sender, RoutedEventArgs e)
    {
        _config.RootDirectory = DirectoryPath;
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