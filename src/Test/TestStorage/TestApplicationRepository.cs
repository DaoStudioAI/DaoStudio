using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Repositories;
using Xunit;

namespace Test.TestStorage
{
    public class TestApplicationRepository : IDisposable
    {
        private readonly string _testDbPath;
        private readonly SqliteApplicationRepository _repository;

        public TestApplicationRepository()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_app_repo_{Guid.NewGuid()}.db");
            _repository = new SqliteApplicationRepository(_testDbPath);
        }

        public void Dispose()
        {
            if (File.Exists(_testDbPath))
            {
                try { File.Delete(_testDbPath); } catch { }
            }
        }

        [Fact]
        public async Task GetApplicationReturnsNullForNonExistent()
        {
            var app = await _repository.GetApplicationAsync(999);
            Assert.Null(app);
        }

        [Fact]
        public async Task CreateAndGetApplicationWorks()
        {
            var app = new Application
            {
                Name = "Test App",
                BriefDescription = "Brief",
                Description = "Long description"
            };

            var created = await _repository.CreateApplicationAsync(app);
            var retrieved = await _repository.GetApplicationAsync(created.Id);

            Assert.NotNull(retrieved);
            Assert.Equal(created.Id, retrieved!.Id);
            Assert.Equal("Test App", retrieved.Name);
            Assert.Equal("Brief", retrieved.BriefDescription);
            Assert.Equal("Long description", retrieved.Description);
            Assert.NotEqual(default, retrieved.CreatedAt);
            Assert.NotEqual(default, retrieved.LastModified);
        }

        [Fact]
        public async Task UpdateApplicationWorks()
        {
            var app = new Application { Name = "Original", BriefDescription = "B", Description = "D" };
            var created = await _repository.CreateApplicationAsync(app);
            var t = created.LastModified;

            created.Name = "Updated";
            created.BriefDescription = null;
            created.Description = "New D";

            var result = await _repository.SaveApplicationAsync(created);
            var updated = await _repository.GetApplicationAsync(created.Id);

            Assert.True(result);
            Assert.NotNull(updated);
            Assert.Equal("Updated", updated!.Name);
            Assert.Null(updated.BriefDescription);
            Assert.Equal("New D", updated.Description);
            Assert.NotEqual(t, updated.LastModified);
        }

        [Fact]
        public async Task DeleteApplicationWorks()
        {
            var app = new Application { Name = "To Delete" };
            var created = await _repository.CreateApplicationAsync(app);

            var deleted = await _repository.DeleteApplicationAsync(created.Id);
            var retrieved = await _repository.GetApplicationAsync(created.Id);

            Assert.True(deleted);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task GetApplicationByNameAndExistsByNameWork()
        {
            var app = new Application { Name = "Unique App" };
            await _repository.CreateApplicationAsync(app);

            Assert.True(_repository.ApplicationExistsByName("Unique App"));
            Assert.False(_repository.ApplicationExistsByName("Other"));

            var retrieved = await _repository.GetApplicationByNameAsync("Unique App");
            Assert.NotNull(retrieved);
            Assert.Equal("Unique App", retrieved!.Name);
        }

        [Fact]
        public async Task GetAllApplicationsReturnsAll()
        {
            await _repository.CreateApplicationAsync(new Application { Name = "App 1" });
            await _repository.CreateApplicationAsync(new Application { Name = "App 2" });

            var all = await _repository.GetAllApplicationsAsync();
            Assert.Equal(2, all.Count());
            Assert.Contains(all, a => a.Name == "App 1");
            Assert.Contains(all, a => a.Name == "App 2");
        }

        [Fact]
        public async Task SaveApplicationWithIdZeroThrows()
        {
            var app = new Application { Id = 0, Name = "Invalid" };
            await Assert.ThrowsAsync<ArgumentException>(() => _repository.SaveApplicationAsync(app));
        }

        [Fact]
        public async Task CreateApplicationWithDuplicateNameThrows()
        {
            await _repository.CreateApplicationAsync(new Application { Name = "Duplicate" });
            await Assert.ThrowsAnyAsync<Exception>(() => _repository.CreateApplicationAsync(new Application { Name = "Duplicate" }));
        }
    }
}
