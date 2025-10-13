using DaoStudio.Interfaces.Plugins;
using Microsoft.Extensions.AI;

namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Factory interface for creating PlainAIFunction instances through dependency injection
    /// </summary>
    internal interface IPlainAIFunctionFactory
    {
        /// <summary>
        /// Creates a new PlainAIFunction instance with the specified parameters
        /// </summary>
        /// <param name="functionWithDescription">The function description and delegate</param>
        /// <param name="session">The session context</param>
        /// <returns>A new AIFunction instance (specifically PlainAIFunction)</returns>
        AIFunction Create(FunctionWithDescription functionWithDescription, ISession session);
    }
}
