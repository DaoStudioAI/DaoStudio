using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.DBStorage.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DaoStudio.Plugins
{
    /// <summary>
    /// Adapter that implements IHostSession by wrapping an ISession instance.
    /// This provides a minimal interface for Tools while delegating to the full ISession implementation.
    /// </summary>
    public class HostSessionAdapter : IHostSession
    {
        private readonly ISession _session;
        private readonly IMessageService _messageService;
        private readonly ILogger<HostSessionAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the HostSessionAdapter class
        /// </summary>
        /// <param name="session">The ISession instance to wrap</param>
        /// <param name="messageService">The message service for creating and saving messages</param>
        /// <param name="logger">The logger instance</param>
        /// <exception cref="ArgumentNullException">Thrown when session, messageService, or logger is null</exception>
        public HostSessionAdapter(ISession session, IMessageService messageService, ILogger<HostSessionAdapter> logger)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the underlying ISession instance that this adapter wraps
        /// </summary>
        public ISession UnderlyingSession => _session;

        /// <summary>
        /// Gets the unique identifier of the session
        /// </summary>
        public long Id => _session.Id;

        /// <summary>
        /// Gets the parent session ID if this is a child session
        /// </summary>
        public long? ParentSessionId => _session.ParentSessionId;

        /// <summary>
        /// Gets the current cancellation token source.
        /// Tools code accesses both .Cancel() and .Token
        /// </summary>
        public CancellationTokenSource? CurrentCancellationToken => _session.CurrentCancellationToken;

        /// <summary>
        /// Gets or sets the tool execution mode for chat interactions
        /// </summary>
        public ToolExecutionMode ToolExecutionMode 
        { 
            get => _session.ToolExecutionMode;
            set => _session.ToolExecutionMode = value;
        }

        /// <summary>
        /// Sends a message to the host session
        /// </summary>
        /// <param name="msgType">The type of message to send</param>
        /// <param name="message">The message content</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task SendMessageAsync(HostSessMsgType msgType, string message)
        {
            try
            {
                _logger.LogInformation("Sending host session message of type {MessageType} to session {SessionId}: {Message}", 
                    msgType, Id, message);

                switch (msgType)
                {
                    case HostSessMsgType.InfoForUserOnly:
                        await CreateInfoForUserOnlyMessageAsync(message);
                        break;

                    case HostSessMsgType.StatusUpdate:
                        await CreateStatusUpdateMessageAsync(message);
                        break;

                    case HostSessMsgType.Message:
                        // Call the main SendMessageAsync method to send to LLM immediately
                        await _session.SendMessageAsync(message);
                        break;

                    default:
                        _logger.LogWarning("Unknown HostSessMsgType: {MessageType}", msgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending host session message of type {MessageType} to session {SessionId}", 
                    msgType, Id);
                throw;
            }
        }

        /// <summary>
        /// Creates an information message that is only shown to the user (not sent to LLM)
        /// </summary>
        /// <param name="message">The message content</param>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task CreateInfoForUserOnlyMessageAsync(string message)
        {
            // Create message using messageService (do not persist yet, we will add binary data first)
            var iMessage = await _messageService.CreateMessageAsync(
                message, 
                MessageRole.System, 
                MessageType.Information,
                Id,
                false);

            // Set binary contents
            iMessage.BinaryContents = new List<Interfaces.IMsgBinaryData>
            {
                new MsgBinaryData
                {
                    Name = "MessageType",
                    Data = System.Text.Encoding.UTF8.GetBytes("InfoForUserOnly")
                }
            };
            
            // Set the Type through the interface property
            iMessage.BinaryContents[0].Type = MsgBinaryDataType.HostSessionMessage;

            // Save the message
            await _messageService.SaveMessageAsync(iMessage, true);

            // Notify about new message using event raiser to support multiple subscribers
            await _session.FireMessageChangedAsync(iMessage, MessageChangeType.New);
        }

        /// <summary>
        /// Creates a status update message that will be sent to LLM until next user message
        /// </summary>
        /// <param name="message">The message content</param>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task CreateStatusUpdateMessageAsync(string message)
        {
            var binaryContents = new List<DBStorage.Models.BinaryData>
            {
                new DBStorage.Models.BinaryData
                {
                    Type = (int) DaoStudio.Interfaces.MsgBinaryDataType.HostSessionMessage,
                    Data = System.Text.Encoding.UTF8.GetBytes("StatusUpdate"),
                    Name = "MessageType"
                }
            };

            var dbMessage = new DBStorage.Models.Message
            {
                SessionId = Id,
                Content = message,
                Role = (int)MessageRole.System,
                Type = (int)MessageType.Information,
                BinaryContents = binaryContents,
                CreatedAt = DateTime.UtcNow
            };

            // Convert to IMessage and use service
            var iMessage = DaoStudio.Message.FromDBMessage(dbMessage);
            await _messageService.SaveMessageAsync(iMessage, true);

            // Note: For status updates, we don't notify OnMessageChanged as they are sent to LLM until next user message
            _logger.LogDebug("Created status update message for session {SessionId}", Id);
        }

        /// <summary>
        /// Gets the custom tool registry for this session.
        /// Used by NamingTool for custom tool management.
        /// </summary>
        /// <returns>Dictionary of tool modules and their functions, or null if not set</returns>
        public Dictionary<string, List<FunctionWithDescription>>? GetTools()
        {
            return _session.GetTools();
        }

        /// <summary>
        /// Sets the custom tool registry for this session.
        /// Used by NamingTool for custom tool management.
        /// </summary>
        /// <param name="tools">Dictionary of tool modules and their functions</param>
        public void SetTools(Dictionary<string, List<FunctionWithDescription>> tools)
        {
            _session.SetTools(tools);
        }

        /// <summary>
        /// Gets the persons associated with this session
        /// </summary>
        /// <returns>List of persons in this session</returns>
        public async Task<List<IHostPerson>?> GetPersonsAsync()
        {
            var persons = await _session.GetPersonsAsync();
            return persons?.Select(p => new HostPersonAdapter(p) as IHostPerson).ToList();
        }


        /// <summary>
        /// Disposes the underlying session
        /// </summary>
        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
