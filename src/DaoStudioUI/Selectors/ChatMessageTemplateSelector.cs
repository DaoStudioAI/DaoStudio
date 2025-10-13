using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using DaoStudioUI.Models;
using DaoStudio.Interfaces;
using System.Linq;

namespace DaoStudioUI.Selectors
{
    public class ChatMessageTemplateSelector : IDataTemplate
    {
        [Content]
        public IDataTemplate? DefaultTemplate { get; set; }

        public IDataTemplate? TimeMessageTemplate { get; set; }
        public IDataTemplate? SystemMessageTemplate { get; set; }
        public IDataTemplate? UserToolMessageTemplate { get; set; }
        public IDataTemplate? UserMessageTemplate { get; set; }
        public IDataTemplate? InformationMessageTemplate { get; set; }  // New addition

        public Control? Build(object? param)
        {
            if (param is ChatMessage message)
            {
                // Check for Information messages from storage layer
                if (IsInformationMessage(message))
                {
                    return InformationMessageTemplate?.Build(param) ?? DefaultTemplate?.Build(param);
                }

                IDataTemplate? template = null;

                // Special check for time marker messages (system messages with empty content)
                if (message.Role == MessageRole.System && string.IsNullOrEmpty(message.Content))
                {
                    return TimeMessageTemplate?.Build(param) ?? DefaultTemplate?.Build(param);
                }

                // Check for tool call messages
                if (message.BinaryContents?.Any(bc => bc.Type == MsgBinaryDataType.ToolCall || bc.Type == MsgBinaryDataType.ToolCallResult) == true)
                {
                    return UserToolMessageTemplate?.Build(param) ?? DefaultTemplate?.Build(param);
                }

                // Determine which template to use based on Role and message Type 
                template = message.Role switch
                {
                    MessageRole.System => SystemMessageTemplate,
                    MessageRole.User => UserMessageTemplate,
                    MessageRole.Assistant => UserMessageTemplate,
                    _ => throw new System.Exception($"Unknown message role: {message.Role}")
                };
                

                return template?.Build(param) ?? DefaultTemplate?.Build(param);
            }

            return DefaultTemplate?.Build(param);
        }

        private bool IsInformationMessage(ChatMessage message)
        {
            // Check if this is an Information message from storage
            return message.Type.HasFlag(MessageType.Information);
        }

        public bool Match(object? data)
        {
            return data is ChatMessage;
        }
    }
}