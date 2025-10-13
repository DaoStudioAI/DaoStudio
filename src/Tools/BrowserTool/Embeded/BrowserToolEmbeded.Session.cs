using Avalonia.Threading;
using Serilog;
using DaoStudio.Interfaces.Plugins;

namespace BrowserTool;

/// <summary>
/// Session management functionality for BrowserToolEmbeded
/// </summary>
internal partial class BrowserToolEmbeded
{

    /// <summary>
    /// Finds the URL from parent or grandparent sessions
    /// </summary>
    /// <param name="sessionId">Current session ID</param>
    /// <param name="parentSessionId">Parent session ID</param>
    /// <returns>The URL from parent hierarchy or null if not found</returns>
    private async Task<string?> FindParentSessionUrl(long sessionId, long? parentSessionId)
    {
        if (!parentSessionId.HasValue)
            return null;

        // Check if parent session has a tab with URL
        if (browserWindow != null)
        {
            var parentTab = browserWindow.FindTabBySessionId(parentSessionId.Value);
            if (parentTab != null && !string.IsNullOrEmpty(parentTab.Url))
            {
                return parentTab.Url;
            }
        }

        // Recursively check grandparent sessions if parent session doesn't have URL
        // Use IHost.OpenHostSession to get parent session data and traverse the hierarchy
        if (host != null)
        {
            try
            {
                using var parentSession = await host.OpenHostSession(parentSessionId.Value);
                if (parentSession != null && parentSession.ParentSessionId.HasValue)
                {
                    // Recursively check the grandparent session
                    var grandparentUrl = await FindParentSessionUrl(parentSessionId.Value, parentSession.ParentSessionId);
                    if (!string.IsNullOrEmpty(grandparentUrl))
                    {
                        Log.Information("Found URL {Url} in grandparent session {GrandparentSessionId} for session {SessionId}",
                            grandparentUrl, parentSession.ParentSessionId, sessionId);
                        return grandparentUrl;
                    }
                }
                // parentSession will be automatically disposed when leaving this scope
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to open parent session {ParentSessionId} while searching for URL for session {SessionId}",
                    parentSessionId, sessionId);
            }
        }

        return null;
    }

    /// <summary>
    /// Ensures that a session has a valid tab with URL before performing operations
    /// </summary>
    /// <param name="hostSession">The host session</param>
    /// <returns>A result indicating success and the session tab (returns empty tab if no valid URL found)</returns>
    private async Task<(bool Success, BrowserTab Tab)> EnsureSessionHasValidTab(IHostSession hostSession)
    {
        // Check if session already has a tab
        BrowserTab? sessionTab = null;
        if (browserWindow == null)
        {
            browserWindow = new BrowserWindow(browserConfig.EnableSessionAware);
            browserWindow.Show();
        }
        sessionTab = browserWindow.FindTabBySessionId(hostSession.Id);
        if (sessionTab != null)
        {
            // If tab has a URL, it's valid
            if (!string.IsNullOrEmpty(sessionTab.Url))
            {
                return (true, sessionTab);
            }
        }

        // If no tab exists or tab has no URL, try to inherit from parent
        if (hostSession.ParentSessionId.HasValue)
        {
            var parentUrl = await FindParentSessionUrl(hostSession.Id, hostSession.ParentSessionId);
            if (!string.IsNullOrEmpty(parentUrl))
            {
                // Create or update session tab with parent URL
                if (sessionTab == null)
                {
                    sessionTab = browserWindow.CreateNewTab(sessionId:hostSession.Id);
                }
                sessionTab.Url = parentUrl;

                // Activate the tab with the inherited URL
                browserWindow!.ActivateTab(sessionTab);

                // Wait for the URL to load using the common function
                var loadResult = await WaitForUrlLoadAsync(parentUrl);

                if (!loadResult.Success)
                {
                    Log.Warning("Failed to load inherited URL {Url} for session {SessionId}: {Message}",
                        parentUrl, hostSession.Id, loadResult.Message);
                    return (false, sessionTab);
                }

                // Update with final URL if there were redirects
                if (!string.IsNullOrEmpty(loadResult.FinalUrl) && loadResult.FinalUrl != parentUrl)
                {
                    sessionTab.Url = loadResult.FinalUrl;
                    Log.Debug("Session {SessionId} inherited URL redirected to: {FinalUrl}", hostSession.Id, loadResult.FinalUrl);
                }

                Log.Information("Session {SessionId} inherited and loaded URL {Url} from parent session {ParentSessionId}",
                    hostSession.Id, sessionTab.Url, hostSession.ParentSessionId);
                return (true, sessionTab);
            }
        }

        // No valid URL found in session hierarchy - return empty tab
        if (sessionTab == null)
        {
            sessionTab = browserWindow.CreateNewTab(sessionId: hostSession.Id);
        }
        return (false, sessionTab);
    }

    /// <summary>
    /// Closes all tabs and cleans up resources for the specified session
    /// </summary>
    /// <param name="sessionId">The session ID to close tabs for</param>
    public void CloseSessionTabs(long sessionId)
    {
        // Only close session tabs if session awareness is enabled
        if (!browserConfig.EnableSessionAware)
        {
            Log.Debug("Session awareness is disabled, skipping session tab cleanup for session {SessionId}", sessionId);
            return;
        }

        // Close tabs in the browser window that belong to this session
        if (browserWindow != null)
        {
            try
            {
                browserWindow.CloseTabsBySessionId(sessionId);
                Log.Information("Closed browser tabs for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to close browser tabs for session {SessionId}", sessionId);
            }
        }

        // Clear session-aware caches
        if (browserConfig.EnableSessionAware)
        {

            // Remove cached tree root for this session
            if (_sessionCachedTreeRoots.Remove(sessionId))
            {
                Log.Debug("Removed cached tree root for session {SessionId}", sessionId);
            }

            // Remove and dispose automation for this session
            if (_sessionAutomations.TryGetValue(sessionId, out var auto))
            {
                if (auto != null)
                {
                    try
                    {
                        auto.Dispose();
                        Log.Debug("Disposed UIA3Automation for session {SessionId}", sessionId);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to dispose UIA3Automation for session {SessionId}", sessionId);
                    }
                }
                _sessionAutomations.Remove(sessionId);
            }

        }
    }

    /// <summary>
    /// Gets all session IDs that have active tabs
    /// </summary>
    /// <returns>A list of session IDs</returns>
    public IEnumerable<long> GetSessionIds()
    {
        if (browserWindow == null)
            return Enumerable.Empty<long>();

        return browserWindow.GetAllTabs()
            .Where(t => t.SessionId.HasValue)
            .Select(t => t.SessionId!.Value)
            .Distinct()
            .ToList();
    }
}
