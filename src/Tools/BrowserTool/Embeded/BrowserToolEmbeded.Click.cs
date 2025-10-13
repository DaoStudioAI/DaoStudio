using Avalonia.Threading;
using Serilog;
using System.ComponentModel;
using System.Text;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using System.Diagnostics;




#if WINDOWS
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
#endif

namespace BrowserTool;


internal partial class BrowserToolEmbeded
{

#if WINDOWS

    [DisplayName("browser_click_on_web_page")]
    [Description("Click on the specified web element. Use element IDs from get_current_page_content. For multiple candidate elements, get_element_info can provide additional details to determine the correct target.")]
    public async Task<ClickResult> ClickElement([Description("Element ID from get_current_page_content result")] long elementId,
        IHostSession hostSession)
    {
        Log.Information("Attempting to click element with ID: {ElementId} for session {SessionId}", elementId, hostSession.Id);
        var result = new ClickResult
        {
            Success = false,
            ElementId = elementId.ToString()
        };

        try
        {
            // Handle session-aware behavior
            if (browserConfig.EnableSessionAware)
            {
                var sessionValidationResult = await EnsureSessionHasValidTab(hostSession);
                if (!sessionValidationResult.Success)
                {
                    result.Error = Properties.Resources.Error_NoWebPageOpen;
                    return result;
                }
            }


            // Validate preconditions
            var validationError = ValidateClickPreconditions(hostSession);
            if (!string.IsNullOrEmpty(validationError))
            {
                result.Error = validationError;
                return result;
            }

            // Find and validate the target element
            var node = FindAndValidateElement(elementId, hostSession);
            if (node == null)
            {
                result.Error = $"Element with ID {elementId} not found or not clickable";
                return result;
            }

            // Get current URL before clicking
            string? initialUrl = await GetCurrentUrlAsync(hostSession);

            // Perform the click operation (non-throwing)
            var clickOutcome = await PerformClickAsync(node);
            if (!clickOutcome.success)
            {
                result.Error = string.IsNullOrWhiteSpace(clickOutcome.error)
                    ? $"Failed to click element with ID {elementId}."
                    : $"Failed to click element with ID {elementId}. {clickOutcome.error}";
                return result;
            }

            for(int i = 0; i < 5; i ++)
            {
                await Task.Delay(1000); // Brief delay to allow any immediate UI changes
            }

            // Wait for potential navigation and get final URL
            var finalUrl = await WaitForNavigationAndGetUrlAsync(initialUrl, hostSession);

            // Clean up cached tree after successful click
            if (browserConfig.EnableSessionAware)
            {
                // Clear only this session cache
                await Dispatcher.UIThread.InvokeAsync(() => { /* ensure UI idle */ });
                // We don't have hostSession in core here, but we pass it
            }
            else
            {
                cachedTreeNodeRoot = null;
            }
            
            result.Success = true;
            result.CurrentUrl = finalUrl;

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error clicking element with ID: {ElementId}", elementId);
            result.Error = $"Error clicking element: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Validates the preconditions for clicking an element
    /// </summary>
    /// <returns>Error message if validation fails, null if validation passes</returns>
    private string? ValidateClickPreconditions(IHostSession session)
    {
        if (browserWindow == null)
        {
            Log.Error("Browser window is null");
            return Properties.Resources.Error_BrowserWindowNull;
        }

        if (browserConfig.EnableSessionAware)
        {
            if (GetCachedTreeNodeRoot(session) == null)
            {
                Log.Error("No cached tree available. Please get page content first");
                return Properties.Resources.Error_GetPageContentFirst;
            }
        }
        else if (cachedTreeNodeRoot == null)
        {
            Log.Error("No cached tree available. Please get page content first");
            return Properties.Resources.Error_GetPageContentFirst;
        }

        return null;
    }

    /// <summary>
    /// Finds and validates that an element is clickable
    /// </summary>
    /// <param name="elementId">The element ID to find</param>
    /// <returns>The found node if valid and clickable, null otherwise</returns>
    private CachedTreeNode? FindAndValidateElement(long elementId, IHostSession session)
    {
        var root = browserConfig.EnableSessionAware ? GetCachedTreeNodeRoot(session)! : cachedTreeNodeRoot!;
        var node = FindNodeById(root, elementId);
        if (node == null)
        {
            Log.Error("Element with ID {ElementId} not found", elementId);
            return null;
        }
        //try
        //{
        //    if (node.Node.Properties.IsKeyboardFocusable.Value != true)
        //    {
        //        Log.Error("Element with ID {ElementId} is not clickable", elementId);
        //        return null;
        //    }
        //}
        //catch (Exception ex)
        //{
        //    // Some properties may throw if not supported; log and proceed
        //    Log.Debug(ex, "IsOffscreen property check failed for element ID {ElementId}", elementId);
        //    return null;
        //}

        return node;
    }


    /// <summary>
    /// Performs the actual click operation on the element
    /// </summary>
    /// <param name="node">The node to click</param>
    private async Task<(bool success, string? error)> PerformClickAsync(CachedTreeNode node)
    {
        Log.Debug("Clicking element with ID: {ElementId}", node.Id);
        
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            browserWindow!.Activate();

            // Attempt to bring the target element into the visible viewport before clicking
            ScrollElementIntoView(node);

            // Perform the click using the most appropriate method (non-throwing)
            var (ok, details) = TryPerformElementClick(node);
            return (ok, ok ? null : details);
        });
    }

