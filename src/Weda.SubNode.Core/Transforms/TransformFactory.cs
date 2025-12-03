using System.Reflection;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace Weda.SubNode.Core.Transforms;

/// <summary>
/// Transform factory delegate type.
/// </summary>
public delegate ITelemetryTransform TransformFactoryDelegate(Dictionary<string, object> parameters);

/// <summary>
/// Factory for creating ITelemetryTransform instances from TransformConfig.
/// Automatically discovers and registers all ITelemetryTransform implementations
/// using static abstract interface members.
/// </summary>
public static class TransformFactory
{
    /// <summary>
    /// Lazy-initialized registry of transform factories keyed by type name (case-insensitive).
    /// </summary>
    private static readonly Lazy<Dictionary<string, TransformFactoryDelegate>> _registry
        = new(BuildRegistry);

    /// <summary>
    /// Gets the registered transform type names (for diagnostics/debugging).
    /// </summary>
    public static IReadOnlyCollection<string> RegisteredTypes => _registry.Value.Keys;

    /// <summary>
    /// Builds the registry by scanning assemblies for ITelemetryTransform implementations.
    /// </summary>
    private static Dictionary<string, TransformFactoryDelegate> BuildRegistry()
    {
        var registry = new Dictionary<string, TransformFactoryDelegate>(StringComparer.OrdinalIgnoreCase);

        // Scan assemblies for ITelemetryTransform implementations
        var assemblies = new[]
        {
            typeof(TransformFactory).Assembly, // Weda.SubNode.Core
        };

        foreach (var assembly in assemblies)
        {
            ScanAssembly(assembly, registry);
        }

        return registry;
    }

    /// <summary>
    /// Scans an assembly for IConfigurableTransform implementations and registers them.
    /// </summary>
    private static void ScanAssembly(Assembly assembly, Dictionary<string, TransformFactoryDelegate> registry)
    {
        var configurableInterface = typeof(IConfigurableTransform<>);

        foreach (var type in assembly.GetTypes())
        {
            // Skip abstract classes and interfaces
            if (type.IsAbstract || type.IsInterface)
                continue;

            // Check if implements IConfigurableTransform<TSelf>
            var implementsConfigurable = type.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == configurableInterface);

            if (!implementsConfigurable)
                continue;

            // Get static TypeName property
            var typeNameProp = type.GetProperty("TypeName",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (typeNameProp == null)
                continue;

            var typeName = (string?)typeNameProp.GetValue(null);
            if (string.IsNullOrEmpty(typeName))
                continue;

            // Get static Create method
            var createMethod = type.GetMethod("Create",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
                [typeof(Dictionary<string, object>)]);
            if (createMethod == null)
                continue;

            // Register factory delegate
            registry[typeName] = parameters =>
                (ITelemetryTransform)createMethod.Invoke(null, [parameters])!;
        }
    }

    /// <summary>
    /// Registers additional assemblies for transform discovery.
    /// Call this before first use of CreateTransform/CreateFromConfigs.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan for transforms</param>
    public static void RegisterAssemblies(params Assembly[] assemblies)
    {
        // Force initialization if not already done
        var registry = _registry.Value;

        foreach (var assembly in assemblies)
        {
            ScanAssembly(assembly, registry);
        }
    }

    /// <summary>
    /// Creates a list of transforms from configuration.
    /// Execution order is determined by the array index in the configuration.
    /// </summary>
    /// <param name="configs">Transform configurations</param>
    /// <returns>List of instantiated transforms</returns>
    public static List<ITelemetryTransform> CreateFromConfigs(List<TransformConfig> configs)
    {
        if (configs == null || configs.Count == 0)
            return [];

        var transforms = new List<ITelemetryTransform>();

        // Process in array order (index 0, 1, 2, ...) - no sorting
        // Only filter out disabled transforms
        foreach (var config in configs.Where(c => c.Enabled))
        {
            var transform = CreateTransform(config);
            if (transform != null)
            {
                transforms.Add(transform);
            }
        }

        return transforms;
    }

    /// <summary>
    /// Creates a single transform from configuration.
    /// </summary>
    /// <param name="config">Transform configuration</param>
    /// <returns>Transform instance or null if disabled</returns>
    /// <exception cref="NotSupportedException">Thrown when transform type is not registered</exception>
    public static ITelemetryTransform? CreateTransform(TransformConfig config)
    {
        if (!config.Enabled)
            return null;

        var typeName = config.Type;

        if (_registry.Value.TryGetValue(typeName, out var factory))
        {
            return factory(config.Parameters);
        }

        throw new NotSupportedException(
            $"Transform type '{config.Type}' is not supported. " +
            $"Registered types: [{string.Join(", ", _registry.Value.Keys)}]");
    }
}