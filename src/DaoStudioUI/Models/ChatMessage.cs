using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaoStudio.Interfaces;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace DaoStudioUI.Models;



/// <summary>
/// Defines the reason for message editing action
/// </summary>
public enum MessageEditReason
{
    Edit,
    Send
}

/// <summary>
/// Represents a message in a chat session for the business layer
/// with built-in editing capabilities for in-place message editing
/// </summary>
public class ChatMessage : ObservableObject
{
    private bool _isEditing;
    private string? _editContent;
    /// <summary>
    /// Action to notify when a message has been edited with reason
    /// </summary>
    public Action<ChatMessage, MessageEditReason>? MessageEdited { get; set; }
    /// <summary>
    /// Action to handle message pressed interactions
    /// </summary>
    public Action<ChatMessage>? MessagePressed { get; set; }

    private long _Id;
    /// <summary>
    /// Unique identifier for the message
    /// </summary>
    public long Id
    {
        get
        {
            return _Id;
        }
        set
        {
            if (value == 0)
            {
                throw new InvalidOperationException("Message ID cannot be 0");
            }
            _Id = value;
        }
    }

    /// <summary>
    /// Session ID this message belongs to
    /// </summary>
    public long SessionId { get; set; }

    private string? _content;
    /// <summary>
    /// Content of the message
    /// </summary>
    public string? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <summary>
    /// Role of the message (user, assistant, system, etc.)
    /// </summary>
    public required DaoStudio.Interfaces.MessageRole Role { get; set; }

    /// <summary>
    /// Type of the message
    /// </summary>
    public MessageType Type { get; set; } = MessageType.Normal;

    /// <summary>
    /// Binary content associated with the message (optional)
    /// </summary>
    public List<IMsgBinaryData>? BinaryContents { get; set; }

    /// <summary>
    /// Type of binary content (enum)
    /// </summary>
    public int BinaryVersion { get; set; }

    /// <summary>
    /// Parent message ID this message is responding to (optional)
    /// </summary>
    public long ParentMsgId { get; set; }

    /// <summary>
    /// Parent session ID for hierarchical sessions (optional)
    /// </summary>
    public long ParentSessId { get; set; }

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the message was last modified
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Gets a human-readable representation of when the message was created
    /// </summary>
    public string HumanizedCreatedAt => CreatedAt.Humanize();

    /// <summary>
    /// Gets the exact local time representation of when the message was last modified
    /// </summary>
    public string LocalModifiedTime => LastModified.ToLocalTime().ToString("G");

    // Editing properties
    /// <summary>
    /// Indicates whether the message is currently being edited
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        private set => SetProperty(ref _isEditing, value);
    }

    /// <summary>
    /// Content being edited
    /// </summary>
    public string? EditContent
    {
        get => _editContent;
        set => SetProperty(ref _editContent, value);
    }

    // Commands for editing actions
    /// <summary>
    /// Command to save edits
    /// </summary>
    public ICommand SaveEditCommand { get; }

    /// <summary>
    /// Command to save and send edits
    /// </summary>
    public ICommand SendEditCommand { get; }
    /// <summary>
    /// Command to cancel editing
    /// </summary>
    public ICommand CancelEditCommand { get; }
    /// <summary>
    /// Command to handle message pressed
    /// </summary>
    public ICommand MessagePressedCommand { get; }


    /// Default constructor
    /// </summary>
    public ChatMessage()
    {        // Initialize commands
        SaveEditCommand = new RelayCommand(SaveEdit);
        SendEditCommand = new RelayCommand(SendEdit);
        CancelEditCommand = new RelayCommand(CancelEdit);
        MessagePressedCommand = new RelayCommand(OnMessagePressed);
    }



    /// <summary>
    /// Start editing mode
    /// </summary>
    public void StartEditing()
    {
        EditContent = Content;
        IsEditing = true;
    }

    /// <summary>
    /// Save the edited content
    /// </summary>
    private void SaveEdit()
    {
        if (string.IsNullOrWhiteSpace(EditContent))
        {
            // Don't save empty content
            CancelEdit();
            return;
        }

        // Update the content
        Content = EditContent;
        IsEditing = false;

        // Update the timestamp
        LastModified = DateTime.UtcNow;
        OnPropertyChanged(nameof(LocalModifiedTime));
        OnPropertyChanged(nameof(HumanizedCreatedAt));

        // Notify that the message has been edited
        MessageEdited?.Invoke(this, MessageEditReason.Edit);
    }

    /// <summary>
    /// Save and send the edited message
    /// </summary>
    private void SendEdit()
    {
        if (string.IsNullOrWhiteSpace(EditContent))
        {
            // Don't send empty content
            CancelEdit();
            return;
        }

        // Update the content
        Content = EditContent;
        IsEditing = false;

        // Update the timestamp
        LastModified = DateTime.UtcNow;
        OnPropertyChanged(nameof(LocalModifiedTime));
        OnPropertyChanged(nameof(HumanizedCreatedAt));

        // Notify that the message has been edited with send flag
        MessageEdited?.Invoke(this, MessageEditReason.Send);
    }

    /// <summary>
    /// Cancel editing and revert to original content
    /// </summary>
    private void CancelEdit()
    {
        EditContent = Content;
        IsEditing = false;
    }    /// <summary>
         /// Handle message pressed - determines action based on message properties
         /// </summary>
    private void OnMessagePressed()
    {
        // Check if this is an Information message
        if (Type.HasFlag(MessageType.Information))
        {
            MessagePressed?.Invoke(this);
        }
        // Check if this is a tool call message (has tool call binary content)
        else if (BinaryContents?.Any(bc => bc.Type == MsgBinaryDataType.ToolCall ||
                                          bc.Type == MsgBinaryDataType.ToolCallResult) == true)
        {
            MessagePressed?.Invoke(this);
        }
        // Default to content editing for normal messages
        else
        {
            StartEditing();
        }
    }

    /// <summary>
    /// Manually trigger property change notification for Content
    /// </summary>
    public new void OnPropertyChanged(string propertyName)
    {
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }


    /// <summary>
    /// Creates a ChatMessage from IMessage
    /// </summary>
    /// <param name="message">The message to convert</param>
    /// <returns>A new ChatMessage instance</returns>
    public static ChatMessage FromMessage(IMessage message)
    {
        return new ChatMessage
        {
            Id = message.Id,
            SessionId = message.SessionId,
            Content = message.Content,
            Role = message.Role,
            Type = message.Type,
            BinaryContents = message.BinaryContents,
            BinaryVersion = message.BinaryVersion,
            ParentMsgId = message.ParentMsgId,
            ParentSessId = message.ParentSessId,
            CreatedAt = message.CreatedAt,
            LastModified = message.LastModified
        };
    }


}
