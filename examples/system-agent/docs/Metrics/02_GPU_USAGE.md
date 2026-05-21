# Using `02_GPU_RESOURCE.dtdl.json` from .NET / C#

End-to-end examples for consuming [`02_GPU_RESOURCE.dtdl.json`](./02_GPU_RESOURCE.dtdl.json) from a C# application. Targets **.NET 8+** with the official `DTDLParser` package. Mirrors the approach in [`01_SYS_RES_CPU_NETWORK_USAGE.md`](./01_SYS_RES_CPU_NETWORK_USAGE.md).

The GPU Resource Interface is intentionally narrow: a single Telemetry (`GpuUtilization`) representing the NVML utilization counter. Read-only by design; the SIL2 fail-safe contract suppresses (does not zero-default) the value on hosts without NVIDIA hardware.

Key things to know:

- **Single Telemetry** — `dtmi:advantech:EdgeSync:SystemInfo:GpuUtilization;1`, schema `integer`, range `[0, 100]`.
- **Platform-dependent** — requires NVIDIA drivers and `libnvidia-ml.so`. Publish is suppressed (null) on absent / unsupported hosts.
- **No type-specific Parameters** beyond `MetricType` / `MetricName`.
- **No v1.1 Sensor Expansion** — multi-GPU support is reserved for a future release (would introduce a `deviceIndex` parameter).

---

## 1. Add the parser package

Skip if you already added it for another USAGE doc.

```bash
dotnet add package DTDLParser
```

```xml
<ItemGroup>
  <EmbeddedResource Include="docs/Metrics/02_GPU_RESOURCE.dtdl.json"
                    LogicalName="dtdl.02_GPU_RESOURCE.json" />
</ItemGroup>
```

---

## 2. Strongly-typed DTMI constants

```csharp
// File: src/SystemAgent/Schema/GpuResourceDtmis.cs
namespace SystemAgentExample.Schema;

public static class GpuResourceDtmis
{
    public const string Interface =
        "dtmi:advantech:EdgeSync:SystemAgent:GpuResource;1";

    public static class Telemetries
    {
        public const string GpuUtilization = "dtmi:advantech:EdgeSync:SystemInfo:GpuUtilization;1";
    }

    public static class Enums
    {
        public const string MetricType       = "dtmi:advantech:EdgeSync:SystemAgent:GpuResource:MetricType;1";
        public const string GpuMetricName    = "dtmi:advantech:EdgeSync:SystemAgent:GpuResource:GpuMetricName;1";
        public const string SensorGroup      = "dtmi:advantech:EdgeSync:SystemAgent:GpuResource:SensorGroup;1";
        public const string SensorInfoSchema = "dtmi:advantech:EdgeSync:SystemAgent:GpuResource:SensorInfoSchema;1";
    }

    public static class Schemas
    {
        public const string GpuSensorParameters =
            "dtmi:advantech:EdgeSync:SystemAgent:GpuResource:GpuSensorParameters;1";
    }
}
```

---

## 3. Load and parse

```csharp
using DTDLParser;
using DTDLParser.Models;
using System.Reflection;

public sealed class GpuResourceModel
{
    public IReadOnlyDictionary<Dtmi, DTEntityInfo> Entities { get; }
    public DTInterfaceInfo Interface { get; }

    private GpuResourceModel(IReadOnlyDictionary<Dtmi, DTEntityInfo> entities)
    {
        Entities = entities;
        Interface = (DTInterfaceInfo)entities[new Dtmi(GpuResourceDtmis.Interface)];
    }

    public static async Task<GpuResourceModel> LoadAsync(CancellationToken ct = default)
    {
        var asm = Assembly.GetExecutingAssembly();
        await using var stream = asm.GetManifestResourceStream("dtdl.02_GPU_RESOURCE.json")
            ?? throw new InvalidOperationException("DTDL resource not embedded.");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);

        var parser = new ModelParser();
        var entities = await parser.ParseAsync(new[] { json }, cancellationToken: ct);
        return new GpuResourceModel(entities);
    }
}
```

---

## 4. Validate a Sensor against the DTDL

GPU sensors are the simplest of any MetricType — only four enum checks plus a fixed schema (`integer`).

