using Newtonsoft.Json;

namespace DaoStudio.Engines.MEAI
{
    /// <summary>
    /// Custom class to deserialize FunctionResultContent JSON
    /// </summary>
    public class FunctionResultDto
    {
        [JsonProperty("PluginName")]
        public string PluginName { get; set; } = string.Empty;

        [JsonProperty("FunctionName")]
        public string FunctionName { get; set; } = string.Empty;

        [JsonProperty("Result")]
        public object? Result { get; set; }

        [JsonProperty("CallId")]
        public string CallId { get; set; } = string.Empty;
    }
}