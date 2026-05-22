using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Schema;

namespace Weda.SubNode.Core.Schema;

/// <summary>
/// Reflection-based JSON Schema generator for strongly-typed parameter classes
/// (transform / DSP filter / command parameters and command responses).
/// </summary>
/// <remarks>
/// v1 scope:
/// <list type="bullet">
/// <item>Primitives: string, bool, integer (byte..ulong), number (float/double/decimal)</item>
/// <item>Enums emitted as string with <c>enum: [name,...]</c></item>
/// <item>Arrays / List&lt;T&gt; / IList / IReadOnlyList / IEnumerable&lt;T&gt;</item>
/// <item>Dictionary&lt;string, T&gt; → object with <c>additionalProperties</c></item>
/// <item>Nested POCO classes (recursive)</item>
/// <item>DataAnnotations: Required, Range, StringLength, MinLength, MaxLength,
///       RegularExpression, AllowedValues, Description, Display, DefaultValue</item>
/// <item>JsonPropertyName for naming override; PascalCase → camelCase fallback</item>
/// </list>
/// Unsupported (fail-fast): abstract / interface properties, polymorphic types,
/// DateTime / Guid / TimeSpan / Uri / object / JsonNode etc., custom validation
/// attributes (runtime validation still applies, just not emitted in schema).
/// <para>
/// Required determination: rule A — driven solely by <c>[Required]</c>. C# nullable
/// reference type annotations are intentionally ignored.
/// </para>
/// </remarks>
public static class JsonSchemaEmitter
{
    public static JsonSchemaDto Emit(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        var root = EmitObject(underlying, new HashSet<Type>(), $"<{type.Name}>");
        return new JsonSchemaDto(root);
    }

    private static JsonObject EmitObject(Type type, HashSet<Type> visited, string path)
    {
        if (!visited.Add(type))
        {
            throw new JsonSchemaEmissionException(
                $"Circular reference detected at '{path}' (type '{type.FullName}'). " +
                $"v1 does not support recursive schemas.");
        }

        try
        {
            var properties = new JsonObject();
            var required = new JsonArray();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead)
                {
                    continue;
                }

                var name = ResolvePropertyName(prop);
                var propPath = $"{path}.{name}";
                properties[name] = EmitProperty(prop, visited, propPath);

                if (prop.GetCustomAttribute<RequiredAttribute>() is not null)
                {
                    required.Add(name);
                }
            }

