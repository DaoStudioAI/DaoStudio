using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DaoStudio.Interfaces;

namespace DesktopUI.Models;

/// <summary>
/// Manages the session navigation hierarchy and stack.
/// </summary>
public partial class SessionNavigationStack : ObservableObject
{
    /// <summary>
    /// All sessions in the navigation stack (ordered from root to current).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<SessionNavigationItem> _sessions = new();

    /// <summary>
    /// The currently active session.
    /// </summary>
    [ObservableProperty]
    private SessionNavigationItem? _currentSession;

    /// <summary>
    /// Index of the current session in the stack.
    /// </summary>
    [ObservableProperty]
    private int _currentIndex = -1;

    /// <summary>
    /// Maximum number of sessions to keep in the stack (memory management).
    /// </summary>
    [ObservableProperty]
    private int _maxStackDepth = 10;

    /// <summary>
    /// Pushes a new session onto the stack and makes it current.
    /// </summary>
    public void PushSession(SessionNavigationItem item)
    {
        // If we're not at the top of the stack, remove forward history
        if (CurrentIndex < Sessions.Count - 1)
        {
            for (int i = Sessions.Count - 1; i > CurrentIndex; i--)
            {
                var removed = Sessions[i];
                // ChatViewModel doesn't implement IDisposable, cleanup handled elsewhere
                // removed.ViewModel?.Dispose();
                Sessions.RemoveAt(i);
            }
        }

        // Add new session
        Sessions.Add(item);
        CurrentIndex = Sessions.Count - 1;
        CurrentSession = item;

        // Enforce stack depth limit
        EnforceStackDepthLimit();
    }

    /// <summary>
    /// Navigates to a session at the specified index.
    /// </summary>
    public SessionNavigationItem? NavigateToIndex(int index)
    {
        if (index < 0 || index >= Sessions.Count)
            return null;

        CurrentIndex = index;
        CurrentSession = Sessions[index];
        return CurrentSession;
    }

    /// <summary>
    /// Checks if navigation backward is possible.
    /// </summary>
    public bool CanNavigateBack => CurrentIndex > 0;

    /// <summary>
    /// Checks if navigation forward is possible.
    /// </summary>
    public bool CanNavigateForward => CurrentIndex < Sessions.Count - 1;

    /// <summary>
    /// Gets the breadcrumb path for the current navigation state.
    /// </summary>
    public ReadOnlyCollection<SessionNavigationItem> GetBreadcrumbPath()
    {
        return new ReadOnlyCollection<SessionNavigationItem>(
            Sessions.Take(CurrentIndex + 1).ToList()
        );
    }

    /// <summary>
    /// Clears all sessions from the stack.
    /// </summary>
    public void Clear()
    {
        // ChatViewModel doesn't implement IDisposable, cleanup handled elsewhere
        // foreach (var item in Sessions)
        // {
        //     item.ViewModel?.Dispose();
        // }
        Sessions.Clear();
        CurrentIndex = -1;
        CurrentSession = null;
    }

    /// <summary>
    /// Removes sessions from the bottom of the stack when exceeding max depth.
    /// </summary>
    private void EnforceStackDepthLimit()
    {
        while (Sessions.Count > MaxStackDepth)
        {
            var oldest = Sessions[0];
            // ChatViewModel doesn't implement IDisposable, cleanup handled elsewhere
            // oldest.ViewModel?.Dispose();
            Sessions.RemoveAt(0);
            CurrentIndex--;
        }
    }
}
