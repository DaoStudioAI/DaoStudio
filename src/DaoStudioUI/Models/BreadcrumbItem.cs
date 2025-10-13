using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DesktopUI.Models;

/// <summary>
/// Represents a single item in the breadcrumb navigation bar.
/// </summary>
public partial class BreadcrumbItem : ObservableObject
{
    /// <summary>
    /// Index of this session in the navigation stack.
    /// </summary>
    [ObservableProperty]
    private int _index;

    /// <summary>
    /// Display title for the breadcrumb.
    /// </summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>
    /// Optional icon for the breadcrumb item.
    /// </summary>
    [ObservableProperty]
    private byte[]? _icon;

    /// <summary>
    /// Whether this is the currently active session.
    /// </summary>
    [ObservableProperty]
    private bool _isCurrent;

    /// <summary>
    /// Whether this is not the first item (used to show separator).
    /// </summary>
    public bool IsNotFirst => Index > 0;

    /// <summary>
    /// Command to execute when this breadcrumb is clicked.
    /// </summary>
    [ObservableProperty]
    private ICommand? _navigateCommand;
}
