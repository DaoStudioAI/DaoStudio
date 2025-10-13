using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaoStudio.Interfaces.Plugins;

namespace DaoStudio.Interfaces
{
    public interface IEngine
    {
        /// <summary>
        /// The person (LLM configuration) this engine operates for
        /// </summary>
        IPerson Person { get; }

        /// <summary>
        /// Event raised when token usage details are available during processing
        /// </summary>
        event EventHandler<UsageDetails>? UsageDetailsReceived;

        /// <summary>
        /// Process a list of messages and stream responses back to the session
        /// </summary>
        /// <param name="messages">The conversation history</param>
        /// <param name="tools">Available tools for function calling</param>
        /// <param name="session">The session to pass to PlainAIFunction for tool execution</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of response messages.IMessage.Content couldn't coexist with IMessage.BinaryContent</returns>
        Task<IAsyncEnumerable<IMessage>> GetMessageAsync(
            List<IMessage> messages, 
            Dictionary<string, List<FunctionWithDescription>>? tools,
            ISession session,
            CancellationToken cancellationToken = default);
    }
}
