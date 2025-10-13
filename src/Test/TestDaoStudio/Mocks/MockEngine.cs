using DaoStudio.Interfaces;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using DaoStudio.Interfaces.Plugins;

namespace TestDaoStudio.Mocks;

/// <summary>
/// Mock implementation of <see cref="IEngine"/> for testing engine behavior.
/// Provides configurable responses and tracks method calls for verification.
/// </summary>
public class MockEngine : IEngine, IDisposable
{
    private readonly Queue<string> _responses = new();
    private readonly Queue<Exception?> _exceptions = new();

    public IPerson Person { get; private set; } = new MockPerson();

    // Event with the project's UsageDetails type
    public event EventHandler<DaoStudio.Interfaces.UsageDetails>? UsageDetailsReceived;

    public int CallCount { get; private set; }

    public void SetResponses(params string[] responses)
    {
        _responses.Clear();
        foreach (var r in responses)
            _responses.Enqueue(r);
    }

    public void SetExceptions(params Exception?[] exceptions)
    {
        _exceptions.Clear();
        foreach (var e in exceptions)
            _exceptions.Enqueue(e);
    }

    /// <summary>
    /// Allows tests to raise a Microsoft.Extensions.AI.UsageDetails instance which will be
    /// converted to the project's UsageDetails type and raised on the event.
    /// </summary>
    public void SimulateUsageDetails(Microsoft.Extensions.AI.UsageDetails usage)
    {
        var u = new DaoStudio.Interfaces.UsageDetails
        {
            InputTokens = usage.InputTokenCount,
            OutputTokens = usage.OutputTokenCount,
            TotalTokens = usage.TotalTokenCount
        };

        UsageDetailsReceived?.Invoke(this, u);
    }

    public Task<IAsyncEnumerable<IMessage>> GetMessageAsync(
        List<IMessage> messages,
        Dictionary<string, List<FunctionWithDescription>>? tools,
        ISession session,
        CancellationToken cancellationToken = default)
    {
        async IAsyncEnumerable<IMessage> Impl([EnumeratorCancellation] CancellationToken ct = default)
        {
            CallCount++;

            if (_exceptions.Count > 0)
            {
                var ex = _exceptions.Dequeue();
                if (ex != null) throw ex;
            }

            var responseText = _responses.Count > 0 ? _responses.Dequeue() : "Mock response from test engine";

            // Yield a single assistant message
            var msg = TestDaoStudio.Helpers.MessageTestHelper.CreateAssistantMessage(responseText);
            yield return msg;

            // Simulate a short delay then fire usage details
            await Task.Delay(10, ct).ConfigureAwait(false);

            var usage = new DaoStudio.Interfaces.UsageDetails
            {
                InputTokens = messages?.Sum(m => (long?)(m.Content?.Length / 4) ?? 0) ?? 0,
                OutputTokens = responseText.Length / 4,
                TotalTokens = (messages?.Sum(m => (long?)(m.Content?.Length / 4) ?? 0) ?? 0) + responseText.Length / 4
            };

            UsageDetailsReceived?.Invoke(this, usage);
        }

        return Task.FromResult(Impl(cancellationToken));
    }

    public void Dispose()
    {
        // nothing to dispose
    }
}
