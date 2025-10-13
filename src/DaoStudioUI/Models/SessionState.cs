using System.Collections.Generic;
using DesktopUI.ViewModels.Home.Chat;

namespace DesktopUI.Models;

/// <summary>
/// Preserves UI state for a session when navigating away.
/// </summary>
public class SessionState
{
    /// <summary>
    /// Vertical scroll position in the message list.
    /// </summary>
    public double ScrollPosition { get; set; }

    /// <summary>
    /// Current text in the input box.
    /// </summary>
    public string InputText { get; set; } = string.Empty;

    /// <summary>
    /// Currently attached files.
    /// </summary>
    public List<Attachment> Attachments { get; set; } = new();

    /// <summary>
    /// Whether the tool panel is open.
    /// </summary>
    public bool IsPaneOpen { get; set; }
}
