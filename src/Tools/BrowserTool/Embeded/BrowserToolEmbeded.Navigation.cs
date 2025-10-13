using Avalonia.Threading;
using Serilog;
using DaoStudio.Interfaces.Plugins;

namespace BrowserTool;

/// <summary>
/// Navigation functionality for BrowserToolEmbeded
/// </summary>
internal partial class BrowserToolEmbeded
{
    /// <summary>
    /// Gets the current URL from the browser
    /// </summary>
    /// <returns>Current URL or null if unable to retrieve</returns>
    private async Task<string?> GetCurrentUrlAsync(IHostSession session)
    {
        try
        {
            if (browserWindow != null)
            {
                return await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // If session-aware is enabled, get URL from session's tab
                    if (browserConfig.EnableSessionAware)
                    {
                        var sessionTab = browserWindow.FindTabBySessionId(session.Id);
                        if (sessionTab != null)
                        {
                            return !string.IsNullOrEmpty(sessionTab.Url) ? sessionTab.Url : null;
                        }
                    }

                    // Original behavior - get from active tab
                    var activeUrl = browserWindow.GetActiveTabUrl();
                    return !string.IsNullOrEmpty(activeUrl) ? activeUrl : null;
                });
            }
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get current URL");
            return null;
        }
    }

    /// <summary>
    /// Waits for a URL to be loaded in the browser with a timeout
    /// </summary>
    /// <param name="url">The URL being loaded</param>
    /// <param name="timeoutSeconds">Timeout in seconds (default: 30)</param>
    /// <returns>A tuple containing success status, page title (or error message), and final URL after redirects</returns>
    private async Task<(bool Success, string Message, string? FinalUrl)> WaitForUrlLoadAsync(string url, int timeoutSeconds = 30)
    {
        var titleCompletionSource = new TaskCompletionSource<string>();

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (browserWindow?.browser != null)
                {
                    // Set up title change handler for completion notification
                    browserWindow.browser.TitleChanged += (s, title) =>
                    {
                        if (!string.IsNullOrEmpty(title))
                        {
                            titleCompletionSource.TrySetResult(title);
                        }
                    };
                }
            });

            // Wait for navigation to complete with timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var completedTask = await Task.WhenAny(titleCompletionSource.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Log.Warning("Timeout waiting for page title after {Timeout} seconds for URL: {Url}", timeoutSeconds, url);
                return (false, "Loading... (timeout)", null);
            }

            var title = await titleCompletionSource.Task;

            // Get final URL after any redirects
            string? finalUrl = null;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                finalUrl = browserWindow?.browser?.Address;
            });

            Log.Information("Successfully loaded URL: {Url} with title: {Title}", url, title);
            return (true, title, finalUrl);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error waiting for URL to load: {Url}", url);
            return (false, $"Error loading page: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Waits for the current page to be fully loaded before proceeding with content extraction
    /// </summary>
    /// <param name="session">The host session</param>
    /// <param name="timeoutSeconds">Maximum time to wait in seconds (default: 10)</param>
    /// <returns>Task representing the async operation</returns>
    private async Task WaitForPageFullyLoadedAsync(IHostSession session, int timeoutSeconds = 10)
    {
        try
        {
            BrowserTab? sessionTab = null;
            
            // Get the appropriate tab based on session-aware configuration
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (browserConfig.EnableSessionAware)
                {
                    // Session-aware mode: get the specific tab for this session
                    if (browserWindow != null)
                    {
                        sessionTab = browserWindow.FindTabBySessionId(session.Id);
                    }
                }
                else
                {
                    // Non-session-aware mode: use the currently active tab
                    if (browserWindow != null)
                    {
                        sessionTab = browserWindow.GetActiveTab();
                    }
                }
            });

            if (sessionTab == null)
            {
                Log.Warning("No tab found for session {SessionId}, skipping page load wait", session.Id);
                return;
            }

            var startTime = DateTime.Now;
            var isLoadingInitially = sessionTab.IsLoading;
            
            Log.Debug("Waiting for page to fully load. Session: {SessionId}, SessionAware: {SessionAware}, IsLoading: {IsLoading}", 
                session.Id, browserConfig.EnableSessionAware, isLoadingInitially);

            // Wait for the page to finish loading with timeout
            while ((DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
            {
                bool isLoading = false;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    isLoading = sessionTab.IsLoading;
                });

                if (!isLoading)
                {
                    // Page has finished loading, wait a small additional delay to ensure DOM is settled
                    await Task.Delay(500);
                    Log.Debug("Page fully loaded after {ElapsedSeconds:F2} seconds", (DateTime.Now - startTime).TotalSeconds);
                    return;
                }

                // Check every 100ms
                await Task.Delay(100);
            }

            Log.Warning("Timeout waiting for page to fully load after {Timeout} seconds for session {SessionId}", timeoutSeconds, session.Id);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while waiting for page to load for session {SessionId}", session.Id);
            // Don't throw - just continue with content extraction even if wait failed
        }
    }
}
