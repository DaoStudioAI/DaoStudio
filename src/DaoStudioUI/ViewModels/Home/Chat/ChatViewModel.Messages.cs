using DaoStudioUI.Services;
using DaoStudioUI.Views;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DesktopUI.Resources;
using DryIoc;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DaoStudioUI.ViewModels;

public partial class ChatViewModel
{
    /// <summary>
    /// Gets whether the session is currently streaming/sending messages
    /// </summary>
    public bool IsStreaming => Session.SessionStatus == SessionStatus.Sending;
    
    [RelayCommand]
    private void CancelStreaming()
    {
        // Cancel the current message sending operation using the session's cancellation token
        Session.CurrentCancellationToken?.Cancel();
    }
    
    [RelayCommand]
    private void MessageContentPressed(Models.ChatMessage message)
    {
        if (message != null)
        {
            message.StartEditing();
        }
    }
      [RelayCommand]
    private async Task ToolCallMessagePressed(Models.ChatMessage message)
    {
        if (message != null && _currentWindow != null)
        {
            var dialog = new ToolCallMessageDialog(message);
            await dialog.ShowDialog(_currentWindow);
        }
    }
    
    [RelayCommand]
    private async Task InformationMessagePressed(Models.ChatMessage message)
    {
        if (message != null && _currentWindow != null)
        {
            try
            {
                await HandleInformationMessageClick(message);
            }
            catch (Exception ex)
            {
                // Log error and show user-friendly message
                Log.Error(ex, "Error handling information message click");
                await DialogService.ShowErrorAsync("Error", "Failed to open subsession", _currentWindow);
            }
        }
    }
    
