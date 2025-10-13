namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Enum representing different types of LLM providers
    /// </summary>
    public enum ProviderType
    {
        Unknown = 0,
        /// <summary>
        /// OpenAI provider (e.g. GPT-4, GPT-3.5)
        /// </summary>
        OpenAI = 1,

        /// <summary>
        /// Anthropic provider (e.g. Claude)
        /// </summary>
        Anthropic = 2,

        /// <summary>
        /// Google provider (e.g. Gemini)
        /// </summary>
        Google = 3,

        /// <summary>
        /// Local provider (e.g. Llama, CodeLlama)
        /// </summary>
        Local = 4,

        /// <summary>
        /// OpenRouter provider implementation
        /// </summary>
        OpenRouter = 5,

        /// <summary>
        /// Ollama provider (local model server)
        /// </summary>
        Ollama = 6,

        /// <summary>
        /// LLama provider (LLamaSharp)
        /// </summary>
        LLama = 7,

        /// <summary>
        /// AWS Bedrock provider
        /// </summary>
        AWSBedrock = 8,
    }
}