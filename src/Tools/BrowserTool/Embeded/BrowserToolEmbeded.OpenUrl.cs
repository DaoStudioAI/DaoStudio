using Avalonia.Threading;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BrowserTool;

internal partial class BrowserToolEmbeded 
{
	[DisplayName("browser_open_web_url")]
	[Description("Opens a browser window and navigates to the specified URL")]
	public async Task<string> OpenWebUrl([Description("URL to navigate to")] string targetaddress,
        IHostSession hostSession)
	{
		Log.Information("Opening web browser with URL: {Url} for session {SessionId}", targetaddress, hostSession.Id);
		
		BrowserTab? sessionTab = null;

		// Normalize the URL
		if (!string.IsNullOrEmpty(targetaddress) && !targetaddress.Contains("://"))
		{
			targetaddress = "https://" + targetaddress;
		}

		// Ensure browser window exists and is visible BEFORE any modification operations
		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			if (browserWindow == null || !browserWindow.IsVisible)
			{
				Log.Debug(browserWindow == null ? "Creating new browser window" : "Reopening closed browser window");
				browserWindow = new BrowserWindow(browserConfig.EnableSessionAware);
				browserWindow.Show();
			}
		});

		// Handle session-aware behavior
		if (browserConfig.EnableSessionAware)
		{
			// Ensure session has a valid tab (will create or inherit from parent if needed)
			var sessionValidationResult = await EnsureSessionHasValidTab(hostSession);
			if (!sessionValidationResult.Success)
			{
				// For OpenWebUrl we have an explicit target URL; log and continue by creating/updating the tab
				Log.Debug("EnsureSessionHasValidTab returned no valid URL for session {SessionId}. Proceeding to open explicit URL.", hostSession.Id);
			}
			
			// Get the session tab (will exist after EnsureSessionHasValidTab)
			sessionTab = sessionValidationResult.Tab;
			
			// Set the new target URL
			sessionTab.Url = targetaddress;
			
			// Clear the session cache since we're navigating to a new URL
			ClearSessionCache(hostSession);
			
			Log.Debug("Session {SessionId} assigned URL: {Url}", hostSession.Id, targetaddress);
		}
		else
		{
			// Clear global cache when not in session-aware mode
			cachedTreeNodeRoot = null;
		}

		// Navigate in the UI thread
		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			if (browserConfig.EnableSessionAware && sessionTab != null)
            {
				browserWindow!.ActivateTab(sessionTab);
            }
            browserWindow!.NavigateToUrl(targetaddress);
        });

		// Wait for the URL to load using the common function
		var loadResult = await WaitForUrlLoadAsync(targetaddress);

		if (!loadResult.Success)
		{
			Log.Warning("Failed to load URL: {Url}. Message: {Message}", targetaddress, loadResult.Message);
			return loadResult.Message;
		}

		// Update session tab with final URL after any redirects
		if (browserConfig.EnableSessionAware && sessionTab != null && !string.IsNullOrEmpty(loadResult.FinalUrl))
		{
			if (loadResult.FinalUrl != targetaddress)
			{
				sessionTab.Url = loadResult.FinalUrl;
				Log.Debug("Session {SessionId} redirected to final URL: {FinalUrl}", hostSession.Id, loadResult.FinalUrl);
			}
		}
		
		var successMessage = string.Format("Successfully opened URL: {0} with title: {1}", targetaddress, loadResult.Message);
		Log.Information(successMessage);
		return successMessage;
	}


    /// <summary>
    /// Gets the current URL from the browser instance
    /// </summary>
    /// <returns>The current URL or null if not available</returns>
    [DisplayName("browser_get_current_url")]
    [Description("Gets the current URL from the browser instance")]
    public async Task<string?> GetCurrentUrl(IHostSession hostSession)
    {
        try
        {
            // Handle session-aware behavior
            if (browserConfig.EnableSessionAware)
            {
                var sessionValidationResult = await EnsureSessionHasValidTab(hostSession);
                if (!sessionValidationResult.Success)
                {
                    Log.Warning("Session {SessionId} has no valid tab", hostSession.Id);
                    return Properties.Resources.Error_NoWebPageOpen;
                }
            }

			return await GetCurrentUrlAsync(hostSession);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting current URL");
            return null;
        }
    }
}
