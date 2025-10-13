using System;
using System.Collections.Generic;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;

namespace DaoStudio;

/// <summary>
/// Extension methods for the Person model to provide tool configuration helpers
/// </summary>
internal static class PersonExtensions
{
    /// <summary>
    /// Determines if all tools are enabled for this person.
    /// Returns true if the UseAllTools parameter is not set (for backward compatibility) 
    /// or if it's explicitly set to "true".
    /// </summary>
    /// <param name="person">The person to check</param>
    /// <returns>True if all tools should be enabled, false if only specific tools should be enabled</returns>
    public static bool IsAllToolsEnabled(this IPerson person)
    {
        // Absence of the parameter means enabled (legacy behavior)
        return !person.Parameters.TryGetValue(PersonParameterNames.UseAllTools, out var value) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sets whether all tools are enabled for this person.
    /// When enabled, removes the UseAllTools parameter (for backward compatibility).
    /// When disabled, sets the UseAllTools parameter to "false".
    /// </summary>
    /// <param name="person">The person to update</param>
    /// <param name="enabled">True to enable all tools, false to enable only specific tools</param>
    public static void SetAllToolsEnabled(this IPerson person, bool enabled)
    {
        if (enabled)
        {
            // Remove the parameter when all tools are enabled (backward compatibility)
            person.Parameters.Remove(PersonParameterNames.UseAllTools);
        }
        else
        {
            // Set to false when only specific tools should be enabled
            person.Parameters[PersonParameterNames.UseAllTools] = "false";
        }
    }
}