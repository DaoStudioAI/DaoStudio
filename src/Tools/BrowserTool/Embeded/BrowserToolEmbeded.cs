using Serilog;
using DaoStudio.Interfaces.Plugins;

#if WINDOWS
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
#endif

namespace BrowserTool;

/// <summary>
/// Represents a cached tree node for browser automation elements
/// </summary>
#if WINDOWS
internal class CachedTreeNode
{
    public required long Id = 0;
    public required AutomationElement Node;
    public List<CachedTreeNode> Children = new List<CachedTreeNode>();
#if DEBUG 
    public string? Text;
#endif
}
#else
// Minimal stub for non-Windows builds to allow session/cache management code to compile
internal class CachedTreeNode
{
    public required long Id = 0;
    // Placeholder to keep type shape; not used on non-Windows
    public object? Node { get; set; }
    public List<CachedTreeNode> Children = new List<CachedTreeNode>();
}
#endif

/// <summary>
/// Main partial class for BrowserToolEmbeded - contains core fields, constructor, and disposal
/// </summary>
internal partial class BrowserToolEmbeded : IDisposable
{
    private bool disposedValue;
    private BrowserWindow? browserWindow;
    private BrowserToolConfig browserConfig;
    private IHost? host;

    // Cache for the CachedTreeNode tree (legacy single-session cache)
    private CachedTreeNode? cachedTreeNodeRoot = null;
    
#if WINDOWS
    private UIA3Automation? cachedAutomation = null;
#endif

    // Session-aware caches
    private readonly Dictionary<long, CachedTreeNode?> _sessionCachedTreeRoots = new();
#if WINDOWS
    private readonly Dictionary<long, UIA3Automation?> _sessionAutomations = new();
#endif

    public BrowserToolEmbeded(BrowserToolConfig browserConfig, IHost? host = null)
    {
        this.browserConfig = browserConfig;
        this.host = host;
        Log.Information("BrowserToolEmbeded initialized with config: {@BrowserConfig}", browserConfig);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Log.Information("Disposing BrowserToolEmbeded");
                browserWindow?.Close();
                browserWindow = null;

                
#if WINDOWS
                cachedAutomation?.Dispose();
                cachedAutomation = null;
#endif

                // Dispose per-session automations
                
#if WINDOWS
                foreach (var kv in _sessionAutomations.ToList())
                {
                    try { kv.Value?.Dispose(); } catch { }
                    _sessionAutomations[kv.Key] = null;
                }
#endif
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    void IDisposable.Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
