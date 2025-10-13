using CommunityToolkit.Mvvm.ComponentModel;
using DaoStudioUI.ViewModels;
using DaoStudio.Interfaces;

namespace DesktopUI.Models;

/// <summary>
/// Wraps a session with its ViewModel and UI state for navigation stack management.
/// </summary>
public partial class SessionNavigationItem : ObservableObject
{
    /// <summary>
    /// The session instance.
    /// </summary>
    [ObservableProperty]
    private ISession _session;

    /// <summary>
    /// The AI model/person for this session.
    /// </summary>
    [ObservableProperty]
    private IPerson _model;

    /// <summary>
    /// The ViewModel managing this session's UI.
    /// </summary>
    [ObservableProperty]
    private ChatViewModel? _viewModel;

    /// <summary>
    /// Display title for the session.
    /// </summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>
    /// Icon for the session (typically the model's icon).
    /// </summary>
    [ObservableProperty]
    private byte[]? _icon;

    /// <summary>
    /// Preserved UI state when navigating away from this session.
    /// </summary>
    [ObservableProperty]
    private SessionState _state = new();

    public SessionNavigationItem(ISession session, IPerson model)
    {
        _session = session;
        _model = model;
        _title = model.Name ?? "Chat";
        _icon = model.Image;
    }
}
