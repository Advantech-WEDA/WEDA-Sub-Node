using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Schema;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Core.Schema;

namespace Weda.SubNode.Core.Transforms;

/// <summary>
/// Factory for creating <see cref="ITelemetryTransform"/> instances from
/// <see cref="TransformConfig"/>, with assembly scanning for self-registering
/// implementations of <see cref="IConfigurableTransform{TSelf, TParameter}"/>.
/// </summary>
/// <remarks>
/// Each discovered implementation caches a <see cref="JsonSchemaDto"/> emitted
/// from its parameter type at registration time; the cache is exposed through
/// <see cref="GetDescriptors"/> for capability upload.
/// </remarks>
public static class TransformFactory
{
    private static readonly Lazy<Dictionary<string, TransformRegistration>> _registry
        = new(BuildRegistry);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Registered transform type names, keyed case-insensitively.
    /// </summary>
    public static IReadOnlyCollection<string> RegisteredTypes => _registry.Value.Keys;

    /// <summary>
    /// Returns the descriptor list for every registered transform.
    /// Used by <c>DeviceConfigurationMappingExtensions</c> when uploading
    /// SubNode capabilities to cloud.
    /// </summary>
    public static IReadOnlyList<TransformDescriptorDto> GetDescriptors() =>
        _registry.Value.Values
            .Select(r => new TransformDescriptorDto(r.TypeName, r.Description, r.ParameterSchema))
            .ToList();

    /// <summary>
    /// Scans additional assemblies for transform implementations. Must be called
    /// before the first <see cref="CreateTransform"/> / <see cref="GetDescriptors"/>
    /// invocation.
    /// </summary>
    public static void RegisterAssemblies(params Assembly[] assemblies)
    {
        var registry = _registry.Value;
        foreach (var assembly in assemblies)
        {
            ScanAssembly(assembly, registry);
        }
    }

    public static List<ITelemetryTransform> CreateFromConfigs(List<TransformConfig> configs)
    {
        if (configs is null || configs.Count == 0)
        {
            return [];
        }

        var transforms = new List<ITelemetryTransform>();
        foreach (var config in configs.Where(c => c.Enabled))
        {
            var transform = CreateTransform(config);
            if (transform is not null)
            {
                transforms.Add(transform);
            }
        }
        return transforms;
    }

    public static ITelemetryTransform? CreateTransform(TransformConfig config)
    {
        if (!config.Enabled)
        {
            return null;
        }

        if (_registry.Value.TryGetValue(config.Type, out var registration))
        {
            return registration.Factory(config.Parameters);
        }

        throw new NotSupportedException(
            $"Transform type '{config.Type}' is not supported. " +
            $"Registered types: [{string.Join(", ", _registry.Value.Keys)}]");
    }

    private static Dictionary<string, TransformRegistration> BuildRegistry()
    {
        var registry = new Dictionary<string, TransformRegistration>(StringComparer.OrdinalIgnoreCase);
        ScanAssembly(typeof(TransformFactory).Assembly, registry);
        return registry;
    }

    private static void ScanAssembly(Assembly assembly, Dictionary<string, TransformRegistration> registry)
    {
        var configurableInterface = typeof(IConfigurableTransform<,>);

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            var iface = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == configurableInterface);
            if (iface is not null)
            {
                TryRegister(type, iface, registry);
            }
        }
    }

    private static void TryRegister(
        Type type, Type configurableIface, Dictionary<string, TransformRegistration> registry)
    {
        var paramType = configurableIface.GetGenericArguments()[1];

        var typeName = GetStaticProperty<string>(type, "TypeName");
        if (string.IsNullOrEmpty(typeName))
        {
            return;
        }

        var description = GetStaticProperty<string?>(type, "Description");

        var createMethod = type.GetMethod(
            "Create", BindingFlags.Public | BindingFlags.Static, [paramType]);
        if (createMethod is null)
        {
            return;
        }

        var schema = JsonSchemaEmitter.Emit(paramType);

        ITelemetryTransform Factory(Dictionary<string, object> dict)
        {
            var json = JsonSerializer.Serialize(dict, JsonOpts);
            var typed = JsonSerializer.Deserialize(json, paramType, JsonOpts)
                ?? throw new InvalidOperationException(
                    $"Transform '{typeName}': failed to deserialize parameters into {paramType.Name}.");

            var ctx = new ValidationContext(typed);
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(typed, ctx, results, validateAllProperties: true))
            {
                var detail = string.Join("; ", results.Select(r => r.ErrorMessage));
                throw new ValidationException(
                    $"Transform '{typeName}': parameter validation failed — {detail}");
            }

            return (ITelemetryTransform)createMethod.Invoke(null, [typed])!;
        }

        registry[typeName] = new TransformRegistration(
            TypeName: typeName,
            Description: description,
            ParameterType: paramType,
            Factory: Factory,
            ParameterSchema: schema);
    }

    private static T? GetStaticProperty<T>(Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        return prop is null ? default : (T?)prop.GetValue(null);
    }
}
