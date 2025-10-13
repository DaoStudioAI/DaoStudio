namespace DesktopUI.Models;

/// <summary>
/// Specifies the direction of session navigation for animation purposes.
/// </summary>
public enum NavigationDirection
{
    /// <summary>
    /// Navigating forward to a child session (slide from right to left).
    /// </summary>
    Forward,

    /// <summary>
    /// Navigating backward to a parent session (slide from left to right).
    /// </summary>
    Backward
}
