using System.ComponentModel;
using Newtonsoft.Json;


#if WINDOWS
#endif

namespace BrowserTool;

// Before Dispose method, add the ClickResult class
// Class to represent the result of a click operation
public class ClickResult
{
    [Description("Operation success status")]
    public bool Success { get; set; }
    
    [Description("Error message if failed")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? Error { get; set; }
    
    [Description("ID of clicked element")]
    public string ElementId { get; set; } = "";
    
    [Description("Current URL after click (if navigation occurred)")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? CurrentUrl { get; set; }
}
