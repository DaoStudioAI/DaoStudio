using DaoStudio.Common;
using DaoStudio.DBStorage.Factory;
using DaoStudio.DBStorage.Models;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Plugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MimeSharp;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DaoUsageDetails = DaoStudio.Interfaces.UsageDetails;
using InterfaceMessageRole = DaoStudio.Interfaces.MessageRole;
using InterfaceMessageType = DaoStudio.Interfaces.MessageType;
using InterfaceMsgBinaryDataType = DaoStudio.Interfaces.MsgBinaryDataType;

namespace DaoStudio.Engines.MEAI
{

    /// <summary>
    /// Base engine class providing shared functionality for all LLM engines
    /// </summary>
    internal abstract class BaseEngine : IEngine
    {
        protected readonly ILogger<BaseEngine> _logger;
        protected readonly IPerson _person;
        protected readonly StorageFactory _storage;
        protected readonly IPlainAIFunctionFactory _plainAIFunctionFactory;
        protected readonly ISettings _settings;
        
        public BaseEngine(IPerson person, ILogger<BaseEngine> logger, StorageFactory storage, IPlainAIFunctionFactory plainAIFunctionFactory, ISettings settings)
        {
            _person = person ?? throw new ArgumentNullException(nameof(person));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _plainAIFunctionFactory = plainAIFunctionFactory ?? throw new ArgumentNullException(nameof(plainAIFunctionFactory));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public IPerson Person => _person;

        public event EventHandler<DaoUsageDetails>? UsageDetailsReceived;

        /// <summary>
        /// Triggers the UsageDetailsReceived event when usage details are available
        /// </summary>
        protected virtual void OnUsageDetailsReceived(DaoUsageDetails usageDetails)
        {
            UsageDetailsReceived?.Invoke(this, usageDetails);
        }

        /// <summary>
        /// Abstract method for creating the chat client specific to each provider
        /// </summary>
        protected abstract Task<IChatClient> CreateChatClientAsync();

        public async Task<IAsyncEnumerable<IMessage>> GetMessageAsync(
            List<IMessage> messages,
            Dictionary<string, List<FunctionWithDescription>>? tools,
            ISession session,
            CancellationToken cancellationToken = default)
        {
            // Validate parameters
            if (messages == null)
                throw new ArgumentNullException(nameof(messages));
            if (messages.Count == 0)
                throw new ArgumentException("Messages list cannot be empty", nameof(messages));
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var chatClient = await CreateChatClientAsync();
            var chatHistory = ConvertToChatMessages(messages);
            
            return ProcessStreamingResponseAsync(chatClient, chatHistory, tools, session, cancellationToken);
        }

        /// <summary>
        /// Convert IMessage list to ChatMessage list for Microsoft.Extensions.AI
        /// </summary>
        protected List<ChatMessage> ConvertToChatMessages(List<IMessage> messages)
        {
            var chatHistory = new List<ChatMessage>();

            foreach (var message in messages)
            {
                var role = message.Role switch
                {
                    InterfaceMessageRole.User => ChatRole.User,
                    InterfaceMessageRole.Assistant => ChatRole.Assistant,
                    InterfaceMessageRole.System => ChatRole.System,
                    InterfaceMessageRole.Developer => ChatRole.System, // Map Developer to System
                    _ => ChatRole.User
                };

                var chatMessage = new ChatMessage(role, message.Content ?? string.Empty);

                // Handle binary content (files, images, tool calls, etc.)
                if (message.BinaryContents?.Any() == true)
                {
                    ProcessBinaryContentsIntoChatMessageContent(chatMessage, message.BinaryContents.ToList(), message.Id);
                    message.Content=null;
                }

                chatHistory.Add(chatMessage);
            }

            return chatHistory;
        }

        /// <summary>
        /// Process streaming response with tool calling support
        /// </summary>
        protected async IAsyncEnumerable<IMessage> ProcessStreamingResponseAsync(
            IChatClient chatClient,
            List<ChatMessage> chatHistory,
            Dictionary<string, List<FunctionWithDescription>>? tools,
            ISession session,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            bool hasReceivedContent = false;

            // _logger available tools
            _logger.LogInformation("Engine for person {PersonName}: Available tool keys: {Keys}", 
                _person.Name, tools?.Keys != null ? string.Join(", ", tools.Keys) : "none");

            var options = new ChatOptions
            {
                AllowMultipleToolCalls = true,
                ToolMode = session.ToolExecutionMode switch
                {
                    ToolExecutionMode.None => ChatToolMode.None,
                    ToolExecutionMode.RequireAny => ChatToolMode.RequireAny,
                    ToolExecutionMode.Auto => ChatToolMode.Auto,
                    _ => ChatToolMode.Auto
                },
                Tools = await ProcessToolsWithConflictResolutionAsync(tools, session)
            };

            // Apply person parameters to chat options
            ApplyPersonParametersToOptions(options);

            await foreach (var update in chatClient.GetStreamingResponseAsync(chatHistory, options, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentMessage = CreateNewMessage(InterfaceMessageRole.Assistant);

                // Handle text content
                if (!string.IsNullOrWhiteSpace(update.Text))
                {
                    hasReceivedContent = true;
                    currentMessage.Content = update.Text;
                    currentMessage.CreatedAt = DateTime.UtcNow;

                    // Yield the updated message
                    yield return currentMessage;
                }

                // Handle content items (like tool calls)
                if (update.Contents?.Any() == true)
                {
                    hasReceivedContent = true;
                    // Build a dedicated message for tool call or tool result
                    var binaryMessage = CreateNewMessage(InterfaceMessageRole.Assistant);
                    var binaryContents = await ProcessChatMessageItemsAsync(binaryMessage, update, session);
                    if (binaryContents.Any())
                    {
                        // Content may have been populated inside ProcessChatMessageItemsAsync
                        binaryMessage.BinaryContents = binaryContents;
                        binaryMessage.CreatedAt = DateTime.UtcNow;
                        yield return binaryMessage;
                    }

                }
            }


            if (!hasReceivedContent)
            {
                _logger.LogWarning("No content received from chat client");
            }
        }

        /// <summary>
        /// Create a new message with the specified role
        /// </summary>
        protected virtual IMessage CreateNewMessage(InterfaceMessageRole role)
        {
            var message = new Message
            {
                SessionId = 0, // Will be set by the Session
                Content = string.Empty,
                Role = (int)role,
                CreatedAt = DateTime.UtcNow
            };
            
            // Set interface properties through explicit interface casting
            ((IMessage)message).Type = InterfaceMessageType.Normal;
            ((IMessage)message).BinaryContents = null; // Will be set when needed
            
            return message;
        }

        /// <summary>
        /// Get MIME type from file name using MimeSharp library
        /// </summary>
        protected virtual string? GetMimeTypeFromName(string fileName)
        {
            try
            {
                return Mime.Lookup(fileName);
            }
            catch
            {
                // Fallback to hardcoded types if MimeSharp fails
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                return extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".webp" => "image/webp",
                    ".svg" => "image/svg+xml",
                    _ => null
                };
            }
        }

        /// <summary>
        /// Processes binary data from AIContent and converts it to appropriate binary data format
        /// </summary>
        protected async Task<IMsgBinaryData?> ProcessBinaryDataAsync(IMessage msg, ChatResponseUpdate update, AIContent item, ISession session)
        {
            // Determine the appropriate type and name based on the content type
            InterfaceMsgBinaryDataType dataType;
            string? data = null;

            switch (item)
            {
                case DataContent binaryContent:
                    _logger.LogInformation("BinaryContent: BinaryContent");
                    Debugger.Break();
                    return null;
                case UriContent uriContent:
                    _logger.LogInformation("FileReferenceContent: FileReferenceContent");
                    Debugger.Break();
                    return null;
                case FunctionCallContent functionCallContent:
                    dataType = InterfaceMsgBinaryDataType.ToolCall;
                    if (!string.IsNullOrWhiteSpace(msg.Content))
                    {
                        msg.Content += " ";
                    }
                    // Note: Properties.Resources.ToolCall_Content would need to be accessible or replaced
                    msg.Content += $"Tool Call: {functionCallContent.Name} with args {System.Text.Json.JsonSerializer.Serialize(functionCallContent.Arguments)} (CallId: {functionCallContent.CallId})";
                    data = System.Text.Json.JsonSerializer.Serialize(functionCallContent);
                    break;
                case FunctionResultContent functionResultContent:
                    dataType = InterfaceMsgBinaryDataType.ToolCallResult;
                    msg.Role = InterfaceMessageRole.User;
                    // Note: Properties.Resources.ToolCall_Result would need to be accessible or replaced
                    msg.Content = $"Tool Result: {functionResultContent.Result} (CallId: {functionResultContent.CallId})";
                    data = System.Text.Json.JsonSerializer.Serialize(new FunctionResultDto
                    {
                        Result = functionResultContent.Result,
                        CallId = functionResultContent.CallId ?? string.Empty
                    });
                    break;
                case TextContent textContent:
                    //if (!string.IsNullOrWhiteSpace(textContent.Text))
                    //{
                    //    if (!string.IsNullOrWhiteSpace(msg.Content))
                    //    {
                    //        msg.Content += " ";
                    //    }
                    //    msg.Content += textContent.Text;
                    //}
                    return null;
                case UsageContent usageContent:
                    {
                        var details = usageContent.Details;
                        // Handle usage details through session
                        var localUsageDetails = new DaoUsageDetails
                        {
                            TotalTokens = details.TotalTokenCount ?? 0L,
                            InputTokens = details.InputTokenCount ?? 0L,
                            OutputTokens = details.OutputTokenCount ?? 0L,
                            AdditionalProperties = details.AdditionalCounts?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString())
                        };
                        OnUsageDetailsReceived(localUsageDetails);
                        return null;
                    }
                case TextReasoningContent textReasoningContent:
                    dataType = InterfaceMsgBinaryDataType.Thinking;
                    data = textReasoningContent.Text;
                    Debugger.Break();
                    break;
                default:
                    _logger.LogWarning("Unknown item type: {ItemType}", item.GetType());
                    Debugger.Break();
                    return null;
            }

            // Create appropriate binary data entry using interface
            var binaryData = new MsgBinaryData
            {
                Name = string.Empty,
                Data = Encoding.UTF8.GetBytes(data ?? string.Empty),
            };
            
            // Set the Type through the interface property to handle the conversion
            ((IMsgBinaryData)binaryData).Type = dataType;
            
            return binaryData;
        }

