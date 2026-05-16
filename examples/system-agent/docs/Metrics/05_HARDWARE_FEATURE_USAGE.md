# Using `05_HARDWARE_FEATURE.dtdl.json` from .NET / C#

End-to-end examples for consuming [`05_HARDWARE_FEATURE.dtdl.json`](./05_HARDWARE_FEATURE.dtdl.json) from a C# application. Targets **.NET 8+** with the `DTDLParser` package.

The DTDL file ships **two** things you can leverage:

| Asset | Where it lives in the JSON | Use in C# |
|-------|---------------------------|-----------|
| 4 Telemetry definitions | `contents[]` | DTMI constants; capability-flag gating; GPIO pin-state publishing. |
| 7 reusable Enums | `schemas[]` | Allow-list validators **plus** the integer-valued `GpioPinLevel` Enum for named Low/High labels. |

Key differences vs. `01` – `04`:

- **Boolean capability flags** (`GpioIsSupported`, `WatchdogIsSupported`, `ThermalProtectionIsSupported`) — should gate other alerting. If `GpioIsSupported == false`, suppress `GpioPinState` alarms on the same device.
- **`GpioPinState` with v1.1 expansion** — same pattern as temperature `Source` / `Sources`, but the parameter names are `PinId` / `PinIds`. Resolution is by name **or** numeric index (the parser falls back to `PinIndexToName`).
- **`GpioPinLevel` Enum<integer>** — a reusable Enum schema mapping `0 → Low`, `1 → High`. Wire values stay integer; consumer dashboards can resolve the label.
- **Writing is NOT supported in v1.x** — no GPIO output, no watchdog kick, no thermal-protection reconfigure. Treat every Telemetry here as **read-only**.
- **Cross-Interface dependency** — `ThermalProtectionIsSupported == false` on a host that runs the `Temperature` sensor from the OnboardSensor Interface is a **safety-relevant** combination (no autonomous over-temperature shutdown).

---

## 1. Add the parser package

```bash
dotnet add package DTDLParser
```

```xml
<ItemGroup>
  <EmbeddedResource Include="docs/Metrics/05_HARDWARE_FEATURE.dtdl.json" LogicalName="dtdl.05_HARDWARE_FEATURE.json" />
</ItemGroup>
```

---

## 2. Strongly-typed DTMI constants

```csharp
// File: src/SystemAgent/Schema/HardwareFeatureDtmis.cs
namespace SystemAgentExample.Schema;

public static class HardwareFeatureDtmis
{
    public const string Interface =
        "dtmi:advantech:WEDA:SystemAgent:HardwareFeature;1";

    public static class Telemetries
    {
        public const string GpioIsSupported              = "dtmi:advantech:WEDA:SystemInfo:GpioIsSupported;1";
        public const string GpioPinState                 = "dtmi:advantech:WEDA:SystemInfo:GpioPinState;1";
        public const string WatchdogIsSupported          = "dtmi:advantech:WEDA:SystemInfo:WatchdogIsSupported;1";
        public const string ThermalProtectionIsSupported = "dtmi:advantech:WEDA:SystemInfo:ThermalProtectionIsSupported;1";
    }

    public static class Enums
    {
        public const string MetricType                  = "dtmi:advantech:WEDA:SystemAgent:HardwareFeature:MetricType;1";
        public const string GpioMetricName              = "dtmi:advantech:WEDA:SystemAgent:HardwareFeature:GpioMetricName;1";
        public const string WatchdogMetricName          = "dtmi:advantech:WEDA:SystemAgent:HardwareFeature:WatchdogMetricName;1";
        public const string ThermalProtectionMetricName = "dtmi:advantech:WEDA:SystemAgent:HardwareFeature:ThermalProtectionMetricName;1";
        public const string GpioPinLevel                = "dtmi:advantech:WEDA:SystemAgent:HardwareFeature:GpioPinLevel;1";
        public const string SensorGroup                 = "dtmi:advantech:WEDA:SystemAgent:HardwareFeature:SensorGroup;1";
        public const string SensorInfoSchema            = "dtmi:advantech:WEDA:SystemAgent:HardwareFeature:SensorInfoSchema;1";
    }
}
```

---

## 3. Load and parse

