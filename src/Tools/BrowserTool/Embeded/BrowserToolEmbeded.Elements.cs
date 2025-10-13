using Avalonia.Threading;
using Serilog;
using System.ComponentModel;
using System.Text;
using DaoStudio.Interfaces.Plugins;

#if WINDOWS
using FlaUI.Core.AutomationElements;
#endif

namespace BrowserTool;

/// <summary>
/// Element interaction functionality for BrowserToolEmbeded
/// </summary>
internal partial class BrowserToolEmbeded
{
#if WINDOWS
    [DisplayName("browser_get_element_info")]
    [Description("Retrieves detailed properties (ClassName, ControlType, interactivity state) for specified elements")]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task<List<ElementInfo>> GetElementInfo([Description("List of element IDs from get_current_page_content")] List<long> elementIds,
        IHostSession hostSession)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        Log.Information("Getting information for elements with IDs: {@ElementIds} for session {SessionId}", elementIds, hostSession.Id);
        var results = new List<ElementInfo>();

        // Handle session-aware behavior
        if (browserConfig.EnableSessionAware)
        {
            var sessionValidationResult = await EnsureSessionHasValidTab(hostSession);
            if (!sessionValidationResult.Success)
            {
                var errorInfo = new ElementInfo
                {
                    ElementId = -1,
                    Error = Properties.Resources.Error_NoWebPageOpen
                };
                results.Add(errorInfo);
                return results;
            }
        }

        if (browserWindow == null)
        {
            Log.Error("Browser window is null");
            var errorInfo = new ElementInfo
            {
                ElementId = -1,
                Error = Properties.Resources.Error_BrowserWindowNull
            };
            results.Add(errorInfo);
            return results;
        }

        try
        {
            // Check if we have a valid cached tree
            if (GetCachedTreeNodeRoot(hostSession) == null)
            {
                Log.Error("No cached tree available. Please get page content first");
                var errorInfo = new ElementInfo
                {
                    ElementId = -1,
                    Error = Properties.Resources.Error_GetPageContentFirst
                };
                results.Add(errorInfo);
                return results;
            }

            // Process each element ID
            foreach (var elementId in elementIds)
            {
                var elementInfo = new ElementInfo
                {
                    ElementId = elementId
                };

                try
                {
                    // Find the node by ID
                    var node = FindNodeById(GetCachedTreeNodeRoot(hostSession)!, elementId);
                    if (node == null)
                    {
                        Log.Error("Element with ID {ElementId} not found", elementId);
                        elementInfo.Error = $"Element with ID {elementId} not found";
                        results.Add(elementInfo);
                        continue;
                    }

                    var automationElement = node.Node;
                    var infoBuilder = new StringBuilder();

                    // Get className
                    try
                    {
                        string className = automationElement.Properties.ClassName.Value;
                        if (!string.IsNullOrWhiteSpace(className))
                        {
                            infoBuilder.AppendLine($"ClassName: {className}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to get ClassName for element {ElementId}", elementId);
                    }

                    // Get ControlType
                    try
                    {
                        var controlType = automationElement.Properties.ControlType.Value;
                        infoBuilder.AppendLine($"ControlType: {controlType}");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to get ControlType for element {ElementId}", elementId);
                    }

                    // Check for InvokePattern (invokable)
                    try
                    {
                        bool isInvokable = automationElement.Patterns.Invoke.IsSupported;
                        infoBuilder.AppendLine($"Invokable: {isInvokable}");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to check InvokePattern for element {ElementId}", elementId);
                    }

                    // Check for ExpandCollapsePattern (expandable)
                    try
                    {
                        bool isExpandable = automationElement.Patterns.ExpandCollapse.IsSupported;
                        infoBuilder.AppendLine($"Expandable: {isExpandable}");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to check ExpandCollapsePattern for element {ElementId}", elementId);
                    }

                    elementInfo.Information = infoBuilder.ToString().TrimEnd();
                    results.Add(elementInfo);
                    Log.Information("Successfully retrieved information for element {ElementId}", elementId);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error retrieving information for element {ElementId}", elementId);
                    elementInfo.Error = $"Error: {ex.Message}";
                    results.Add(elementInfo);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in GetElementInfo");
            var errorInfo = new ElementInfo
            {
                ElementId = -1,
                Error = $"General error: {ex.Message}"
            };
            results.Add(errorInfo);
            return results;
        }
    }
#endif
}
