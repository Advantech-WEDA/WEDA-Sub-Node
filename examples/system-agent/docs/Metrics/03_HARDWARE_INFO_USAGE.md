# Using `03_HARDWARE_INFO.dtdl.json` from .NET / C#

End-to-end examples for consuming [`03_HARDWARE_INFO.dtdl.json`](./03_HARDWARE_INFO.dtdl.json) from a C# application. Targets **.NET 8+** with the `DTDLParser` package. Same overall shape as [`01_SYS_RES_CPU_NETWORK_USAGE.md`](./01_SYS_RES_CPU_NETWORK_USAGE.md) and [`01_SYS_RES_MEMORY_DISK_USAGE.md`](./01_SYS_RES_MEMORY_DISK_USAGE.md) — read either of those first if you haven't.

The DTDL file ships **two** things you can leverage:

| Asset | Where it lives in the JSON | Use in C# |
|-------|---------------------------|-----------|
| 6 Telemetry definitions | `contents[]` | Strongly-typed DTMI constants; identity-change detection per field. |
| 4 reusable Enums | `schemas[]` | Allow-list validators for `Sensor.Parameters.MetricType`, `HwinfoMetricName`, `SensorGroup`, `SensorInfoSchema`. |

Key differences vs. `01` / `02`:

- **All telemetries are `string`** — no numeric ranges, no monotonic counters. Validation is **identity-drift detection** (the value should be constant per boot; a change implies a BIOS / driver update or hardware swap).
- **No type-specific parameters** — `hwinfo` sensors have no `MountPoint`, no `Interface`, no `Source`. Validation is the simplest of any metric type.
- **Long polling interval recommended** — `Report.Interval` of `60000` ms is typical. Values are constant per boot; faster polling adds cost without value.
- **Platform-dependent** — on hosts without the platform driver, every Telemetry is suppressed (null). The agent does **not** publish an empty string.

---

## 1. Add the parser package

Skip if already added for `01` / `02`.

```bash
dotnet add package DTDLParser
```

```xml
<ItemGroup>
  <EmbeddedResource Include="docs/Metrics/03_HARDWARE_INFO.dtdl.json" LogicalName="dtdl.03_HARDWARE_INFO.json" />
</ItemGroup>
```

---

## 2. Strongly-typed DTMI constants

```csharp
// File: src/SystemAgent/Schema/HardwareInfoDtmis.cs
namespace SystemAgentExample.Schema;

public static class HardwareInfoDtmis
{
    public const string Interface =
        "dtmi:advantech:EdgeSync:SystemAgent:HardwareInfo;1";

    public static class Telemetries
    {
        public const string HwinfoMotherboardName = "dtmi:advantech:EdgeSync:SystemInfo:HwinfoMotherboardName;1";
        public const string HwinfoManufacturer    = "dtmi:advantech:EdgeSync:SystemInfo:HwinfoManufacturer;1";
        public const string HwinfoBiosRevision    = "dtmi:advantech:EdgeSync:SystemInfo:HwinfoBiosRevision;1";
        public const string HwinfoDriverVersion   = "dtmi:advantech:EdgeSync:SystemInfo:HwinfoDriverVersion;1";
        public const string HwinfoLibraryVersion  = "dtmi:advantech:EdgeSync:SystemInfo:HwinfoLibraryVersion;1";
        public const string HwinfoEcRevision      = "dtmi:advantech:EdgeSync:SystemInfo:HwinfoEcRevision;1";
    }

    public static class Enums
    {
        public const string MetricType        = "dtmi:advantech:EdgeSync:SystemAgent:HardwareInfo:MetricType;1";
        public const string HwinfoMetricName  = "dtmi:advantech:EdgeSync:SystemAgent:HardwareInfo:HwinfoMetricName;1";
        public const string SensorGroup       = "dtmi:advantech:EdgeSync:SystemAgent:HardwareInfo:SensorGroup;1";
        public const string SensorInfoSchema  = "dtmi:advantech:EdgeSync:SystemAgent:HardwareInfo:SensorInfoSchema;1";
    }
}
```

---

## 3. Load and parse