```csharp
using DTDLParser;
using DTDLParser.Models;
using System.Reflection;

public sealed class HardwareFeatureModel
{
    public IReadOnlyDictionary<Dtmi, DTEntityInfo> Entities { get; }
    public DTInterfaceInfo Interface { get; }

    private HardwareFeatureModel(IReadOnlyDictionary<Dtmi, DTEntityInfo> entities)
    {
        Entities = entities;
        Interface = (DTInterfaceInfo)entities[new Dtmi(HardwareFeatureDtmis.Interface)];
    }

    public static async Task<HardwareFeatureModel> LoadAsync(CancellationToken ct = default)
    {
        var asm = Assembly.GetExecutingAssembly();
        await using var stream = asm.GetManifestResourceStream(
            "dtdl.05_HARDWARE_FEATURE.json")
            ?? throw new InvalidOperationException("DTDL resource not embedded.");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);

        var parser = new ModelParser();
        var entities = await parser.ParseAsync(new[] { json }, cancellationToken: ct);
        return new HardwareFeatureModel(entities);
    }
}
```

---

## 4. Resolve the `GpioPinLevel` Enum for named labels

The reusable Enum maps `0 → "Low"`, `1 → "High"`. Pull it once at startup so dashboards / log lines can display the label instead of the raw integer:

```csharp
public sealed class GpioPinLevelResolver
{
    private readonly IReadOnlyDictionary<int, string> _levels;

    public GpioPinLevelResolver(HardwareFeatureModel model)
    {
        var dtmi = new Dtmi(HardwareFeatureDtmis.Enums.GpioPinLevel);
        var enumInfo = (DTEnumInfo)model.Entities[dtmi];

        _levels = enumInfo.EnumValues
            .Where(v => v.EnumValue is int)
            .ToDictionary(v => (int)v.EnumValue!, v => v.Name);
    }

    public string LevelOf(int wireValue) =>
        _levels.TryGetValue(wireValue, out var label) ? label : $"Unknown({wireValue})";
}

// Usage
var resolver = new GpioPinLevelResolver(model);
_logger.LogInformation("GPIO {Pin} level = {Label}", "UIO_GPIO2", resolver.LevelOf(1)); // "High"
```

---

## 5. Validate a Sensor against the DTDL

```csharp
using ErrorOr;

public sealed class HardwareFeatureSensorValidator
{
    private readonly IReadOnlySet<string> _metricTypes;
    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _metricNamesByType;
    private readonly IReadOnlySet<string> _sensorGroups;
    private readonly IReadOnlySet<string> _sensorInfoSchemas;
    private readonly IReadOnlyDictionary<(string Type, string Name), string> _expectedSchemaByMetric;

    public HardwareFeatureSensorValidator(HardwareFeatureModel model)
    {
        _metricTypes       = model.Entities.GetStringEnumValues(HardwareFeatureDtmis.Enums.MetricType);
        _sensorGroups      = model.Entities.GetStringEnumValues(HardwareFeatureDtmis.Enums.SensorGroup);
        _sensorInfoSchemas = model.Entities.GetStringEnumValues(HardwareFeatureDtmis.Enums.SensorInfoSchema);

        _metricNamesByType = new Dictionary<string, IReadOnlySet<string>>
        {
            ["gpio"]              = model.Entities.GetStringEnumValues(HardwareFeatureDtmis.Enums.GpioMetricName),
            ["watchdog"]          = model.Entities.GetStringEnumValues(HardwareFeatureDtmis.Enums.WatchdogMetricName),
            ["thermalprotection"] = model.Entities.GetStringEnumValues(HardwareFeatureDtmis.Enums.ThermalProtectionMetricName),
        };

        _expectedSchemaByMetric = new Dictionary<(string, string), string>
        {
            { ("gpio",              "isSupported"), "boolean" },
            { ("gpio",              "pinState"),    "integer" },
            { ("watchdog",          "isSupported"), "boolean" },
            { ("thermalprotection", "isSupported"), "boolean" },
        };
    }

    public ErrorOr<Success> Validate(SensorConfig sensor)
    {
        var errors = new List<Error>();
        var p = sensor.Parameters;

        if (!_metricTypes.Contains(p.MetricType))
            errors.Add(Error.Validation("Sensor.MetricType",
                $"'{p.MetricType}' not in allowed enum [{string.Join(", ", _metricTypes)}]."));

        if (_metricNamesByType.TryGetValue(p.MetricType, out var allowedNames) &&
            !allowedNames.Contains(p.MetricName))
        {
            errors.Add(Error.Validation("Sensor.MetricName",
                $"'{p.MetricName}' not valid for MetricType '{p.MetricType}'. Allowed: [{string.Join(", ", allowedNames)}]."));
        }

        // GPIO pinState specifics: at least one of (PinId, PinIds set, PinIds empty for auto-detect)
        // must be present. v1.0 requires PinId. We enforce the v1.1 contract: anything goes, but
        // both PinId AND PinIds set together is ambiguous (runtime picks PinId).
        if (p.MetricType == "gpio" && p.MetricName == "pinState")
        {
            if (!string.IsNullOrWhiteSpace(p.PinId) && (p.PinIds?.Count ?? 0) > 0)
                errors.Add(Error.Validation("Sensor.Parameters",
                    "Both 'PinId' and 'PinIds' set on gpio.pinState; runtime picks 'PinId' but the config is ambiguous."));
        }

        if (sensor.SensorGroup is not null && !_sensorGroups.Contains(sensor.SensorGroup))
            errors.Add(Error.Validation("Sensor.SensorGroup",
                $"'{sensor.SensorGroup}' not in allowed enum."));

        if (!_sensorInfoSchemas.Contains(sensor.SensorInfo.Schema))
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"'{sensor.SensorInfo.Schema}' not in allowed enum."));

        if (_expectedSchemaByMetric.TryGetValue((p.MetricType, p.MetricName), out var expected) &&
            !string.Equals(expected, sensor.SensorInfo.Schema, StringComparison.Ordinal))
        {
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"For ({p.MetricType}, {p.MetricName}) expected schema '{expected}', got '{sensor.SensorInfo.Schema}'."));
        }

        return errors.Count == 0 ? Result.Success : (ErrorOr<Success>)errors;
    }
}

// Extend SensorParameters to carry GPIO pin discriminators.
public sealed record SensorParameters(
    string MetricType,
    string MetricName,
    string? PinId = null,
    IReadOnlyList<string>? PinIds = null,
    // ... pre-existing optional parameters
    string? Source = null,
    IReadOnlyList<string>? Sources = null,
    string? MountPoint = null,
    string? Interface = null,
    IReadOnlyList<string>? Interfaces = null);
```

