namespace DaoStudio.Interfaces
{
    /// <summary>
    /// String-based enum for person parameter names used in Person.Parameters.
    /// This provides a centralized, strongly-typed way to reference common parameters.
    /// </summary>
    public static class PersonParameterNames
    {

        /// <summary>
        /// Whether all tools should be enabled for this person.
        /// Legacy parameter for backward compatibility.
        /// </summary>
        public const string UseAllTools = "UseAllTools";

        // Additional parameter names requested by user (values mirror the constant names)
        public const string LimitMaxContextLength = "LimitMaxContextLength";
        public const string LimitsMaxOutputTokens = "LimitsMaxOutputTokens";
        public const string LimitsMaxPromptTokens = "LimitsMaxPromptTokens";
        public const string LimitsMaxInputs = "LimitsMaxInputs";

        public const string PricingPrompt = "PricingPrompt";
        public const string PricingCompletion = "PricingCompletion";
        public const string PricingRequest = "PricingRequest";
        public const string PricingImage = "PricingImage";
        public const string PricingWebSearch = "PricingWebSearch";
        public const string PricingAudio = "PricingAudio";
        public const string PricingInternalReasoning = "PricingInternalReasoning";
        public const string PricingInputCacheRead = "PricingInputCacheRead";
        public const string PricingInputCacheWrite = "PricingInputCacheWrite";

        public const string HttpHeaders = "HttpHeaders";
        public const string AdditionalContent = "AdditionalContent";
    }
}