```csharp
using DTDLParser;
using DTDLParser.Models;
using System.Reflection;

public sealed class HardwareInfoModel
{
    public IReadOnlyDictionary<Dtmi, DTEntityInfo> Entities { get; }
    public DTInterfaceInfo Interface { get; }

    private HardwareInfoModel(IReadOnlyDictionary<Dtmi, DTEntityInfo> entities)
    {
        Entities = entities;
        Interface = (DTInterfaceInfo)entities[new Dtmi(HardwareInfoDtmis.Interface)];
    }

    public static async Task<HardwareInfoModel> LoadAsync(CancellationToken ct = default)
    {
        var asm = Assembly.GetExecutingAssembly();
        await using var stream = asm.GetManifestResourceStream(
            "dtdl.03_HARDWARE_INFO.json")
            ?? throw new InvalidOperationException("DTDL resource not embedded.");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);

        var parser = new ModelParser();
        var entities = await parser.ParseAsync(new[] { json }, cancellationToken: ct);
        return new HardwareInfoModel(entities);
    }
}
```

---

## 4. Validate a Sensor against the DTDL

Hardware-info sensors are the simplest to validate — only four enum checks plus the per-`MetricName` schema mapping (always `string`).

```csharp
using ErrorOr;

public sealed class HardwareInfoSensorValidator
{
    private readonly IReadOnlySet<string> _metricTypes;
    private readonly IReadOnlySet<string> _hwinfoMetricNames;
    private readonly IReadOnlySet<string> _sensorGroups;
    private readonly IReadOnlySet<string> _sensorInfoSchemas;

    public HardwareInfoSensorValidator(HardwareInfoModel model)
    {
        _metricTypes       = model.Entities.GetStringEnumValues(HardwareInfoDtmis.Enums.MetricType);
        _hwinfoMetricNames = model.Entities.GetStringEnumValues(HardwareInfoDtmis.Enums.HwinfoMetricName);
        _sensorGroups      = model.Entities.GetStringEnumValues(HardwareInfoDtmis.Enums.SensorGroup);
        _sensorInfoSchemas = model.Entities.GetStringEnumValues(HardwareInfoDtmis.Enums.SensorInfoSchema);
    }

    public ErrorOr<Success> Validate(SensorConfig sensor)
    {
        var errors = new List<Error>();
        var mt = sensor.Parameters.MetricType;
        var mn = sensor.Parameters.MetricName;

        if (!_metricTypes.Contains(mt))
            errors.Add(Error.Validation("Sensor.MetricType",
                $"'{mt}' not in allowed enum [{string.Join(", ", _metricTypes)}]."));

        if (mt == "hwinfo" && !_hwinfoMetricNames.Contains(mn))
            errors.Add(Error.Validation("Sensor.MetricName",
                $"'{mn}' not valid for MetricType 'hwinfo'. Allowed: [{string.Join(", ", _hwinfoMetricNames)}]."));

        if (sensor.SensorGroup is not null && !_sensorGroups.Contains(sensor.SensorGroup))
            errors.Add(Error.Validation("Sensor.SensorGroup",
                $"'{sensor.SensorGroup}' not in allowed enum."));

        if (!_sensorInfoSchemas.Contains(sensor.SensorInfo.Schema))
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"'{sensor.SensorInfo.Schema}' not in allowed enum."));

        // Every hwinfo metric is 'string'.
        if (mt == "hwinfo" && sensor.SensorInfo.Schema != "string")
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"For (hwinfo, *) expected schema 'string', got '{sensor.SensorInfo.Schema}'."));

        return errors.Count == 0 ? Result.Success : (ErrorOr<Success>)errors;
    }
}
```

---

## 5. Identity-drift detection

Hardware-info values should be **stable per boot**. The "validation rule" for these telemetries is therefore not a range check but a *change-detection* policy: log every change, optionally page on changes that imply a security-relevant event (BIOS update, motherboard swap).

```csharp
public sealed class HardwareIdentityTracker
{
    private readonly Dictionary<string, string?> _lastValueByDtmi = new();
    private readonly ILogger<HardwareIdentityTracker> _logger;

    public HardwareIdentityTracker(ILogger<HardwareIdentityTracker> logger) => _logger = logger;

    /// <summary>
    /// Record a hwinfo sample and emit an audit-log entry if the value changed
    /// (or transitioned to/from null). Returns true when the value is unchanged.
    /// </summary>
    public bool Observe(string dtmi, string? value)
    {
        var hadPrior = _lastValueByDtmi.TryGetValue(dtmi, out var prior);
        _lastValueByDtmi[dtmi] = value;

        if (!hadPrior)
        {
            _logger.LogInformation("{Dtmi} first observation: '{Value}'", dtmi, value ?? "<null>");
            return true; // first observation is not a change
        }

        if (!string.Equals(prior, value, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "{Dtmi} identity changed: '{Prior}' -> '{New}'. " +
                "Likely BIOS update, driver reload, or hardware swap -- audit.",
                dtmi, prior ?? "<null>", value ?? "<null>");
            return false;
        }
        return true;
    }
}
```

