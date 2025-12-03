using System.Reflection;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Dsp;

/// <summary>
/// DSP filter factory delegate type.
/// </summary>
public delegate IDspFilter DspFilterFactoryDelegate(Dictionary<string, object> parameters);

/// <summary>
/// Factory for creating IDspFilter instances from DspFilterConfig.
/// Automatically discovers and registers all IConfigurableDspFilter implementations
/// using static abstract interface members.
/// </summary>
public static class DspFilterFactory
{
    /// <summary>
    /// Lazy-initialized registry of filter factories keyed by type name (case-insensitive).
    /// </summary>
    private static readonly Lazy<Dictionary<string, DspFilterFactoryDelegate>> _registry
        = new(BuildRegistry);

    /// <summary>
    /// Gets the registered filter type names (for diagnostics/debugging).
    /// </summary>
    public static IReadOnlyCollection<string> RegisteredTypes => _registry.Value.Keys;

    /// <summary>
    /// Builds the registry by scanning assemblies for IConfigurableDspFilter implementations.
    /// </summary>
    private static Dictionary<string, DspFilterFactoryDelegate> BuildRegistry()
    {
        var registry = new Dictionary<string, DspFilterFactoryDelegate>(StringComparer.OrdinalIgnoreCase);

        // Scan assemblies for IConfigurableDspFilter implementations
        var assemblies = new[]
        {
            typeof(DspFilterFactory).Assembly, // Weda.SubNode.Core
        };

        foreach (var assembly in assemblies)
        {
            ScanAssembly(assembly, registry);
        }

        return registry;
    }

    /// <summary>
    /// Scans an assembly for IConfigurableDspFilter implementations and registers them.
    /// </summary>
    private static void ScanAssembly(Assembly assembly, Dictionary<string, DspFilterFactoryDelegate> registry)
    {
        var configurableInterface = typeof(IConfigurableDspFilter<>);

        foreach (var type in assembly.GetTypes())
        {
            // Skip abstract classes and interfaces
            if (type.IsAbstract || type.IsInterface)
                continue;

            // Check if implements IConfigurableDspFilter<TSelf>
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
                (IDspFilter)createMethod.Invoke(null, [parameters])!;
        }
    }

    /// <summary>
    /// Registers additional assemblies for filter discovery.
    /// Call this before first use of CreateFilter/CreateFromConfigs.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan for filters</param>
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
    /// Creates a list of DSP filters from configuration.
    /// Execution order is determined by the array index in the configuration.
    /// </summary>
    /// <param name="configs">DSP filter configurations</param>
    /// <returns>List of instantiated filters</returns>
    public static List<IDspFilter> CreateFromConfigs(List<DspFilterConfig> configs)
    {
        if (configs == null || configs.Count == 0)
            return [];

        var filters = new List<IDspFilter>();

        // Process in array order (index 0, 1, 2, ...) - no sorting
        // Only filter out disabled filters
        foreach (var config in configs.Where(c => c.Enabled))
        {
            var filter = CreateFilter(config);
            if (filter != null)
            {
                filters.Add(filter);
            }
        }

        return filters;
    }

    /// <summary>
    /// Creates a single DSP filter from configuration.
    /// </summary>
    /// <param name="config">DSP filter configuration</param>
    /// <returns>Filter instance or null if disabled</returns>
    /// <exception cref="NotSupportedException">Thrown when filter type is not registered</exception>
    public static IDspFilter? CreateFilter(DspFilterConfig config)
    {
        if (!config.Enabled)
            return null;

        var typeName = config.Type;

        if (_registry.Value.TryGetValue(typeName, out var factory))
        {
            return factory(config.Parameters);
        }

        throw new NotSupportedException(
            $"DSP filter type '{config.Type}' is not supported. " +
            $"Registered types: [{string.Join(", ", _registry.Value.Keys)}]");
    }
}