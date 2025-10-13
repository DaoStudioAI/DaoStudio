using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DaoStudio.Common.Plugins
{
    /// <summary>
    /// Represents the result of a child session execution
    /// </summary>
    public class ChildSessionResult
    {
        /// <summary>
        /// Indicates whether the child session completed successfully
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if the child session failed
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Result string if the child session completed successfully
        /// </summary>
        public string? Result { get; set; }
        
        /// <summary>
        /// Creates a successful result with the given value
        /// </summary>
        /// <param name="result">The result value</param>
        /// <returns>A successful ChildSessionResult</returns>
        public static ChildSessionResult CreateSuccess(string result)
        {
            return new ChildSessionResult
            {
                Success = true,
                Result = result,
                ErrorMessage = null
            };
        }
        
        /// <summary>
        /// Creates an error result with the given error message
        /// </summary>
        /// <param name="errorMessage">The error message</param>
        /// <returns>An error ChildSessionResult</returns>
        public static ChildSessionResult CreateError(string errorMessage)
        {
            return new ChildSessionResult
            {
                Success = false,
                Result = null,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Creates an error result with the given error message
        /// </summary>
        /// <param name="errorMessage">The error message</param>
        /// <returns>An error ChildSessionResult</returns>
        public static ChildSessionResult CreateErrorReport(string errorMessage)
        {
            return new ChildSessionResult
            {
                Success = false,
                Result = null,
                ErrorMessage = errorMessage
            };
        }
    }
}