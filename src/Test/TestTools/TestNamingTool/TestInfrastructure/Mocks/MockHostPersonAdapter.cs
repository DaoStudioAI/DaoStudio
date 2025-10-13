using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;

namespace TestNamingTool.TestInfrastructure.Mocks
{
    /// <summary>
    /// Mock adapter that wraps an IPerson to provide IHostPerson functionality for testing
    /// </summary>
    public class MockHostPersonAdapter : IHostPerson
    {
        private readonly IPerson _person;

        public MockHostPersonAdapter(IPerson person)
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