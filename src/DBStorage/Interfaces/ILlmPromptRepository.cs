using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Models;

namespace DaoStudio.DBStorage.Interfaces
{
    /// <summary>
    /// Interface for LLM prompt repository operations
    /// </summary>
    public interface ILlmPromptRepository
    {
        /// <summary>
        /// Get a prompt by ID
        /// </summary>
        /// <param name="id">The ID of the prompt</param>
        /// <returns>LLM prompt or null if not found</returns>
        Task<LlmPrompt?> GetPromptAsync(long id);

        /// <summary>
        /// Create a new prompt
        /// </summary>
        /// <param name="prompt">The prompt to create</param>
        /// <returns>The created prompt with assigned ID</returns>
        Task<LlmPrompt> CreatePromptAsync(LlmPrompt prompt);

        /// <summary>
        /// Save a prompt
        /// </summary>
        /// <param name="prompt">The prompt to save</param>
        /// <returns>True if successful</returns>
        Task<bool> SavePromptAsync(LlmPrompt prompt);

        /// <summary>
        /// Delete a prompt
        /// </summary>
        /// <param name="id">The ID of the prompt to delete</param>
        /// <returns>True if successful</returns>
        Task<bool> DeletePromptAsync(long id);

        /// <summary>
        /// Get all prompts
        /// </summary>
        /// <returns>List of all prompts</returns>
        Task<IEnumerable<LlmPrompt>> GetAllPromptsAsync();

        /// <summary>
        /// Get prompts by category
        /// </summary>
        /// <param name="category">The category to filter by</param>
        /// <returns>List of prompts in the specified category</returns>
        Task<IEnumerable<LlmPrompt>> GetPromptsByCategoryAsync(string category);


    }
} 