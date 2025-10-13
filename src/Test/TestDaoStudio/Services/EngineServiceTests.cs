using DaoStudio.Common.Plugins;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Services;
using DryIoc;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestDaoStudio.Mocks;
using Xunit;

namespace TestDaoStudio.Services
{
    public class EngineServiceTests
    {
        [Fact]
        public async Task CreateEngineAsync_ReturnsDistinctInstancesPerCall()
        {
            var container = new Container();
            var logger = Mock.Of<ILogger<EngineService>>();
            var service = new RecordingEngineService(container, logger);

            var person = MockPerson.CreateAssistant();

            var engine1 = await service.CreateEngineAsync(person);
            var engine2 = await service.CreateEngineAsync(person);

            engine1.Should().NotBeSameAs(engine2);
            service.CreatedEngines.Should().HaveCount(2);
        }

        [Fact]
        public async Task UsageDetails_DoNotLeakBetweenSessions()
        {
            var container = new Container();
            var logger = Mock.Of<ILogger<EngineService>>();
            var engineService = new RecordingEngineService(container, logger);

            var messageService = new Mock<IMessageService>();
            var sessionRepository = new Mock<ISessionRepository>();
            sessionRepository
                .Setup(sr => sr.SaveSessionAsync(It.IsAny<Session>()))
                .ReturnsAsync(true);

            var toolService = new Mock<IToolService>();
            toolService
                .Setup(ts => ts.GetAllToolsAsync())
                .ReturnsAsync(new List<ITool>());

            var pluginService = new Mock<IPluginService>();
            pluginService
                .SetupGet(ps => ps.PluginTools)
                .Returns(new Dictionary<long, IPluginTool>());

            var peopleService = new Mock<IPeopleService>();
            var sessionLogger = Mock.Of<ILogger<DaoStudio.Session>>();

            var person = MockPerson.CreateAssistant();
            var dbSession1 = CreateDbSession(1, person);
            var dbSession2 = CreateDbSession(2, person);

            var session1 = new DaoStudio.Session(
                messageService.Object,
                sessionRepository.Object,
                toolService.Object,
                dbSession1,
                person,
                sessionLogger,
                pluginService.Object,
                engineService,
                peopleService.Object);

            var session2 = new DaoStudio.Session(
                messageService.Object,
                sessionRepository.Object,
                toolService.Object,
                dbSession2,
                person,
                sessionLogger,
                pluginService.Object,
                engineService,
                peopleService.Object);

            await session1.InitializeAsync();
            await session2.InitializeAsync();

            engineService.CreatedEngines.Should().HaveCount(2);

            var engineForSession1 = engineService.CreatedEngines[0];
            var engineForSession2 = engineService.CreatedEngines[1];

            engineForSession2.EmitUsage(100, 60, 40);

            session2.TotalTokenCount.Should().Be(100);
            session2.InputTokenCount.Should().Be(60);
            session2.OutputTokenCount.Should().Be(40);

            session1.TotalTokenCount.Should().Be(0);
            session1.InputTokenCount.Should().Be(0);
            session1.OutputTokenCount.Should().Be(0);

            engineForSession1.EmitUsage(24, 10, 14);

            session1.TotalTokenCount.Should().Be(24);
            session1.InputTokenCount.Should().Be(10);
            session1.OutputTokenCount.Should().Be(14);
        }

        private static Session CreateDbSession(long id, IPerson person)
        {
            return new Session
            {
                Id = id,
                Title = $"Session {id}",
                Description = "Test session",
                PersonNames = new List<string> { person.Name },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
        }

        private sealed class RecordingEngineService : EngineService
        {
            private int _engineCounter;

            public RecordingEngineService(Container container, ILogger<EngineService> logger)
                : base(container, logger)
            {
            }

            public List<InstrumentedEngine> CreatedEngines { get; } = new();

            protected override IEngine CreateEngineInternal(IPerson person)
            {
                var engine = new InstrumentedEngine(person, Interlocked.Increment(ref _engineCounter));
                CreatedEngines.Add(engine);
                return engine;
            }
        }

        private sealed class InstrumentedEngine : IEngine
        {
            public InstrumentedEngine(IPerson person, int identifier)
            {
                Person = person;
                Identifier = identifier;
            }

            public IPerson Person { get; }

            public int Identifier { get; }

            public event EventHandler<UsageDetails>? UsageDetailsReceived;

            public Task<IAsyncEnumerable<IMessage>> GetMessageAsync(
                List<IMessage> messages,
                Dictionary<string, List<FunctionWithDescription>>? tools,
                ISession session,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Empty());
            }

            public void EmitUsage(long total, long input, long output)
            {
                var details = new UsageDetails
                {
                    TotalTokens = total,
                    InputTokens = input,
                    OutputTokens = output
                };

                UsageDetailsReceived?.Invoke(this, details);
            }

            private static async IAsyncEnumerable<IMessage> Empty()
            {
                yield break;
            }
        }
    }
}
