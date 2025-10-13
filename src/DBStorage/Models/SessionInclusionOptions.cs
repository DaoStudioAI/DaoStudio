namespace DaoStudio.DBStorage.Models
{
    /// <summary>
    /// Enum representing options for including child sessions in queries
    /// </summary>
    public enum SessionInclusionOptions
    {
        /// <summary>
        /// Include all sessions (both parent and child sessions)
        /// </summary>
        All = 0,

        /// <summary>
        /// Include only parent sessions (sessions without a parent)
        /// </summary>
        ParentsOnly = 1,

        /// <summary>
        /// Include only child sessions (sessions with a parent)
        /// </summary>
        ChildrenOnly = 2
    }
}