using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Core.Schema;

/// <summary>
/// Reflection-based DTDL v3 Interface generator for strongly-typed parameter
/// classes (transform / DSP filter / command / device / sensor).
/// </summary>
/// <remarks>
/// Output shape mirrors <c>examples/system-agent/docs/Metrics/dtdl/CpuSensorConfig.v2.json</c>:
/// <code>
/// {
///   "@context": "dtmi:dtdl:context;3",
///   "@id":      "{Prefix}:{Category}:{TypeName};{Version}",
///   "@type":    "Interface",
///   "schemas":  [ Enum / Object / Array / Map definitions ],
///   "contents": [ Property bindings referencing schemas by DTMI ]
/// }
/// </code>
/// <para>DataAnnotation constraints DTDL cannot express (Range / Required /
/// Pattern / Default / StringLength) are echoed into the field's
/// <c>description</c> as parenthesised hints; runtime enforcement remains the
/// responsibility of <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/>.</para>
/// </remarks>
public static class DtdlInterfaceEmitter
{
    public sealed record Options(
        string Prefix,
        string Category,
        string TypeName,
        string? DisplayName = null,
        string? Description = null,
        int Version = 1);

    public sealed record PropertyBinding(string Name, Type Type, bool Writable = true);

    public static JsonObject Emit(Options opts, params PropertyBinding[] properties)
    {
        if (properties.Length == 0)
        {
            throw new ArgumentException(
                "At least one property binding is required.",
                nameof(properties));
        }

        var seen = new HashSet<string>();
        var contents = new JsonArray();
        var schemas = new JsonArray();
        var dtmisByType = new Dictionary<Type, string>();

        foreach (var property in properties)
        {
            if (!property.Type.IsClass)
            {
                throw new DtdlInterfaceEmissionException(
                    $"The property {property.Name} must be a class");
            }
            if (!seen.Add(property.Name))
            {
                throw new DtdlInterfaceEmissionException(
                    $"The property {property.Name} is duplicate");
            }

            var dtmi = $"{opts.Prefix}:{opts.Category}:{opts.TypeName}:{property.Name};{opts.Version}";
            var visited = new HashSet<Type>();
            var fields = BuildFields(property.Type, opts, schemas, dtmisByType, visited);

            contents.Add(new JsonObject
            {
                ["@type"] = "Property",
                ["name"] = property.Name,
                ["schema"] = dtmi,
                ["writable"] = property.Writable,
            });

            schemas.Add(new JsonObject
            {
                ["@id"] = dtmi,
                ["@type"] = "Object",
                ["fields"] = fields,
            });
        }

        var iface = new JsonObject
        {
            ["@context"] = "dtmi:dtdl:context;3",
            ["@id"] = $"{opts.Prefix}:{opts.Category}:{opts.TypeName};{opts.Version}",
            ["@type"] = "Interface",
            ["contents"] = contents,
            ["schemas"] = schemas,
        };

        if (!string.IsNullOrEmpty(opts.DisplayName))
        {
            iface["displayName"] = opts.DisplayName;
        }
        if (!string.IsNullOrEmpty(opts.Description))
        {
            iface["description"] = opts.Description;
        }
        return iface;
    }

