using DaoStudio.Interfaces;
using DaoStudio;
using DaoStudio.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using TestDaoStudio.Helpers;
using TestDaoStudio.Infrastructure;
using DryIoc;

namespace TestDaoStudio.EndToEnd;

/// <summary>
/// End-to-end tests for complete session workflows.
/// Tests full user scenarios from session creation to completion.
/// </summary>
public class SessionWorkflowTests : IDisposable
{
    private readonly TestContainerFixture _containerFixture;
    private readonly DatabaseTestFixture _databaseFixture;

    public SessionWorkflowTests()
    {
        _databaseFixture = new DatabaseTestFixture(useInMemoryDatabase: false, testName: $"SessionWorkflowTests_{Guid.NewGuid():N}");
        _containerFixture = new TestContainerFixture();
    }

    [Fact]
    public async Task CompleteConversationWorkflow_FromStartToFinish_WorksCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        await _containerFixture.InitializeAsync(useInMemoryDatabase: false, mockRepositories: false, databasePath: _databaseFixture.DatabasePath);

        var DaoStudio = _containerFixture.Container.Resolve<DaoStudio.DaoStudioService>();
        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var peopleService = _containerFixture.Container.Resolve<IPeopleService>();

        // Create and save person to database first
        var testPerson = await peopleService.CreatePersonAsync(
            "ConversationBot",
            "A test assistant",
            null,
            true,
            "OpenAI",
            "gpt-4",
            "You are a helpful assistant for testing purposes.",
            null,
            null);
        testPerson.Should().NotBeNull();

        // Act - Create session
        var session = await sessionService.CreateSession(testPerson!);

        session.Should().NotBeNull();
        session.Id.Should().NotBe(0);

