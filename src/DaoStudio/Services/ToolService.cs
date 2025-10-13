using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DaoStudio.Interfaces;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using DaoStudio.Common.Plugins;

namespace DaoStudio.Services
{
    /// <summary>
    /// Service implementation for LLM tool operations
    /// </summary>
    public class ToolService : IToolService
    {
        private readonly ILlmToolRepository toolRepository;
        private readonly ILogger<ToolService> logger;

        public ToolService(ILlmToolRepository toolRepository, ILogger<ToolService> logger)
        {
            this.toolRepository = toolRepository ?? throw new ArgumentNullException(nameof(toolRepository));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Events

        /// <summary>
        /// Event raised when a tool is created, updated, or deleted
        /// </summary>
        public event EventHandler<ToolOperationEventArgs>? ToolChanged;

        /// <summary>
        /// Event raised when the tool list is updated (tools added or removed)
        /// </summary>
        public event EventHandler<ToolListUpdateEventArgs>? ToolListUpdated;

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
        public async Task<ITool> CreateToolAsync(string name, string description, string staticId, string toolConfig = "", Dictionary<string, string>? parameters = null, bool isEnabled = true, long appId = 0)
        {
            try
            {
                var tool = new Tool()
                {
                    Name = name,
                    Description = description,
                    StaticId = staticId,
                    ToolConfig = toolConfig,
                    Parameters = parameters ?? new Dictionary<string, string>(),
                    IsEnabled = isEnabled,
                    AppId = appId
                };

                // Convert ITool to LlmTool for database operations
                var llmTool = (LlmTool)tool;
                llmTool.LastModified = DateTime.UtcNow;
                llmTool.CreatedAt = DateTime.UtcNow;

                var createdLlmTool = await toolRepository.CreateToolAsync(llmTool);
                
                // Convert back to ITool for return and events
                var createdTool = new Tool(createdLlmTool);

                // Raise the ToolChanged event with Created operation type
                ToolChanged?.Invoke(this, new ToolOperationEventArgs(ToolOperationType.Created, createdTool));

                // Raise the ToolListUpdated event since a new tool was added
                ToolListUpdated?.Invoke(this, new ToolListUpdateEventArgs(ToolListUpdateType.Added, createdTool));

                return createdTool;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating tool: {ToolName}", name);
                throw;
            }
        }


        /// <summary>
        /// Get a tool by ID
        /// </summary>
        /// <param name="id">The ID of the tool</param>
        /// <returns>LLM tool or null if not found</returns>
        public async Task<ITool?> GetToolAsync(long id)
        {
            try
            {
                var llmTool = await toolRepository.GetToolAsync(id);
                return llmTool != null ? new Tool(llmTool) : null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting tool: {ToolId}", id);
                throw;
            }
        }

        /// <summary>
        /// Get all tools
        /// </summary>
        /// <returns>List of all tools</returns>
        public async Task<IEnumerable<ITool>> GetAllToolsAsync()
        {
            try
            {
                var llmTools = await toolRepository.GetAllToolsAsync();
                return llmTools.Select(llmTool => new Tool(llmTool));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting all tools");
                throw;
            }
        }

        /// <summary>
        /// Get tools by their static ID
        /// </summary>
        /// <param name="staticId">The static ID of the tools</param>
        /// <returns>List of LLM tools matching the static ID</returns>
        public async Task<IEnumerable<ITool>> GetToolsByStaticIdAsync(string staticId)
        {
            try
            {
                var llmTools = await toolRepository.GetToolsByStaticIdAsync(staticId);
                return llmTools.Select(llmTool => new Tool(llmTool));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting tools by static ID: {StaticId}", staticId);
                throw;
            }
        }

        /// <summary>
        /// Update an existing tool
        /// </summary>
        /// <param name="tool">The tool to update</param>
        /// <returns>True if successful</returns>
        public async Task<bool> UpdateToolAsync(ITool tool)
        {
            try
            {
                // Look up the existing LlmTool by id to avoid unsafe casting from ITool
                var existing = await toolRepository.GetToolAsync(tool.Id);

                if (existing == null)
                {
                    logger.LogWarning("Attempted to update tool that does not exist: {ToolId}", tool.Id);
                    return false;
                }

                // Merge values from the incoming ITool into the stored LlmTool where appropriate.
                // We assume Tool (implementation of ITool) has matching properties; copy only intended fields.
                existing.Name = tool.Name;
                existing.Description = tool.Description;
                existing.StaticId = tool.StaticId;
                existing.ToolConfig = tool.ToolConfig;
                existing.Parameters = tool.Parameters ?? new System.Collections.Generic.Dictionary<string, string>();
                existing.IsEnabled = tool.IsEnabled;
                existing.AppId = tool.AppId;
                existing.LastModified = DateTime.UtcNow;

                var success = await toolRepository.SaveToolAsync(existing);

                if (success)
                {
                    // Raise the ToolChanged event with Updated operation type
                    ToolChanged?.Invoke(this, new ToolOperationEventArgs(ToolOperationType.Updated, new Tool(existing)));
                }

                return success;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating tool: {ToolName} (ID: {ToolId})", tool.Name, tool.Id);
                throw;
            }
        }

        /// <summary>
        /// Delete a tool by ID
        /// </summary>
        /// <param name="id">The ID of the tool to delete</param>
        /// <returns>True if successful</returns>
        public async Task<bool> DeleteToolAsync(long id)
        {
            try
            {
                var success = await toolRepository.DeleteToolAsync(id);

                if (success)
                {
                    // Raise the ToolChanged event with Deleted operation type
                    ToolChanged?.Invoke(this, new ToolOperationEventArgs(id));

                    // Raise the ToolListUpdated event since a tool was removed
                    ToolListUpdated?.Invoke(this, new ToolListUpdateEventArgs(ToolListUpdateType.Removed, id));
                }

                return success;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting tool: {ToolId}", id);
                throw;
            }
        }

        #endregion
    }
}