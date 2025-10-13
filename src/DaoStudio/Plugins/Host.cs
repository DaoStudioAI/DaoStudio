using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DryIoc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaoStudio.Plugins
{
    internal class Host : IHost
    {
        Container _container;
        private readonly IPeopleService _peopleService;
        private readonly Lazy<ISessionService> _sessionServiceLazy;
        private readonly IMessageService _messageService;
        private readonly ILogger<Host> _logger;
        private readonly IApplicationPathsService _applicationPathsService;

        public Host(IPeopleService peopleService, Lazy<ISessionService> sessionServiceLazy, IMessageService messageService, ILogger<Host> logger,
            Container container, IApplicationPathsService applicationPathsService)
        {
            _peopleService = peopleService;
            _sessionServiceLazy = sessionServiceLazy;
            _messageService = messageService;
            _logger = logger;
            _container = container;
            _applicationPathsService = applicationPathsService;
        }

        /// <summary>
        /// Gets persons by name filter - delegates to PeopleService
        /// </summary>
        /// <param name="name">The name filter (null for all persons)</param>
        /// <returns>List of persons matching the criteria</returns>
        public async Task<List<IPerson>> GetPersonsAsync(string? name)
        {
            return await _peopleService.GetPersonsAsync(name);
        }

        /// <summary>
        /// Start a new session with optional parent and person - delegates to SessionService
        /// </summary>
        /// <param name="parentSessId">Optional parent session ID</param>
        /// <param name="personName">Optional person name for the session</param>
        /// <returns>The created session</returns>
        public async Task<ISession> StartNewSession(long? parentSessId, string? personName = null)
        {
            return await _sessionServiceLazy.Value.StartNewSession(parentSessId, personName);
        }

        /// <summary>
        /// Open an existing host session by ID - delegates to SessionService
        /// </summary>
        /// <param name="sessionId">The ID of the session to open</param>
        /// <returns>The opened session</returns>
        public async Task<ISession> OpenHostSession(long sessionId)
        {
            return await _sessionServiceLazy.Value.OpenHostSession(sessionId);
        }

        /// <summary>
        /// Creates a new session and returns it as IHostSession for use by plugins
        /// </summary>
        /// <param name="parent">The parent session (can be IHostSession)</param>
        /// <param name="personName">Optional person name for the session</param>
        /// <returns>The created session wrapped as IHostSession</returns>
        public async Task<IHostSession> StartNewHostSessionAsync(IHostSession? parent, string? personName = null)
        {
            var newSession = await _sessionServiceLazy.Value.StartNewSession(parent?.Id, personName);
            var lf =  _container.Resolve<ILogger<HostSessionAdapter>>();
            return new HostSessionAdapter(newSession, _messageService, lf);
        }

        /// <summary>
        /// Gets persons as IHostPerson for use by plugins
        /// </summary>
        /// <param name="name">The name filter (null for all persons)</param>
        /// <returns>List of persons wrapped as IHostPerson</returns>
        public async Task<List<IHostPerson>> GetHostPersonsAsync(string? name)
        {
            var persons = await _peopleService.GetPersonsAsync(name);
            return persons.Select(p => new HostPersonAdapter(p) as IHostPerson).ToList();
        }

        /// <summary>
        /// Gets the plugin configuration folder path for the specified plugin id
        /// </summary>
        /// <param name="pluginId">The plugin identifier used to create a subfolder under the config folder</param>
        /// <returns>The path to the plugin configuration folder for the plugin</returns>
        public string GetPluginConfigureFolderPath(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                throw new ArgumentException("pluginId cannot be null or whitespace", nameof(pluginId));
            }

            // sanitize pluginId to be safe as a folder name
            var invalidChars = System.IO.Path.GetInvalidFileNameChars().Concat(System.IO.Path.GetInvalidPathChars()).Distinct().ToArray();
            var sanitized = new string(pluginId.Where(c => !invalidChars.Contains(c)).ToArray());
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                // fallback to config folder if pluginId becomes empty after sanitization
                return _applicationPathsService.ConfigFolderPath;
            }

            return System.IO.Path.Combine(_applicationPathsService.ConfigFolderPath, sanitized);
        }
    }
}
