namespace BrowserTool;

public class BrowserToolConfig
{
    public int Version { get; set; } = 1;
    public BrowserToolType BrowserToolType { get; set; } = BrowserToolType.Embeded;
    public BrowserType BrowserType { get; set; } = BrowserType.Chrome;
    public string BrowserPath { get; set; } = string.Empty;
    public int MinElementSize { get; set; } = 3; // Minimum size in pixels for elements to be included
    public int NavigationTimeoutMs { get; set; } = 500; // Maximum time to wait for navigation after click (milliseconds)
    public bool EnableSessionAware { get; set; } = true; // Enable session-aware browser tab management
}
