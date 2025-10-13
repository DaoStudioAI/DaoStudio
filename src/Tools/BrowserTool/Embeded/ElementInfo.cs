using System.ComponentModel;
using Newtonsoft.Json;


#if WINDOWS
#endif

namespace BrowserTool;

// Class to represent detailed information about an element
public class ElementInfo
{
    [Description("Element identifier")]
    public long ElementId { get; set; }
    
    [Description("Element details including ClassName, ControlType and supported patterns")]
    public string Information { get; set; } = "";
    
    [Description("Error message if retrieval failed")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? Error { get; set; }
}
