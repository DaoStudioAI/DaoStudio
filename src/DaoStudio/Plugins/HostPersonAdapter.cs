using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;

namespace DaoStudio.Plugins
{
    /// <summary>
    /// Adapter that implements IHostPerson by wrapping an IPerson instance.
    /// This provides a minimal interface for Tools while delegating to the full IPerson implementation.
    /// </summary>
    public class HostPersonAdapter : IHostPerson
    {
        private readonly IPerson _person;

        /// <summary>
        /// Initializes a new instance of the HostPersonAdapter class
        /// </summary>
        /// <param name="person">The IPerson instance to wrap</param>
        /// <exception cref="ArgumentNullException">Thrown when person is null</exception>
        public HostPersonAdapter(IPerson person)
        {
            _person = person ?? throw new ArgumentNullException(nameof(person));
        }

        /// <summary>
        /// Gets the name of the person
        /// </summary>
        public string Name => _person.Name;

        /// <summary>
        /// Gets the description of the person
        /// </summary>
        public string Description => _person.Description;
    }
}