---

## 6. Capability-flag gating

Boolean `*IsSupported` flags are the *gates* for the corresponding feature-specific alerts:

```csharp
public sealed class HardwareFeatureGate
{
    private bool _gpioSupported = true;
    private bool _watchdogSupported = true;
    private bool _thermalProtectionSupported = true;

    public void UpdateGpio(bool supported)              => _gpioSupported = supported;
    public void UpdateWatchdog(bool supported)          => _watchdogSupported = supported;
    public void UpdateThermalProtection(bool supported) => _thermalProtectionSupported = supported;

    public bool ShouldAlertOnGpioPinState() => _gpioSupported;
    public bool ShouldAlertOnWatchdogTimeout() => _watchdogSupported;

    /// <summary>
    /// SAFETY: if thermal protection is unsupported, the host has no autonomous
    /// over-temperature response. Operators must watch the temperature dashboard live.
    /// Combine with high CPU thermal to raise a manned-watch alarm.
    /// </summary>
    public bool RequiresOperatorThermalWatch() => !_thermalProtectionSupported;
}
```

Wire up against the boolean Telemetry stream:

```csharp
public sealed class CapabilityFlagSubscriber
{
    private readonly HardwareFeatureGate _gate;
    private readonly ILogger<CapabilityFlagSubscriber> _logger;

    public CapabilityFlagSubscriber(HardwareFeatureGate gate, ILogger<CapabilityFlagSubscriber> logger)
    {
        _gate = gate;
        _logger = logger;
    }

    public void OnSample(string dtmi, bool value)
    {
        switch (dtmi)
        {
            case HardwareFeatureDtmis.Telemetries.GpioIsSupported:
                _gate.UpdateGpio(value);
                break;
            case HardwareFeatureDtmis.Telemetries.WatchdogIsSupported:
                _gate.UpdateWatchdog(value);
                if (!value) _logger.LogWarning("Watchdog unsupported -- no hardware-side timeout fail-safe.");
                break;
            case HardwareFeatureDtmis.Telemetries.ThermalProtectionIsSupported:
                _gate.UpdateThermalProtection(value);
                if (!value) _logger.LogError(
                    "Thermal protection unsupported -- SAFETY: no autonomous over-temperature shutdown. " +
                    "Operator must monitor Temperature telemetry live.");
                break;
        }
    }
}
```

---

## 7. Value validators driven by the DTDL `comment` field

```csharp
public sealed class TelemetryValueValidator
{
    private static readonly HashSet<int> AllowedPinLevels = new() { 0, 1 };

    private static readonly Dictionary<string, Func<object?, bool>> Predicates = new()
    {
        [HardwareFeatureDtmis.Telemetries.GpioIsSupported]              = v => v is bool,
        [HardwareFeatureDtmis.Telemetries.GpioPinState]                 = v => v is int i && AllowedPinLevels.Contains(i),
        [HardwareFeatureDtmis.Telemetries.WatchdogIsSupported]          = v => v is bool,
        [HardwareFeatureDtmis.Telemetries.ThermalProtectionIsSupported] = v => v is bool,
    };

    public bool IsValid(string telemetryDtmi, object? value) =>
        Predicates.TryGetValue(telemetryDtmi, out var fn) && fn(value);
}
```