            var node = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties,
            };
            if (required.Count > 0)
            {
                node["required"] = required;
            }
            return node;
        }
        finally
        {
            visited.Remove(type);
        }
    }

    private static JsonObject EmitProperty(PropertyInfo prop, HashSet<Type> visited, string path)
    {
        var propertyType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        var node = EmitTypeNode(propertyType, visited, path);

        var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description
                       ?? prop.GetCustomAttribute<DisplayAttribute>()?.GetDescription();
        if (!string.IsNullOrEmpty(description))
        {
            node["description"] = description;
        }

        var defaultValue = prop.GetCustomAttribute<DefaultValueAttribute>()?.Value;
        if (defaultValue is not null)
        {
            node["default"] = JsonValue.Create(defaultValue);
        }

        var schemaType = (string?)node["type"];
        var isString = schemaType == "string";
        var isArray = schemaType == "array";
        var isNumeric = schemaType is "integer" or "number";

        if (prop.GetCustomAttribute<StringLengthAttribute>() is { } strLen)
        {
            node["maxLength"] = strLen.MaximumLength;
            if (strLen.MinimumLength > 0)
            {
                node["minLength"] = strLen.MinimumLength;
            }
        }

        if (prop.GetCustomAttribute<MinLengthAttribute>() is { } minLen)
        {
            if (isString) node["minLength"] = minLen.Length;
            else if (isArray) node["minItems"] = minLen.Length;
        }

        if (prop.GetCustomAttribute<MaxLengthAttribute>() is { } maxLen)
        {
            if (isString) node["maxLength"] = maxLen.Length;
            else if (isArray) node["maxItems"] = maxLen.Length;
        }

        if (prop.GetCustomAttribute<RegularExpressionAttribute>() is { } regex && isString)
        {
            node["pattern"] = regex.Pattern;
        }

        if (prop.GetCustomAttribute<RangeAttribute>() is { } range && isNumeric)
        {
            node["minimum"] = JsonValue.Create(range.Minimum);
            node["maximum"] = JsonValue.Create(range.Maximum);
        }

        if (prop.GetCustomAttribute<AllowedValuesAttribute>() is { } allowed)
        {
            var enumArr = new JsonArray();
            foreach (var v in allowed.Values)
            {
                enumArr.Add(JsonValue.Create(v));
            }
            node["enum"] = enumArr;
        }

        return node;
    }

    private static JsonObject EmitTypeNode(Type type, HashSet<Type> visited, string path)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string)) return new JsonObject { ["type"] = "string" };
        if (type == typeof(bool)) return new JsonObject { ["type"] = "boolean" };
        if (IsIntegerType(type)) return new JsonObject { ["type"] = "integer" };
        if (IsNumberType(type)) return new JsonObject { ["type"] = "number" };

        // `object` and raw JSON node types map to an empty (open) schema —
        // JSON Schema interprets `{}` as "any value is valid". Used by commands
        // that legitimately accept polymorphic values (e.g. analog output value
        // varying by register type).
        if (type == typeof(object) || IsRawJsonType(type))
        {
            return new JsonObject();
        }

        if (type.IsEnum)
        {
            var names = new JsonArray();
            foreach (var n in Enum.GetNames(type))
            {
                names.Add(n);
            }
            return new JsonObject { ["type"] = "string", ["enum"] = names };
        }

        if (IsStringDictionary(type, out var valueType))
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = EmitTypeNode(valueType, visited, $"{path}.<value>"),
            };
        }

        if (IsArrayLike(type, out var elementType))
        {
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = EmitTypeNode(elementType, visited, $"{path}[]"),
            };
        }

        if (type.IsAbstract || type.IsInterface)
        {
            throw new JsonSchemaEmissionException(
                $"Property '{path}' has abstract / interface type '{type.FullName}'. " +
                $"v1 does not support polymorphism. Use a concrete class.");
        }

        if (IsUnsupportedBclType(type))
        {
            throw new JsonSchemaEmissionException(
                $"Property '{path}' has BCL type '{type.FullName}' which v1 does not support. " +
                $"Supported: primitives, string, enums, arrays, Dictionary<string, T>, POCO classes.");
        }

        if (type.IsClass || (type.IsValueType && !type.IsPrimitive))
        {
            return EmitObject(type, visited, path);
        }

        throw new JsonSchemaEmissionException(
            $"Property '{path}' has unsupported type '{type.FullName}'.");
    }

    private static string ResolvePropertyName(PropertyInfo prop)
    {
        var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
        if (!string.IsNullOrEmpty(jsonName))
        {
            return jsonName;
        }
        return ToCamelCase(prop.Name);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static bool IsIntegerType(Type t) =>
        t == typeof(byte) || t == typeof(sbyte) ||
        t == typeof(short) || t == typeof(ushort) ||
        t == typeof(int) || t == typeof(uint) ||
        t == typeof(long) || t == typeof(ulong);

    private static bool IsNumberType(Type t) =>
        t == typeof(float) || t == typeof(double) || t == typeof(decimal);

    private static bool IsStringDictionary(Type t, out Type valueType)
    {
        valueType = null!;
        if (!t.IsGenericType) return false;
        var def = t.GetGenericTypeDefinition();
        if (def != typeof(Dictionary<,>) &&
            def != typeof(IDictionary<,>) &&
            def != typeof(IReadOnlyDictionary<,>))
        {
            return false;
        }
        var args = t.GetGenericArguments();
        if (args[0] != typeof(string)) return false;
        valueType = args[1];
        return true;
    }

    private static bool IsArrayLike(Type t, out Type elementType)
    {
        elementType = null!;
        if (t.IsArray)
        {
            elementType = t.GetElementType()!;
            return true;
        }
        if (t.IsGenericType)
        {
            var def = t.GetGenericTypeDefinition();
            if (def == typeof(List<>) ||
                def == typeof(IList<>) ||
                def == typeof(IEnumerable<>) ||
                def == typeof(ICollection<>) ||
                def == typeof(IReadOnlyList<>) ||
                def == typeof(IReadOnlyCollection<>))
            {
                elementType = t.GetGenericArguments()[0];
                return true;
            }
        }
        // Non-generic IEnumerable (e.g., ArrayList) — unsupported, fall through.
        return false;
    }

    private static bool IsUnsupportedBclType(Type t)
    {
        if (t.Namespace is null) return false;
        if (!t.Namespace.StartsWith("System", StringComparison.Ordinal)) return false;

        // Common BCL "value-ish" types we explicitly do not descend into.
        // Note: `object` and raw JSON types (JsonElement / JsonNode) are handled
        // earlier in EmitTypeNode as open schemas, not as unsupported.
        return t == typeof(DateTime) ||
               t == typeof(DateTimeOffset) ||
               t == typeof(DateOnly) ||
               t == typeof(TimeOnly) ||
               t == typeof(TimeSpan) ||
               t == typeof(Guid) ||
               t == typeof(Uri);
    }

    private static bool IsRawJsonType(Type t)
    {
        if (t.Namespace is null) return false;
        if (!t.Namespace.StartsWith("System.Text.Json", StringComparison.Ordinal)) return false;
        return typeof(JsonNode).IsAssignableFrom(t) ||
               t == typeof(System.Text.Json.JsonElement) ||
               t == typeof(System.Text.Json.JsonDocument);
    }
}

public sealed class JsonSchemaEmissionException : Exception
{
    public JsonSchemaEmissionException(string message) : base(message) { }
    public JsonSchemaEmissionException(string message, Exception inner) : base(message, inner) { }
}