> Non-empty / ASCII / constant-per-boot value rules for the hwinfo telemetries are documented in `03_HARDWARE_INFO_FIELDS.md` and enforced ingest-side. The identity-drift tracker above is the only stateful check applied agent-side.

---

## 6. Integration test

```csharp
using DTDLParser;
using Xunit;

public sealed class HardwareInfoDtdlTests
{
    [Fact]
    public async Task Dtdl_parses_and_declares_no_telemetries()
    {
        var json   = await File.ReadAllTextAsync("docs/Metrics/03_HARDWARE_INFO.dtdl.json");
        var parser = new ModelParser();
        var model  = await parser.ParseAsync(new[] { json });

        var iface = (DTInterfaceInfo)model[new Dtmi(HardwareInfoDtmis.Interface)];
        Assert.Empty(iface.Contents.Values.OfType<DTTelemetryInfo>());
    }

    [Theory]
    [InlineData(HardwareInfoDtmis.Enums.MetricType,        1)]
    [InlineData(HardwareInfoDtmis.Enums.HwinfoMetricName,  6)]
    [InlineData(HardwareInfoDtmis.Enums.SensorGroup,       7)]
    [InlineData(HardwareInfoDtmis.Enums.SensorInfoSchema,  5)]
    public async Task Enum_has_expected_value_count(string enumDtmi, int expected)
    {
        var json   = await File.ReadAllTextAsync("docs/Metrics/03_HARDWARE_INFO.dtdl.json");
        var model  = await new ModelParser().ParseAsync(new[] { json });
        Assert.Equal(expected, model.GetStringEnumValues(enumDtmi).Count);
    }

    [Fact]
    public async Task Identity_change_is_flagged()
    {
        var logger  = new TestLogger<HardwareIdentityTracker>();
        var tracker = new HardwareIdentityTracker(logger);

        tracker.Observe(HardwareInfoDtmis.Telemetries.HwinfoBiosRevision, "V1.05");
        var unchanged = tracker.Observe(HardwareInfoDtmis.Telemetries.HwinfoBiosRevision, "V1.05");
        var changed   = tracker.Observe(HardwareInfoDtmis.Telemetries.HwinfoBiosRevision, "V1.06");

        Assert.True(unchanged);
        Assert.False(changed);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("identity changed"));
    }
}
```

---

## 7. End-to-end sketch

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(_ => HardwareInfoModel.LoadAsync().GetAwaiter().GetResult());
builder.Services.AddSingleton<HardwareInfoSensorValidator>();
builder.Services.AddSingleton<HardwareIdentityTracker>();

var host = builder.Build();
await host.RunAsync();
```

Co-load with the other DTDL files so one `ModelParser` invocation covers the whole agent:

```csharp
var dtdls = await Task.WhenAll(
    File.ReadAllTextAsync("docs/Metrics/01_CPU_NETWORK.dtdl.json"),
    File.ReadAllTextAsync("docs/Metrics/02_MEMORY_DISK_SYSTEM_GPU.dtdl.json"),
    File.ReadAllTextAsync("docs/Metrics/03_HARDWARE_INFO.dtdl.json"));
var entities = await new ModelParser().ParseAsync(dtdls);
```

---

## Related

- [`03_HARDWARE_INFO_FIELDS.md`](./03_HARDWARE_INFO_FIELDS.md) — the configuration field reference this code consumes.
- [`03_HARDWARE_INFO.dtdl.json`](./03_HARDWARE_INFO.dtdl.json) — the source-of-truth DTDL Interface.
- [`01_SYS_RES_CPU_NETWORK_USAGE.md`](./01_SYS_RES_CPU_NETWORK_USAGE.md), [`01_SYS_RES_MEMORY_DISK_USAGE.md`](./01_SYS_RES_MEMORY_DISK_USAGE.md) — the equivalent guides for the other metric categories; share the `GetStringEnumValues` helper.
