using CommunityToolkit.Mvvm.ComponentModel;
using DaoStudioUI.ViewModels;

namespace DesktopUI.Models;

/// <summary>
/// Wrapper for chat session content used in the TransitioningContentControl.
/// </summary>
public partial class ChatSessionContent : ObservableObject
{
    /// <summary>
    /// The ViewModel for the current session.
    /// </summary>
    [ObservableProperty]
    private ChatViewModel _viewModel;

    public ChatSessionContent(ChatViewModel viewModel)
    {
        _viewModel = viewModel;
    }
}
