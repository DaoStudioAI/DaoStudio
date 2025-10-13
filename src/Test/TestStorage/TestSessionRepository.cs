using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Repositories;
using Xunit;

namespace Test.TestStorage
{
    public class TestSessionRepository : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISessionRepository _sessionRepository;

        public TestSessionRepository()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_session_repo_{Guid.NewGuid()}.db");
            
            // Initialize repository directly with SqliteSessionRepository
            _sessionRepository = new SqliteSessionRepository(_testDbPath);
        }

        public void Dispose()
        {
            // Clean up the test database after tests
            if (File.Exists(_testDbPath))
            {
                try
                {
                    File.Delete(_testDbPath);
                }
                catch
                {
                    // Ignore deletion errors during cleanup
                }
            }
        }

        [Fact]
        public async Task GetSessionReturnsNullForNonExistentSession()
        {
            // Arrange - nothing to arrange

            // Act
            var session = await _sessionRepository.GetSessionAsync(999);

            // Assert
            Assert.Null(session);
        }

        [Fact]
        public async Task SaveAndGetSessionWorks()
        {
            // Arrange
            var newSession = new Session
            {
                Title = "Test Session",
                Description = "Test session description",
                PersonNames = new List<string> { "ModelA", "ModelB" },
                ToolNames = new List<string> { "WebTool", "FileTool" },
                SessionType = 0, // Normal
                AppId = 12345L,
                PreviousId = 999L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            // Act
            var saveResult = await _sessionRepository.CreateSessionAsync(newSession);
            var retrievedSession = await _sessionRepository.GetSessionAsync(newSession.Id);

            // Assert
            Assert.NotNull(saveResult);
            Assert.NotNull(retrievedSession);
            Assert.Equal(newSession.Id, retrievedSession.Id);
            Assert.Equal("Test Session", retrievedSession.Title);
            Assert.Equal("Test session description", retrievedSession.Description);
            Assert.Equal(new List<string> { "ModelA", "ModelB" }, retrievedSession.PersonNames);
            Assert.Equal(new List<string> { "WebTool", "FileTool" }, retrievedSession.ToolNames);
            Assert.Equal(0, retrievedSession.SessionType); // Normal
            Assert.Equal(12345L, retrievedSession.AppId);
            Assert.Equal(999L, retrievedSession.PreviousId);
        }

        [Fact]
        public async Task GetAllSessionsReturnsAllSessions()
        {
            // Arrange
            var session1 = new Session
            {
                Title = "Session 1",
                Description = "Description 1",
                PersonNames = new List<string> { "ModelX" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            var session2 = new Session
            {
                Title = "Session 2",
                Description = "Description 2",
                PersonNames = new List<string> { "ModelY", "ModelZ" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            await _sessionRepository.CreateSessionAsync(session1);
            await _sessionRepository.CreateSessionAsync(session2);

            // Act
            var allSessions = await _sessionRepository.GetAllSessionsAsync();

            // Assert
            Assert.Equal(2, allSessions.Count());
            Assert.Contains(allSessions, s => s.Title == "Session 1");
            Assert.Contains(allSessions, s => s.Title == "Session 2");
        }

        [Fact]
        public async Task DeleteSessionWorks()
        {
            // Arrange
            var session = new Session
            {
                Title = "Session to delete",
                Description = "Will be deleted",
                PersonNames = new List<string> { "ToDelete" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            await _sessionRepository.CreateSessionAsync(session);

            // Act
            var deleteResult = await _sessionRepository.DeleteSessionAsync(session.Id);
            var retrievedSession = await _sessionRepository.GetSessionAsync(session.Id);

            // Assert
            Assert.True(deleteResult);
            Assert.Null(retrievedSession);
        }

        [Fact]
        public async Task UpdateSessionWorks()
        {
            // Arrange
            var session = new Session
            {
                Title = "Original Title",
                Description = "Original Description",
                PersonNames = new List<string> { "OriginalModel" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            await _sessionRepository.CreateSessionAsync(session);
            
            // Update the session
            session.Title = "Updated Title";
            session.Description = "Updated Description";
            session.PersonNames = new List<string> { "UpdatedModel1", "UpdatedModel2" };
            session.LastModified = DateTime.UtcNow;

            // Act
            var updateResult = await _sessionRepository.SaveSessionAsync(session);
            var retrievedSession = await _sessionRepository.GetSessionAsync(session.Id);

            // Assert
            Assert.True(updateResult);
            Assert.NotNull(retrievedSession);
            Assert.Equal("Updated Title", retrievedSession.Title);
            Assert.Equal("Updated Description", retrievedSession.Description);
            Assert.Equal(new List<string> { "UpdatedModel1", "UpdatedModel2" }, retrievedSession.PersonNames);
        }

        [Fact]
        public async Task ParentSessIdWorksCorrectly()
        {
            // Arrange
            var parentSession = new Session
            {
                Title = "Parent Session",
                Description = "This is a parent session",
                PersonNames = new List<string> { "ParentModel" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            await _sessionRepository.CreateSessionAsync(parentSession);
            
            var childSession = new Session
            {
                Title = "Child Session",
                Description = "This is a child session",
                PersonNames = new List<string> { "ChildModel" },
                ParentSessId = parentSession.Id,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            await _sessionRepository.CreateSessionAsync(childSession);

            // Act
            var retrievedChildSession = await _sessionRepository.GetSessionAsync(childSession.Id);

            // Assert
            Assert.NotNull(retrievedChildSession);
            Assert.Equal(childSession.Id, retrievedChildSession.Id);
            Assert.Equal("Child Session", retrievedChildSession.Title);
            Assert.Equal(parentSession.Id, retrievedChildSession.ParentSessId);
            Assert.Equal("ChildModel", retrievedChildSession.PersonNames.First());
            
            // Update child session with new parent
            var newParentSession = new Session
            {
                Title = "New Parent Session",
                Description = "This is a new parent session",
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            await _sessionRepository.CreateSessionAsync(newParentSession);
            
            retrievedChildSession.ParentSessId = newParentSession.Id;
            await _sessionRepository.SaveSessionAsync(retrievedChildSession);
            
            // Get updated child session
            var updatedChildSession = await _sessionRepository.GetSessionAsync(childSession.Id);
            
            // Assert the parent was updated
            Assert.Equal(newParentSession.Id, updatedChildSession!.ParentSessId);
        }
        
        [Fact]
        public async Task GetSessionsByParentSessIdAsyncWorks()
        {
            // Arrange
            var parentSession1 = new Session
            {
                Title = "Parent Session 1",
                Description = "First parent session",
                PersonNames = new List<string> { "ParentModel1" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            var parentSession2 = new Session
            {
                Title = "Parent Session 2",
                Description = "Second parent session",
                PersonNames = new List<string> { "ParentModel2" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            await _sessionRepository.CreateSessionAsync(parentSession1);
            await _sessionRepository.CreateSessionAsync(parentSession2);
            
            // Child sessions for parent 1
            var childSession1 = new Session
            {
                Title = "Child Session 1-1",
                Description = "First child of parent 1",
                PersonNames = new List<string> { "ChildModel1" },
                ParentSessId = parentSession1.Id,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            var childSession2 = new Session
            {
                Title = "Child Session 1-2",
                Description = "Second child of parent 1",
                PersonNames = new List<string> { "ChildModel2" },
                ParentSessId = parentSession1.Id,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            // Child session for parent 2
            var childSession3 = new Session
            {
                Title = "Child Session 2-1",
                Description = "First child of parent 2",
                PersonNames = new List<string> { "ChildModel3" },
                ParentSessId = parentSession2.Id,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            await _sessionRepository.CreateSessionAsync(childSession1);
            await _sessionRepository.CreateSessionAsync(childSession2);
            await _sessionRepository.CreateSessionAsync(childSession3);

            // Act
            var childrenOfParent1 = await _sessionRepository.GetSessionsByParentSessIdAsync(parentSession1.Id);
            var childrenOfParent2 = await _sessionRepository.GetSessionsByParentSessIdAsync(parentSession2.Id);
            var childrenOfNonExistentParent = await _sessionRepository.GetSessionsByParentSessIdAsync(999);

            // Assert
            Assert.Equal(2, childrenOfParent1.Count());
            Assert.Contains(childrenOfParent1, s => s.Title == "Child Session 1-1");
            Assert.Contains(childrenOfParent1, s => s.Title == "Child Session 1-2");
            
            Assert.Single(childrenOfParent2);
            Assert.Equal("Child Session 2-1", childrenOfParent2.First().Title);
            
            Assert.Empty(childrenOfNonExistentParent);
        }

        [Fact]
        public async Task GetAllSessionsWithInclusionOptionsWorks()
        {
            // Arrange
            var parentSession1 = new Session
            {
                Title = "Parent Session 1",
                Description = "First parent session",
                PersonNames = new List<string> { "ParentModel1" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            var parentSession2 = new Session
            {
                Title = "Parent Session 2",
                Description = "Second parent session",
                PersonNames = new List<string> { "ParentModel2" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            var childSession1 = new Session
            {
                Title = "Child Session 1",
                Description = "First child session",
                PersonNames = new List<string> { "ChildModel1" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            var childSession2 = new Session
            {
                Title = "Child Session 2",
                Description = "Second child session",
                PersonNames = new List<string> { "ChildModel2" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            await _sessionRepository.CreateSessionAsync(parentSession1);
            await _sessionRepository.CreateSessionAsync(parentSession2);
            await _sessionRepository.CreateSessionAsync(childSession1);
            await _sessionRepository.CreateSessionAsync(childSession2);
            
            // Set child sessions' parent IDs
            childSession1.ParentSessId = parentSession1.Id;
            childSession2.ParentSessId = parentSession2.Id;
            await _sessionRepository.SaveSessionAsync(childSession1);
            await _sessionRepository.SaveSessionAsync(childSession2);

            // Act & Assert

            // Test All sessions (default behavior)
            var allSessions = await _sessionRepository.GetAllSessionsAsync(SessionInclusionOptions.All);
            Assert.Equal(4, allSessions.Count());
            Assert.Contains(allSessions, s => s.Title == "Parent Session 1");
            Assert.Contains(allSessions, s => s.Title == "Parent Session 2");
            Assert.Contains(allSessions, s => s.Title == "Child Session 1");
            Assert.Contains(allSessions, s => s.Title == "Child Session 2");

            // Test All sessions (without specifying parameter - should default to All)
            var allSessionsDefault = await _sessionRepository.GetAllSessionsAsync();
            Assert.Equal(4, allSessionsDefault.Count());

            // Test ParentsOnly
            var parentSessions = await _sessionRepository.GetAllSessionsAsync(SessionInclusionOptions.ParentsOnly);
            Assert.Equal(2, parentSessions.Count());
            Assert.Contains(parentSessions, s => s.Title == "Parent Session 1");
            Assert.Contains(parentSessions, s => s.Title == "Parent Session 2");
            Assert.DoesNotContain(parentSessions, s => s.Title == "Child Session 1");
            Assert.DoesNotContain(parentSessions, s => s.Title == "Child Session 2");

            // Test ChildrenOnly
            var childSessions = await _sessionRepository.GetAllSessionsAsync(SessionInclusionOptions.ChildrenOnly);
            Assert.Equal(2, childSessions.Count());
            Assert.Contains(childSessions, s => s.Title == "Child Session 1");
            Assert.Contains(childSessions, s => s.Title == "Child Session 2");
            Assert.DoesNotContain(childSessions, s => s.Title == "Parent Session 1");
            Assert.DoesNotContain(childSessions, s => s.Title == "Parent Session 2");
        }

        [Fact]
        public async Task SessionTypeFieldIsPersisted()
        {
            // Arrange
            var session = new Session
            {
                Title = "SessionType Test Session",
                Description = "Testing SessionType field",
                PersonNames = new List<string> { "TestModel" },
                ToolNames = new List<string> { "TestTool" },
                SessionType = 0, // Normal
                AppId = 555L,
                PreviousId = 333L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            // Act
            var createdSession = await _sessionRepository.CreateSessionAsync(session);
            var retrievedSession = await _sessionRepository.GetSessionAsync(createdSession.Id);

            // Assert
            Assert.NotNull(retrievedSession);
            Assert.Equal(0, retrievedSession.SessionType); // Normal
            Assert.Equal("SessionType Test Session", retrievedSession.Title);

            // Test updating SessionType
            retrievedSession.SessionType = 0; // Normal
            await _sessionRepository.SaveSessionAsync(retrievedSession);
            var updatedSession = await _sessionRepository.GetSessionAsync(retrievedSession.Id);

            Assert.Equal(0, updatedSession?.SessionType); // Normal
        }

        [Fact]
        public async Task AppIdAndPreviousIdFieldsArePersisted()
        {
            // Arrange
            var session = new Session
            {
                Title = "AppId Test Session",
                Description = "Testing AppId and PreviousId fields",
                PersonNames = new List<string> { "AppTestModel" },
                ToolNames = new List<string> { "AppTestTool" },
                SessionType = 0, // Normal
                AppId = 77777L,
                PreviousId = 88888L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            // Act
            var createdSession = await _sessionRepository.CreateSessionAsync(session);
            var retrievedSession = await _sessionRepository.GetSessionAsync(createdSession.Id);

            // Assert
            Assert.NotNull(retrievedSession);
            Assert.Equal(77777L, retrievedSession.AppId);
            Assert.Equal(88888L, retrievedSession.PreviousId);

            // Test updating AppId and PreviousId
            retrievedSession.AppId = 99999L;
            retrievedSession.PreviousId = 11111L;
            await _sessionRepository.SaveSessionAsync(retrievedSession);
            var updatedSession = await _sessionRepository.GetSessionAsync(retrievedSession.Id);

            Assert.Equal(99999L, updatedSession?.AppId);
            Assert.Equal(11111L, updatedSession?.PreviousId);
        }

        [Fact]
        public async Task ToolNamesFieldIsPersisted()
        {
            // Arrange
            var toolNames = new List<string> { "WebSearchTool", "FileBrowserTool", "CodeExecutorTool" };
            var session = new Session
            {
                Title = "ToolNames Test Session",
                Description = "Testing ToolNames field",
                PersonNames = new List<string> { "ToolTestModel" },
                ToolNames = toolNames,
                SessionType = 0, // Normal
                AppId = 1234L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            // Act
            var createdSession = await _sessionRepository.CreateSessionAsync(session);
            var retrievedSession = await _sessionRepository.GetSessionAsync(createdSession.Id);

            // Assert
            Assert.NotNull(retrievedSession);
            Assert.NotNull(retrievedSession.ToolNames);
            Assert.Equal(3, retrievedSession.ToolNames.Count);
            Assert.Equal(toolNames, retrievedSession.ToolNames);
            Assert.Contains("WebSearchTool", retrievedSession.ToolNames);
            Assert.Contains("FileBrowserTool", retrievedSession.ToolNames);
            Assert.Contains("CodeExecutorTool", retrievedSession.ToolNames);

            // Test updating ToolNames
            retrievedSession.ToolNames = new List<string> { "NewTool1", "NewTool2" };
            await _sessionRepository.SaveSessionAsync(retrievedSession);
            var updatedSession = await _sessionRepository.GetSessionAsync(retrievedSession.Id);

            Assert.NotNull(updatedSession?.ToolNames);
            Assert.Equal(2, updatedSession.ToolNames.Count);
            Assert.Contains("NewTool1", updatedSession.ToolNames);
            Assert.Contains("NewTool2", updatedSession.ToolNames);
            Assert.DoesNotContain("WebSearchTool", updatedSession.ToolNames);
        }

        [Fact]
        public async Task EmptyToolNamesFieldIsPersisted()
        {
            // Arrange
            var session = new Session
            {
                Title = "Empty ToolNames Session",
                Description = "Testing empty ToolNames field",
                PersonNames = new List<string> { "EmptyToolTestModel" },
                ToolNames = new List<string>(), // Empty list
                SessionType = 0, // Normal
                AppId = 5555L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            // Act
            var createdSession = await _sessionRepository.CreateSessionAsync(session);
            var retrievedSession = await _sessionRepository.GetSessionAsync(createdSession.Id);

            // Assert
            Assert.NotNull(retrievedSession);
            Assert.NotNull(retrievedSession.ToolNames);
            Assert.Empty(retrievedSession.ToolNames);
        }

        [Fact]
        public async Task AllNewFieldsWorkTogether()
        {
            // Arrange - comprehensive test with all new fields
            var session = new Session
            {
                Title = "Comprehensive New Fields Test",
                Description = "Testing all newly added fields together",
                PersonNames = new List<string> { "ComprehensiveModel1", "ComprehensiveModel2" },
                ToolNames = new List<string> { "CompTool1", "CompTool2", "CompTool3" },
                SessionType = 0, // Normal
                AppId = 42424242L,
                PreviousId = 13131313L,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            // Act
            var createdSession = await _sessionRepository.CreateSessionAsync(session);
            var retrievedSession = await _sessionRepository.GetSessionAsync(createdSession.Id);

            // Assert all new fields
            Assert.NotNull(retrievedSession);
            Assert.Equal("Comprehensive New Fields Test", retrievedSession.Title);
            Assert.Equal(0, retrievedSession.SessionType); // Normal
            Assert.Equal(42424242L, retrievedSession.AppId);
            Assert.Equal(13131313L, retrievedSession.PreviousId);
            Assert.Equal(new List<string> { "ComprehensiveModel1", "ComprehensiveModel2" }, retrievedSession.PersonNames);
            Assert.Equal(new List<string> { "CompTool1", "CompTool2", "CompTool3" }, retrievedSession.ToolNames);

            // Test updating all fields
            retrievedSession.SessionType = 0; // Normal
            retrievedSession.AppId = 99999999L;
            retrievedSession.PreviousId = null;
            retrievedSession.ToolNames = new List<string> { "UpdatedTool" };
            
            await _sessionRepository.SaveSessionAsync(retrievedSession);
            var updatedSession = await _sessionRepository.GetSessionAsync(retrievedSession.Id);

            Assert.NotNull(updatedSession);
            Assert.Equal(0, updatedSession.SessionType); // Normal
            Assert.Equal(99999999L, updatedSession.AppId);
            Assert.Null(updatedSession.PreviousId);
            Assert.Single(updatedSession.ToolNames);
            Assert.Equal("UpdatedTool", updatedSession.ToolNames.First());
        }

        [Fact]
        public async Task CreateSessionWithDuplicateIdHandledCorrectly()
        {
            // Arrange
            var session1 = new Session
            {
                Id = 999, // Setting a specific ID
                Title = "Test Session 1",
                Description = "Test session 1 description",
                PersonNames = new List<string> { "Model1" },
                ToolNames = new List<string> { "Tool1" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            var session2 = new Session
            {
                Id = 999, // Same ID as session1
                Title = "Test Session 2",
                Description = "Test session 2 description",
                PersonNames = new List<string> { "Model2" },
                ToolNames = new List<string> { "Tool2" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            // Act
            var createdSession1 = await _sessionRepository.CreateSessionAsync(session1);
            var createdSession2 = await _sessionRepository.CreateSessionAsync(session2);

            // Assert
            // Both should be created successfully with different auto-generated IDs
            Assert.NotEqual(999, createdSession1.Id); // Should not use the provided ID
            Assert.NotEqual(999, createdSession2.Id); // Should not use the provided ID
            Assert.NotEqual(createdSession1.Id, createdSession2.Id); // Should have different IDs
            Assert.Equal("Test Session 1", createdSession1.Title);
            Assert.Equal("Test Session 2", createdSession2.Title);
        }

        [Fact]
        public async Task MultipleSessionsWithSameTitleAllowed()
        {
            // Arrange
            var session1 = new Session
            {
                Title = "Same Title Session",
                Description = "Description 1",
                PersonNames = new List<string> { "Model1" },
                ToolNames = new List<string> { "Tool1" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            var session2 = new Session
            {
                Title = "Same Title Session", // Same title as session1
                Description = "Description 2",
                PersonNames = new List<string> { "Model2" },
                ToolNames = new List<string> { "Tool2" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            // Act
            var createdSession1 = await _sessionRepository.CreateSessionAsync(session1);
            var createdSession2 = await _sessionRepository.CreateSessionAsync(session2);

            // Assert
            // Both should be created successfully since there's no unique constraint on title
            Assert.NotEqual(createdSession1.Id, createdSession2.Id);
            Assert.Equal("Same Title Session", createdSession1.Title);
            Assert.Equal("Same Title Session", createdSession2.Title);
            Assert.Equal("Description 1", createdSession1.Description);
            Assert.Equal("Description 2", createdSession2.Description);
        }

        [Fact]
        public async Task SaveSessionWithoutIdThrowsException()
        {
            // Arrange
            var session = new Session
            {
                Id = 0, // Invalid ID for save
                Title = "Test Session",
                Description = "Test description",
                PersonNames = new List<string> { "TestModel" },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _sessionRepository.SaveSessionAsync(session));
        }
    }
}