---

## 8. Publish a `TelemetryMeasure` — GPIO with pin expansion

`GpioPinState` follows the same expansion-by-suffix pattern as `01.network.bytes_*` (per interface) and `04.temperature` (per source):

```csharp
using Weda.SubNode.Abstractions.Telemetry;

public sealed class HardwareFeaturePublisher
{
    private readonly ICommunication _communication;
    private readonly TelemetryValueValidator _validator;
    private readonly GpioPinLevelResolver _levelResolver;
    private readonly ILogger<HardwareFeaturePublisher> _logger;

    public HardwareFeaturePublisher(
        ICommunication communication,
        TelemetryValueValidator validator,
        GpioPinLevelResolver levelResolver,
        ILogger<HardwareFeaturePublisher> logger)
    {
        _communication = communication;
        _validator     = validator;
        _levelResolver = levelResolver;
        _logger        = logger;
    }

    public Task PublishGpioSupportedAsync(bool supported, CancellationToken ct = default)
        => PublishAsync(HardwareFeatureDtmis.Telemetries.GpioIsSupported, supported, ct);

    public Task PublishGpioPinStateAsync(string pinId, int level, CancellationToken ct = default)
    {
        _logger.LogDebug("GPIO {Pin} level = {Label} ({Wire})", pinId, _levelResolver.LevelOf(level), level);
        return PublishAsync(HardwareFeatureDtmis.Telemetries.GpioPinState, level, ct, discriminator: pinId);
    }

    public Task PublishWatchdogSupportedAsync(bool supported, CancellationToken ct = default)
        => PublishAsync(HardwareFeatureDtmis.Telemetries.WatchdogIsSupported, supported, ct);

    public Task PublishThermalProtectionSupportedAsync(bool supported, CancellationToken ct = default)
        => PublishAsync(HardwareFeatureDtmis.Telemetries.ThermalProtectionIsSupported, supported, ct);

    private async Task PublishAsync(
        string dtmi, object value, CancellationToken ct, string? discriminator = null)
    {
        if (!_validator.IsValid(dtmi, value))
        {
            _logger.LogWarning("Dropping hardware-feature {Dtmi}={Value} (failed validation)", dtmi, value);
            return;
        }

        var measure = new TelemetryMeasure
        {
            ResourceId = discriminator is null ? dtmi : $"{dtmi}#{discriminator}",
            Value      = value,
        };
        await _communication.PublishAsync(measure, ct);
    }
}
```

---

## 9. Integration test

```csharp
using DTDLParser;
using Xunit;

public sealed class HardwareFeatureDtdlTests
{
    [Fact]
    public async Task Dtdl_parses_without_errors()
    {
        var json   = await File.ReadAllTextAsync("docs/Metrics/05_HARDWARE_FEATURE.dtdl.json");
        var parser = new ModelParser();
        var model  = await parser.ParseAsync(new[] { json });

        var iface = (DTInterfaceInfo)model[new Dtmi(HardwareFeatureDtmis.Interface)];
        Assert.Equal(4, iface.Contents.Values.OfType<DTTelemetryInfo>().Count());
    }

    [Theory]
    [InlineData(HardwareFeatureDtmis.Enums.MetricType,                  3)]
    [InlineData(HardwareFeatureDtmis.Enums.GpioMetricName,              2)]
    [InlineData(HardwareFeatureDtmis.Enums.WatchdogMetricName,          1)]
    [InlineData(HardwareFeatureDtmis.Enums.ThermalProtectionMetricName, 1)]
    [InlineData(HardwareFeatureDtmis.Enums.SensorGroup,                 7)]
    [InlineData(HardwareFeatureDtmis.Enums.SensorInfoSchema,            5)]
    public async Task Enum_has_expected_value_count(string enumDtmi, int expected)
    {
        var json   = await File.ReadAllTextAsync("docs/Metrics/05_HARDWARE_FEATURE.dtdl.json");
        var model  = await new ModelParser().ParseAsync(new[] { json });
        Assert.Equal(expected, model.GetStringEnumValues(enumDtmi).Count);
    }

    [Fact]
    public async Task GpioPinLevel_enum_maps_zero_to_low_and_one_to_high()
    {
        var model    = await HardwareFeatureModel.LoadAsync();
        var resolver = new GpioPinLevelResolver(model);

        Assert.Equal("Low",  resolver.LevelOf(0));
        Assert.Equal("High", resolver.LevelOf(1));
        Assert.StartsWith("Unknown(", resolver.LevelOf(2));
    }

    [Fact]
    public async Task Gpio_pinState_with_both_pinId_and_pinIds_is_flagged()
    {
        var model  = await HardwareFeatureModel.LoadAsync();
        var sut    = new HardwareFeatureSensorValidator(model);

        var sensor = new SensorConfig("gpio_pinState", "DI",
            new SensorParameters("gpio", "pinState",
                PinId: "UIO_GPIO2",
                PinIds: new[] { "DI_0", "DI_1" }),
            new SensorReport(true, 6000),
            new SensorInfo("integer", "x", "Pin State"));

        Assert.True(sut.Validate(sensor).IsError);
    }
}
```