    private async Task HandleInformationMessageClick(Models.ChatMessage message)
    {
        try
        {
            // Check for subsession ID in binary contents first (new format)
            var subsessionBinaryData = message.BinaryContents?.FirstOrDefault(bc => 
                bc.Type == MsgBinaryDataType.SubsessionId);
            
            if (subsessionBinaryData != null)
            {
                // Extract subsession ID from binary data
                var subsessionId = BitConverter.ToInt64(subsessionBinaryData.Data, 0);
                await OpenSubsession(subsessionId);
                return;
            }

            // For legacy support, if the message content is just "Subsession created"
            // we can't extract the subsession ID anymore since AdditionalData was removed
            // This is intentional as we're migrating to the new format
            Log.Information("Information message clicked but no subsession ID found in binary data");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing information message click");
        }
    }
      private async Task OpenSubsession(long subsessionId)
    {
        try
        {
            // Open the subsession using the existing DaoStudio infrastructure
            var subsession = await _sessionService.OpenSession(subsessionId);
            
            // Get the person/model for the subsession
            var currentPerson = subsession.CurrentPerson;
            var person = await _peopleService.GetPersonAsync(currentPerson.Name);
            if (person == null)
            {
                Console.WriteLine($"Could not find person with name: {currentPerson.Name}");
                return;
            }
              
            // Create new ChatViewModel for the subsession
            var getChatViewModel = App.GetContainer().Resolve<Func<ISession, IPerson, Avalonia.Controls.Window?, ChatViewModel>>();
            var subsessionViewModel = getChatViewModel(subsession, person, _currentWindow);
            
            // Instead of creating a new window, navigate to the subsession
            if (NavigationStack != null)
            {
                var subsessionItem = new DesktopUI.Models.SessionNavigationItem(subsession, person)
                {
                    ViewModel = subsessionViewModel,
                    Title = person.Name
                };

                NavigationStack.PushSession(subsessionItem);
                await PerformNavigationAsync(subsessionItem, DesktopUI.Models.NavigationDirection.Forward);

                Log.Information("Successfully navigated to subsession {SubsessionId}", subsessionId);
            }
            else
            {
                // Fallback to old behavior if navigation stack not initialized
                var chatWindow = new ChatWindow();
                chatWindow.SetViewModel(subsessionViewModel);
                
                // Set parent window relationship and positioning
                if (_currentWindow != null)
                {
                    chatWindow.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner;
                }
                
                // Show the new window
                chatWindow.Show();
                Log.Information("Navigation stack not initialized, opened subsession window for {SubsessionId}", subsessionId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open subsession {SubsessionId}", subsessionId);
            if (_currentWindow != null)
            {
                await DialogService.ShowErrorAsync("Error", $"Failed to open subsession: {ex.Message}", _currentWindow);
            }
        }
    }
    
    private async Task LoadMessagesAsync()
    {
        try
        {
            // Use IMessageService to get messages by session id
            var sessionMessages = await _messageService.GetMessagesBySessionIdAsync(Session.Id);
            
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Clear existing messages
                Messages.Clear();
                // Add messages to the UI collection with time markers
                foreach (var message in sessionMessages)
                {
                    // Configure the message and add it with time marker support
                    var chatMessage = ConfigureChatMessage((IMessage)message);
                    AddMessageWithTimeMarker(chatMessage);
                }
                Log.Information("Loaded {Count} messages for session {SessionId}", Messages.Count, Session.Id);
                
                // Trigger Session_PropertyChanged to handle any session status changes after loading messages
                Session_PropertyChanged(this, new PropertyChangeNotification(nameof(Session.SessionStatus)));
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading messages for session {SessionId}", Session.Id);
            await ShowErrorAsync(Strings.Chat_Error_LoadChatHistory);
        }
    }
    Task ShowErrorAsync(string message)
    {
        return DialogService.ShowErrorAsync(Strings.Common_Error, message, _currentWindow);
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) && !HasAttachments) return;
        
        var userMessageContent = InputText.Trim();
        InputText = string.Empty; // Clear input immediately
        
        try
        {
            // Handle user message and get response using ISession
            if (HasAttachments)
            {
                await ProcessAttachmentMessageAsync(userMessageContent);
            }
            else
            {
                // Use ISession to send message and get response
                // SessionStatus will be automatically set to Sending/Idle by the Session
                var response = await Session.SendMessageAsync(userMessageContent);
                
                // Handle the response - it should already be in the Messages collection
                // through the session's storage mechanism
                Log.Information("Message sent and response received with ID: {MessageId}", response.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to process message");
            await ShowErrorAsync(ex.Message);
        }
    }

    private async Task ProcessAttachmentMessageAsync(string userMessageContent)
    {
        //try
        //{
        //    // Create a list of IMsgBinaryData objects from all attachments
        //    var binaryDataList = Attachments.Select(attachment => new DaoStudio.MsgBinaryData
        //    {
        //        Name = attachment.Description,
        //        //Type = DaoStudio.Session.MapUIMessageTypeToBinaryDataType((int)attachment.Type),
        //        Data = attachment.Data
        //    } as IMsgBinaryData).ToList();

        //    // Create user message with all attachments
        //    var userMessage = new Models.ChatMessage
        //    {
        //        SessionId = Session.Id,
        //        Content = string.IsNullOrWhiteSpace(userMessageContent) ?
        //            string.Join(", ", Attachments.Select(a => a.Description)) : userMessageContent,
        //        Role = MessageRole.User,
        //        Type = Attachments.First().Type, // Use the type of the first attachment
        //        BinaryContents = binaryDataList,
        //        CreatedAt = DateTime.UtcNow
        //    };
        //      // Add message to UI
        //    ConfigureChatMessageUI(userMessage);
        //    Messages.Add(userMessage);
            
        //    // Clear attachments
        //    Attachments.Clear();
            
        //    // Use ISession to generate response based on all messages, including the attachment message
        //    await Session.SendMessageAsync(userMessage.Content);
        //}
        //catch (Exception ex)
        //{
        //    Log.Error(ex, "Error processing attachment message");
        //    throw;
        //}
    }


    private async void OnMessageEdited(Models.ChatMessage message, Models.MessageEditReason reason)
    {
        try
        {
            // Get the existing message from the database and update it
            var existingMessage = await _messageService.GetMessageByIdAsync(message.Id);
            if (existingMessage != null)
            {
                // Sync the changes from the UI message to the database message
                existingMessage.Content = message.Content;
                // Note: Other properties like Role, Type, etc. would be synced here if they can be edited
                
                // Save the updated message
                await _messageService.SaveMessageAsync(existingMessage, false);
            }
            
            // Update session last modified time
            await Session.UpdateSessionLastModifiedAsync();
            
            Log.Information("Message {MessageId} edited with reason: {Reason}", message.Id, reason);
            
            // Handle based on edit reason
            if (reason == Models.MessageEditReason.Send)
            {
                // If send reason, we need to regenerate responses
                await RegenerateResponsesAfterEditAsync(message);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling edited message {MessageId} with reason {Reason}", message.Id, reason);
            await ShowErrorAsync(Strings.Chat_Error_ProcessEditedMessage);
        }
    }
    
    private async Task RegenerateResponsesAfterEditAsync(Models.ChatMessage editedMessage)
    {
        try
        {
            Log.Information("Regenerating responses after message {MessageId} edit", editedMessage.Id);
            
            // Find the index of the edited message
            int editedIndex = Messages.IndexOf(editedMessage);
            if (editedIndex < 0)
            {
                Log.Warning("Could not find edited message {MessageId} in messages collection", editedMessage.Id);
                return;
            }
            
            // Remove all messages that come after the edited message directly from the collection
            while (Messages.Count > editedIndex )
            {
                Messages.RemoveAt(editedIndex);
            }
            
            await _messageService.DeleteMessageInSessionAsync(Session.Id, editedMessage.Id, true);
            
            if (editedMessage.Role == MessageRole.User)
            {
                try
                {
                    if (editedMessage.Content==null)
                    {
                        await ShowErrorAsync(Strings.Chat_Error_ProcessEditedMessage);
                        return;
                    }
                    await Session.SendMessageAsync(editedMessage.Content);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error regenerating response after edit");
                    await ShowErrorAsync(string.Format(Strings.Chat_Error_RegenerateResponse, ex.Message));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error regenerating responses after message edit");
            await ShowErrorAsync(Strings.Chat_Error_RegenerateResponses);
        }
    }

    /// <summary>
    /// Configures a storage message by converting it to a chat message and setting up delegates
    /// </summary>
    /// <param name="message">The storage message to configure</param>
    /// <returns>A configured chat message ready for UI</returns>
    private Models.ChatMessage ConfigureChatMessage(IMessage message)
    {
        // Convert storage message to UI message
        var chatMessage = Models.ChatMessage.FromMessage(message);
        
        ConfigureChatMessageUI(chatMessage);
        
        return chatMessage;
    }
    
    /// <summary>
    /// Configures UI-specific properties for a chat message (both new and converted)
    /// </summary>
    /// <param name="chatMessage">The chat message to configure</param>
    private void ConfigureChatMessageUI(Models.ChatMessage chatMessage)
    {
        // Subscribe to message edited action
        chatMessage.MessageEdited = OnMessageEdited;
        
        // Set interaction delegates
        chatMessage.MessagePressed = (msg) => 
        {
            // Determine the appropriate action based on message properties
            if (msg.Type.HasFlag(MessageType.Information))
            {
                if (InformationMessagePressedCommand.CanExecute(msg)) InformationMessagePressedCommand.Execute(msg);
            }
            else if (msg.BinaryContents?.Any(bc => bc.Type == MsgBinaryDataType.ToolCall ||
                                                  bc.Type == MsgBinaryDataType.ToolCallResult) == true)
            {
                if (ToolCallMessagePressedCommand.CanExecute(msg)) ToolCallMessagePressedCommand.Execute(msg);
            }
        };
    }

    /// <summary>
    /// Adds a message to the collection, including time markers if needed
    /// </summary>
    /// <param name="chatMessage">The chat message to add</param>
    private void AddMessageWithTimeMarker(Models.ChatMessage chatMessage)
    {
        // Check if we need to add a time marker
        if (Messages.Count > 0)
        {
            var lastMessage = Messages[Messages.Count - 1];
            AddTimeMarkerIfNeeded(lastMessage.CreatedAt, chatMessage.CreatedAt);
        }
        
        // Add the message to the collection
        Messages.Add(chatMessage);
    }

    /// <summary>
    /// Creates a time marker message for indicating time separation in conversations
    /// </summary>
    /// <param name="createdAt">The timestamp to display</param>
    /// <returns>A formatted system message for time indication</returns>
    private Models.ChatMessage CreateTimeMarkerMessage(DateTime createdAt)
    {
        return new Models.ChatMessage
        {
            Id = 1, // Temporary ID for UI time marker
            SessionId = Session.Id,
            Content = "",  // Empty content for time messages, will display timestamp only
            Role = MessageRole.System,  // Use System role for time markers
            Type = MessageType.Normal,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// Adds a time marker to the conversation if sufficient time has passed
    /// </summary>
    /// <param name="lastMessageTime">Time of the previous message</param>
    /// <param name="currentMessageTime">Time of the current message</param>
    private void AddTimeMarkerIfNeeded(DateTime lastMessageTime, DateTime currentMessageTime)
    {
        // Add time marker if messages are more than 1 hour apart
        if ((currentMessageTime - lastMessageTime).TotalHours >= 1)
        {
            var timeMarker = CreateTimeMarkerMessage(currentMessageTime);
            Messages.Add(timeMarker);
        }
    }
}