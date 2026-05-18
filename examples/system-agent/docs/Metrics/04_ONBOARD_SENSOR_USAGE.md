# Using `04_ONBOARD_SENSOR.dtdl.json` from .NET / C#

End-to-end examples for consuming [`04_ONBOARD_SENSOR.dtdl.json`](./04_ONBOARD_SENSOR.dtdl.json) from a C# application. Targets **.NET 8+** with the `DTDLParser` package. Read [`01_SYS_RES_CPU_NETWORK_USAGE.md`](./01_SYS_RES_CPU_NETWORK_USAGE.md) first for the parser setup pattern.

The DTDL file ships **two** things you can leverage:

| Asset | Where it lives in the JSON | Use in C# |
|-------|---------------------------|-----------|
| 3 Telemetry definitions | `contents[]` | DTMI constants; semantic-type unit handling. |
| 6 reusable Enums | `schemas[]` | Allow-list validators for `MetricType`, the three per-type `MetricName` enums, `SensorGroup`, `SensorInfoSchema`. |

Key differences vs. `01` / `02` / `03`:

- **Temperature supports v1.1 source expansion** — `Parameters.Source` (single), `Parameters.Sources` (array), or auto-detect. Same pattern as `01_CPU_NETWORK.network` with `Interface` / `Interfaces`.
- **Temperature `MetricName` is dual-mode** — v1.1 uses `"therm"` + `Source`/`Sources`; v1.0-compatible mode lets `MetricName` itself be the source name. Validator must allow `MetricName == "therm"` *or* any non-empty string.
- **Semantic types apply** — `Temperature` (`unit: "degreeCelsius"`), `Voltage` (`unit: "volt"`). Unit-aware Azure tooling auto-formats the value.
- **Platform-dependent** — null/suppressed on hosts without the platform driver. The agent never publishes `0.0` as a fallback.

---

## 1. Add the parser package

```bash
dotnet add package DTDLParser
```

```xml
<ItemGroup>
  <EmbeddedResource Include="docs/Metrics/04_ONBOARD_SENSOR.dtdl.json" LogicalName="dtdl.04_ONBOARD_SENSOR.json" />
</ItemGroup>
```

---

## 2. Strongly-typed DTMI constants

```csharp
// File: src/SystemAgent/Schema/OnboardSensorDtmis.cs
namespace SystemAgentExample.Schema;

public static class OnboardSensorDtmis
{
    public const string Interface =
        "dtmi:advantech:EdgeSync:SystemAgent:OnboardSensor;1";

    public static class Telemetries
    {
        public const string Temperature = "dtmi:advantech:EdgeSync:SystemInfo:Temperature;1";
        public const string Voltage     = "dtmi:advantech:EdgeSync:SystemInfo:Voltage;1";
        public const string FanSpeed    = "dtmi:advantech:EdgeSync:SystemInfo:FanSpeed;1";
    }

    public static class Enums
    {
        public const string MetricType             = "dtmi:advantech:EdgeSync:SystemAgent:OnboardSensor:MetricType;1";
        public const string TemperatureMetricName  = "dtmi:advantech:EdgeSync:SystemAgent:OnboardSensor:TemperatureMetricName;1";
        public const string VoltageMetricName      = "dtmi:advantech:EdgeSync:SystemAgent:OnboardSensor:VoltageMetricName;1";
        public const string FanspeedMetricName     = "dtmi:advantech:EdgeSync:SystemAgent:OnboardSensor:FanspeedMetricName;1";
        public const string SensorGroup            = "dtmi:advantech:EdgeSync:SystemAgent:OnboardSensor:SensorGroup;1";
        public const string SensorInfoSchema       = "dtmi:advantech:EdgeSync:SystemAgent:OnboardSensor:SensorInfoSchema;1";
    }
}
```

---

## 3. Load and parse

```csharp
using DTDLParser;
using DTDLParser.Models;
using System.Reflection;

public sealed class OnboardSensorModel
{
    public IReadOnlyDictionary<Dtmi, DTEntityInfo> Entities { get; }
    public DTInterfaceInfo Interface { get; }

    private OnboardSensorModel(IReadOnlyDictionary<Dtmi, DTEntityInfo> entities)
    {
        Entities = entities;
        Interface = (DTInterfaceInfo)entities[new Dtmi(OnboardSensorDtmis.Interface)];
    }

    public static async Task<OnboardSensorModel> LoadAsync(CancellationToken ct = default)
    {
        var asm = Assembly.GetExecutingAssembly();
        await using var stream = asm.GetManifestResourceStream(
            "dtdl.04_ONBOARD_SENSOR.json")
            ?? throw new InvalidOperationException("DTDL resource not embedded.");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);

        var parser = new ModelParser();
        var entities = await parser.ParseAsync(new[] { json }, cancellationToken: ct);
        return new OnboardSensorModel(entities);
    }
}
```

