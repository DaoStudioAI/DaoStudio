using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;

namespace Test.TestStorage.Mocks
{
    public class MockSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, Settings> _settings = new Dictionary<string, Settings>();

        public Task<Settings?> GetSettingsAsync(string applicationName)
        {
            if (_settings.TryGetValue(applicationName, out var settings))
            {
                return Task.FromResult<Settings?>(settings);
            }
            return Task.FromResult<Settings?>(null);
        }

        public Task<IEnumerable<Settings>> GetAllSettingsAsync()
        {
            return Task.FromResult<IEnumerable<Settings>>(_settings.Values);
        }

        public Task<bool> SaveSettingsAsync(Settings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.LastModified = DateTime.UtcNow;
            _settings[settings.ApplicationName] = settings;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteSettingsAsync(string applicationName)
        {
            return Task.FromResult(_settings.Remove(applicationName));
        }
    }
} 