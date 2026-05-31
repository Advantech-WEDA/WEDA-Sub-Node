using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Weda.Dtdl.Emit;

using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Telemetry;

/// <summary>
/// SDK-wide catalog of strongly-typed sensor types declared via
/// <see cref="IConfigurableSensor{TParameter}"/>. Two responsibilities:
/// <list type="number">
///   <item>Scan assemblies at startup; for each implementation emit a DTDL v3
///         Interface (<c>extends Sensor:base</c>) carrying a single
///         <c>Parameters</c> Property bound to the POCO. Indexed by
///         <c>DeviceTypeName</c> (so N sensor types per device are supported).</item>
///   <item>At config-load time, resolve a sensor instance's typed dtmi via
///         <c>try-validate-each</c> — bind <see cref="Sensor.Parameters"/> to each
///         registered POCO under the parent <c>DeviceTypeName</c>; pick the unique
///         POCO that validates. Discriminator is encoded in the POCO itself
///         (single-value enum on a key field, distinct required fields, etc.) so
///         devicecfg.json needs no new fields.</item>
/// </list>
///
/// <para>Throws on duplicate <c>(DeviceTypeName, SensorTypeName)</c> registration
/// and on ambiguous / zero matches at resolve time, surfacing POCO authoring
/// mistakes at startup rather than at first telemetry tick.</para>
/// </summary>
public static class SensorTypeRegistry
{
    private static readonly Lazy<Dictionary<string, List<SensorTypeRegistration>>> _byDevice
        = new(BuildRegistry);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        // IConfiguration binds devicecfg.json scalars as strings, so numeric
        // POCO fields (ushort RegisterAddress, int interval, ...) need to
        // accept "0" / "5000" strings rather than rejecting them as wrong type.
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() },
    };

    public sealed record SensorTypeRegistration(
        string DeviceTypeName,
        string SensorTypeName,
        string Dtmi,
        Type ParametersType,
        string? Description,
        JsonObject Schema,
        Func<IReadOnlyDictionary<string, object>?, bool> TryValidate);

    /// <summary>
    /// Flat list of every registered sensor type — used by
    /// <c>DeviceConfigurationMappingExtensions</c> to populate
    /// <c>DeviceCapDto.sensorTypes[]</c> and to add the type Interfaces (plus
    /// <c>Sensor:base</c>) to <c>dtdl[]</c>.
    /// </summary>
    public static IReadOnlyList<SensorTypeRegistration> All =>
        _byDevice.Value.Values.SelectMany(x => x).ToList();

    /// <summary>
    /// True when at least one sensor type is registered under the given device
    /// type. The mapping layer uses this to know whether to add
    /// <see cref="SensorBase"/> Interface to dtdl[].
    /// </summary>
    public static bool HasAny => _byDevice.Value.Count > 0;

    /// <summary>
    /// Returns the sensor type registration matching this instance's
    /// <c>Parameters</c> under the given <paramref name="deviceTypeName"/>.
    /// Throws when the device type has no registrations, or when zero / multiple
    /// candidates validate (the latter signals a POCO discriminator authoring
    /// bug — fix by tightening Parameters enums or adding distinct required fields).
    /// </summary>
    public static SensorTypeRegistration Resolve(string deviceTypeName, Sensor sensor)
    {
        if (!_byDevice.Value.TryGetValue(deviceTypeName, out var candidates) || candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No IConfigurableSensor registered for device type '{deviceTypeName}'. " +
                $"Register one via assembly scan (or check that the device class's DeviceTypeName matches its sensor's).");
        }

        var matched = candidates.Where(c => c.TryValidate(sensor.Parameters)).ToList();

        return matched.Count switch
        {
            1 => matched[0],
            0 => throw new InvalidOperationException(
                $"Sensor '{sensor.Name}' Parameters {ShapeOf(sensor.Parameters)} do not match any registered " +
                $"sensor type for device '{deviceTypeName}'. Candidates tried: " +
                $"[{string.Join(", ", candidates.Select(c => c.SensorTypeName))}]."),
            _ => throw new InvalidOperationException(
                $"Sensor '{sensor.Name}' Parameters {ShapeOf(sensor.Parameters)} ambiguously match multiple " +
                $"sensor types for device '{deviceTypeName}': " +
                $"[{string.Join(", ", matched.Select(m => m.SensorTypeName))}]. " +
                $"Disambiguate the POCOs with single-value enums or distinct required fields."),
        };
    }

    /// <summary>
    /// Add additional assemblies (e.g. an example's own assembly that ships
    /// <c>IConfigurableSensor</c> implementations) to the registry. Must be
    /// called before the first read of <see cref="All"/> / <see cref="Resolve"/>
    /// — once the lazy is materialised, only the initial SDK scan + assemblies
    /// registered here are visible.
    /// </summary>
    public static void RegisterAssemblies(params Assembly[] assemblies)
    {
        var registry = _byDevice.Value;
        foreach (var asm in assemblies) ScanAssembly(asm, registry);
    }

    private static Dictionary<string, List<SensorTypeRegistration>> BuildRegistry()
    {
        var registry = new Dictionary<string, List<SensorTypeRegistration>>(StringComparer.OrdinalIgnoreCase);
        ScanAssembly(typeof(SensorTypeRegistry).Assembly, registry);

        // Install Abstractions-side hook so DeviceConfiguration.InitializeDtdl
        // can take the typed dispatch path without depending on Core.
        TypedSensorDispatch.Resolve = (deviceTypeName, sensor) =>
            Resolve(deviceTypeName, sensor).Dtmi;

        return registry;
    }

    private static void ScanAssembly(
        Assembly assembly, Dictionary<string, List<SensorTypeRegistration>> registry)
    {
        var ifaceDef = typeof(IConfigurableSensor<>);
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
        Type type, Type configurableIface, Dictionary<string, List<SensorTypeRegistration>> registry)
    {
        var paramType = configurableIface.GetGenericArguments()[0];
        var deviceTypeName = GetStaticProperty<string>(type, "DeviceTypeName");
        var sensorTypeName = GetStaticProperty<string>(type, "SensorTypeName");
        var description    = GetStaticProperty<string?>(type, "Description");

        if (string.IsNullOrEmpty(deviceTypeName) || string.IsNullOrEmpty(sensorTypeName))
        {
            return;
        }

        if (registry.TryGetValue(deviceTypeName, out var existing) &&
            existing.Any(r => string.Equals(r.SensorTypeName, sensorTypeName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Duplicate IConfigurableSensor registration: device '{deviceTypeName}' " +
                $"sensor '{sensorTypeName}' is implemented by multiple classes (latest: {type.FullName}).");
        }

        // Emit DTDL: extends Sensor:base + one Property "Parameters" bound to paramType.
        var schema = WedaDtdlEmitter.Emit(
            new WedaDtdlEmitter.Options(
                Prefix: "dtmi:advantech:weda",
                Category: "sensor",
                TypeName: $"{deviceTypeName}.{sensorTypeName}",  // Sanitize collapses .  / - to _
                DisplayName: sensorTypeName,
                Description: description,
                Extends: new[] { SensorBase.Dtmi }),
            new WedaDtdlEmitter.PropertyBinding("Parameters", paramType));

        var dtmi = (string)schema["@id"]!;

        Func<IReadOnlyDictionary<string, object>?, bool> tryValidate = source =>
        {
            try
            {
                var json = JsonSerializer.Serialize(
                    source ?? (IReadOnlyDictionary<string, object>)new Dictionary<string, object>(),
                    JsonOpts);
                var typed = JsonSerializer.Deserialize(json, paramType, JsonOpts);
                if (typed is null) return false;
                var ctx = new ValidationContext(typed);
                return Validator.TryValidateObject(typed, ctx, [], validateAllProperties: true);
            }
            catch
            {
                return false;
            }
        };

        var reg = new SensorTypeRegistration(
            DeviceTypeName: deviceTypeName,
            SensorTypeName: sensorTypeName,
            Dtmi: dtmi,
            ParametersType: paramType,
            Description: description,
            Schema: schema,
            TryValidate: tryValidate);

        if (!registry.TryGetValue(deviceTypeName, out var list))
        {
            list = new List<SensorTypeRegistration>();
            registry[deviceTypeName] = list;
        }
        list.Add(reg);
    }

    private static T? GetStaticProperty<T>(Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        if (prop is null) return default;
        var value = prop.GetValue(null);
        return value is T t ? t : default;
    }

    private static string ShapeOf(IReadOnlyDictionary<string, object>? dict) =>
        dict is null ? "{}" : "{" + string.Join(", ", dict.Keys.OrderBy(k => k)) + "}";
}
