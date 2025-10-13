namespace DaoStudio.Interfaces;

/// <summary>
/// Service interface for LLM tool operations
/// </summary>
public interface IToolService
{
    #region Events

    /// <summary>
    /// Event raised when a tool is created, updated, or deleted
    /// </summary>
    event EventHandler<ToolOperationEventArgs>? ToolChanged;

    /// <summary>
    /// Event raised when the tool list is updated (tools added or removed)
    /// </summary>
    event EventHandler<ToolListUpdateEventArgs>? ToolListUpdated;

    #endregion

    #region Tool CRUD Operations

    /// <summary>
    /// Create a new LLM tool with individual parameters
    /// </summary>
    /// <param name="name">The name of the tool</param>
    /// <param name="description">The description of the tool</param>
    /// <param name="staticId">The static identifier of the tool</param>
    /// <param name="toolConfig">The tool configuration</param>
    /// <param name="parameters">The tool parameters</param>
    /// <param name="isEnabled">Whether the tool is enabled</param>
    /// <param name="appId">The app identifier</param>
    /// <returns>The created tool with assigned ID</returns>
    Task<ITool> CreateToolAsync(string name, string description, string staticId, string toolConfig = "", Dictionary<string, string>? parameters = null, bool isEnabled = true, long appId = 0);

    /// <summary>
    /// Get a tool by ID
    /// </summary>
    /// <param name="id">The ID of the tool</param>
    /// <returns>LLM tool or null if not found</returns>
    Task<ITool?> GetToolAsync(long id);

    /// <summary>
    /// Get all tools
    /// </summary>
    /// <returns>List of all tools</returns>
    Task<IEnumerable<ITool>> GetAllToolsAsync();

    /// <summary>
    /// Get tools by their static ID
    /// </summary>
    /// <param name="staticId">The static ID of the tools</param>
    /// <returns>List of LLM tools matching the static ID</returns>
    Task<IEnumerable<ITool>> GetToolsByStaticIdAsync(string staticId);

    /// <summary>
    /// Update an existing tool
    /// </summary>
    /// <param name="tool">The tool to update</param>
    /// <returns>True if successful</returns>
    Task<bool> UpdateToolAsync(ITool tool);

    /// <summary>
    /// Delete a tool by ID
    /// </summary>
    /// <param name="id">The ID of the tool to delete</param>
    /// <returns>True if successful</returns>
    Task<bool> DeleteToolAsync(long id);

    #endregion
}