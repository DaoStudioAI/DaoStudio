using Xilium.CefGlue;

namespace BrowserTool
{
    // String visitor class to handle GetSource callback
    class StringVisitor : CefStringVisitor
    {
        private readonly TaskCompletionSource<string> _taskCompletionSource;

        public StringVisitor(TaskCompletionSource<string> taskCompletionSource)
        {
            _taskCompletionSource = taskCompletionSource;
        }

        protected override void Visit(string value)
        {
            try
            {
                _taskCompletionSource.SetResult(value);
            }
            catch (Exception ex)
            {
                _taskCompletionSource.SetResult(string.Format(Properties.Resources.Error_ProcessingSource, ex.Message));
            }
        }
    }
} 