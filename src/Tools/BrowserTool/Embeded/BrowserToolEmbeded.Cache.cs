using Avalonia.Threading;
using Serilog;
using DaoStudio.Interfaces.Plugins;

#if WINDOWS
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
#endif

namespace BrowserTool;

/// <summary>
/// Cache management functionality for BrowserToolEmbeded
/// </summary>
internal partial class BrowserToolEmbeded
{
    /// <summary>
    /// Gets the cached tree node root for a session
    /// </summary>
    private CachedTreeNode? GetCachedTreeNodeRoot(IHostSession session)
    {
        if (browserConfig.EnableSessionAware)
        {
            return _sessionCachedTreeRoots.TryGetValue(session.Id, out var root) ? root : null;
        }
        return cachedTreeNodeRoot;
    }

    /// <summary>
    /// Sets the cached tree node root for a session
    /// </summary>
    private void SetCachedTreeNodeRoot(IHostSession session, CachedTreeNode? root)
    {
        if (browserConfig.EnableSessionAware)
        {
            _sessionCachedTreeRoots[session.Id] = root;
        }
        else
        {
            cachedTreeNodeRoot = root;
        }
    }

    /// <summary>
    /// Gets the automation instance for a session
    /// </summary>
    
#if WINDOWS
    private UIA3Automation? GetAutomation(IHostSession session)
    {
        if (browserConfig.EnableSessionAware)
        {
            return _sessionAutomations.TryGetValue(session.Id, out var a) ? a : null;
        }
        return cachedAutomation;
    }
    
    #endif

    /// <summary>
    /// Sets the automation instance for a session
    /// </summary>
    
#if WINDOWS
    private void SetAutomation(IHostSession session, UIA3Automation? automation)
    {
        if (browserConfig.EnableSessionAware)
        {
            _sessionAutomations[session.Id] = automation;
        }
        else
        {
            cachedAutomation = automation;
        }
    }
    #endif

    /// <summary>
    /// Clears the cache for a session
    /// </summary>
    private void ClearSessionCache(IHostSession session)
    {
        if (browserConfig.EnableSessionAware)
        {
            _sessionCachedTreeRoots[session.Id] = null;
            
#if WINDOWS
            if (_sessionAutomations.TryGetValue(session.Id, out var a) && a != null)
            {
                try { a.Dispose(); } catch { }
            }
            _sessionAutomations[session.Id] = null;
#endif
        }
        else
        {
            cachedTreeNodeRoot = null;
            
#if WINDOWS
            if (cachedAutomation != null)
            {
                try { cachedAutomation.Dispose(); } catch { }
            }
            cachedAutomation = null;
#endif
        }
    }

#if WINDOWS
    /// <summary>
    /// Creates a CachedTreeNode tree from an automation element. 
    /// </summary>
    /// <param name="element">The automation element to create a node from</param>
    /// <param name="nextId">Reference to the next ID to assign</param>
    /// <returns>The created CachedTreeNode</returns>
    private CachedTreeNode CreateCachedTreeNodeTree(AutomationElement element, ref long nextId)
    {
        var rootNode = new CachedTreeNode
        {
            Id = nextId++,
            Node = element
        };

        // Use a stack to track nodes to process
        var stack = new Stack<Tuple<CachedTreeNode, AutomationElement>>();
        stack.Push(new Tuple<CachedTreeNode, AutomationElement>(rootNode, element));

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var currentNode = current.Item1;
            var currentElement = current.Item2;

            try
            {
                // Get all children of the current element
                var children = currentElement.FindAllChildren();

                // Process each child
                foreach (var childElement in children)
                {
                    string name = "";
                    try
                    {
                        name = childElement.Name;
                    }
                    catch
                    {
                    }
                    string aid = "";
                    try
                    {
                        aid = childElement.AutomationId;
                    }
                    catch
                    {
                    }
                    var childNode = new CachedTreeNode
                    {
                        Id = nextId++,
                        Node = childElement,
#if DEBUG
                        Text = $"{name} _ {aid}"
#endif
                    };

                    // Add to parent's children list
                    currentNode.Children.Add(childNode);

                    // Push to stack for processing its children
                    stack.Push(new Tuple<CachedTreeNode, AutomationElement>(childNode, childElement));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing automation element children");
                // Continue with other nodes even if one fails
            }
        }

        return rootNode;
    }

    /// <summary>
    /// Session-aware version of tree generation targeting the session's active tab
    /// </summary>
    private async Task<CachedTreeNode?> GenerateCachedTreeNodeTreeForSession(IHostSession session, bool force = false)
    {
        Log.Debug("Generating cached tree for session {SessionId}. Force: {Force}", session.Id, force);
        if (browserWindow == null)
        {
            Log.Error("Browser window is null");
            return null;
        }

        try
        {
            if (force)
            {
                ClearSessionCache(session);
            }

            var existingRoot = GetCachedTreeNodeRoot(session);
            var existingAutomation = GetAutomation(session);
            if (existingRoot != null && existingAutomation != null)
            {
                Log.Debug("Using existing cached tree for session {SessionId}", session.Id);
                return existingRoot;
            }

            var automation = existingAutomation;
            if (automation == null)
            {
                automation = new();
                SetAutomation(session, automation);
            }

            var cefBrowser = browserWindow!.browser?.CefBrowser;
            if (cefBrowser == null)
            {
                Log.Error("CEF browser is null after waiting for initialization for session {SessionId}", session.Id);
                return null;
            }

            // Get the native browser handle
            var cefBrowserHost = cefBrowser.GetHost();
            var windowHandle = cefBrowserHost.GetWindowHandle();

            // Get the browser window with FlaUI
            AutomationElement? window = null;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                window = GetAutomation(session)!.FromHandle((IntPtr)windowHandle);
            });
            if (window == null)
            {
                throw new InvalidOperationException("Failed to get automation element from browser handle");
            }

            // Generate the tree starting with ID 1
            long nextId = 1;
            var newRoot = CreateCachedTreeNodeTree(window, ref nextId);
            SetCachedTreeNodeRoot(session, newRoot);
            Log.Information("Successfully generated cached tree with {Count} nodes for session {SessionId}", nextId - 1, session.Id);
            return newRoot;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating cached tree for session {SessionId}", session.Id);
            var a = GetAutomation(session);
            if (a != null)
            {
                try { a.Dispose(); } catch { }
                SetAutomation(session, null);
            }
            SetCachedTreeNodeRoot(session, null);
            return null;
        }
    }
#endif
}