```csharp
using ErrorOr;

public sealed class GpuResourceSensorValidator
{
    private readonly IReadOnlySet<string> _metricTypes;
    private readonly IReadOnlySet<string> _gpuMetricNames;
    private readonly IReadOnlySet<string> _sensorGroups;
    private readonly IReadOnlySet<string> _sensorInfoSchemas;

    public GpuResourceSensorValidator(GpuResourceModel model)
    {
        _metricTypes       = model.Entities.GetStringEnumValues(GpuResourceDtmis.Enums.MetricType);
        _gpuMetricNames    = model.Entities.GetStringEnumValues(GpuResourceDtmis.Enums.GpuMetricName);
        _sensorGroups      = model.Entities.GetStringEnumValues(GpuResourceDtmis.Enums.SensorGroup);
        _sensorInfoSchemas = model.Entities.GetStringEnumValues(GpuResourceDtmis.Enums.SensorInfoSchema);
    }

    public ErrorOr<Success> Validate(SensorConfig sensor)
    {
        var errors = new List<Error>();
        var mt = sensor.Parameters.MetricType;
        var mn = sensor.Parameters.MetricName;

        if (!_metricTypes.Contains(mt))
            errors.Add(Error.Validation("Sensor.MetricType", $"'{mt}' not allowed (must be 'gpu')."));

        if (mt == "gpu" && !_gpuMetricNames.Contains(mn))
            errors.Add(Error.Validation("Sensor.MetricName",
                $"'{mn}' not valid for gpu. Allowed: [{string.Join(", ", _gpuMetricNames)}]."));

        if (sensor.SensorGroup is not null && !_sensorGroups.Contains(sensor.SensorGroup))
            errors.Add(Error.Validation("Sensor.SensorGroup",
                $"'{sensor.SensorGroup}' not in allowed enum."));

        if (!_sensorInfoSchemas.Contains(sensor.SensorInfo.Schema))
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"'{sensor.SensorInfo.Schema}' not in allowed enum."));

        if (mt == "gpu" && sensor.SensorInfo.Schema != "integer")
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"For (gpu, *) expected schema 'integer', got '{sensor.SensorInfo.Schema}'."));

        return errors.Count == 0 ? Result.Success : (ErrorOr<Success>)errors;
    }
}
```

---

## 5. Integration test

The DTDL is config-only — `contents[]` is empty. The asserts below confirm that and check the reusable Enum sizes.

```csharp
using DTDLParser;
using DTDLParser.Models;
using Xunit;

public sealed class GpuResourceDtdlTests
{
    private const string DtdlPath = "docs/Metrics/02_GPU_RESOURCE.dtdl.json";

    [Fact]
    public async Task Dtdl_parses_and_declares_no_telemetries()
    {
        var json   = await File.ReadAllTextAsync(DtdlPath);
        var parser = new ModelParser();
        var model  = await parser.ParseAsync(new[] { json });

        var iface = (DTInterfaceInfo)model[new Dtmi(GpuResourceDtmis.Interface)];
        Assert.Empty(iface.Contents.Values.OfType<DTTelemetryInfo>());
    }

    [Theory]
    [InlineData(GpuResourceDtmis.Enums.MetricType,    1)]
    [InlineData(GpuResourceDtmis.Enums.GpuMetricName, 1)]
    [InlineData(GpuResourceDtmis.Enums.SensorGroup,   7)]
    public async Task Enum_has_expected_value_count(string enumDtmi, int expected)
    {
        var json   = await File.ReadAllTextAsync(DtdlPath);
        var model  = await new ModelParser().ParseAsync(new[] { json });
        Assert.Equal(expected, model.GetStringEnumValues(enumDtmi).Count);
    }
}
```

---

## 6. End-to-end sketch

```csharp
// Program.cs
builder.Services.AddSingleton(_ => GpuResourceModel.LoadAsync().GetAwaiter().GetResult());
builder.Services.AddSingleton<GpuResourceSensorValidator>();
```

---

## Related

- [`02_GPU_FIELDS.md`](./02_GPU_FIELDS.md) — the configuration field reference this code consumes.
- [`02_GPU_RESOURCE.dtdl.json`](./02_GPU_RESOURCE.dtdl.json) — the source-of-truth DTDL Interface.
- [`01_SYS_RES_CPU_NETWORK_USAGE.md`](./01_SYS_RES_CPU_NETWORK_USAGE.md), [`01_SYS_RES_MEMORY_DISK_USAGE.md`](./01_SYS_RES_MEMORY_DISK_USAGE.md) — sibling System Resource USAGE docs.
