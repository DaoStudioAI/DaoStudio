namespace DaoStudio.Interfaces.Plugins
{
    /// <summary>
    /// Minimal interface for person information used by Tools.
    /// Provides only the essential properties needed by plugin tools.
    /// </summary>
    public interface IHostPerson
    {
        /// <summary>
        /// Gets the name of the person
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the person
        /// </summary>
        string Description { get; }
    }
}
