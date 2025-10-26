namespace DaoStudio.Interfaces;

/// <summary>
/// Defines the type of session event
/// </summary>
public enum SessionEventType
{
    /// <summary>
    /// A new session was created (includes both regular sessions and subsessions)
    /// </summary>
    Created,

    /// <summary>
    /// A session was closed
    /// </summary>
    Closed
}

/// <summary>
/// Event arguments for session events
/// </summary>
public class SessionEventArgs : EventArgs
{
    /// <summary>
    /// The type of session event
    /// </summary>
    public SessionEventType EventType { get; set; }

    /// <summary>
    /// The session involved in the event
    /// </summary>
    public ISession Session { get; set; }

    /// <summary>
    /// The parent session (only populated for subsession Created events)
    /// </summary>
    public ISession? ParentSession { get; set; }

    /// <summary>
    /// Constructor for session events
    /// </summary>
    /// <param name="eventType">The type of event</param>
    /// <param name="session">The session involved in the event</param>
    /// <param name="parentSession">Optional parent session (for subsession creation)</param>
    public SessionEventArgs(SessionEventType eventType, ISession session, ISession? parentSession = null)
    {
        EventType = eventType;
        Session = session;
        ParentSession = parentSession;
    }
}