---

## 4. Validate a Sensor against the DTDL — including temperature's dual-mode `MetricName`

```csharp
using ErrorOr;

public sealed class OnboardSensorValidator
{
    private readonly IReadOnlySet<string> _metricTypes;
    private readonly IReadOnlySet<string> _temperatureMetricNames;
    private readonly IReadOnlySet<string> _voltageMetricNames;
    private readonly IReadOnlySet<string> _fanspeedMetricNames;
    private readonly IReadOnlySet<string> _sensorGroups;
    private readonly IReadOnlySet<string> _sensorInfoSchemas;

    public OnboardSensorValidator(OnboardSensorModel model)
    {
        _metricTypes            = model.Entities.GetStringEnumValues(OnboardSensorDtmis.Enums.MetricType);
        _temperatureMetricNames = model.Entities.GetStringEnumValues(OnboardSensorDtmis.Enums.TemperatureMetricName);
        _voltageMetricNames     = model.Entities.GetStringEnumValues(OnboardSensorDtmis.Enums.VoltageMetricName);
        _fanspeedMetricNames    = model.Entities.GetStringEnumValues(OnboardSensorDtmis.Enums.FanspeedMetricName);
        _sensorGroups           = model.Entities.GetStringEnumValues(OnboardSensorDtmis.Enums.SensorGroup);
        _sensorInfoSchemas      = model.Entities.GetStringEnumValues(OnboardSensorDtmis.Enums.SensorInfoSchema);
    }

    public ErrorOr<Success> Validate(SensorConfig sensor)
    {
        var errors = new List<Error>();
        var p = sensor.Parameters;

        if (!_metricTypes.Contains(p.MetricType))
            errors.Add(Error.Validation("Sensor.MetricType",
                $"'{p.MetricType}' not in allowed enum [{string.Join(", ", _metricTypes)}]."));

        // Per-type MetricName rules.
        switch (p.MetricType)
        {
            case "temperature":
                // v1.1 canonical: "therm"; v1.0-compat: any non-empty source name.
                if (!_temperatureMetricNames.Contains(p.MetricName) && string.IsNullOrWhiteSpace(p.MetricName))
                    errors.Add(Error.Validation("Sensor.MetricName",
                        "Temperature.MetricName must be 'therm' (v1.1) or a non-empty source name (v1.0 compat)."));

                // Source / Sources mutual-exclusion check (advisory).
                if (!string.IsNullOrWhiteSpace(p.Source) && (p.Sources?.Count ?? 0) > 0)
                    errors.Add(Error.Validation("Sensor.Parameters",
                        "Both 'Source' and 'Sources' set on temperature sensor; runtime picks 'Source' but the config is ambiguous."));
                break;

            case "voltage":
                if (!_voltageMetricNames.Contains(p.MetricName))
                    errors.Add(Error.Validation("Sensor.MetricName",
                        $"Voltage.MetricName must be 'voltage', got '{p.MetricName}'."));
                break;

            case "fanspeed":
                if (!_fanspeedMetricNames.Contains(p.MetricName))
                    errors.Add(Error.Validation("Sensor.MetricName",
                        $"Fanspeed.MetricName must be 'fanspeed', got '{p.MetricName}'."));
                break;
        }

        if (sensor.SensorGroup is not null && !_sensorGroups.Contains(sensor.SensorGroup))
            errors.Add(Error.Validation("Sensor.SensorGroup",
                $"'{sensor.SensorGroup}' not in allowed enum."));

        // All three onboard metrics emit 'double'.
        if (!_sensorInfoSchemas.Contains(sensor.SensorInfo.Schema))
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"'{sensor.SensorInfo.Schema}' not in allowed enum."));
        if (sensor.SensorInfo.Schema != "double")
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"Onboard sensors require SensorInfo.Schema = 'double', got '{sensor.SensorInfo.Schema}'."));

        return errors.Count == 0 ? Result.Success : (ErrorOr<Success>)errors;
    }
}

// Extend SensorParameters to carry the optional temperature fields.
public sealed record SensorParameters(
    string MetricType,
    string MetricName,
    string? Source = null,
    IReadOnlyList<string>? Sources = null,
    // ... pre-existing optional parameters for other metric types
    string? MountPoint = null,
    string? Interface = null,
    IReadOnlyList<string>? Interfaces = null);
```