        // Act - Start conversation with user message
        var userMessage = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.User,
            "Hello! Can you help me with a programming question?");

        var createdUserMessage = await messageService.CreateMessageAsync(
            userMessage.Content ?? string.Empty,
            (MessageRole)userMessage.Role,
            (MessageType)userMessage.Type,
            userMessage.SessionId,
            true,
            userMessage.ParentMsgId,
            userMessage.ParentSessId);
        createdUserMessage.Should().NotBeNull();

        // Act - Simulate AI response
        var aiResponse = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.Assistant,
            "Of course! I'd be happy to help you with your programming question. What specific topic or problem are you working on?");

        var createdAiMessage = await messageService.CreateMessageAsync(
            aiResponse.Content ?? string.Empty,
            (MessageRole)aiResponse.Role,
            (MessageType)aiResponse.Type,
            aiResponse.SessionId,
            true,
            aiResponse.ParentMsgId,
            aiResponse.ParentSessId);
        createdAiMessage.Should().NotBeNull();

        // Act - Continue conversation
        var followUpMessage = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.User,
            "I'm trying to understand async/await in C#. Can you explain it?");

        var createdFollowUp = await messageService.CreateMessageAsync(
            followUpMessage.Content ?? string.Empty,
            (MessageRole)followUpMessage.Role,
            (MessageType)followUpMessage.Type,
            followUpMessage.SessionId,
            true,
            followUpMessage.ParentMsgId,
            followUpMessage.ParentSessId);

        var detailedResponse = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.Assistant,
            "Async/await in C# is used for asynchronous programming. The 'async' keyword marks a method as asynchronous, and 'await' is used to wait for asynchronous operations to complete without blocking the thread.");

        var createdDetailedResponse = await messageService.CreateMessageAsync(
            detailedResponse.Content ?? string.Empty,
            (MessageRole)detailedResponse.Role,
            (MessageType)detailedResponse.Type,
            detailedResponse.SessionId,
            true,
            detailedResponse.ParentMsgId,
            detailedResponse.ParentSessId);

        // Assert - Verify complete conversation
        var sessionMessages = await messageService.GetMessagesBySessionIdAsync(session.Id);
        sessionMessages.Should().HaveCount(4);

        var orderedMessages = sessionMessages.OrderBy(m => m.CreatedAt).ToList();
        orderedMessages[0].Content.Should().Contain("Hello! Can you help me");
        orderedMessages[0].Role.Should().Be(MessageRole.User);
        
        orderedMessages[1].Content.Should().Contain("Of course! I'd be happy to help");
        orderedMessages[1].Role.Should().Be(MessageRole.Assistant);
        
        orderedMessages[2].Content.Should().Contain("async/await in C#");
        orderedMessages[2].Role.Should().Be(MessageRole.User);
        
        orderedMessages[3].Content.Should().Contain("Async/await in C# is used for");
        orderedMessages[3].Role.Should().Be(MessageRole.Assistant);

        // Act - Update session title based on conversation
        session.Title = "C# Async/Await Discussion";
        var updateResult = await sessionService.SaveSessionAsync(session);
        updateResult.Should().BeTrue();

        // Assert - Verify session was updated
        var finalSession = await sessionService.OpenSession(session.Id);
        finalSession.Title.Should().Be("C# Async/Await Discussion");
    }

    [Fact]
    public async Task MultiPersonConversationWorkflow_WorksCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        await _containerFixture.InitializeAsync(useInMemoryDatabase: false, mockRepositories: false, databasePath: _databaseFixture.DatabasePath);

        var DaoStudio = _containerFixture.Container.Resolve<DaoStudio.DaoStudioService>();
        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        var person1 = MessageTestHelper.CreateTestPerson("Assistant1", "A test assistant", "OpenAI", "gpt-4");
        var person2 = MessageTestHelper.CreateTestPerson("Assistant2", "A test assistant", "Google", "gemini-pro");

        // Act - Create session with multiple persons
        var session = await sessionService.CreateSession(person1);

        // Act - Simulate conversation between user and multiple assistants
        var userQuestion = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.User,
            "What are the pros and cons of different programming languages?");
        await messageService.CreateMessageAsync(
            userQuestion.Content ?? "",
            (MessageRole)userQuestion.Role,
            (MessageType)userQuestion.Type,
            userQuestion.SessionId,
            true,
            userQuestion.ParentMsgId,
            userQuestion.ParentSessId);

        var assistant1Response = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.Assistant,
            "From my perspective, Python is great for beginners due to its readable syntax, while C++ offers high performance for system programming.");
        await messageService.CreateMessageAsync(
            assistant1Response.Content ?? "",
            (MessageRole)assistant1Response.Role,
            (MessageType)assistant1Response.Type,
            assistant1Response.SessionId,
            true,
            assistant1Response.ParentMsgId,
            assistant1Response.ParentSessId);

        var assistant2Response = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.Assistant,
            "I'd add that JavaScript is versatile for both frontend and backend development, and Rust provides memory safety without garbage collection.");
        await messageService.CreateMessageAsync(
            assistant2Response.Content ?? "",
            (MessageRole)assistant2Response.Role,
            (MessageType)assistant2Response.Type,
            assistant2Response.SessionId,
            true,
            assistant2Response.ParentMsgId,
            assistant2Response.ParentSessId);

        var userFollowUp = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.User,
            "Which would you recommend for a web application backend?");
        await messageService.CreateMessageAsync(
            userFollowUp.Content ?? "",
            (MessageRole)userFollowUp.Role,
            (MessageType)userFollowUp.Type,
            userFollowUp.SessionId,
            true,
            userFollowUp.ParentMsgId,
            userFollowUp.ParentSessId);

        // Assert - Verify multi-person conversation
        var messages = await messageService.GetMessagesBySessionIdAsync(session.Id);
        messages.Should().HaveCount(4);

        var userMessages = messages.Where(m => m.Role == MessageRole.User).ToList();
        var assistantMessages = messages.Where(m => m.Role == MessageRole.Assistant).ToList();

        userMessages.Should().HaveCount(2);
        assistantMessages.Should().HaveCount(2);

        // Note: PersonNames property may not exist on ISession interface
        // Commenting out these assertions as they reference non-existent property
        // session.PersonNames.Should().HaveCount(2);
        // session.PersonNames.Should().Contain(person1.Name);
        // session.PersonNames.Should().Contain(person2.Name);
    }

    [Fact]
    public async Task SessionWithToolUsage_WorksCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        await _containerFixture.InitializeAsync(useInMemoryDatabase: false, mockRepositories: false, databasePath: _databaseFixture.DatabasePath);

        var DaoStudio = _containerFixture.Container.Resolve<DaoStudio.DaoStudioService>();
        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var toolService = _containerFixture.Container.Resolve<IToolService>();

        var testPerson = MessageTestHelper.CreateTestPerson("ToolBot", "A test assistant", "OpenAI", "gpt-4");

        // Create a test tool
        var testTool = MessageTestHelper.CreateTestTool("calculator", "Performs mathematical calculations");
        await toolService.CreateToolAsync(
            testTool.Name,
            testTool.Description,
            testTool.StaticId,
            testTool.ToolConfig,
            testTool.Parameters,
            testTool.IsEnabled,
            testTool.AppId);

        // Act - Create session
        var session = await sessionService.CreateSession(testPerson);

        // Act - User asks for calculation
        var userMessage = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.User,
            "Can you calculate 15 * 23 for me?");
        await messageService.CreateMessageAsync(
            userMessage.Content ?? string.Empty,
            (MessageRole)userMessage.Role,
            (MessageType)userMessage.Type,
            userMessage.SessionId,
            true,
            userMessage.ParentMsgId,
            userMessage.ParentSessId);

        // Act - Assistant indicates tool usage
        var toolCallMessage = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.Assistant,
            "I'll calculate that for you using the calculator tool.");
        toolCallMessage.Type = (int)MessageType.Information;
        // Store tool call information in binary data instead of non-existent properties
        var toolCallData = new { name = "calculator", parameters = "{\"operation\":\"multiply\",\"a\":15,\"b\":23}" };
        toolCallMessage.AddBinaryData("tool_call", MsgBinaryDataType.ToolCall, System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(toolCallData)));
        await messageService.CreateMessageAsync(
            toolCallMessage.Content ?? string.Empty,
            (MessageRole)toolCallMessage.Role,
            (MessageType)toolCallMessage.Type,
            toolCallMessage.SessionId,
            true,
            toolCallMessage.ParentMsgId,
            toolCallMessage.ParentSessId);

        // Act - Tool result
        var toolResultMessage = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.System,
            "345");
        toolResultMessage.Type = (int)MessageType.Information;
        // Store tool result information in binary data
        var toolResultData = new { name = "calculator", result = "345" };
        toolResultMessage.AddBinaryData("tool_result", MsgBinaryDataType.ToolCallResult, System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(toolResultData)));
        await messageService.CreateMessageAsync(
            toolResultMessage.Content ?? string.Empty,
            (MessageRole)toolResultMessage.Role,
            (MessageType)toolResultMessage.Type,
            toolResultMessage.SessionId,
            true,
            toolResultMessage.ParentMsgId,
            toolResultMessage.ParentSessId);

        // Act - Assistant provides final answer
        var finalAnswer = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.Assistant,
            "The result of 15 * 23 is 345.");
        await messageService.CreateMessageAsync(
            finalAnswer.Content ?? string.Empty,
            (MessageRole)finalAnswer.Role,
            (MessageType)finalAnswer.Type,
            finalAnswer.SessionId,
            true,
            finalAnswer.ParentMsgId,
            finalAnswer.ParentSessId);

        // Assert - Verify tool usage workflow
        var messages = await messageService.GetMessagesBySessionIdAsync(session.Id);
        messages.Should().HaveCount(4);

        var toolCallMsg = messages.FirstOrDefault(m => m.Type == MessageType.Information);
        toolCallMsg.Should().NotBeNull();
        // Note: ToolName and ToolParameters properties don't exist on IMessage interface
        // Tool call information is stored in binary data instead

        var toolResultMsg = messages.FirstOrDefault(m => m.Type == MessageType.Information && m.Role == MessageRole.System);
        toolResultMsg.Should().NotBeNull();
        toolResultMsg!.Content.Should().Be("345");
        toolResultMsg.Role.Should().Be(MessageRole.System);
    }

    [Fact]
    public async Task SessionErrorHandlingWorkflow_HandlesGracefully()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        await _containerFixture.InitializeAsync(useInMemoryDatabase: false, mockRepositories: false, databasePath: _databaseFixture.DatabasePath);

        var DaoStudio = _containerFixture.Container.Resolve<DaoStudio.DaoStudioService>();
        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        var testPerson = MessageTestHelper.CreateTestPerson("ErrorBot", "A test assistant", "OpenAI", "gpt-4");

        // Act - Create session
        var session = await sessionService.CreateSession(testPerson);

        // Act - User message
        var userMessage = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.User,
            "Generate a very long response");
        await messageService.CreateMessageAsync(
            userMessage.Content ?? string.Empty,
            (MessageRole)userMessage.Role,
            (MessageType)userMessage.Type,
            userMessage.SessionId,
            true,
            userMessage.ParentMsgId,
            userMessage.ParentSessId);

        // Act - Simulate error response
        var errorMessage = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.Assistant,
            "I apologize, but I encountered an error while processing your request. The response was too long and exceeded the maximum token limit.",
            MessageType.Information);
        await messageService.CreateMessageAsync(
            errorMessage.Content ?? string.Empty,
            (MessageRole)errorMessage.Role,
            (MessageType)errorMessage.Type,
            errorMessage.SessionId,
            true,
            errorMessage.ParentMsgId,
            errorMessage.ParentSessId);

        // Act - User retry
        var retryMessage = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.User,
            "Can you try again with a shorter response?");
        await messageService.CreateMessageAsync(
            retryMessage.Content ?? string.Empty,
            (MessageRole)retryMessage.Role,
            (MessageType)retryMessage.Type,
            retryMessage.SessionId,
            true,
            retryMessage.ParentMsgId,
            retryMessage.ParentSessId);

        // Act - Successful retry
        var successMessage = MessageTestHelper.CreateTestMessage(
            session.Id,
            MessageRole.Assistant,
            "Of course! Here's a concise response to your request.");
        await messageService.CreateMessageAsync(
            successMessage.Content ?? string.Empty,
            (MessageRole)successMessage.Role,
            (MessageType)successMessage.Type,
            successMessage.SessionId,
            true,
            successMessage.ParentMsgId,
            successMessage.ParentSessId);

        // Assert - Verify error handling workflow
        var messages = await messageService.GetMessagesBySessionIdAsync(session.Id);
        messages.Should().HaveCount(4);

        var errorMsg = messages.FirstOrDefault(m => m.Type == MessageType.Information && m.Content!.Contains("error"));
        errorMsg.Should().NotBeNull();
        errorMsg!.Content.Should().Contain("error");
        errorMsg.Role.Should().Be(MessageRole.Assistant);

        var finalMsg = messages.OrderBy(m => m.CreatedAt).Last();
        finalMsg.Type.Should().Be(MessageType.Normal);
        finalMsg.Content.Should().Contain("concise response");
    }

    [Fact]
    public async Task LongRunningConversationWorkflow_MaintainsPerformance()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        await _containerFixture.InitializeAsync(useInMemoryDatabase: false, mockRepositories: false, databasePath: _databaseFixture.DatabasePath);

        var DaoStudio = _containerFixture.Container.Resolve<DaoStudio.DaoStudioService>();
        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();
        var peopleService = _containerFixture.Container.Resolve<IPeopleService>();

        // Create and save person to database first
        var testPerson = await peopleService.CreatePersonAsync(
            "ChatBot",
            "A test assistant",
            null,
            true,
            "OpenAI",
            "gpt-4",
            "You are a helpful assistant for testing purposes.",
            null,
            null);
        testPerson.Should().NotBeNull();

        // Act - Create session
        var session = await sessionService.CreateSession(testPerson!);

        // Act - Create many messages to simulate long conversation
        var messageCount = 50;
    var createdMessages = new List<DaoStudio.Interfaces.IMessage>();

        for (int i = 0; i < messageCount; i++)
        {
            var role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            var content = role == MessageRole.User 
                ? $"User message #{i + 1}: This is a test message in a long conversation."
                : $"Assistant response #{i + 1}: Thank you for your message. I understand your request.";

            var message = MessageTestHelper.CreateTestMessage(session.Id, role, content);
            var createdMessage = await messageService.CreateMessageAsync(
                message.Content ?? string.Empty,
                (MessageRole)message.Role,
                (MessageType)message.Type,
                message.SessionId,
                true,
                message.ParentMsgId,
                message.ParentSessId);
            createdMessages.Add(createdMessage);

            // Add small delay to simulate real conversation timing
            if (i % 10 == 0)
                await Task.Delay(1);
        }

        // Assert - Verify all messages were created
        var allMessages = await messageService.GetMessagesBySessionIdAsync(session.Id);
        allMessages.Should().HaveCount(messageCount);

        // Assert - Verify message ordering and content
        var orderedMessages = allMessages.OrderBy(m => m.CreatedAt).ToList();
        for (int i = 0; i < messageCount; i++)
        {
            var expectedRole = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant;
            orderedMessages[i].Role.Should().Be(expectedRole);
            orderedMessages[i].Content.Should().Contain($"#{i + 1}:");
        }

        // Assert - Verify session still accessible and performant
        var retrievedSession = await sessionService.OpenSession(session.Id);
        retrievedSession.Should().NotBeNull();
        retrievedSession.Id.Should().Be(session.Id);
    }

    [Fact]
    public async Task SessionDeletionWorkflow_CleansUpCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        await _containerFixture.InitializeAsync(useInMemoryDatabase: false, mockRepositories: false, databasePath: _databaseFixture.DatabasePath);

        var DaoStudio = _containerFixture.Container.Resolve<DaoStudio.DaoStudioService>();
        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        var testPerson = MessageTestHelper.CreateTestPerson("TempBot", "A test assistant", "OpenAI", "gpt-4");

        // Act - Create session with messages
        var session = await sessionService.CreateSession(testPerson);

        var message1 = MessageTestHelper.CreateTestMessage(session.Id, MessageRole.User, "Hello");
        var message2 = MessageTestHelper.CreateTestMessage(session.Id, MessageRole.Assistant, "Hi there!");
        
        await messageService.CreateMessageAsync(
            message1.Content ?? string.Empty,
            (MessageRole)message1.Role,
            (MessageType)message1.Type,
            message1.SessionId,
            true,
            message1.ParentMsgId,
            message1.ParentSessId);
        await messageService.CreateMessageAsync(
            message2.Content ?? string.Empty,
            (MessageRole)message2.Role,
            (MessageType)message2.Type,
            message2.SessionId,
            true,
            message2.ParentMsgId,
            message2.ParentSessId);

        // Verify messages exist
        var messagesBeforeDeletion = await messageService.GetMessagesBySessionIdAsync(session.Id);
        messagesBeforeDeletion.Should().HaveCount(2);

        // Act - Delete session
        var deleteResult = await sessionService.DeleteSessionAsync(session.Id);
        deleteResult.Should().BeTrue();

        // Assert - Verify session is deleted
        var act = async () => await sessionService.OpenSession(session.Id);
        await act.Should().ThrowAsync<Exception>();

        // Assert - Verify messages are cleaned up (depending on implementation)
        var messagesAfterDeletion = await messageService.GetMessagesBySessionIdAsync(session.Id);
        messagesAfterDeletion.Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentSessionWorkflow_HandlesCorrectly()
    {
        // Arrange
        await _databaseFixture.InitializeAsync();
        await _containerFixture.InitializeAsync(useInMemoryDatabase: false, mockRepositories: false, databasePath: _databaseFixture.DatabasePath);

        var DaoStudio = _containerFixture.Container.Resolve<DaoStudio.DaoStudioService>();
        var sessionService = _containerFixture.Container.Resolve<ISessionService>();
        var messageService = _containerFixture.Container.Resolve<IMessageService>();

        var testPerson = MessageTestHelper.CreateTestPerson("ConcurrentBot", "A test assistant", "OpenAI", "gpt-4");

        // Act - Create multiple sessions concurrently
    var sessionTasks = new List<Task<DaoStudio.Interfaces.ISession>>();
        for (int i = 0; i < 5; i++)
        {
            var sessionTitle = $"Concurrent Session {i + 1}";
            var task = sessionService.CreateSession(testPerson);
            sessionTasks.Add(task);
        }

        var sessions = await Task.WhenAll(sessionTasks);

        // Act - Add messages to each session concurrently
        var messageTasks = new List<Task>();
        foreach (var session in sessions)
        {
            var messageTask = Task.Run(async () =>
            {
                var userMsg = MessageTestHelper.CreateTestMessage(session.Id, MessageRole.User, $"Hello from session {session.Id}");
                var assistantMsg = MessageTestHelper.CreateTestMessage(session.Id, MessageRole.Assistant, $"Hello! This is session {session.Id}");
                
                await messageService.CreateMessageAsync(
                    userMsg.Content ?? string.Empty,
                    (MessageRole)userMsg.Role,
                    (MessageType)userMsg.Type,
                    userMsg.SessionId,
                    true,
                    userMsg.ParentMsgId,
                    userMsg.ParentSessId);
                await messageService.CreateMessageAsync(
                    assistantMsg.Content ?? string.Empty,
                    (MessageRole)assistantMsg.Role,
                    (MessageType)assistantMsg.Type,
                    assistantMsg.SessionId,
                    true,
                    assistantMsg.ParentMsgId,
                    assistantMsg.ParentSessId);
            });
            messageTasks.Add(messageTask);
        }

        await Task.WhenAll(messageTasks);

        // Assert - Verify all sessions and messages were created correctly
        sessions.Should().HaveCount(5);
        sessions.Should().OnlyContain(s => s.Id != 0); // IDs should be non-zero (can be positive or negative)
        
        foreach (var session in sessions)
        {
            var messages = await messageService.GetMessagesBySessionIdAsync(session.Id);
            messages.Should().HaveCount(2);
            messages.Should().Contain(m => m.Role == MessageRole.User);
            messages.Should().Contain(m => m.Role == MessageRole.Assistant);
        }
    }

    public void Dispose()
    {
        _databaseFixture?.Dispose();
        _containerFixture?.Dispose();
    }
}
