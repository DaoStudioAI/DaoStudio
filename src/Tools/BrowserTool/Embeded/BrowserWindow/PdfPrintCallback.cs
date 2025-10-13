using Xilium.CefGlue;

namespace BrowserTool
{
    // PDF print callback class to handle PrintToPdf callback
    class PdfPrintCallback : CefPdfPrintCallback
    {
        private readonly TaskCompletionSource<string> _taskCompletionSource;

        public PdfPrintCallback(TaskCompletionSource<string> taskCompletionSource)
        {
            _taskCompletionSource = taskCompletionSource;
        }

        protected override void OnPdfPrintFinished(string path, bool ok)
        {
            if (ok)
            {
                _taskCompletionSource.SetResult(path);
            }
            else
            {
                _taskCompletionSource.SetResult(string.Empty);
            }
        }
    }
} 