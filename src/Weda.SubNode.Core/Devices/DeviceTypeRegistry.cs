using System.Reflection;
using System.Text.Json.Nodes;

using Weda.Dtdl.Emit;

using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// SDK-wide catalog of strongly-typed device types declared via
/// <see cref="IConfigurableDevice{TCommunication, TProperties}"/>. Scans assemblies
/// at startup and for each implementation emits a DTDL v3 Interface carrying two
/// top-level Properties — <c>Communication</c> (transport-layer settings, e.g.
/// Host / Port) and <c>Properties</c> (protocol-specific settings, e.g. SlaveId /
/// ByteOrder). Indexed by <c>DeviceTypeName</c> for catalog upload.
///
/// <para>Unlike <c>SensorTypeRegistry</c>, no runtime <c>Resolve</c> is needed
/// for instances: a device instance's type is established at config-load time via
/// <c>AddDevice&lt;TDevice&gt;("sectionName")</c> registrations, propagated to
/// <c>DeviceConfiguration.DeviceTypeName</c> by the host loader.</para>
/// </summary>
public static class DeviceTypeRegistry
{
    private static readonly Lazy<Dictionary<string, DeviceTypeRegistration>> _byTypeName
        = new(BuildRegistry);

    public sealed record DeviceTypeRegistration(
        string DeviceTypeName,
        string Dtmi,
        Type CommunicationType,
        Type PropertiesType,
        string? Description,
        JsonObject Schema);

    /// <summary>
    /// Flat list of every registered device type — used by
    /// <c>DeviceConfigurationMappingExtensions</c> to populate
    /// <c>DeviceCapDto.devices[]</c> and to add each device-type Interface to
    /// <c>dtdl[]</c>.
    /// </summary>
    public static IReadOnlyList<DeviceTypeRegistration> All =>
        _byTypeName.Value.Values.ToList();

    /// <summary>
    /// Lookup by <c>DeviceTypeName</c>. Returns null when no
    /// <see cref="IConfigurableDevice{TComm, TProps}"/> is registered for that name.
    /// </summary>
    public static DeviceTypeRegistration? Get(string deviceTypeName) =>
        _byTypeName.Value.TryGetValue(deviceTypeName, out var reg) ? reg : null;

    /// <summary>
    /// Add additional assemblies to the registry. Must be called before the
    /// first read of <see cref="All"/> / <see cref="Get"/>.
    /// </summary>
    public static void RegisterAssemblies(params Assembly[] assemblies)
    {
        var registry = _byTypeName.Value;
        foreach (var asm in assemblies) ScanAssembly(asm, registry);
    }

    private static Dictionary<string, DeviceTypeRegistration> BuildRegistry()
    {
        var registry = new Dictionary<string, DeviceTypeRegistration>(StringComparer.OrdinalIgnoreCase);
        ScanAssembly(typeof(DeviceTypeRegistry).Assembly, registry);
        return registry;
    }

    private static void ScanAssembly(
        Assembly assembly, Dictionary<string, DeviceTypeRegistration> registry)
    {
        var ifaceDef = typeof(IConfigurableDevice<,>);
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;
            var iface = type.GetInterfaces().FirstOrDefault(
                i => i.IsGenericType && i.GetGenericTypeDefinition() == ifaceDef);
            if (iface is null) continue;
            TryRegister(type, iface, registry);
        }
    }

    private static void TryRegister(
        Type type, Type configurableIface, Dictionary<string, DeviceTypeRegistration> registry)
    {
        var args = configurableIface.GetGenericArguments();
        var commType = args[0];
        var propsType = args[1];

        var deviceTypeName = GetStaticProperty<string>(type, "DeviceTypeName");
        var description    = GetStaticProperty<string?>(type, "Description");

        if (string.IsNullOrEmpty(deviceTypeName)) return;

        if (registry.ContainsKey(deviceTypeName))
        {
            throw new InvalidOperationException(
                $"Duplicate IConfigurableDevice registration: device type " +
                $"'{deviceTypeName}' is implemented by multiple classes (latest: {type.FullName}).");
        }

        var schema = WedaDtdlEmitter.Emit(
            new WedaDtdlEmitter.Options(
                Prefix: "dtmi:advantech:weda",
                Category: "device",
                TypeName: deviceTypeName,
                DisplayName: deviceTypeName,
                Description: description),
            new WedaDtdlEmitter.PropertyBinding("Communication", commType),
            new WedaDtdlEmitter.PropertyBinding("Properties",    propsType));

        var dtmi = (string)schema["@id"]!;

        registry[deviceTypeName] = new DeviceTypeRegistration(
            DeviceTypeName: deviceTypeName,
            Dtmi: dtmi,
            CommunicationType: commType,
            PropertiesType: propsType,
            Description: description,
            Schema: schema);
    }

    private static T? GetStaticProperty<T>(Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        if (prop is null) return default;
        var value = prop.GetValue(null);
        return value is T t ? t : default;
    }
}
