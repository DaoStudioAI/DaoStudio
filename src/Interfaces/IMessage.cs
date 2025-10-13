using System;
using System.Collections.Generic;

namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Defines the role of a message in a chat session
    /// </summary>
    public enum MessageRole
    {
        Unknown = 0,
        User = 1,
        Assistant = 2,
        System = 3,
        Developer = 4
    }

    /// <summary>
    /// Defines the type of message content structure
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// Normal text message (default/common type)
        /// </summary>
        Normal = 0,
        
        /// <summary>
        /// Information message with JSON-encoded content
        /// Content format is decided by upper layer
        /// </summary>
        Information = 1
    }

    /// <summary>
    /// Binary data type enumeration
    /// </summary>
    public enum MsgBinaryDataType
    {
        Text = 1,
        Image = 2,
        Audio = 3,
        Video = 4,
        File = 5,
        ToolCall = 6,
        ToolCallResult = 7,
        Thinking = 8,
        SubsessionId = 9,
        HostSessionMessage = 10,
    }


    /// <summary>
    /// Binary data interface
    /// </summary>
    public interface IMsgBinaryData
    {
        string Name { get; set; }
        MsgBinaryDataType Type { get; set; }
        byte[] Data { get; set; }
    }

    /// <summary>
    /// Core message interface for UI layer
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// Unique identifier for the message
        /// </summary>
        long Id { get; set; }

        /// <summary>
        /// Session ID this message belongs to
        /// </summary>
        long SessionId { get; set; }

        /// <summary>
        /// Content of the message
        /// </summary>
        string? Content { get; set; }

        /// <summary>
        /// Role of the message (user, assistant, system, etc.)
        /// </summary>
        MessageRole Role { get; set; }

        /// <summary>
        /// Type of the message content structure
        /// </summary>
        MessageType Type { get; set; }

        /// <summary>
        /// Binary contents associated with the message (optional)
        /// </summary>
        List<IMsgBinaryData>? BinaryContents { get; set; }

        /// <summary>
        /// Binary version (current version must be 0)
        /// </summary>
        int BinaryVersion { get; set; }

        /// <summary>
        /// Parent message ID this message is responding to (reserved for future usage)
        /// </summary>
        long ParentMsgId { get; set; }

        /// <summary>
        /// Parent session ID for hierarchical sessions (eserved for future usage)
        /// </summary>
        long ParentSessId { get; set; }

        /// <summary>
        /// Timestamp when the message was created, UTC time
        /// </summary>
        DateTime CreatedAt { get; set; }

        /// <summary>
        /// Timestamp when the message was last modified, UTC time
        /// </summary>
        DateTime LastModified { get; set; }

        /// <summary>
        /// Adds binary data to the message
        /// </summary>
        /// <param name="name">Name of the binary data</param>
        /// <param name="type">Type of the binary data</param>
        /// <param name="data">Binary data content</param>
        void AddBinaryData(string name, MsgBinaryDataType type, byte[] data);
    }
}