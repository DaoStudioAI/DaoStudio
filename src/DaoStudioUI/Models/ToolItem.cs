using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using DaoStudio.Common.Plugins;
using DaoStudioUI.ViewModels;
using DaoStudio.Constants;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;

namespace DaoStudioUI.Models;

/// <summary>
/// Unified tool item model that supports both tool management and tool selection scenarios
/// </summary>
public partial class ToolItem : ObservableObject
{
    // Core tool properties (mapped to/from LlmTool)
    [ObservableProperty]
    private long _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _staticId = string.Empty;

    [ObservableProperty]
    private string _toolConfig = string.Empty;

    [ObservableProperty]
    private ToolType _toolType;

    [ObservableProperty]
    private Dictionary<string, string> _parameters = new();

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private ToolState _state = ToolState.Stateless;

    [ObservableProperty]
    private DateTime _lastModified = DateTime.UtcNow;

    [ObservableProperty]
    private long _appId = 0;

    // Management-specific properties
    [ObservableProperty]
    private bool _hasChanges = false;

    // Selection-specific properties  
    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private string _pluginName = string.Empty;

    // Store the functions with descriptions from the plugin
    public List<FunctionWithDescription>? Functions { get; set; }

    // Event that parent can subscribe to for selection changes
    public event EventHandler? SelectionChanged;

    // Optional parent reference for scenarios that need it (like ChatViewModel)
    private ChatViewModel? _parent;

    // Computed properties
    public bool CanShowConfigWindow => Parameters.TryGetValue(LlmToolParameterNames.ShowConfigWin, out var valueString) && bool.TryParse(valueString, out bool boolValue) && boolValue;

    public bool IsLoading => _parent?.IsLoading ?? false;

    // Constructors
    public ToolItem()
    {
        // Default constructor for tool management scenarios
    }

    public ToolItem(ChatViewModel parent) : this()
    {
        _parent = parent;
        if (parent != null)
        {
            parent.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ChatViewModel.IsLoading))
                {
                    OnPropertyChanged(nameof(IsLoading));
                }
            };
        }
    }

    // Factory methods for creating from database model
    public static ToolItem FromTool(ITool tool, ChatViewModel? parent = null)
    {
        var toolItem = parent != null ? new ToolItem(parent) : new ToolItem();
        
        toolItem.Id = tool.Id;
        toolItem.Name = tool.Name;
        toolItem.Description = tool.Description;
        toolItem.StaticId = tool.StaticId;
        toolItem.ToolConfig = tool.ToolConfig;
        toolItem.ToolType = tool.ToolType;
        toolItem.Parameters = tool.Parameters ?? new Dictionary<string, string>();
        toolItem.IsEnabled = tool.IsEnabled;
        toolItem.State = tool.State;
        toolItem.LastModified = tool.LastModified;
        toolItem.AppId = tool.AppId;
        toolItem.PluginName = tool.StaticId; // For backward compatibility
        toolItem.HasChanges = false;
        
        return toolItem;
    }

    public ITool ToTool()
    {
        // Note: This would need to be handled by the calling code to create appropriate Tool instance
        // since we can't instantiate interface directly
        throw new NotImplementedException("Use ToolService to convert ToolItem back to Tool");
    }

    // Property change handlers
    partial void OnNameChanged(string value)
    {
        HasChanges = true;
        LastModified = DateTime.UtcNow;
    }

    partial void OnDescriptionChanged(string value) => HasChanges = true;
    partial void OnStaticIdChanged(string value) => HasChanges = true;
    partial void OnToolConfigChanged(string value) => HasChanges = true;
    partial void OnToolTypeChanged(ToolType value) => HasChanges = true;
    partial void OnParametersChanged(Dictionary<string, string> value)
    {
        HasChanges = true;
        OnPropertyChanged(nameof(CanShowConfigWindow));
    }
    partial void OnIsEnabledChanged(bool value) => HasChanges = true;
    partial void OnStateChanged(ToolState value) => HasChanges = true;
    partial void OnAppIdChanged(long value) => HasChanges = true;

    partial void OnIsSelectedChanged(bool value)
    {
        // Trigger the selection changed event
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
