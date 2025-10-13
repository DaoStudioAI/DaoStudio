using System;

namespace DaoStudio.Interfaces
{
    /// <summary>
    /// POCO class representing basic session information without tool and token details
    /// </summary>
    public class SessionInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier of the session
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the parent session ID if this is a child session
        /// </summary>
        public long? ParentSessionId { get; set; }

        /// <summary>
        /// Gets or sets the title of the session
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the session
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the session was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the session was last modified
        /// </summary>
        public DateTime LastModified { get; set; }


        /// <summary>
        /// Gets or sets the current streaming/processing status of the session
        /// </summary>
        public SessionStatus SessionStatus { get; set; }

        /// <summary>
        /// Gets or sets the current person associated with the session
        /// </summary>
        public IPerson? CurrentPerson { get; set; }
    }
}
