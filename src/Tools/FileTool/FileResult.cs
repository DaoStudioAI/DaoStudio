using Newtonsoft.Json;
using System.ComponentModel;

namespace FileTool;

public class FileResult
{
    public bool Success { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? Error { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? Content { get; set; }

    public string Path { get; set; } = string.Empty;
}