    private static JsonArray BuildFields(
        Type pocoType,
        Options opts,
        JsonArray schemas,
        Dictionary<Type, string> dtmisByType,
        HashSet<Type> visited)
    {
        if (!visited.Add(pocoType))
        {
            throw new DtdlInterfaceEmissionException(
                $"Circular reference detected at type '{pocoType.FullName}'. " +
                $"Recursive schemas are not supported.");
        }

        try
        {
            var fields = new JsonArray();
            foreach (var propInfo in pocoType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!propInfo.CanRead) continue;

                var underlying = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;
                var fieldName = ResolveFieldName(propInfo);
                var schemaRef = ResolveSchemaRef(underlying, fieldName, opts, schemas, dtmisByType, visited);

                if (schemaRef is null) continue;

                var field = new JsonObject
                {
                    ["name"] = fieldName,
                    ["schema"] = schemaRef,
                };

                var displayName = propInfo.GetCustomAttribute<DisplayAttribute>()?.GetName();
                if (!string.IsNullOrEmpty(displayName))
                {
                    field["displayName"] = displayName;
                }

                var description = BuildFieldDescription(propInfo);
                if (!string.IsNullOrEmpty(description))
                {
                    field["description"] = description;
                }

                fields.Add(field);
            }
            return fields;
        }
        finally
        {
            visited.Remove(pocoType);
        }
    }

    private static JsonNode? ResolveSchemaRef(
        Type t,
        string fieldName,
        Options opts,
        JsonArray schemas,
        Dictionary<Type, string> dtmisByType,
        HashSet<Type> visited)
    {
        if (t.IsEnum)
        {
            return EnsureEnumSchema(t, opts, schemas, dtmisByType);
        }

        var primitive = MapPrimitive(t);
        if (primitive is not null) return primitive;

        if (IsStringDictionary(t, out var valueType))
        {
            return EnsureMapSchema(t, valueType, fieldName, opts, schemas, dtmisByType, visited);
        }

        if (IsArrayLike(t, out var elementType))
        {
            return EnsureArraySchema(t, elementType, fieldName, opts, schemas, dtmisByType, visited);
        }

        if (IsUnsupportedBclType(t))
        {
            throw new DtdlInterfaceEmissionException(
                $"Field type '{t.FullName}' is not supported by DTDL emission. " +
                $"Supported: primitives, enums, arrays, Dictionary<string, T>, POCO classes.");
        }

        if (t.IsAbstract || t.IsInterface)
        {
            throw new DtdlInterfaceEmissionException(
                $"Field has abstract / interface type '{t.FullName}'. " +
                $"DTDL emission does not support polymorphism — use a concrete class.");
        }

        if (IsObjectLike(t))
        {
            return EnsureNestedObjectSchema(t, opts, schemas, dtmisByType, visited);
        }

        return null;
    }

    private static string EnsureEnumSchema(
        Type enumType,
        Options opts,
        JsonArray schemas,
        Dictionary<Type, string> dtmisByType)
    {
        if (dtmisByType.TryGetValue(enumType, out var existing)) return existing;

        var dtmi = $"{opts.Prefix}:{opts.Category}:{opts.TypeName}:{enumType.Name};{opts.Version}";
        dtmisByType[enumType] = dtmi;

        var values = new JsonArray();
        foreach (var name in Enum.GetNames(enumType))
        {
            var memberValue = enumType.GetField(name)
                ?.GetCustomAttribute<EnumMemberAttribute>()?.Value;
            values.Add(new JsonObject
            {
                ["name"] = name,
                ["enumValue"] = memberValue ?? name,
            });
        }

        schemas.Add(new JsonObject
        {
            ["@id"] = dtmi,
            ["@type"] = "Enum",
            ["valueSchema"] = "string",
            ["enumValues"] = values,
        });
        return dtmi;
    }

    private static string EnsureNestedObjectSchema(
        Type t,
        Options opts,
        JsonArray schemas,
        Dictionary<Type, string> dtmisByType,
        HashSet<Type> visited)
    {
        if (dtmisByType.TryGetValue(t, out var existing)) return existing;

        var dtmi = $"{opts.Prefix}:{opts.Category}:{opts.TypeName}:{t.Name};{opts.Version}";
        dtmisByType[t] = dtmi;

        var fields = BuildFields(t, opts, schemas, dtmisByType, visited);
        schemas.Add(new JsonObject
        {
            ["@id"] = dtmi,
            ["@type"] = "Object",
            ["fields"] = fields,
        });
        return dtmi;
    }

    private static string EnsureArraySchema(
        Type t,
        Type elementType,
        string fieldName,
        Options opts,
        JsonArray schemas,
        Dictionary<Type, string> dtmisByType,
        HashSet<Type> visited)
    {
        if (dtmisByType.TryGetValue(t, out var existing)) return existing;

        var segment = Pascalize(fieldName) + "List";
        var dtmi = $"{opts.Prefix}:{opts.Category}:{opts.TypeName}:{segment};{opts.Version}";
        dtmisByType[t] = dtmi;

        var elementUnderlying = Nullable.GetUnderlyingType(elementType) ?? elementType;
        var elementSchema = ResolveSchemaRef(elementUnderlying, fieldName, opts, schemas, dtmisByType, visited)
            ?? throw new DtdlInterfaceEmissionException(
                $"Unsupported array element type '{elementType.FullName}' on field '{fieldName}'.");

        schemas.Add(new JsonObject
        {
            ["@id"] = dtmi,
            ["@type"] = "Array",
            ["elementSchema"] = elementSchema,
        });
        return dtmi;
    }

    private static string EnsureMapSchema(
        Type t,
        Type valueType,
        string fieldName,
        Options opts,
        JsonArray schemas,
        Dictionary<Type, string> dtmisByType,
        HashSet<Type> visited)
    {
        if (dtmisByType.TryGetValue(t, out var existing)) return existing;

        var segment = Pascalize(fieldName) + "Map";
        var dtmi = $"{opts.Prefix}:{opts.Category}:{opts.TypeName}:{segment};{opts.Version}";
        dtmisByType[t] = dtmi;

        var valueUnderlying = Nullable.GetUnderlyingType(valueType) ?? valueType;
        var valueSchema = ResolveSchemaRef(valueUnderlying, fieldName + "Value", opts, schemas, dtmisByType, visited)
            ?? throw new DtdlInterfaceEmissionException(
                $"Unsupported map value type '{valueType.FullName}' on field '{fieldName}'.");

        schemas.Add(new JsonObject
        {
            ["@id"] = dtmi,
            ["@type"] = "Map",
            ["mapKey"]   = new JsonObject { ["name"] = "key",   ["schema"] = "string" },
            ["mapValue"] = new JsonObject { ["name"] = "value", ["schema"] = valueSchema },
        });
        return dtmi;
    }

    /// <summary>
    /// Composes a field's <c>description</c> string from <see cref="DescriptionAttribute"/> +
    /// echoed DataAnnotation hints (Required / Range / StringLength / RegularExpression / DefaultValue).
    /// </summary>
    private static string BuildFieldDescription(PropertyInfo prop)
    {
        var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description
                          ?? prop.GetCustomAttribute<DisplayAttribute>()?.GetDescription()
                          ?? string.Empty;

        var hints = new List<string>();

        if (prop.GetCustomAttribute<RequiredAttribute>() is not null)
        {
            hints.Add("required");
        }
        if (prop.GetCustomAttribute<RangeAttribute>() is { } range)
        {
            hints.Add($"range {range.Minimum}..{range.Maximum}");
        }
        if (prop.GetCustomAttribute<StringLengthAttribute>() is { } strLen)
        {
            hints.Add(strLen.MinimumLength > 0
                ? $"length {strLen.MinimumLength}..{strLen.MaximumLength}"
                : $"max length {strLen.MaximumLength}");
        }
        else
        {
            if (prop.GetCustomAttribute<MinLengthAttribute>() is { } minLen)
            {
                hints.Add($"min length {minLen.Length}");
            }
            if (prop.GetCustomAttribute<MaxLengthAttribute>() is { } maxLen)
            {
                hints.Add($"max length {maxLen.Length}");
            }
        }
        if (prop.GetCustomAttribute<RegularExpressionAttribute>() is { } regex)
        {
            hints.Add($"pattern {regex.Pattern}");
        }
        if (prop.GetCustomAttribute<DefaultValueAttribute>() is { Value: not null } def)
        {
            hints.Add($"default {def.Value}");
        }

        if (hints.Count == 0) return description;

        var hintText = $"({string.Join(", ", hints)})";
        return string.IsNullOrEmpty(description) ? hintText : $"{description} {hintText}";
    }

    private static string ResolveFieldName(PropertyInfo prop)
    {
        var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
        if (!string.IsNullOrEmpty(jsonName)) return jsonName;
        return ToCamelCase(prop.Name);
    }

    private static string? MapPrimitive(Type t)
    {
        if (t == typeof(string)) return "string";
        if (t == typeof(bool)) return "boolean";
        if (t == typeof(byte) || t == typeof(sbyte) ||
            t == typeof(short) || t == typeof(ushort) ||
            t == typeof(int) || t == typeof(uint)) return "integer";
        if (t == typeof(long) || t == typeof(ulong)) return "long";
        if (t == typeof(float)) return "float";
        if (t == typeof(double) || t == typeof(decimal)) return "double";
        return null;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0])) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string Pascalize(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsUpper(name[0])) return name;
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

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
        return false;
    }

    private static bool IsUnsupportedBclType(Type t)
    {
        if (t.Namespace is null) return false;
        if (!t.Namespace.StartsWith("System", StringComparison.Ordinal)) return false;
        return t == typeof(DateTime) ||
               t == typeof(DateTimeOffset) ||
               t == typeof(DateOnly) ||
               t == typeof(TimeOnly) ||
               t == typeof(TimeSpan) ||
               t == typeof(Guid) ||
               t == typeof(Uri);
    }

    private static bool IsObjectLike(Type t)
    {
        if (t == typeof(string)) return false;
        if (t.IsPrimitive || t.IsEnum) return false;
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(t)) return false;
        return t.IsClass || (t.IsValueType && !t.IsPrimitive);
    }
}

public sealed class DtdlInterfaceEmissionException : Exception
{
    public DtdlInterfaceEmissionException(string message) : base(message) { }
    public DtdlInterfaceEmissionException(string message, Exception inner) : base(message, inner) { }
}
