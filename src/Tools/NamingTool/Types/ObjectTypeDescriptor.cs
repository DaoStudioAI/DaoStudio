using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Naming.Types
{
    /// <summary>
    /// Represents a complex object type with its full hierarchy for OpenAI API integration.
    /// This class captures the complete schema structure of object parameters that can be
    /// serialized to JSON schema format for OpenAI function calling.
    /// </summary>
    public class ObjectTypeDescriptor : Type
    {
        /// <summary>
        /// The name of the object type
        /// </summary>
        public string TypeName { get; }
        
        /// <summary>
        /// Description of the object type
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Dictionary of properties in the object, where key is property name and value is the property type descriptor
        /// </summary>
        public Dictionary<string, PropertyDescriptor> Properties { get; }
        
        /// <summary>
        /// List of required property names
        /// </summary>
        public List<string> RequiredProperties { get; }

        public ObjectTypeDescriptor(string typeName, string description, Dictionary<string, PropertyDescriptor> properties)
        {
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
            RequiredProperties = properties.Where(p => p.Value.IsRequired).Select(p => p.Key).ToList();
        }

        /// <summary>
        /// Creates an ObjectTypeDescriptor from a ParameterConfig with ObjectProperties
        /// </summary>
        public static ObjectTypeDescriptor FromParameterConfig(ParameterConfig paramConfig)
        {
            if (paramConfig.Type != ParameterType.Object)
                throw new ArgumentException("ParameterConfig must be of type Object", nameof(paramConfig));

            var properties = new Dictionary<string, PropertyDescriptor>();

            if (paramConfig.ObjectProperties != null)
            {
                foreach (var property in paramConfig.ObjectProperties)
                {
                    properties[property.Name] = PropertyDescriptor.FromParameterConfig(property);
                }
            }

            return new ObjectTypeDescriptor(
                paramConfig.Name,
                paramConfig.Description,
                properties);
        }

        /// <summary>
        /// Converts this object type descriptor to a JSON schema object for OpenAI API
        /// </summary>
        public JsonObject ToJsonSchema()
        {
            var schema = new JsonObject
            {
                ["type"] = "object",
                ["description"] = Description
            };

            var propertiesObj = new JsonObject();
            foreach (var property in Properties)
            {
                propertiesObj[property.Key] = property.Value.ToJsonSchema();
            }
            schema["properties"] = propertiesObj;

            if (RequiredProperties.Count > 0)
            {
                var requiredArray = new JsonArray();
                foreach (var requiredProp in RequiredProperties)
                {
                    requiredArray.Add(requiredProp);
                }
                schema["required"] = requiredArray;
            }

            return schema;
        }

        #region Type Implementation (Minimal required overrides)
        
        public override string Name => TypeName;
        public override string FullName => $"ObjectTypeDescriptor.{TypeName}";
        public override Type BaseType => typeof(object);
        public override Assembly Assembly => typeof(ObjectTypeDescriptor).Assembly;
        public override string? Namespace => "Naming.Types";
        public override Module Module => typeof(ObjectTypeDescriptor).Module;
        public override Guid GUID => Guid.NewGuid(); // Each instance gets a unique GUID
        public override bool IsGenericTypeDefinition => false;
        public override bool IsConstructedGenericType => false;
        public override Type UnderlyingSystemType => this;
        public override string? AssemblyQualifiedName => $"{FullName}, {Assembly.FullName}";

        protected override TypeAttributes GetAttributeFlagsImpl() => TypeAttributes.Class | TypeAttributes.Public;
        protected override bool IsArrayImpl() => false;
        protected override bool IsByRefImpl() => false;
        protected override bool IsCOMObjectImpl() => false;
        protected override bool IsPointerImpl() => false;
        protected override bool IsPrimitiveImpl() => false;
        protected override bool HasElementTypeImpl() => false;
        public override Type GetElementType() => throw new NotSupportedException();

        public override Type[] GetGenericArguments() => Array.Empty<Type>();
        public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();
        public override bool IsDefined(Type attributeType, bool inherit) => false;
        
        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) => Array.Empty<ConstructorInfo>();
        public override EventInfo? GetEvent(string name, BindingFlags bindingAttr) => null;
        public override EventInfo[] GetEvents(BindingFlags bindingAttr) => Array.Empty<EventInfo>();
        public override FieldInfo? GetField(string name, BindingFlags bindingAttr) => null;
        public override FieldInfo[] GetFields(BindingFlags bindingAttr) => Array.Empty<FieldInfo>();
        public override Type? GetInterface(string name, bool ignoreCase) => null;
        public override Type[] GetInterfaces() => Array.Empty<Type>();
        public override MemberInfo[] GetMembers(BindingFlags bindingAttr) => Array.Empty<MemberInfo>();
        public override MethodInfo[] GetMethods(BindingFlags bindingAttr) => Array.Empty<MethodInfo>();
        public override Type? GetNestedType(string name, BindingFlags bindingAttr) => null;
        public override Type[] GetNestedTypes(BindingFlags bindingAttr) => Array.Empty<Type>();
        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr) => Array.Empty<PropertyInfo>();
        public override object? InvokeMember(string name, BindingFlags invokeAttr, Binder? binder, object? target, object?[]? args, ParameterModifier[]? modifiers, CultureInfo? culture, string[]? namedParameters) => throw new NotSupportedException();

        protected override MethodInfo? GetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers) => null;
        protected override PropertyInfo? GetPropertyImpl(string name, BindingFlags bindingAttr, Binder? binder, Type? returnType, Type[]? types, ParameterModifier[]? modifiers) => null;
        protected override ConstructorInfo? GetConstructorImpl(BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers) => null;

        public override string ToString() => $"ObjectTypeDescriptor: {TypeName}";

        #endregion
    }

    /// <summary>
    /// Represents an array type containing ObjectTypeDescriptor elements
    /// </summary>
    public class ObjectTypeDescriptorArrayType : Type
    {
        private readonly ObjectTypeDescriptor _elementType;
        
        public override string Name => _elementType.Name + "[]";
        public override string? Namespace => _elementType.Namespace;
        public override string? FullName => _elementType.FullName + "[]";
        public override Assembly Assembly => _elementType.Assembly;
        public override Type? BaseType => typeof(Array);
        public override Guid GUID => Guid.Empty;
        public override Module Module => Assembly.GetModules()[0];
        public override string? AssemblyQualifiedName => FullName + ", " + Assembly.FullName;
        public override Type UnderlyingSystemType => this;

        public ObjectTypeDescriptorArrayType(ObjectTypeDescriptor elementType)
        {
            _elementType = elementType;
        }

        public override Type GetElementType() => _elementType;
        protected override bool IsArrayImpl() => true;
        protected override bool HasElementTypeImpl() => true;
        
        // Standard Type implementation stubs
        public override bool IsAssignableFrom(Type? c) => c == this;
        public override bool IsInstanceOfType(object? o) => false;
        public override object? InvokeMember(string name, BindingFlags invokeAttr, Binder? binder, object? target, object?[]? args, ParameterModifier[]? modifiers, CultureInfo? culture, string[]? namedParameters) => throw new NotSupportedException();
        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) => Array.Empty<ConstructorInfo>();
        public override EventInfo? GetEvent(string name, BindingFlags bindingAttr) => null;
        public override EventInfo[] GetEvents(BindingFlags bindingAttr) => Array.Empty<EventInfo>();
        public override FieldInfo? GetField(string name, BindingFlags bindingAttr) => null;
        public override FieldInfo[] GetFields(BindingFlags bindingAttr) => Array.Empty<FieldInfo>();
        public override Type? GetInterface(string name, bool ignoreCase) => null;
        public override Type[] GetInterfaces() => Array.Empty<Type>();
        public override MemberInfo[] GetMembers(BindingFlags bindingAttr) => Array.Empty<MemberInfo>();
        public override MethodInfo[] GetMethods(BindingFlags bindingAttr) => Array.Empty<MethodInfo>();
        public override Type? GetNestedType(string name, BindingFlags bindingAttr) => null;
        public override Type[] GetNestedTypes(BindingFlags bindingAttr) => Array.Empty<Type>();
        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr) => Array.Empty<PropertyInfo>();
        public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();
        public override bool IsDefined(Type attributeType, bool inherit) => false;
        
        protected override TypeAttributes GetAttributeFlagsImpl() => TypeAttributes.Public | TypeAttributes.Class;
        protected override ConstructorInfo? GetConstructorImpl(BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[] types, ParameterModifier[]? modifiers) => null;
        protected override MethodInfo? GetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers) => null;
        protected override PropertyInfo? GetPropertyImpl(string name, BindingFlags bindingAttr, Binder? binder, Type? returnType, Type[]? types, ParameterModifier[]? modifiers) => null;
        protected override bool IsByRefImpl() => false;
        protected override bool IsCOMObjectImpl() => false;
        protected override bool IsPointerImpl() => false;
        protected override bool IsPrimitiveImpl() => false;
    }

    /// <summary>
    /// Represents a property within an object type
    /// </summary>
    public class PropertyDescriptor
    {
        public string Name { get; }
        public string Description { get; }
        public Type PropertyType { get; }
        public bool IsRequired { get; }
        public ObjectTypeDescriptor? ObjectTypeDescriptor { get; }
        public PropertyDescriptor? ArrayElementType { get; }

        public PropertyDescriptor(string name, string description, Type propertyType, bool isRequired, 
            ObjectTypeDescriptor? objectTypeDescriptor = null, PropertyDescriptor? arrayElementType = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            PropertyType = propertyType ?? throw new ArgumentNullException(nameof(propertyType));
            IsRequired = isRequired;
            ObjectTypeDescriptor = objectTypeDescriptor;
            ArrayElementType = arrayElementType;
        }

        /// <summary>
        /// Creates a PropertyDescriptor from a ParameterConfig
        /// </summary>
        public static PropertyDescriptor FromParameterConfig(ParameterConfig paramConfig)
        {
            ObjectTypeDescriptor? objectDescriptor = null;
            PropertyDescriptor? arrayElementDescriptor = null;
            Type propertyType;

            switch (paramConfig.Type)
            {
                case ParameterType.String:
                    propertyType = typeof(string);
                    break;
                case ParameterType.Number:
                    propertyType = typeof(double);
                    break;
                case ParameterType.Bool:
                    propertyType = typeof(bool);
                    break;
                case ParameterType.Object:
                    objectDescriptor = ObjectTypeDescriptor.FromParameterConfig(paramConfig);
                    propertyType = objectDescriptor;
                    break;
                case ParameterType.Array:
                    if (paramConfig.ArrayElementConfig != null)
                    {
                        arrayElementDescriptor = FromParameterConfig(paramConfig.ArrayElementConfig);
                        
                        // Handle custom types specially for arrays
                        if (arrayElementDescriptor.PropertyType is ObjectTypeDescriptor descriptor)
                        {
                            propertyType = new ObjectTypeDescriptorArrayType(descriptor);
                        }
                        else
                        {
                            propertyType = arrayElementDescriptor.PropertyType.MakeArrayType();
                        }
                    }
                    else
                    {
                        propertyType = typeof(object[]);
                    }
                    break;
                default:
                    propertyType = typeof(string);
                    break;
            }

            return new PropertyDescriptor(
                paramConfig.Name,
                paramConfig.Description,
                propertyType,
                paramConfig.IsRequired,
                objectDescriptor,
                arrayElementDescriptor);
        }

        /// <summary>
        /// Converts this property descriptor to a JSON schema object
        /// </summary>
        public JsonObject ToJsonSchema()
        {
            var schema = new JsonObject();

            if (ObjectTypeDescriptor != null)
            {
                // This is an object property
                return ObjectTypeDescriptor.ToJsonSchema();
            }
            else if (ArrayElementType != null)
            {
                // This is an array property
                schema["type"] = "array";
                schema["items"] = ArrayElementType.ToJsonSchema();
            }
            else
            {
                // This is a primitive property
                schema["type"] = GetJsonType(PropertyType);
            }

            if (!string.IsNullOrEmpty(Description))
            {
                schema["description"] = Description;
            }

            return schema;
        }

        private static string GetJsonType(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(double) || type == typeof(int) || type == typeof(float) || type == typeof(decimal)) return "number";
            if (type == typeof(bool)) return "boolean";
            if (type.IsArray) return "array";
            return "object";
        }
    }
}