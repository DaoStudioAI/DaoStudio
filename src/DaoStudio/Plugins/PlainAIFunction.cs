using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DaoStudio.Plugins
{
    /// <summary>
    /// Represents an AI function that can be invoked by an AI model,
    /// specifically designed to generate a JSON schema compatible with OpenAI's tool calling feature.
    /// It wraps a delegate and provides metadata for the function, including its name, description,
    /// and a JSON schema for its parameters.
    /// </summary>
    internal class PlainAIFunction : AIFunction
    {
        private const string DasSession = "das_Session";
        private readonly FunctionWithDescription _functionWithDescription;
        private readonly IHostSession _session;
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            // Consider adding other converters if needed, e.g., for specific custom types.
        };

        public PlainAIFunction(FunctionWithDescription functionWithDescription, IHostSession session)
        {
            _functionWithDescription = functionWithDescription ?? throw new ArgumentNullException(nameof(functionWithDescription));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public override string Name => _functionWithDescription.Description.Name;

        public override string Description => _functionWithDescription.Description.Description;

        public override MethodInfo? UnderlyingMethod => _functionWithDescription.Function.Method;

        public override JsonElement JsonSchema
        {
            get
            {
                var schemaObject = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject()
                };
                var requiredProperties = new JsonArray();

                // Support for strict mode
                if (_functionWithDescription.Description.StrictMode)
                {
                    schemaObject["strict"] = true;
                    schemaObject["additionalProperties"] = false;
                }

                // Process each parameter in the function description
                foreach (var paramInfo in _functionWithDescription.Description.Parameters)
                {
                    var propertySchema = BuildSchemaFromMetadata(paramInfo);

                    // Add parameter to properties
                    if (!string.IsNullOrEmpty(paramInfo.Name))
                    {
                        ((JsonObject)schemaObject["properties"]!).Add(paramInfo.Name, propertySchema);
                        if (paramInfo.IsRequired)
                        {
                            requiredProperties.Add(paramInfo.Name);
                        }
                    }
                }

                if (requiredProperties.Any())
                {
                    schemaObject["required"] = requiredProperties;
                }

                using var doc = JsonDocument.Parse(schemaObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                return doc.RootElement.Clone();
            }
        }


        /// <summary>
        /// Builds a JSON schema object from FunctionTypeMetadata, handling nested structures recursively
        /// </summary>
        private JsonObject BuildSchemaFromMetadata(FunctionTypeMetadata metadata)
        {
            var propertySchema = new JsonObject();
            
            // Handle type information
            propertySchema["type"] = GetJsonType(metadata.ParameterType);
            
            // Handle nullable types or multi-type support by analyzing parameter type
            if (Nullable.GetUnderlyingType(metadata.ParameterType) != null)
            {
                var typeArray = new JsonArray();
                typeArray.Add(GetJsonType(Nullable.GetUnderlyingType(metadata.ParameterType)!));
                typeArray.Add("null");
                propertySchema["type"] = typeArray;
            }

            // Handle enum types - use metadata's EnumValues if available, otherwise fall back to reflection
            Type typeToCheck = metadata.ParameterType;
            if (Nullable.GetUnderlyingType(typeToCheck) != null)
            {
                typeToCheck = Nullable.GetUnderlyingType(typeToCheck)!;
            }
            
            if (metadata.EnumValues != null && metadata.EnumValues.Count > 0)
            {
                // Use enum values from metadata
                var enumArray = new JsonArray();
                foreach (var enumValue in metadata.EnumValues)
                {
                    enumArray.Add(enumValue);
                }
                propertySchema["enum"] = enumArray;
            }
            else if (typeToCheck.IsEnum)
            {
                // Fall back to reflection for enum types
                var enumArray = new JsonArray();
                foreach (var enumValue in Enum.GetNames(typeToCheck))
                {
                    enumArray.Add(enumValue);
                }
                propertySchema["enum"] = enumArray;
            }

            // Handle array types - use metadata's ArrayElementMetadata if available
            string? primaryType = null;
            if (propertySchema["type"] is JsonValue jsonValue)
            {
                primaryType = jsonValue.GetValue<string>();
            }

            if (primaryType == "array" || propertySchema["type"] is JsonArray typeArrayValue && typeArrayValue.Any(t => t?.GetValue<string>() == "array"))
            {
                if (metadata.ArrayElementMetadata != null)
                {
                    // Use the enhanced metadata to recursively build the array element schema
                    propertySchema["items"] = BuildSchemaFromMetadata(metadata.ArrayElementMetadata);
                }
                else
                {
                    // Fall back to reflection for array element type
                    Type? elementType = null;
                    if (metadata.ParameterType.IsArray)
                    {
                        elementType = metadata.ParameterType.GetElementType();
                    }
                    else if (metadata.ParameterType.IsGenericType &&
                             metadata.ParameterType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        elementType = metadata.ParameterType.GetGenericArguments()[0];
                    }

                    if (elementType != null)
                    {
                        var itemsSchema = new JsonObject
                        {
                            ["type"] = GetJsonType(elementType)
                        };
                        propertySchema["items"] = itemsSchema;
                    }
                    else
                    {
                        propertySchema["items"] = new JsonObject { ["type"] = "object" };
                    }
                }
            }

            // Handle object types - use metadata's ObjectProperties if available
            if (primaryType == "object" && metadata.ObjectProperties != null && metadata.ObjectProperties.Count > 0)
            {
                var objectPropertiesSchema = new JsonObject();
                var objectRequiredProperties = new JsonArray();

                foreach (var property in metadata.ObjectProperties)
                {
                    objectPropertiesSchema[property.Key] = BuildSchemaFromMetadata(property.Value);
                    if (property.Value.IsRequired)
                    {
                        objectRequiredProperties.Add(property.Key);
                    }
                }

                propertySchema["properties"] = objectPropertiesSchema;
                if (objectRequiredProperties.Count > 0)
                {
                    propertySchema["required"] = objectRequiredProperties;
                }
            }

            // Add description if available
            if (!string.IsNullOrEmpty(metadata.Description))
            {
                propertySchema["description"] = metadata.Description;
            }

            return propertySchema;
        }


        /// <summary>
        /// Serializes an object to JSON without quotes around property names and in a compact format
        /// </summary>
        /// <param name="obj">The object to serialize</param>
        /// <returns>A compact JSON string with unquoted property names</returns>
        private static string SerializeToolcallResult(object obj)
        {
            if (obj == null)
                return string.Empty;

            // Create a custom JsonTextWriter that doesn't write quotes around property names
            using (var stringWriter = new StringWriter())
            using (var jsonWriter = new Newtonsoft.Json.JsonTextWriter(stringWriter))
            {
                // Configure the writer to not quote property names and produce compact output
                jsonWriter.QuoteName = false;
                jsonWriter.Indentation = 0; // No indentation
                jsonWriter.Formatting = Newtonsoft.Json.Formatting.None; // No indentation or extra whitespace

                // Create serializer
                var serializer = new Newtonsoft.Json.JsonSerializer();

                // Serialize the object directly to the writer
                serializer.Serialize(jsonWriter, obj);

                return stringWriter.ToString();
            }
        }


        /// <summary>
        /// Checks if an object has any description attributes (either on the class or its properties)
        /// </summary>
        /// <param name="obj">The object to check for description attributes</param>
        /// <returns>True if the object has any description attributes, false otherwise</returns>
        private bool HasDescriptionAttribute(object obj)
        {
            if (obj == null)
                return false;

            var type = obj.GetType();
            
            // Check if class has description attribute
            var classDescAttr = type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            if (classDescAttr != null)
                return true;
            
            // Check if any property has description attribute
            foreach (var prop in type.GetProperties())
            {
                var propDescAttr = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
                if (propDescAttr != null)
                    return true;
            }
            
            return false;
        }

        /// <summary>
        /// Gets the description of an object and its properties from Description attributes
        /// </summary>
        /// <param name="obj">The object to get descriptions from</param>
        /// <returns>A string containing the object and property descriptions</returns>
        private string GetObjectDescription(object obj)
        {
            if (obj == null)
                return string.Empty;

            var type = obj.GetType();
            var sb = new StringBuilder();

            // Get class description
            var classDescAttr = type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            if (classDescAttr != null)
            {
                sb.Append($"{type.Name}: {classDescAttr.Description}");
            }
            else
            {
                sb.Append($"{type.Name}");
            }
            sb.Append("{");

            // Get property descriptions
            foreach (var prop in type.GetProperties())
            {
                var propDescAttr = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
                if (propDescAttr != null)
                {
                    sb.Append($"{prop.Name}: {propDescAttr.Description}\n");
                }
            }
            sb.Append("}");

            return sb.ToString().TrimEnd();
        }

        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken = default)
        {
            var methodInfo = GetMethodInfo();
            var callParameters = BuildCallParameters(arguments, methodInfo);
            var result = await InvokeMethodAsync(methodInfo, callParameters);
            
            return ProcessResult(result);
        }

        private MethodInfo GetMethodInfo()
        {
            var methodInfo = _functionWithDescription.Function.Method;
            if (methodInfo == null)
            {
                throw new InvalidOperationException("Delegate's MethodInfo is not available from FunctionWithDescription.Function.Method.");
            }
            return methodInfo;
        }

        private object?[] BuildCallParameters(AIFunctionArguments arguments, MethodInfo methodInfo)
        {
            var methodParameters = methodInfo.GetParameters();
            var callParameters = new object?[methodParameters.Length];

            if (IsSingleDictionaryParameter(methodParameters))
            {
                callParameters[0] = ConvertToDictionary(arguments);
            }
            else if (methodParameters.Length == 1 && typeof(IHostSession).IsAssignableFrom(methodParameters[0].ParameterType))
            {
                // Handle a single session parameter (IHostSession)
                callParameters[0] = _session;
            }
            else
            {
                PopulateRegularParameters(arguments, methodParameters, callParameters);
            }

            return callParameters;
        }

        private static bool IsSingleDictionaryParameter(ParameterInfo[] parameters)
        {
            return parameters.Length == 1 && 
                   parameters[0].ParameterType == typeof(Dictionary<string, object?>);
        }

        private Dictionary<string, object?> ConvertToDictionary(AIFunctionArguments arguments)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var kvp in arguments)
            {
                if (kvp.Value is JsonElement jElement)
                {
                    var deserializedValue = JsonSerializer.Deserialize<object>(jElement.GetRawText(), _serializerOptions);
                    dict[kvp.Key] = ConvertObjectToDictionary(deserializedValue);
                }
                else
                {
                    dict[kvp.Key] = ConvertObjectToDictionary(kvp.Value);
                }
            }
            dict.Add(DasSession, _session);
            return dict;
        }

        private object? ConvertObjectToDictionary(object? value)
        {
            if (value == null)
                return null;

            // Handle primitive types and strings
            if (value is string || value is bool || value is DateTime || value is DateTimeOffset ||
                value is sbyte || value is byte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal)
            {
                return value;
            }

            // Handle JsonElement
            if (value is JsonElement jsonElement)
            {
                return ConvertJsonElementToDictionary(jsonElement);
            }

            // Handle arrays and lists
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    list.Add(ConvertObjectToDictionary(item));
                }
                return list;
            }

            // Handle objects that can be converted to dictionaries
            try
            {
                // Serialize to JSON and then deserialize as JsonElement for consistent handling
                var jsonString = JsonSerializer.Serialize(value, _serializerOptions);
                var deserializedElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
                return ConvertJsonElementToDictionary(deserializedElement);
            }
            catch
            {
                // If serialization fails, return the original value
                return value;
            }
        }

        private object? ConvertJsonElementToDictionary(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object?>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = ConvertJsonElementToDictionary(property.Value);
                    }
                    return dict;

                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonElementToDictionary(item));
                    }
                    return list;

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    if (element.TryGetInt64(out long longValue))
                        return longValue;
                    if (element.TryGetDouble(out double doubleValue))
                        return doubleValue;
                    return element.GetDecimal();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return element.ToString();
            }
        }

        private void PopulateRegularParameters(AIFunctionArguments arguments, ParameterInfo[] methodParameters, object?[] callParameters)
        {
            for (int i = 0; i < methodParameters.Length; i++)
            {
                var param = methodParameters[i];
                
                // Check if this parameter is of type IHostSession
                if (typeof(IHostSession).IsAssignableFrom(param.ParameterType))
                {
                    callParameters[i] = _session;
                }
                else
                {
                    callParameters[i] = GetParameterValue(arguments, param);
                }
            }
        }

        private object? GetParameterValue(AIFunctionArguments arguments, ParameterInfo param)
        {
            if (arguments.TryGetValue(param.Name!, out var argValueObj))
            {
                return ConvertArgumentValue(argValueObj, param);
            }
            
            return GetDefaultParameterValue(param);
        }

        private object? ConvertArgumentValue(object? argValueObj, ParameterInfo param)
        {
            if (argValueObj is JsonElement jElement)
            {
                return DeserializeJsonElement(jElement, param);
            }
            
            if (argValueObj != null && param.ParameterType.IsAssignableFrom(argValueObj.GetType()))
            {
                return argValueObj;
            }
            
            if (argValueObj != null)
            {
                return ConvertArgumentType(argValueObj, param);
            }
            
            return HandleNullArgument(param);
        }

        private object? DeserializeJsonElement(JsonElement jElement, ParameterInfo param)
        {
            try
            {
                return JsonSerializer.Deserialize(jElement.GetRawText(), param.ParameterType, _serializerOptions);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Failed to deserialize argument '{param.Name}' from JsonElement to type '{param.ParameterType.Name}'. Value: {jElement.GetRawText()}", ex);
            }
        }

        private object? ConvertArgumentType(object argValueObj, ParameterInfo param)
        {
            try
            {
                return Convert.ChangeType(argValueObj, param.ParameterType, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException || ex is ArgumentNullException)
            {
                return TryJsonFallbackConversion(argValueObj, param);
            }
        }

        private object? TryJsonFallbackConversion(object argValueObj, ParameterInfo param)
        {
            if (argValueObj is string s && (s.TrimStart().StartsWith("{") || s.TrimStart().StartsWith("[")))
            {
                try
                {
                    return JsonSerializer.Deserialize(s, param.ParameterType, _serializerOptions);
                }
                catch (JsonException jsonEx)
                {
                    throw new ArgumentException($"Failed to convert/deserialize string argument '{param.Name}' to type '{param.ParameterType.Name}'. Value: {s}", jsonEx);
                }
            }
            
            throw new ArgumentException($"Failed to convert argument '{param.Name}' of type '{argValueObj.GetType().FullName}' to target type '{param.ParameterType.Name}'. Value: {argValueObj}");
        }

        private object? HandleNullArgument(ParameterInfo param)
        {
            if (param.ParameterType.IsValueType && Nullable.GetUnderlyingType(param.ParameterType) == null)
            {
                return param.HasDefaultValue ? param.DefaultValue : Activator.CreateInstance(param.ParameterType);
            }
            
            return null;
        }

        private object? GetDefaultParameterValue(ParameterInfo param)
        {
            if (param.HasDefaultValue)
            {
                return param.DefaultValue;
            }
            
            if (param.ParameterType.IsValueType && Nullable.GetUnderlyingType(param.ParameterType) == null)
            {
                return Activator.CreateInstance(param.ParameterType);
            }
            
            return null;
        }

        private async ValueTask<object?> InvokeMethodAsync(MethodInfo methodInfo, object?[] callParameters)
        {
            var targetInstance = GetTargetInstance(methodInfo);
            var result = methodInfo.Invoke(targetInstance, callParameters);
            
            return await ProcessAsyncResult(result);
        }

        private object? GetTargetInstance(MethodInfo methodInfo)
        {
            var targetInstance = methodInfo.IsStatic ? null : _functionWithDescription.Function.Target;
            
            if (!methodInfo.IsStatic && targetInstance == null)
            {
                throw new InvalidOperationException($"Target object is null for non-static method '{_functionWithDescription.Description.Name}'. Ensure FunctionWithDescription.Function.Target is set for instance methods.");
            }
            
            return targetInstance;
        }

        private async ValueTask<object?> ProcessAsyncResult(object? result)
        {
            if (result is Task taskResult)
            {
                await taskResult.ConfigureAwait(false);
                return taskResult.GetType().IsGenericType ? ((dynamic)taskResult).Result : null;
            }
            
            if (result is ValueTask valueTask)
            {
                await valueTask.ConfigureAwait(false);
                var resultType = result.GetType();
                return resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(ValueTask<>) 
                    ? ((dynamic)result).Result 
                    : null;
            }
            
            return result;
        }

        private object? ProcessResult(object? result)
        {
            if (result == null || result is string)
            {
                return result;
            }
            
            var serializedResult = SerializeToolcallResult(result);
            
            if (HasDescriptionAttribute(result))
            {
                var description = GetObjectDescription(result);
                return $"{description}\n{serializedResult}";
            }
            
            return serializedResult;
        }

        /// <summary>
        /// Gets the JSON schema describing the function's return value.
        /// Matches the updated Microsoft.Extensions.AI.AIFunction.ReturnJsonSchema property signature.
        /// </summary>
        public override JsonElement? ReturnJsonSchema
        {
            get
            {
                var returnParam = _functionWithDescription.Description.ReturnParameter;
                if (returnParam == null)
                {
                    return null;
                }

                // Use the enhanced metadata-based schema builder for consistency
                var schemaObject = BuildSchemaFromMetadata(returnParam);

                using var doc = JsonDocument.Parse(schemaObject.ToJsonString());
                return doc.RootElement.Clone();
            }
        }

        private JsonObject GenerateSchema(Type type)
        {
            return GenerateSchema(type, new HashSet<Type>());
        }

        private JsonObject GenerateSchema(Type type, HashSet<Type> visitedTypes)
        {
            // Handle Task<T> return types by extracting the generic type argument
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                type = type.GetGenericArguments()[0];
            }

            if (!visitedTypes.Add(type))
            {
                // Circular reference detected, return a schema that indicates this or is empty.
                // For simplicity, returning an object type can break the recursion.
                // A more sophisticated approach might use JSON schema references ($ref), but this is safer for now.
                return new JsonObject { ["type"] = GetJsonType(type) };
            }

            var schemaObject = new JsonObject
            {
                ["type"] = GetJsonType(type)
            };

            try
            {
                var descriptionAttribute = type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
                if (descriptionAttribute != null)
                {
                    schemaObject["description"] = descriptionAttribute.Description;
                }

                if (GetJsonType(type) == "object")
                {
                    var properties = new JsonObject();
                    var requiredProperties = new JsonArray();

                    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (prop.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() != null)
                        {
                            continue;
                        }

                        var propSchema = GenerateSchema(prop.PropertyType, visitedTypes);
                        var propDescriptionAttribute = prop.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
                        if (propDescriptionAttribute != null)
                        {
                            propSchema["description"] = propDescriptionAttribute.Description;
                        }

                        properties[prop.Name] = propSchema;

                        // Simple check for required properties (e.g., non-nullable value types)
                        if (Nullable.GetUnderlyingType(prop.PropertyType) == null && prop.PropertyType.IsValueType)
                        {
                            requiredProperties.Add(prop.Name);
                        }
                    }

                    schemaObject["properties"] = properties;
                    if (requiredProperties.Count > 0)
                    {
                        schemaObject["required"] = requiredProperties;
                    }
                }
                else if (GetJsonType(type) == "array")
                {
                    Type? elementType = null;
                    if (type.IsArray)
                    {
                        elementType = type.GetElementType();
                    }
                    else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        elementType = type.GetGenericArguments()[0];
                    }

                    if (elementType != null)
                    {
                        schemaObject["items"] = GenerateSchema(elementType, visitedTypes);
                    }
                    else
                    {
                        schemaObject["items"] = new JsonObject { ["type"] = "object" };
                    }
                }
            }
            finally
            {
                visitedTypes.Remove(type);
            }

            return schemaObject;
        }
        

        private string GetJsonType(Type type)
        {
            if (type == typeof(string) || type == typeof(char) || type == typeof(Guid) || type.IsEnum)
                return "string";
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
                type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte))
                return "integer";
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return "number";
            if (type == typeof(bool))
                return "boolean";
            if (type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return "array";
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
                return "string";
            return "object";
        }
    }
}