---

## 5. Integration test

The DTDL is config-only — `contents[]` is empty. The asserts below confirm that and check the reusable Enum sizes.

```csharp
using DTDLParser;
using DTDLParser.Models;
using Xunit;

public sealed class OnboardSensorDtdlTests
{
    [Fact]
    public async Task Dtdl_parses_and_declares_no_telemetries()
    {
        var json   = await File.ReadAllTextAsync("docs/Metrics/04_ONBOARD_SENSOR.dtdl.json");
        var parser = new ModelParser();
        var model  = await parser.ParseAsync(new[] { json });

        var iface = (DTInterfaceInfo)model[new Dtmi(OnboardSensorDtmis.Interface)];
        Assert.Empty(iface.Contents.Values.OfType<DTTelemetryInfo>());
    }

    [Theory]
    [InlineData(OnboardSensorDtmis.Enums.MetricType,            3)]
    [InlineData(OnboardSensorDtmis.Enums.TemperatureMetricName, 1)] // "therm" only; v1.0-compat is open-ended
    [InlineData(OnboardSensorDtmis.Enums.VoltageMetricName,     1)]
    [InlineData(OnboardSensorDtmis.Enums.FanspeedMetricName,    1)]
    [InlineData(OnboardSensorDtmis.Enums.SensorGroup,           7)]
    [InlineData(OnboardSensorDtmis.Enums.SensorInfoSchema,      5)]
    public async Task Enum_has_expected_value_count(string enumDtmi, int expected)
    {
        var json   = await File.ReadAllTextAsync("docs/Metrics/04_ONBOARD_SENSOR.dtdl.json");
        var model  = await new ModelParser().ParseAsync(new[] { json });
        Assert.Equal(expected, model.GetStringEnumValues(enumDtmi).Count);
    }

    [Fact]
    public async Task Temperature_v10_compat_metric_name_is_allowed()
    {
        var model  = await OnboardSensorModel.LoadAsync();
        var sut    = new OnboardSensorValidator(model);

        // v1.0-style: MetricName is the source name; no Source / Sources.
        var sensor = new SensorConfig("temperature_cpu_therm", "TEMP",
            new SensorParameters("temperature", "cpU-therm"),
            new SensorReport(true, 1000),
            new SensorInfo("double", "x", "CPU Thermal"));

        Assert.False(sut.Validate(sensor).IsError);
    }

    [Fact]
    public async Task Temperature_with_both_source_and_sources_is_flagged()
    {
        var model  = await OnboardSensorModel.LoadAsync();
        var sut    = new OnboardSensorValidator(model);

        var sensor = new SensorConfig("temperature", "TEMP",
            new SensorParameters("temperature", "therm",
                Source: "cpU-therm",
                Sources: new[] { "gpU-therm" }),
            new SensorReport(true, 1000),
            new SensorInfo("double", "x", "Temperature"));

        Assert.True(sut.Validate(sensor).IsError);
    }
}
```

---

## 6. End-to-end sketch

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(_ => OnboardSensorModel.LoadAsync().GetAwaiter().GetResult());
builder.Services.AddSingleton<OnboardSensorValidator>();

var host = builder.Build();
await host.RunAsync();
```

Co-load with the other DTDLs for a single parser pass:

```csharp
var dtdls = await Task.WhenAll(
    File.ReadAllTextAsync("docs/Metrics/01_SYSTEM_RESOURCE.dtdl.json"),
    File.ReadAllTextAsync("docs/Metrics/02_GPU_RESOURCE.dtdl.json"),
    File.ReadAllTextAsync("docs/Metrics/03_HARDWARE_INFO.dtdl.json"),
    File.ReadAllTextAsync("docs/Metrics/04_ONBOARD_SENSOR.dtdl.json"),
    File.ReadAllTextAsync("docs/Metrics/05_HARDWARE_FEATURE.dtdl.json"));
var entities = await new ModelParser().ParseAsync(dtdls);
```

---

## Related

- [`04_ONBOARD_SENSOR_FIELDS.md`](./04_ONBOARD_SENSOR_FIELDS.md) — the configuration field reference this code consumes.
- [`04_ONBOARD_SENSOR.dtdl.json`](./04_ONBOARD_SENSOR.dtdl.json) — the source-of-truth DTDL Interface.
- [`05_HARDWARE_FEATURE_USAGE.md`](./05_HARDWARE_FEATURE_USAGE.md) — pair `Temperature` alarms with `ThermalProtectionIsSupported` from the HardwareFeature interface; without thermal protection the host has no autonomous over-temperature response.
