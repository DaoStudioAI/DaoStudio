using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces.Plugins;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Naming
{
    /// <summary>
    /// Utility class for calculating delegation levels by traversing session hierarchy
    /// </summary>
    public static class NamingLevelCalculator
    {
        /// <summary>
        /// Calculate current delegation level by traversing session hierarchy
        /// </summary>
        /// <param name="currentSession">The current session</param>
        /// <param name="host">The host interface for accessing session data</param>
        /// <returns>Delegation level (0 = root, 1 = first delegation, etc.)</returns>
        public static async Task<int> CalculateCurrentLevelAsync(IHostSession currentSession, IHost host)
        {
            if (currentSession == null) throw new ArgumentNullException(nameof(currentSession));
            if (host == null) throw new ArgumentNullException(nameof(host));

            try
            {
                // Start from current session and traverse parent chain
                return await TraverseParentChainAsync(currentSession.ParentSessionId, host, 0);
            }
            catch (Exception)
            {
                // If calculation fails (e.g., broken hierarchy), fall back to 0 so that we err on the side of
                // caution and prevent unlimited delegation.
                return 0;
            }
        }

        /// <summary>
        /// Traverse parent session chain recursively to count delegation depth
        /// </summary>
        /// <param name="parentSessionId">Current parent session ID to traverse</param>
        /// <param name="host">The host interface for accessing session data</param>
        /// <param name="currentDepth">Current depth in the traversal</param>
        /// <returns>Total delegation level</returns>
        private static async Task<int> TraverseParentChainAsync(long? parentSessionId, IHost host, int currentDepth)
        {
            // No more parents – we've reached the root session.
            if (!parentSessionId.HasValue)
            {
                return currentDepth;
            }

            // Safety guard in case of circular references.
            if (currentDepth > 100)
            {
                return currentDepth;
            }

            try
            {
                // Try to open the parent session via the host so that we can continue walking up the chain.
                var parentSession = await host.OpenHostSession(parentSessionId.Value);
                var nextParentId = parentSession?.ParentSessionId;

                // Increment the depth and recurse.
                return await TraverseParentChainAsync(nextParentId, host, currentDepth + 1);
            }
            catch (Exception)
            {
                // If the parent session cannot be opened (e.g., missing from repository), assume one additional
                // level and stop traversal.
                return currentDepth + 1;
            }
        }
    }
}