        /// <summary>
        /// Processes a collection of ChatMessageContentItems and converts them to IMsgBinaryData objects.
        /// </summary>
        protected async Task<List<IMsgBinaryData>> ProcessChatMessageItemsAsync(IMessage msg, ChatResponseUpdate update, ISession session)
        {
            var binaryContents = new List<IMsgBinaryData>();
            IEnumerable<AIContent> items = update.Contents;


            if (items == null || !items.Any())
            {
                return binaryContents;
            }
            
            foreach (var item in items)
            {
                var binaryData = await ProcessBinaryDataAsync(msg, update,item, session);
                if (binaryData != null)
                {
                    binaryContents.Add(binaryData);
                }
            }
            
            return binaryContents;
        }

        /// <summary>
        /// Processes binary contents from a message and adds them to a ChatMessage.
        /// </summary>
        protected void ProcessBinaryContentsIntoChatMessageContent(
            ChatMessage messageContent,
            List<IMsgBinaryData> binaryContents,
            long messageId)
        {
            if (binaryContents == null || binaryContents.Count == 0)
            {
                return;
            }
            messageContent.Contents.Clear();

            foreach (var binaryContent in binaryContents)
            {
                try
                {
                    // Process tool calls and tool call results
                    if (binaryContent.Type == InterfaceMsgBinaryDataType.ToolCall)
                    {
                        // Deserialize the function call content
                        var functionCallData = Encoding.UTF8.GetString(binaryContent.Data);
                        var functionCall = System.Text.Json.JsonSerializer.Deserialize<FunctionCallContent>(functionCallData);
                        
                        if (functionCall != null)
                        {
                            messageContent.Contents.Add(functionCall);
                        }
                    }
                    else if (binaryContent.Type == InterfaceMsgBinaryDataType.ToolCallResult)
                    {
                        // Deserialize the function result content
                        var functionResultData = Encoding.UTF8.GetString(binaryContent.Data);
                        var dto = System.Text.Json.JsonSerializer.Deserialize<FunctionResultDto>(functionResultData);
                        
                        if (dto != null)
                        {
                            var functionResult = new FunctionResultContent(
                                dto.CallId ?? string.Empty,
                                dto.Result ?? string.Empty);
                                
                            messageContent.Contents.Add(functionResult);
                            messageContent.Role = ChatRole.Tool;
                        }
                    }
                    else if (binaryContent.Type == InterfaceMsgBinaryDataType.SubsessionId)
                    {
                        // SubsessionId binary data doesn't need to be added to chat contents
                        // It's used by the UI to handle navigation to subsessions
                        _logger.LogDebug("Processed SubsessionId binary data for message {MessageId}", messageId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process binary content for message {MessageId}", messageId);
                }
            }
        }

        /// <summary>
        /// Processes tools dictionary to resolve function name conflicts by adding prefixes when necessary
        /// </summary>
        protected virtual async Task<AITool[]?> ProcessToolsWithConflictResolutionAsync(
            Dictionary<string, List<FunctionWithDescription>>? tools,
            ISession session)
        {
            if (tools?.Any() != true)
                return null;

            // First, collect all functions with their original keys
            var allFunctions = new List<(string OriginalKey, FunctionWithDescription Function)>();
            foreach (var kvp in tools)
            {
                foreach (var function in kvp.Value)
                {
                    allFunctions.Add((kvp.Key, function));
                }
            }

            // Group functions by their name to identify conflicts
            var functionGroups = allFunctions
                .GroupBy(f => f.Function.Description.Name)
                .ToList();

            // Process each group
            var processedFunctions = new List<FunctionWithDescription>();
            
            foreach (var group in functionGroups)
            {
                var functionsInGroup = group.ToList();
                
                if (functionsInGroup.Count == 1)
                {
                    // No conflict, use original function
                    processedFunctions.Add(functionsInGroup[0].Function);
                }
                else
                {
                    // Conflict detected - check settings to determine behavior
                    var autoResolve = _settings.AutoResolveToolNameConflicts;
                    if (!autoResolve)
                    {
                        // Throw exception when auto-resolution is disabled - include specific module name for first conflict
                        var firstConflict = functionsInGroup.First();
                        var functionName = firstConflict.Function.Description.Name;
                        var moduleName = firstConflict.OriginalKey;
                        throw new UIException($"Tool function name conflicts detected for '{functionName}' in module '{moduleName}'. Please rename conflicting functions or enable auto-resolution in settings.");
                    }

                    // Auto-resolve conflicts by adding prefixes (current behavior)
                    var sanitizedPrefixes = new Dictionary<string, int>(); // Track prefix usage counts
                    
                    foreach (var (originalKey, function) in functionsInGroup)
                    {
                        // Sanitize the key to create a valid prefix
                        var sanitizedPrefix = SanitizeKeyForPrefix(originalKey);
                        
                        // Ensure the prefix is unique
                        var uniquePrefix = MakeUniquePrefix(sanitizedPrefix, sanitizedPrefixes);
                        sanitizedPrefixes[uniquePrefix] = sanitizedPrefixes.GetValueOrDefault(uniquePrefix, 0) + 1;
                        
                        // Create a new function with prefixed name
                        var prefixedFunction = CreateFunctionWithPrefixedName(function, uniquePrefix);
                        processedFunctions.Add(prefixedFunction);
                    }
                }
            }

            // Convert to AITool array
            return processedFunctions
                .Select(function => (AITool)_plainAIFunctionFactory.Create(function, session))
                .ToArray();
        }

        /// <summary>
        /// Sanitizes a dictionary key to be used as a function name prefix, keeping only a-zA-Z and underscore
        /// </summary>
        protected virtual string SanitizeKeyForPrefix(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "tool";

            var sanitized = new StringBuilder();
            foreach (char c in key)
            {
                if (char.IsLetter(c) || c == '_')
                {
                    sanitized.Append(c);
                }
            }

            return sanitized.Length > 0 ? sanitized.ToString() : "tool";
        }

        /// <summary>
        /// Makes a prefix unique by appending a number if necessary
        /// </summary>
        protected virtual string MakeUniquePrefix(string prefix, Dictionary<string, int> usedPrefixes)
        {
            if (!usedPrefixes.ContainsKey(prefix))
                return prefix;

            int counter = 2;
            string uniquePrefix;
            do
            {
                uniquePrefix = $"{prefix}{counter}";
                counter++;
            }
            while (usedPrefixes.ContainsKey(uniquePrefix));

            return uniquePrefix;
        }

        /// <summary>
        /// Creates a new FunctionWithDescription with a prefixed name
        /// </summary>
        protected virtual FunctionWithDescription CreateFunctionWithPrefixedName(
            FunctionWithDescription originalFunction,
            string prefix)
        {
            var newDescription = new FunctionDescription
            {
                Name = $"{prefix}_{originalFunction.Description.Name}",
                Description = originalFunction.Description.Description,
                Parameters = originalFunction.Description.Parameters,
                ReturnParameter = originalFunction.Description.ReturnParameter,
                StrictMode = originalFunction.Description.StrictMode
            };

            return new FunctionWithDescription
            {
                Function = originalFunction.Function,
                Description = newDescription,
            };
        }

        /// <summary>
        /// Applies person-specific parameters to ChatOptions
        /// </summary>
        protected virtual void ApplyPersonParametersToOptions(ChatOptions options)
        {
            // Apply direct person properties from the database
            if (_person.Temperature.HasValue)
            {
                options.Temperature = (float)_person.Temperature.Value;
            }
            
            if (_person.TopP.HasValue)
            {
                options.TopP = (float)_person.TopP.Value;
            }
            
            if (_person.TopK.HasValue)
            {
                options.TopK = _person.TopK.Value;
            }
            
            if (_person.FrequencyPenalty.HasValue)
            {
                options.FrequencyPenalty = (float)_person.FrequencyPenalty.Value;
            }
            
            if (_person.PresencePenalty.HasValue)
            {
                options.PresencePenalty = (float)_person.PresencePenalty.Value;
            }

            // Apply remaining parameters from the Parameters dictionary
            if (_person.Parameters == null || _person.Parameters.Count == 0)
                return;

            foreach (var parameter in _person.Parameters)
            {
                try
                {
                    switch (parameter.Key.ToLowerInvariant())
                    {
                        case var key when key.Equals(PersonParameterNames.LimitMaxContextLength, StringComparison.OrdinalIgnoreCase):
                            if (int.TryParse(parameter.Value, out int maxTokens))
                            {
                                options.MaxOutputTokens = maxTokens;
                            }
                            break;
                        case var key when key.Equals(PersonParameterNames.AdditionalContent, StringComparison.OrdinalIgnoreCase):
                            ApplyAdditionalContentToOptions(options, parameter.Value);
                            break;
                        default:
                            // Log unrecognized parameters but don't fail
                            _logger.LogDebug("Unrecognized parameter {ParameterName} with value {ParameterValue} for person {PersonName}", 
                                parameter.Key, parameter.Value, _person.Name);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to apply parameter {ParameterName} with value {ParameterValue} for person {PersonName}", 
                        parameter.Key, parameter.Value, _person.Name);
                }
            }
        }

        /// <summary>
        /// Parse AdditionalContent JSON and apply to ChatOptions.AdditionalProperties
        /// </summary>
        protected virtual void ApplyAdditionalContentToOptions(ChatOptions options, string additionalContentJson)
        {
            if (string.IsNullOrWhiteSpace(additionalContentJson))
            {
                return;
            }

            try
            {
                // Parse JSON as a dictionary
                var additionalContent = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(additionalContentJson);
                
                if (additionalContent == null || additionalContent.Count == 0)
                {
                    return;
                }

                // AdditionalProperties should already be initialized, but check just in case
                if (options.AdditionalProperties == null)
                {
                    options.AdditionalProperties = new AdditionalPropertiesDictionary();
                }

                // Add each key-value pair to AdditionalProperties
                foreach (var kvp in additionalContent)
                {
                    options.AdditionalProperties[kvp.Key] = kvp.Value;
                    _logger.LogDebug("Added AdditionalProperty: {PropertyName} for person {PersonName}", 
                        kvp.Key, _person.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse AdditionalContent JSON for person {PersonName}: {Json}", 
                    _person.Name, additionalContentJson);
            }
        }

        /// <summary>
        /// Helper method to get error message for LLM initialization failures
        /// </summary>
        protected string GetInitializationErrorMessage(string providerName, Exception? innerException = null)
        {
            var baseMessage = $"Failed to initialize {providerName} LLM client";
            if (innerException != null)
            {
                return $"{baseMessage}: {innerException.Message}";
            }
            return baseMessage;
        }
    }
}