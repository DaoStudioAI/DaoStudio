using DaoStudio.Common.Plugins;
using TestNamingTool.TestInfrastructure.Mocks;

namespace TestNamingTool.TestInfrastructure.Builders
{
    public class MockChildSessionResult : ChildSessionResult
    {
        public MockChildSessionResult(bool success, string? result = null, string? error = null)
        {
            Success = success;
            Result = result;
            ErrorMessage = error;
        }
    }

    /// <summary>
    /// Enhanced mock host session that can simulate child session execution
    /// </summary>
    public class MockHostSessionWithChildExecution : MockHostSession
    {
        private readonly Queue<ChildSessionResult> _childSessionResults = new();
        private readonly List<string> _childSessionPrompts = new();

        public MockHostSessionWithChildExecution(long id, long? parentSessionId = null) 
            : base(id, parentSessionId)
        {
        }

        public List<string> ChildSessionPrompts => _childSessionPrompts;

        public void EnqueueChildSessionResult(ChildSessionResult result)
        {
            _childSessionResults.Enqueue(result);
        }

        public void EnqueueSuccessResult(string result)
        {
            _childSessionResults.Enqueue(new MockChildSessionResult(true, result));
        }

        public void EnqueueFailureResult(string error)
        {
            _childSessionResults.Enqueue(new MockChildSessionResult(false, error: error));
        }

        // Simulate the WaitChildSessionAsync extension method behavior
        public Task<ChildSessionResult> SimulateChildSessionAsync(string prompt)
        {
            _childSessionPrompts.Add(prompt);
            
            if (_childSessionResults.Count > 0)
            {
                return Task.FromResult(_childSessionResults.Dequeue());
            }
            
            // Default to success if no specific result was queued
            return Task.FromResult<ChildSessionResult>(new MockChildSessionResult(true, "Default success result"));
        }
    }
}
