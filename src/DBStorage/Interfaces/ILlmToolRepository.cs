using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Interfaces
{
    /// <summary>
    /// Interface for LLM tool repository operations
    /// </summary>
    public interface ILlmToolRepository
    {
        /// <summary>
        /// Get a tool by ID
        /// </summary>
        /// <param name="id">The ID of the tool</param>
        /// <returns>LLM tool or null if not found</returns>
        Task<LlmTool?> GetToolAsync(long id);

        /// <summary>
        /// Create a new tool
        /// </summary>
        /// <param name="tool">The tool to create</param>
        /// <returns>The created tool with assigned ID</returns>
        Task<LlmTool> CreateToolAsync(LlmTool tool);

        /// <summary>
        /// Save a tool
        /// </summary>
        /// <param name="tool">The tool to save</param>
        /// <returns>True if successful</returns>
        Task<bool> SaveToolAsync(LlmTool tool);

        /// <summary>
        /// Delete a tool
        /// </summary>
        /// <param name="id">The ID of the tool to delete</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteToolAsync(long id);

        /// <summary>
        /// Get all tools with optional state data loading
        /// </summary>
        /// <param name="includeStateData">Whether to include StateData BLOB field</param>
        /// <returns>List of all tools</returns>
        Task<IEnumerable<LlmTool>> GetAllToolsAsync(bool includeStateData = true);        /// <summary>
        /// Get tools by their static ID
        /// </summary>
        /// <param name="staticId">The static ID of the tools</param>
        /// <returns>List of LLM tools matching the static ID</returns>
        Task<IEnumerable<LlmTool>> GetToolsByStaticIdAsync(string staticId);
    }
} 