    /// <summary>
    /// Scrolls the element into view if possible
    /// </summary>
    /// <param name="node">The node to scroll into view</param>
    private void ScrollElementIntoView(CachedTreeNode node)
    {
        try
        {
            node.Node.Focus();
        }
        catch { }
        try
        {
            node.Node.FocusNative();
        }
        catch { }
        try
        {
            if (node.Node.Patterns.ScrollItem.IsSupported)
            {
                node.Node.Patterns.ScrollItem.Pattern.ScrollIntoView();
            }
        }
        catch (Exception scrollEx)
        {
            // Scrolling is a best-effort operation; proceed even if it fails but log for diagnostics
            Log.Debug(scrollEx, "ScrollIntoView failed, proceeding with click anyway.");
        }
    }

    /// <summary>
    /// Performs the actual click on the element using the most appropriate method
    /// </summary>
    /// <param name="node">The node to click</param>
    private (bool ok, string errorDetails) TryPerformElementClick(CachedTreeNode node)
    {
        // Try several strategies in order, catching and falling back instead of failing early
        var elem = node.Node;
        var errors = new List<string>();

        // Best-effort: ensure element has focus
        try { elem.Focus(); } catch (Exception focusEx) { Log.Debug(focusEx, "Element.Focus() failed; continuing."); }

        // Strategy 1: UIA Invoke pattern
        try
        {
            bool isEnabled = IsElementEnabledSafe(elem);
            if (isEnabled && elem.Patterns.Invoke.IsSupported)
            {
                elem.Patterns.Invoke.Pattern.Invoke();
                return (true, string.Empty);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Invoke failed: {ex.Message}");
            Log.Debug(ex, "Invoke pattern click failed; will try fallbacks.");
        }

        // Strategy 2: Direct mouse click via AutomationElement
        try
        {
            elem.Click();
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            errors.Add($"Direct Click failed: {ex.Message}");
            Log.Debug(ex, "Direct Click failed; will try other patterns.");
        }

        // Strategy 3: SelectionItem.Select (for list items, menu items, etc.)
        try
        {
            if (elem.Patterns.SelectionItem.IsSupported)
            {
                elem.Patterns.SelectionItem.Pattern.Select();
                return (true, string.Empty);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"SelectionItem.Select failed: {ex.Message}");
            Log.Debug(ex, "SelectionItem.Select failed; trying next.");
        }

        // Strategy 4: Toggle (checkboxes, switches)
        try
        {
            if (elem.Patterns.Toggle.IsSupported)
            {
                elem.Patterns.Toggle.Pattern.Toggle();
                return (true, string.Empty);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Toggle failed: {ex.Message}");
            Log.Debug(ex, "Toggle failed; trying next.");
        }

        // Strategy 5: LegacyIAccessible DoDefaultAction as a last resort
        try
        {
            bool isEnabled = IsElementEnabledSafe(elem);
            if (elem.Patterns.LegacyIAccessible.IsSupported && isEnabled)
            {
                elem.Patterns.LegacyIAccessible.Pattern.DoDefaultAction();
                return (true, string.Empty);
            }
        }
        catch (Exception ex)
        {
            errors.Add($"LegacyIAccessible.DoDefaultAction failed: {ex.Message}");
            Log.Debug(ex, "LegacyIAccessible.DoDefaultAction failed; no more fallbacks.");
        }

        // If we reach here, all strategies failed. Return consolidated error details instead of throwing.
        return (false, $"Error occurs when clicking {node.Id}");
    }

    /// <summary>
    /// Safely checks if an automation element is enabled. If the property is not supported
    /// or throws, we assume enabled to avoid blocking clicks on elements that don't expose it.
    /// </summary>
    private bool IsElementEnabledSafe(AutomationElement element)
    {
        try
        {
            // Some providers do not support IsEnabled; reading .Value may throw
            return element.Properties.IsEnabled.Value;
        }
        catch (Exception ex)
        {
            // Property not supported or retrieval failed; log at debug and assume enabled
            Log.Debug(ex, "IsEnabled property not supported; assuming element is enabled.");
            return true;
        }
    }

    /// <summary>
    /// Waits for potential navigation after a click and returns the final URL
    /// </summary>
    /// <param name="initialUrl">The URL before the click</param>
    /// <returns>The final URL after waiting for navigation</returns>
    private async Task<string?> WaitForNavigationAndGetUrlAsync(string? initialUrl, IHostSession session)
    {
        try
        {
            // Monitor for navigation within the timeout period
            var navigationTask = MonitorForNavigation(initialUrl, session);
            var timeoutTask = Task.Delay(browserConfig.NavigationTimeoutMs);
            
            var completedTask = await Task.WhenAny(navigationTask, timeoutTask);
            
            if (completedTask == navigationTask)
            {
                // Navigation completed before timeout
                var result = await navigationTask;
                Log.Information("Navigation completed within timeout. Final URL: {FinalUrl}", result);
                return result;
            }
            else
            {
                // Timeout reached, get current URL
                string? currentUrl = await GetCurrentUrlAsync(session);
                Log.Debug("Navigation timeout reached. Current URL: {CurrentUrl}", currentUrl);
                return currentUrl;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error waiting for navigation. Returning initial URL.");
            return initialUrl;
        }
    }

    /// <summary>
    /// Monitors for navigation changes and returns when navigation is complete or stable
    /// </summary>
    /// <param name="initialUrl">The initial URL before the click</param>
    /// <returns>The final stable URL after navigation</returns>
    private async Task<string?> MonitorForNavigation(string? initialUrl, IHostSession session)
    {
        const int checkIntervalMs = 50; // Check every 50ms for faster detection
        const int stableChecksRequired = 1; // URL must be stable for 3 consecutive checks
        
        string? lastUrl = initialUrl;
        string? currentUrl = initialUrl;
        int stableCount = 0;
        bool navigationDetected = false;

        while (true)
        {
            await Task.Delay(checkIntervalMs);
            currentUrl = await GetCurrentUrlAsync(session);
            
            // Check if URL has changed from initial (navigation started)
            if (!navigationDetected && !string.Equals(initialUrl, currentUrl, StringComparison.OrdinalIgnoreCase))
            {
                navigationDetected = true;
                Log.Information("Navigation detected. Initial URL: {InitialUrl}, New URL: {CurrentUrl}", initialUrl, currentUrl);
                lastUrl = currentUrl;
                stableCount = 1;
                continue;
            }
            
            // If no navigation detected yet, continue monitoring
            if (!navigationDetected)
            {
                continue;
            }
            
            // Check if URL is stable (navigation completed)
            if (string.Equals(lastUrl, currentUrl, StringComparison.OrdinalIgnoreCase))
            {
                stableCount++;
                if (stableCount >= stableChecksRequired)
                {
                    Log.Debug("Navigation completed and stable. Final URL: {FinalUrl}", currentUrl);
                    return currentUrl;
                }
            }
            else
            {
                // URL changed again, reset stability counter
                stableCount = 1;
                lastUrl = currentUrl;
                Log.Debug("URL changed during navigation. New URL: {CurrentUrl}", currentUrl);
            }
        }
    }

    
#endif

}
