using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System;

namespace DaoStudio.Plugins
{
    /// <summary>
    /// Factory implementation for creating PlainAIFunction instances through dependency injection
    /// </summary>
    internal class PlainAIFunctionFactory : IPlainAIFunctionFactory
    {
        private readonly IMessageService _messageService;
        private readonly ILogger<HostSessionAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the PlainAIFunctionFactory class
        /// </summary>
        /// <param name="messageService">The message service for creating HostSessionAdapter</param>
        /// <param name="logger">The logger for HostSessionAdapter</param>
        public PlainAIFunctionFactory(IMessageService messageService, ILogger<HostSessionAdapter> logger)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new PlainAIFunction instance with the specified parameters
        /// </summary>
        /// <param name="functionWithDescription">The function description and delegate</param>
        /// <param name="session">The session context</param>
        /// <returns>A new AIFunction instance (specifically PlainAIFunction)</returns>
        public AIFunction Create(FunctionWithDescription functionWithDescription, ISession session)
        {
            if (functionWithDescription == null)
                throw new ArgumentNullException(nameof(functionWithDescription));
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            // Convert ISession to IHostSession using HostSessionAdapter
            var hostSession = new HostSessionAdapter(session, _messageService, _logger);
            
            return new PlainAIFunction(functionWithDescription, hostSession);
        }
    }
}
