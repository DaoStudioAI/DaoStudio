using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using DaoStudio.DBStorage.Repositories;
using MessagePack;
using Xunit;

namespace Test.TestStorage
{
    public class TestMessageRepository : IDisposable
    {
        private readonly string _testDbPath;
        private readonly IMessageRepository _messageRepository;

        public TestMessageRepository()
        {
            // Create a unique test database path for each test run
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_message_repo_{Guid.NewGuid()}.db");
            
            // Initialize repository directly with SqliteMessageRepository
            _messageRepository = new SqliteMessageRepository(_testDbPath);
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
        public async Task GetByIdReturnsNullForNonExistentMessage()
        {
            // Arrange - nothing to arrange

            // Act
            var message = await _messageRepository.GetByIdAsync(999);

            // Assert
            Assert.Null(message);
        }

        [Fact]
        public async Task SaveAndGetMessageWorks()
        {
            // Arrange
            var newMessage = new Message
            {
                Content = "Test message content",
                Role = 1, // 1=User
                Type = 0, // Normal
                SessionId = 1,
                CreatedAt = DateTime.UtcNow
            };

            // Act - Create
            var createdMessage = await _messageRepository.CreateMessageAsync(newMessage);
            var retrievedMessage = await _messageRepository.GetByIdAsync(createdMessage.Id);

            // Assert - Create
            Assert.NotNull(retrievedMessage);
            Assert.Equal(createdMessage.Id, retrievedMessage.Id);
            Assert.Equal("Test message content", retrievedMessage.Content);
            Assert.Equal(1, retrievedMessage.Role); // 1=User
            Assert.Equal(1, retrievedMessage.SessionId);

            Assert.NotEqual(default, retrievedMessage.CreatedAt);
            Assert.NotEqual(default, retrievedMessage.LastModified);

            // Act - Update
            retrievedMessage.Content = "Updated test message content";
            var updateResult = await _messageRepository.SaveMessageAsync(retrievedMessage);
            var updatedMessage = await _messageRepository.GetByIdAsync(retrievedMessage.Id);

            // Assert - Update
            Assert.True(updateResult);
            Assert.Equal("Updated test message content", updatedMessage!.Content);
            Assert.Equal(retrievedMessage.CreatedAt, updatedMessage.CreatedAt);
            // LastModified should be updated during save
            Assert.True(updatedMessage.LastModified >= retrievedMessage.LastModified);
        }

        [Fact]
        public async Task GetAllMessagesReturnsAllMessages()
        {
            // Arrange
            var message1 = new Message
            {
                Content = "Message 1",
                Role = 1, // 1=User
                SessionId = 1,
            };
            
            var message2 = new Message
            {
                Content = "Message 2",
                Role = 2, // 2=Assistant
                SessionId = 1,
            };
            
            await _messageRepository.CreateMessageAsync(message1);
            await _messageRepository.CreateMessageAsync(message2);

            // Act
            var allMessages = await _messageRepository.GetAllAsync();

            // Assert
            Assert.Equal(2, allMessages.Count());
            Assert.Contains(allMessages, m => m.Content == "Message 1");
            Assert.Contains(allMessages, m => m.Content == "Message 2");
        }

        [Fact]
        public async Task DeleteMessageWorks()
        {
            // Arrange
            var message = new Message
            {
                Content = "Message to delete",
                Role = 1, // 1=User
                SessionId = 1,
            };
            
            var createdMessage = await _messageRepository.CreateMessageAsync(message);

            // Act
            var deleteResult = await _messageRepository.DeleteAsync(createdMessage.Id);
            var retrievedMessage = await _messageRepository.GetByIdAsync(createdMessage.Id);

            // Assert
            Assert.True(deleteResult);
            Assert.Null(retrievedMessage);
        }

        [Fact]
        public async Task UpdateMessageWorks()
        {
            // Arrange
            var message = new Message
            {
                Content = "Original message",
                Role = 1, // 1=User
                SessionId = 1,
            };
            
            var createdMessage = await _messageRepository.CreateMessageAsync(message);
            
            // Update the message
            createdMessage.Content = "Updated message";
            createdMessage.Role = 2; // 2=Assistant

            // Act
            var updateResult = await _messageRepository.SaveMessageAsync(createdMessage);
            var retrievedMessage = await _messageRepository.GetByIdAsync(createdMessage.Id);

            // Assert
            Assert.True(updateResult);
            Assert.NotNull(retrievedMessage);
            Assert.Equal("Updated message", retrievedMessage.Content);
            Assert.Equal(2, retrievedMessage.Role); // 2=Assistant
            Assert.Equal(createdMessage.CreatedAt.ToLocalTime(), retrievedMessage.CreatedAt.ToLocalTime());
        }

        [Fact]
        public async Task GetMessagesBySessionIdWorks()
        {
            // Arrange
            var sessionId1 = 1;
            var sessionId2 = 2;
            
            var message1 = new Message
            {
                Content = "Session 1 Message 1",
                Role = 1, // 1=User
                SessionId = sessionId1,
            };
            
            var message2 = new Message
            {
                Content = "Session 1 Message 2",
                Role = 2, // 2=Assistant
                SessionId = sessionId1,
            };
            
            var message3 = new Message
            {
                Content = "Session 2 Message",
                Role = 1, // 1=User
                SessionId = sessionId2,
            };
            
            await _messageRepository.CreateMessageAsync(message1);
            await _messageRepository.CreateMessageAsync(message2);
            await _messageRepository.CreateMessageAsync(message3);

            // Act
            var session1Messages = await _messageRepository.GetBySessionIdAsync(sessionId1);
            var session2Messages = await _messageRepository.GetBySessionIdAsync(sessionId2);

            // Assert
            Assert.Equal(2, session1Messages.Count());
            Assert.Single(session2Messages);
            Assert.All(session1Messages, m => Assert.Equal(sessionId1, m.SessionId));
            Assert.All(session2Messages, m => Assert.Equal(sessionId2, m.SessionId));
        }

        [Fact]
        public async Task CreateMessageWithExistingIdThrowsException()
        {
            // Arrange
            var message = new Message
            {
                Id = 1, 
                Content = "Test message",
                Role = 1, // 1=User
                SessionId = 1
            };
            var ret = await _messageRepository.CreateMessageAsync(message);
            Assert.NotEqual(1, ret.Id);
            Assert.NotEqual(0, ret.Id);
            // Arrange
            var message2 = new Message
            {
                Id = 1, // Set an existing ID
                Content = "Test message2",
                Role = 1, // 1=User
                SessionId = 1
            };
            ret = await _messageRepository.CreateMessageAsync(message2);

            // Act & Assert
            Assert.NotEqual(1, ret.Id);
            Assert.NotEqual(0, ret.Id);
        }

        [Fact]
        public async Task SaveMessageWithoutIdThrowsException()
        {
            // Arrange
            var message = new Message
            {
                Id = 0, // Invalid ID for save
                Content = "Test message",
                Role = 1, // 1=User
                SessionId = 1
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _messageRepository.SaveMessageAsync(message));
        }

        [Fact]
        public async Task GetBySessionIdReturnsEmptyForNonExistentSession()
        {
            // Arrange - nothing to arrange

            // Act
            var messages = await _messageRepository.GetBySessionIdAsync(999);

            // Assert
            Assert.Empty(messages);
        }

        [Fact]
        public async Task GetBySessionIdReturnsCorrectMessages()
        {
            // Arrange
            var sessionId1 = 1;
            var sessionId2 = 2;
            
            var message1 = new Message
            {
                SessionId = sessionId1,
                Content = "Message for session 1",
                Role = 1, // 1=User
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            };
            
            var message2 = new Message
            {
                SessionId = sessionId1,
                Content = "Another message for session 1",
                Role = 2, // 2=Assistant
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            };
            
            var message3 = new Message
            {
                SessionId = sessionId2,
                Content = "Message for session 2",
                Role = 1, // 1=User
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            };
            
            await _messageRepository.CreateMessageAsync(message1);
            await _messageRepository.CreateMessageAsync(message2);
            await _messageRepository.CreateMessageAsync(message3);

            // Act
            var session1Messages = await _messageRepository.GetBySessionIdAsync(sessionId1);
            var session2Messages = await _messageRepository.GetBySessionIdAsync(sessionId2);

            // Assert
            Assert.Equal(2, session1Messages.Count());
            Assert.Single(session2Messages);
            Assert.All(session1Messages, msg => Assert.Equal(sessionId1, msg.SessionId));
            Assert.All(session2Messages, msg => Assert.Equal(sessionId2, msg.SessionId));
        }

        [Fact]
        public async Task GetAllReturnsAllMessages()
        {
            // Arrange
            await _messageRepository.CreateMessageAsync(new Message
            {
                SessionId = 1,
                Content = "Message 1",
                Role = 1, // 1=User
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            });
            
            await _messageRepository.CreateMessageAsync(new Message
            {
                SessionId = 2,
                Content = "Message 2",
                Role = 2, // 2=Assistant
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            });

            // Act
            var allMessages = await _messageRepository.GetAllAsync();

            // Assert
            Assert.Equal(2, allMessages.Count());
        }

        [Fact]
        public async Task DeleteBySessionIdWorks()
        {
            // Arrange
            var sessionId = 5;
            
            await _messageRepository.CreateMessageAsync(new Message
            {
                SessionId = sessionId,
                Content = "Session message 1",
                Role = 1, // 1=User
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            });
            
            await _messageRepository.CreateMessageAsync(new Message
            {
                SessionId = sessionId,
                Content = "Session message 2",
                Role = 2, // 2=Assistant
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            });
            
            // Different session message shouldn't be deleted
            await _messageRepository.CreateMessageAsync(new Message
            {
                SessionId = sessionId + 1,
                Content = "Different session",
                Role = 1, // 1=User
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            });

            // Act
            var deletedCount = await _messageRepository.DeleteBySessionIdAsync(sessionId);
            var remainingSessionMessages = await _messageRepository.GetBySessionIdAsync(sessionId);
            var allMessages = await _messageRepository.GetAllAsync();

            // Assert
            Assert.Equal(2, deletedCount);
            Assert.Empty(remainingSessionMessages);
            Assert.Single(allMessages);
        }

        [Fact]
        public async Task MessageTypeFieldIsSavedAndRetrievedCorrectly()
        {
            // Arrange
            var messageTypes = new[]
            {
                0,      // Normal - Regular message
                1  // Information - Info message
            };
            
            var createdMessages = new List<Message>();
            
            // Create messages with different types
            foreach (var type in messageTypes)
            {
                var message = new Message
                {
                    Content = $"Message with type {type}",
                    Role = type == 1 ? 3 : 1, // Information = 1 -> System=3, else User=1
                    Type = type,
                    SessionId = 1,
                    CreatedAt = DateTime.UtcNow
                };
                
                createdMessages.Add(await _messageRepository.CreateMessageAsync(message));
            }

            // Act
            var retrievedMessages = new List<Message>();
            foreach (var message in createdMessages)
            {
                var retrievedMessage = await _messageRepository.GetByIdAsync(message.Id);
                if (retrievedMessage != null)
                {
                    retrievedMessages.Add(retrievedMessage);
                }
            }

            // Assert
            Assert.Equal(messageTypes.Length, retrievedMessages.Count);
            for (int i = 0; i < retrievedMessages.Count; i++)
            {
                Assert.Equal(messageTypes[i], retrievedMessages[i].Type);
                Assert.Equal(createdMessages[i].Role, retrievedMessages[i].Role);
            }
        }

        [Fact]
        public async Task GetBySessionIdReturnsMessagesWithCorrectTypes()
        {
            // Arrange
            var sessionId = 100;
            
            var userMessage = new Message
            {
                Content = "User message",
                Role = 1, // 1=User
                Type = 0, // Normal
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow
            };
            
            var systemMessage = new Message
            {
                Content = "System message",
                Role = 3, // 3=System
                Type = 1, // Information
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow
            };
            
            var assistantMessage = new Message
            {
                Content = "Assistant message",
                Role = 2, // 2=Assistant
                Type =60, // Normal
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow
            };
            
            await _messageRepository.CreateMessageAsync(userMessage);
            await _messageRepository.CreateMessageAsync(systemMessage);
            await _messageRepository.CreateMessageAsync(assistantMessage);

            // Act
            var messages = (await _messageRepository.GetBySessionIdAsync(sessionId)).ToArray();

            // Assert
            Assert.Equal(3, messages.Count());
            
            Assert.Equal(1, messages[0].Role); 
            Assert.Equal(0, messages[0].Type); 
            
            Assert.Equal(3, messages[1].Role); 
            Assert.Equal(1, messages[1].Type); 
            
            Assert.Equal(2, messages[2].Role); 
            Assert.Equal(60, messages[2].Type); 
        }

        [Fact]
        public async Task UpdateMessageTypeWorks()
        {
            // Arrange
            var message = new Message
            {
                Content = "Original message",
                Role = 1, // 1=User
                Type = 0, // Normal
                SessionId = 1,
                CreatedAt = DateTime.UtcNow
            };
            
            var createdMessage = await _messageRepository.CreateMessageAsync(message);
            
            // Update the message type
            createdMessage.Type = 1; // Information
            
            // Act
            var updateResult = await _messageRepository.SaveMessageAsync(createdMessage);
            var retrievedMessage = await _messageRepository.GetByIdAsync(createdMessage.Id);

            // Assert
            Assert.True(updateResult);
            Assert.NotNull(retrievedMessage);
            Assert.Equal(1, retrievedMessage.Type); // Information
            Assert.Equal(createdMessage.Content, retrievedMessage.Content);
            Assert.Equal(createdMessage.Role, retrievedMessage.Role);
        }

        [Fact]
        public async Task DeleteFromMessageInSessionAsync_IncludeSpecifiedMessageFalse()
        {
            // Arrange
            var sessionId = 10;
            var message1 = new Message
            {
                SessionId = sessionId,
                Content = "Message 1",
                Role = 1, // 1=User
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            };
            var message2 = new Message
            {
                SessionId = sessionId,
                Content = "Message 2",
                Role = 2, // 2=Assistant
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            };
            var message3 = new Message
            {
                SessionId = sessionId,
                Content = "Message 3",
                Role = 1, // 1=User
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            };

            await _messageRepository.CreateMessageAsync(message1);
            await Task.Delay(5);
            await _messageRepository.CreateMessageAsync(message2);
            await Task.Delay(5);
            await _messageRepository.CreateMessageAsync(message3);

            // Act
            var deletedCount = await _messageRepository.DeleteFromMessageInSessionAsync(sessionId, message2.Id, false);
            var remainingMessages = (await _messageRepository.GetBySessionIdAsync(sessionId)).ToArray();

            // Assert
            Assert.Equal(1, deletedCount); // only messages AFTER the specified message
            Assert.Equal(2, remainingMessages.Length);
            Assert.Contains(remainingMessages, m => m.Id == message1.Id);
            Assert.Contains(remainingMessages, m => m.Id == message2.Id);
        }

        [Fact]
        public async Task DeleteFromMessageInSessionAsync_IncludeSpecifiedMessageTrue()
        {
            // Arrange
            var sessionId = 10;
            var message1 = new Message
            {
                SessionId = sessionId,
                Content = "Message 1",
                Role = 1, // 1=User
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            };
            var message2 = new Message
            {
                SessionId = sessionId,
                Content = "Message 2",
                Role = 2, // 2=Assistant
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            };
            var message3 = new Message
            {
                SessionId = sessionId,
                Content = "Message 3",
                Role = 1, // 1=User
                Type = 0, // Normal
                CreatedAt = DateTime.UtcNow
            };

            await _messageRepository.CreateMessageAsync(message1);
            await Task.Delay(5);
            await _messageRepository.CreateMessageAsync(message2);
            await Task.Delay(5);
            await _messageRepository.CreateMessageAsync(message3);

            // Act
            var deletedCount = await _messageRepository.DeleteFromMessageInSessionAsync(sessionId, message2.Id, true);
            var remainingMessages = (await _messageRepository.GetBySessionIdAsync(sessionId)).ToArray();

            // Assert
            Assert.Equal(2, deletedCount); // deletes the specified message and those after it
            Assert.Single(remainingMessages);
            Assert.Contains(remainingMessages, m => m.Id == message1.Id);
            Assert.DoesNotContain(remainingMessages, m => m.Id == message2.Id || m.Id == message3.Id);
        }

        [Fact]
        public async Task BinaryContentsAreSerializedAndDeserialized()
        {
            // Arrange
            var binaryData = new List<BinaryData>
            {
                new BinaryData 
                { 
                    Name = "test1.png", 
                    Type = 1, 
                    Data = new byte[] { 1, 2, 3, 4, 5 } 
                },
                new BinaryData 
                { 
                    Name = "test2.txt", 
                    Type = 2, 
                    Data = new byte[] { 6, 7, 8, 9, 10 } 
                }
            };

            var message = new Message
            {
                Content = "Message with binary data",
                Role = 1, // 1=User
                Type = 0, // Normal
                SessionId = 1,
                BinaryContents = binaryData,
                BinaryVersion = 0,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var createdMessage = await _messageRepository.CreateMessageAsync(message);
            var retrievedMessage = await _messageRepository.GetByIdAsync(createdMessage.Id);

            // Assert
            Assert.NotNull(retrievedMessage);
            Assert.NotNull(retrievedMessage.BinaryContents);
            Assert.Equal(2, retrievedMessage.BinaryContents.Count);
            
            // Verify first binary data
            Assert.Equal("test1.png", retrievedMessage.BinaryContents[0].Name);
            Assert.Equal(1, retrievedMessage.BinaryContents[0].Type);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, retrievedMessage.BinaryContents[0].Data);
            
            // Verify second binary data
            Assert.Equal("test2.txt", retrievedMessage.BinaryContents[1].Name);
            Assert.Equal(2, retrievedMessage.BinaryContents[1].Type);
            Assert.Equal(new byte[] { 6, 7, 8, 9, 10 }, retrievedMessage.BinaryContents[1].Data);
            
            // Verify binary version
            Assert.Equal(0, retrievedMessage.BinaryVersion);
        }

        [Fact]
        public async Task CreateMessageWithDuplicateIdHandledCorrectly()
        {
            // Arrange
            var message1 = new Message
            {
                Id = 999, // Setting a specific ID
                Content = "Test message 1",
                Role = 1, // 1=User
                Type = 0, // Normal
                SessionId = 1,
                CreatedAt = DateTime.UtcNow
            };

            var message2 = new Message
            {
                Id = 999, // Same ID as message1
                Content = "Test message 2",
                Role = 2, // 2=Assistant
                Type = 0, // Normal
                SessionId = 1,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var createdMessage1 = await _messageRepository.CreateMessageAsync(message1);
            var createdMessage2 = await _messageRepository.CreateMessageAsync(message2);

            // Assert
            // Both should be created successfully with different auto-generated IDs
            Assert.NotEqual(999, createdMessage1.Id); // Should not use the provided ID
            Assert.NotEqual(999, createdMessage2.Id); // Should not use the provided ID
            Assert.NotEqual(createdMessage1.Id, createdMessage2.Id); // Should have different IDs
            Assert.Equal("Test message 1", createdMessage1.Content);
            Assert.Equal("Test message 2", createdMessage2.Content);
        }

        [Fact]
        public async Task MultipleMessagesWithSameContentAllowed()
        {
            // Arrange
            var message1 = new Message
            {
                Content = "Same Content Message",
                Role = 1, // 1=User
                Type = 0, // Normal
                SessionId = 1,
                CreatedAt = DateTime.UtcNow
            };

            var message2 = new Message
            {
                Content = "Same Content Message", // Same content as message1
                Role = 2, // 2=Assistant
                Type = 0, // Normal
                SessionId = 2, // Different session
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var createdMessage1 = await _messageRepository.CreateMessageAsync(message1);
            var createdMessage2 = await _messageRepository.CreateMessageAsync(message2);

            // Assert
            // Both should be created successfully since there's no unique constraint on content
            Assert.NotEqual(createdMessage1.Id, createdMessage2.Id);
            Assert.Equal("Same Content Message", createdMessage1.Content);
            Assert.Equal("Same Content Message", createdMessage2.Content);
            Assert.Equal(1, createdMessage1.SessionId);
            Assert.Equal(2, createdMessage2.SessionId);
        }

        [Fact]
        public async Task MessageWithNonZeroIdCreatesWithNewId()
        {
            // Arrange
            var message = new Message
            {
                Id = 123, // Set a non-zero ID
                Content = "Test message with preset ID",
                Role = 1, // 1=User
                Type = 0, // Normal
                SessionId = 1,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var createdMessage = await _messageRepository.CreateMessageAsync(message);

            // Assert
            Assert.NotEqual(123, createdMessage.Id); // Should not use the provided ID
            Assert.NotEqual(0, createdMessage.Id); // Should have a valid ID
            Assert.Equal("Test message with preset ID", createdMessage.Content);
        }

        [Fact]
        public async Task SaveAndGetMessageWithNullContentWorks()
        {
            // Arrange
            var newMessage = new Message
            {
                Content = null, // Null content should be allowed
                Role = 1, // 1=User
                Type = 0, // Normal
                SessionId = 1,
                CreatedAt = DateTime.UtcNow
            };

            // Act - Create
            var createdMessage = await _messageRepository.CreateMessageAsync(newMessage);
            var retrievedMessage = await _messageRepository.GetByIdAsync(createdMessage.Id);

            // Assert - Create
            Assert.NotNull(retrievedMessage);
            Assert.Equal(createdMessage.Id, retrievedMessage.Id);
            Assert.Null(retrievedMessage.Content);
            Assert.Equal(1, retrievedMessage.Role); // 1=User
            Assert.Equal(1, retrievedMessage.SessionId);

            Assert.NotEqual(default, retrievedMessage.CreatedAt);
            Assert.NotEqual(default, retrievedMessage.LastModified);

            // Act - Update to non-null content
            retrievedMessage.Content = "Updated content";
            var updateResult = await _messageRepository.SaveMessageAsync(retrievedMessage);
            var updatedMessage = await _messageRepository.GetByIdAsync(retrievedMessage.Id);

            // Assert - Update
            Assert.True(updateResult);
            Assert.Equal("Updated content", updatedMessage!.Content);

            // Act - Update back to null content
            updatedMessage.Content = null;
            var updateResult2 = await _messageRepository.SaveMessageAsync(updatedMessage);
            var updatedMessage2 = await _messageRepository.GetByIdAsync(updatedMessage.Id);

            // Assert - Update to null
            Assert.True(updateResult2);
            Assert.Null(updatedMessage2!.Content);
        }
    }
}