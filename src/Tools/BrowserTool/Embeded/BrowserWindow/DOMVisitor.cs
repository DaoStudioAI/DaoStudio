using Xilium.CefGlue;

namespace BrowserTool
{
    // DOM visitor class to extract HTML content
    class DOMVisitor : CefDomVisitor
    {
        private readonly TaskCompletionSource<string> _taskCompletionSource;

        public DOMVisitor(TaskCompletionSource<string> taskCompletionSource)
        {
            _taskCompletionSource = taskCompletionSource;
        }


        protected override void Visit(CefDomDocument document)
        {
            try
            {
                var html = document.Root.GetAsMarkup();
                _taskCompletionSource.SetResult(html);
            }
            catch (Exception ex)
            {
                _taskCompletionSource.SetResult(string.Format(Properties.Resources.Error_ProcessingDOM, ex.Message));
            }
        }

    }
} 