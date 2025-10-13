using LiteDB;

namespace DaoStudio.Plugins.KVStore.Models;

/// <summary>
/// Data model for storing key-value pairs in LiteDB
/// Each record represents a key-value pair for a specific top session
/// </summary>
public class KeyValuePair
{
    /// <summary>
    /// Auto-generated ObjectId by LiteDB
    /// </summary>
    [BsonId]
    public ObjectId Id { get; set; }

    /// <summary>
    /// The top session identifier that owns this key-value pair
    /// Used for session isolation
    /// </summary>
    [BsonField("session_id")]
    public string TopSessionId { get; set; } = string.Empty;

    /// <summary>
    /// The key identifier
    /// </summary>
    [BsonField("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The stored value as a string
    /// </summary>
    [BsonField("value")]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// When this record was created
    /// </summary>
    [BsonField("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this record was last updated
    /// </summary>
    [BsonField("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Default constructor for LiteDB deserialization
    /// </summary>
    public KeyValuePair()
    {
        Id = ObjectId.NewObjectId(); // Initialize with a new ObjectId
    }

    /// <summary>
    /// Constructor for creating a new key-value pair
    /// </summary>
    /// <param name="topSessionId">The top session identifier</param>
    /// <param name="key">The key</param>
    /// <param name="value">The value</param>
    public KeyValuePair(string topSessionId, string key, string value)
    {
        Id = ObjectId.NewObjectId(); // Initialize with a new ObjectId
        TopSessionId = topSessionId ?? string.Empty;
        Key = key ?? string.Empty;
        Value = value ?? string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}