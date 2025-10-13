using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Newtonsoft.Json;
using Serilog;
using System.ComponentModel;


namespace BrowserTool;

// Class to represent a node in the DOM tree
public class ElementTreeNode
{
    [Description("Current web page address")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? CurrentUrl { get; set; } = null;

    [Description("Element text content")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? T { get; set; } = "";

    [Description("Unique element identifier used for click operations. This Id is invisible for users. Use hierarchy to describe node to users")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public long C { get; set; }

    [Description("Child elements nested within this element")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public List<ElementTreeNode>? S { get; set; }

    [Description("Operation status or error message")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? Result { get; set; }

    [Description("X coordinate of the element")]
    [JsonIgnore]
    public double X { get; set; }

    [Description("Y coordinate of the element")]
    [JsonIgnore]
    public double Y { get; set; }

    [Description("Width of the element")]
    [JsonIgnore]
    public double Width { get; set; }

    [Description("Height of the element")]
    [JsonIgnore]
    public double Height { get; set; }

}

internal partial class BrowserToolEmbeded 
{

#if WINDOWS
    [DisplayName("browser_get_current_page_content")]
    [Description("Extracts and returns the current webpage content as a hierarchical tree structure")]
    public async Task<ElementTreeNode> GetCurrentPageContent(IHostSession hostSession)
    {
        long? elementid = null;
        int levels = int.MaxValue;
        Log.Information("Getting current page content for session {SessionId}. ElementId: {ElementId}, Levels: {Levels}", hostSession.Id, elementid, levels);
        var result = new ElementTreeNode
        {
            Result = "Success",
            X = 0,
            Y = 0,
            Width = 0,
            Height = 0
        };
        try
        {
            result.CurrentUrl = await GetCurrentUrl(hostSession);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting current URL for session {SessionId}", hostSession.Id);
            result.Result = $"Error getting current URL: {ex.Message}";
            return result;
        }

        try
        {
            // Handle session-aware behavior
            if (browserConfig.EnableSessionAware)
            {
                var sessionValidationResult = await EnsureSessionHasValidTab(hostSession);
                if (!sessionValidationResult.Success)
                {
                    result.Result = Properties.Resources.Error_NoWebPageOpen;
                    return result;
                }
            }

            // Wait for the page to fully load before extracting content
            await WaitForPageFullyLoadedAsync(hostSession);
            Log.Debug("Page fully loaded, proceeding with content extraction");

            // Step 1: Generate cached tree
            CachedTreeNode? cachedTree = null;
            var existingRoot = GetCachedTreeNodeRoot(hostSession);
            if (existingRoot == null)
            {
                Log.Debug("Generating new cached tree");
                cachedTree = await Task.Run(() => GenerateCachedTreeNodeTreeForSession(hostSession)).ConfigureAwait(false);
                if (cachedTree == null)
                {
                    Log.Error("Failed to generate cached tree");
                    result.Result = "Failed to generate cached tree";
                    return result;
                }
            }
            else
            {
                Log.Debug("Using existing cached tree");
                cachedTree = existingRoot;
            }

            // Find the starting node if elementid is provided
            CachedTreeNode? startNode = cachedTree;
            if (elementid != null)
            {
                Log.Debug("Finding node with ID: {ElementId}", elementid);
                startNode = cachedTree != null ? FindNodeById(cachedTree, elementid.Value) : null;
                if (startNode == null)
                {
                    Log.Error("Element with ID {ElementId} not found", elementid);
                    result.Result = $"Element with ID {elementid} not found";
                    return result;
                }
            }

            Log.Debug("Starting ConvertToElementTreeNode");
            // Step 2: Generate ElementTreeNode from the cached tree
            var rootNode = await Task.Run(() => ConvertToElementTreeNode(startNode!, 0, levels) ?? new ElementTreeNode() { X = 0, Y = 0, Width = 0, Height = 0 }).ConfigureAwait(false);

            // Set the current URL in the root node
            rootNode.CurrentUrl = await GetCurrentUrl(hostSession);

            // Step 3: Simplify the tree by joining adjacent text-only nodes
            //CombineText(rootNode);
            //CombineTextByXCoordinate(rootNode);


            // Step 4: Remove redundant single child nodes with same text
            RemoveRedundantSingleChildNodes(rootNode);

            // Step 5: Remove empty intermediate nodes
            RemoveEmptyIntermediateNodes(rootNode);

            // Step 6: Remove redundant parent text when it matches combined children text
            RemoveRedundantParentText(rootNode);


            Log.Information("Successfully retrieved page content");
            return rootNode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while retrieving page content");
            result.Result = $"Error while retrieving page content: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Finds a node by its ID in the cached tree
    /// </summary>
    /// <param name="rootNode">The root node to start searching from</param>
    /// <param name="id">The ID to find</param>
    /// <returns>The found node, or null if not found</returns>
    private CachedTreeNode? FindNodeById(CachedTreeNode rootNode, long id)
    {
        if (rootNode.Id == id)
        {
            return rootNode;
        }

        foreach (var child in rootNode.Children)
        {
            var found = FindNodeById(child, id);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a CachedTreeNode to an ElementTreeNode, applying filtering rules
    /// </summary>
    /// <param name="cachedNode">The cached node to convert</param>
    /// <param name="currentLevel">The current depth level</param>
    /// <param name="maxLevels">The maximum depth level to process</param>
    /// <returns>The converted ElementTreeNode, or null if it should be filtered out</returns>
    private ElementTreeNode? ConvertToElementTreeNode(CachedTreeNode cachedNode, int currentLevel, int maxLevels)
    {
        if (currentLevel > maxLevels || cachedNode == null)
        {
            return null;
        }

        var automationElement = cachedNode.Node;
        if (automationElement == null)
        {
            return null;
        }

        try
        {
            bool clickable = false;
            try
            {
                clickable = automationElement.Patterns.Invoke.IsSupported && automationElement.Properties.IsKeyboardFocusable.Value;
            }
            catch { }
            string text = string.Empty;
            try
            {
                text = automationElement.Name;
            }
            catch { }
            var elementNode = new ElementTreeNode
            {
                C = clickable ? cachedNode.Id : 0,
                T = string.IsNullOrWhiteSpace(text) ? null : text,
            };

            // Get element bounds
            try
            {
                var boundingRectangle = automationElement.BoundingRectangle;
                elementNode.X = boundingRectangle.X;
                elementNode.Y = boundingRectangle.Y;
                elementNode.Width = boundingRectangle.Width;
                elementNode.Height = boundingRectangle.Height;
            }
            catch 
            { 
                // If bounds are not available, use default values
                elementNode.X = 0;
                elementNode.Y = 0;
                elementNode.Width = 0;
                elementNode.Height = 0;
            }


            // Process children if we haven't reached max depth
            if (currentLevel < maxLevels)
            {
                foreach (var cachedChild in cachedNode.Children)
                {
                    var childNode = ConvertToElementTreeNode(cachedChild, currentLevel + 1, maxLevels);
                    if (childNode != null)
                    {
                        if (elementNode.S == null)
                        {
                            elementNode.S = new();
                        }
                        elementNode.S.Add(childNode);
                    }
                }
            }

            if ((!string.IsNullOrWhiteSpace(elementNode.T)) || (elementNode.S?.Count > 0) || (elementNode.C > 0))
            {
                return elementNode;
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing node with ID: {Id}", cachedNode.Id);
            // If there's an error processing this node, skip it
            return null;
        }
    }

    /// <summary>
    /// Simplifies the element tree by joining adjacent sequential text-only nodes
    /// </summary>
    /// <param name="node">The root node to simplify</param>
    private void CombineText(ElementTreeNode node)
    {
        if (node?.S == null || node.S.Count <1)
        {
            return;
        }

        // Recursively simplify children first
        foreach (var child in node.S)
        {
            CombineText(child);
        }

        // Join adjacent text-only nodes
        var simplifiedChildren = new List<ElementTreeNode>();
        for (int i = 0; i < node.S.Count; i++)
        {
            var current = node.S[i];
            
            // Check if this is a text-only node (C=0, S=null or empty, Result=null or empty)
            if (IsTextOnlyNode(current))
            {
                if (current.T ==null)
                {
                    continue;
                }
                var combinedText = current.T; 
                
                // Look ahead for consecutive text-only nodes
                int j = i + 1;
                while (j < node.S.Count && IsTextOnlyNode(node.S[j]))
                {
                    var nextText = node.S[j].T ?? "";
                    if (!string.IsNullOrEmpty(nextText))
                    {
                        // Insert a space when the last char of the current text and the first char
                        // of the next text are both non-whitespace to avoid “HelloWorld”.
                        if (combinedText.Length > 0 &&
                            !char.IsWhiteSpace(combinedText[^1]) &&
                            !char.IsWhiteSpace(nextText[0]))
                        {
                            combinedText += " ";
                        }
                        combinedText += nextText;
                    }
                    j++;
                }
                
                // Create a new combined node if we found multiple text-only nodes
                if (j > i + 1)
                {
                    var combinedNode = new ElementTreeNode
                    {
                        T = combinedText,
                        C = 0,
                        S = null,
                        Result = null,
                        X = 0,
                        Y = 0,
                        Width = 0,
                        Height = 0
                    };
                    simplifiedChildren.Add(combinedNode);
                    i = j - 1; // Skip the nodes we've combined
                }
                else
                {
                    // Single text-only node, add as is
                    simplifiedChildren.Add(current);
                }
            }
            else
            {
                // Non-text-only node, add as is
                simplifiedChildren.Add(current);
            }
        }
        
        node.S = simplifiedChildren;
    }

    /// <summary>
    /// Checks if a node is a text-only node (C=0, S=null or empty, Result=null or empty)
    /// </summary>
    /// <param name="node">The node to check</param>
    /// <returns>True if the node is text-only</returns>
    private bool IsTextOnlyNode(ElementTreeNode node)
    {
        return node.C == 0 && 
               (node.S == null || node.S.Count == 0) && 
               string.IsNullOrEmpty(node.Result);
    }

    /// <summary>
    /// Combines text-only nodes that have the same X coordinate
    /// </summary>
    /// <param name="node">The root node to process</param>
    private void CombineTextByXCoordinate(ElementTreeNode node)
    {
        if (node?.S == null || node.S.Count < 1)
        {
            return;
        }

        // Recursively process children first
        foreach (var child in node.S)
        {
            CombineTextByXCoordinate(child);
        }

        // Group text-only nodes by their X coordinate
        var groupedByX = new Dictionary<double, List<ElementTreeNode>>();
        var nonTextNodes = new List<ElementTreeNode>();

        for (int i = 0; i < node.S.Count; i++)
        {
            var current = node.S[i];
            
            if (IsTextOnlyNode(current) && !string.IsNullOrEmpty(current.T) && current.X > 0)
            {
                if (!groupedByX.ContainsKey(current.X))
                {
                    groupedByX[current.X] = new List<ElementTreeNode>();
                }
                groupedByX[current.X].Add(current);
            }
            else
            {
                nonTextNodes.Add(current);
            }
        }

        // Create combined nodes for each X coordinate group
        var combinedChildren = new List<ElementTreeNode>();
        
        // Add non-text nodes first to maintain order
        combinedChildren.AddRange(nonTextNodes);

        // Process each X coordinate group
        foreach (var kvp in groupedByX.OrderBy(x => x.Key))
        {
            var xCoordinate = kvp.Key;
            var textNodes = kvp.Value;

            if (textNodes.Count == 1)
            {
                // Single node, add as is
                combinedChildren.Add(textNodes[0]);
            }
            else
            {
                // Multiple nodes with same X, combine their text
                var combinedText = "";
                double minY = double.MaxValue;
                double maxHeight = 0;
                double width = 0;

                foreach (var textNode in textNodes.OrderBy(n => n.Y))
                {
                    if (!string.IsNullOrEmpty(textNode.T))
                    {
                        // Insert a space when the last char of the current text and the first char
                        // of the next text are both non-whitespace to avoid "HelloWorld".
                        if (combinedText.Length > 0 &&
                            !char.IsWhiteSpace(combinedText[^1]) &&
                            !char.IsWhiteSpace(textNode.T[0]))
                        {
                            combinedText += " ";
                        }
                        combinedText += textNode.T;
                    }

                    // Calculate bounding box
                    minY = Math.Min(minY, textNode.Y);
                    maxHeight = Math.Max(maxHeight, textNode.Height);
                    width = Math.Max(width, textNode.Width);
                }

                // Create combined node
                var combinedNode = new ElementTreeNode
                {
                    T = combinedText,
                    C = 0,
                    S = null,
                    Result = null,
                    X = xCoordinate,
                    Y = minY,
                    Width = width,
                    Height = maxHeight
                };
                combinedChildren.Add(combinedNode);
            }
        }

        node.S = combinedChildren;
    }

    /// <summary>
    /// Removes redundant single child nodes that have the same text as their parent.
    /// If the child node is clickable (C > 0) and has no children (S is null or empty),
    /// the parent's children list is set to null.
    /// </summary>
    /// <param name="node">The root node to process</param>
    private void RemoveRedundantSingleChildNodes(ElementTreeNode node)
    {
        if (node?.S == null || node.S.Count == 0)
        {
            return;
        }

        // Recursively process children first
        foreach (var child in node.S)
        {
            RemoveRedundantSingleChildNodes(child);
        }

        // Check if this node has exactly one child with the same text
        if (node.S.Count == 1)
        {
            var child = node.S[0];

            // Check if the child is clickable and has no children
            if ((child.C == 0) && (child.S == null || child.S.Count == 0))
            {
                // Check if the child has the same text as the parent
                if (string.Equals(node.T, child.T, StringComparison.OrdinalIgnoreCase))
                {
                    // Remove the children list from the current node
                    node.S = null;
                }
            }
        }
    }

    /// <summary>
    /// Removes empty intermediate nodes that have no text (T=null), are not clickable (C=0),
    /// and have only one child. The child node replaces the empty intermediate node.
    /// </summary>
    /// <param name="node">The root node to process</param>
    private void RemoveEmptyIntermediateNodes(ElementTreeNode node)
    {
        if (node?.S == null || node.S.Count == 0)
        {
            return;
        }


        // Process the children list to remove empty intermediate nodes
        for (int i = 0; i < node.S.Count; i++)
        {
            var child = node.S[i];
            if (child.S==null)
            {
                continue;
            }

            // Check if the child is an empty intermediate node
            if (string.IsNullOrEmpty(child.T) && child.C == 0 )
            {
                if (child.S.Count == 1)
                {
                    // Replace the empty intermediate node with its single child
                    node.S[i] = child.S[0];
                }
                else if (node.S.Count == 1)
                {
                    node.S = child.S;
                }

            }
            // Process the replacement node recursively in case it's also an empty intermediate
            RemoveEmptyIntermediateNodes(node.S[i]);
        }
    }

    /// <summary>
    /// Removes redundant parent text when it matches the combined text of all its children.
    /// This handles cases where a parent node's text is formed by joining all its children's text content.
    /// If the parent has non-empty text (T), is clickable (C > 0), has children, and all children are text-only nodes
    /// whose combined text equals the parent's text, then the parent's text is set to null.
    /// </summary>
    /// <param name="node">The root node to process</param>
    private void RemoveRedundantParentText(ElementTreeNode node)
    {
        if (node?.S == null || node.S.Count == 0)
        {
            return;
        }

        // Recursively process children first
        foreach (var child in node.S)
        {
            RemoveRedundantParentText(child);
        }

        // Check if this node has non-empty text, is clickable, and has children
        if (!string.IsNullOrEmpty(node.T) && node.C > 0 && node.S.Count > 0)
        {
            // Check if all children are text-only nodes (C=0, S=null or empty)
            bool allChildrenAreTextOnly = node.S.All(child => 
                child.C == 0 && (child.S == null || child.S.Count == 0));

            if (allChildrenAreTextOnly)
            {
                // Try to "subtract" each child's text from the parent text
                var remainingParentText = node.T.Trim();
                bool allChildrenMatchParent = true;

                foreach (var child in node.S)
                {
                    if (!string.IsNullOrEmpty(child.T))
                    {
                        var childText = child.T.Trim();
                        
                        // Check if the remaining parent text starts with this child's text (case-insensitive)
                        if (remainingParentText.StartsWith(childText, StringComparison.OrdinalIgnoreCase))
                        {
                            // "Subtract" the child text from the beginning of the remaining parent text
                            remainingParentText = remainingParentText.Substring(childText.Length).Trim();
                        }
                        else
                        {
                            // Child text doesn't match the expected position in parent text
                            allChildrenMatchParent = false;
                            break;
                        }
                    }
                }

                // If all children's text was successfully "subtracted" and nothing remains, remove parent text
                if (allChildrenMatchParent && string.IsNullOrEmpty(remainingParentText))
                {
                    node.T = null;
                }
            }
        }
    }
#endif

}
