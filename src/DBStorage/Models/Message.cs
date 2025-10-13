using System;
using System.Collections.Generic;
using DaoStudio.DBStorage.Common;
using MessagePack;

namespace DaoStudio.DBStorage.Models;


/// <summary>
/// Represents a message in a chat session
/// </summary>
public class Message
{
    /// <summary>
    /// Unique identifier for the message
    /// </summary>
    public long Id { get; set; } 

    /// <summary>
    /// Session ID this message belongs to
    /// </summary>
    public long SessionId { get; set; }

    /// <summary>
    /// Content of the message
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Role of the message (user, assistant, system, etc.)
    /// </summary>
    public required int Role { get; set; } // 0=Unknown, 1=User, 2=Assistant, 3=System, 4=Developer

    /// <summary>
    /// Type of the message content structure
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Binary contents associated with the message (optional)
    /// </summary>
    public List<BinaryData>? BinaryContents { get; set; }

    /// <summary>
    /// Binary version (current version must be 0)
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
    /// Timestamp when the message was created,utc time
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the message was last modified, UTC time
    /// </summary>
    public DateTime LastModified { get; set; }
}