---

## 10. End-to-end sketch — full agent with all five DTDL files

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// All five DTDL interfaces share one parser pass.
builder.Services.AddSingleton<IReadOnlyDictionary<Dtmi, DTEntityInfo>>(_ =>
{
    var jsons = new[]
    {
        File.ReadAllText("docs/Metrics/01_CPU_NETWORK.dtdl.json"),
        File.ReadAllText("docs/Metrics/02_MEMORY_DISK_SYSTEM_GPU.dtdl.json"),
        File.ReadAllText("docs/Metrics/03_HARDWARE_INFO.dtdl.json"),
        File.ReadAllText("docs/Metrics/04_ONBOARD_SENSOR.dtdl.json"),
        File.ReadAllText("docs/Metrics/05_HARDWARE_FEATURE.dtdl.json"),
    };
    return new ModelParser().ParseAsync(jsons).GetAwaiter().GetResult();
});

// Per-Interface model wrappers.
builder.Services.AddSingleton<CpuNetworkModel>();
builder.Services.AddSingleton<MemoryDiskSystemGpuModel>();
builder.Services.AddSingleton<HardwareInfoModel>();
builder.Services.AddSingleton<OnboardSensorModel>();
builder.Services.AddSingleton<HardwareFeatureModel>();

// Per-Interface validators + publishers + gates.
builder.Services.AddSingleton<CpuNetworkSensorValidator>();
builder.Services.AddSingleton<MemoryDiskSystemGpuSensorValidator>();
builder.Services.AddSingleton<HardwareInfoSensorValidator>();
builder.Services.AddSingleton<OnboardSensorValidator>();
builder.Services.AddSingleton<HardwareFeatureSensorValidator>();

builder.Services.AddSingleton<TelemetryValueValidator>();
builder.Services.AddSingleton<MonotonicCounterValidator>();
builder.Services.AddSingleton<HardwareFeatureGate>();
builder.Services.AddSingleton<GpioPinLevelResolver>();
builder.Services.AddSingleton<FanHealthHeuristic>();
builder.Services.AddSingleton<HardwareIdentityTracker>();

builder.Services.AddSingleton<CpuNetworkTelemetryPublisher>();
builder.Services.AddSingleton<MemoryDiskSystemGpuPublisher>();
builder.Services.AddSingleton<HardwareInfoPublisher>();
builder.Services.AddSingleton<OnboardSensorPublisher>();
builder.Services.AddSingleton<HardwareFeaturePublisher>();

// Config-time validator that runs every Sensor through the right per-Interface validator.
builder.Services.AddSingleton<DeviceConfigLoader>();

var host = builder.Build();

host.Services
    .GetRequiredService<DeviceConfigLoader>()
    .LoadAndValidate("/opt/system-agent/devicecfg.json");

await host.RunAsync();
```

---

## Related

- [`05_HARDWARE_FEATURE_FIELDS.md`](./05_HARDWARE_FEATURE_FIELDS.md) — the configuration field reference this code consumes.
- [`05_HARDWARE_FEATURE.dtdl.json`](./05_HARDWARE_FEATURE.dtdl.json) — the source-of-truth DTDL Interface.
- [`04_ONBOARD_SENSOR_USAGE.md`](./04_ONBOARD_SENSOR_USAGE.md) — pair `ThermalProtectionIsSupported == false` with active `Temperature` sensors and require manned thermal monitoring.
- [`01_CPU_NETWORK_USAGE.md`](./01_CPU_NETWORK_USAGE.md), [`02_MEMORY_DISK_SYSTEM_GPU_USAGE.md`](./02_MEMORY_DISK_SYSTEM_GPU_USAGE.md), [`03_HARDWARE_INFO_USAGE.md`](./03_HARDWARE_INFO_USAGE.md) — equivalent guides for the other metric categories.
