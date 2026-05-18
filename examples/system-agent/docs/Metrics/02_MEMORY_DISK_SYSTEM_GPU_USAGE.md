# Using `02_MEMORY_DISK_SYSTEM_GPU.dtdl.json` from .NET / C#

End-to-end examples for consuming [`02_MEMORY_DISK_SYSTEM_GPU.dtdl.json`](./02_MEMORY_DISK_SYSTEM_GPU.dtdl.json) from a C# application. Targets **.NET 8+** with the official `DTDLParser` package. Mirrors the approach in [`01_CPU_NETWORK_USAGE.md`](./01_CPU_NETWORK_USAGE.md) — read that first if you haven't, then this doc focuses on the deltas for Memory / Disk / System / GPU.

The DTDL file ships **two** things you can leverage:

| Asset | Where it lives in the JSON | Use in C# |
|-------|---------------------------|-----------|
| 26 Telemetry definitions | `contents[]` | Strongly-typed DTMI constants; payload schema lookup; validation-rule extraction. |
| 7 reusable Enums | `schemas[]` | Allow-list validators for `Sensor.Parameters.MetricType`, the per-type `MetricName`, `SensorGroup`, and `SensorInfo.Schema`. |

Key differences vs. `01_CPU_NETWORK`:

- **No interface expansion** — these metric types don't support v1.1 `Interfaces` / auto-detect.
- **`disk` requires `Parameters.MountPoint`** — an extra validator beyond the enum allow-list.
- **More schema diversity** — `long`, `double`, *and* `integer` are all in use (vs. `01` which is only `long` and `double`).
- **`gpu` may be unsupported** — on non-NVIDIA hosts the value is suppressed (null), not zero.

---

## 1. Add the parser package

Same as `01`. Skip if you already added it.

```bash
dotnet add package DTDLParser
```

```xml
<ItemGroup>
  <EmbeddedResource Include="docs/Metrics/02_MEMORY_DISK_SYSTEM_GPU.dtdl.json" LogicalName="dtdl.02_MEMORY_DISK_SYSTEM_GPU.json" />
</ItemGroup>
```

---

## 2. Strongly-typed DTMI constants

```csharp
// File: src/SystemAgent/Schema/MemoryDiskSystemGpuDtmis.cs
namespace SystemAgentExample.Schema;

/// <summary>
/// DTMI constants for dtmi:advantech:EdgeSync:SystemAgent:MemoryDiskSystemGpu;1.
/// Keep in sync with docs/Metrics/02_MEMORY_DISK_SYSTEM_GPU.dtdl.json.
/// </summary>
public static class MemoryDiskSystemGpuDtmis
{
    public const string Interface =
        "dtmi:advantech:EdgeSync:SystemAgent:MemoryDiskSystemGpu;1";

    public static class Telemetries
    {
        // Memory
        public const string MemoryTotal      = "dtmi:advantech:EdgeSync:SystemInfo:MemoryTotal;1";
        public const string MemoryAvailable  = "dtmi:advantech:EdgeSync:SystemInfo:MemoryAvailable;1";
        public const string MemoryUsed       = "dtmi:advantech:EdgeSync:SystemInfo:MemoryUsed;1";
        public const string MemoryFree       = "dtmi:advantech:EdgeSync:SystemInfo:MemoryFree;1";
        public const string MemoryCached     = "dtmi:advantech:EdgeSync:SystemInfo:MemoryCached;1";
        public const string MemoryBuffers    = "dtmi:advantech:EdgeSync:SystemInfo:MemoryBuffers;1";
        public const string MemorySwapTotal  = "dtmi:advantech:EdgeSync:SystemInfo:MemorySwapTotal;1";
        public const string MemorySwapFree   = "dtmi:advantech:EdgeSync:SystemInfo:MemorySwapFree;1";

        // Disk
        public const string DiskTotal            = "dtmi:advantech:EdgeSync:SystemInfo:DiskTotal;1";
        public const string DiskAvailable        = "dtmi:advantech:EdgeSync:SystemInfo:DiskAvailable;1";
        public const string DiskFree             = "dtmi:advantech:EdgeSync:SystemInfo:DiskFree;1";
        public const string DiskUsed             = "dtmi:advantech:EdgeSync:SystemInfo:DiskUsed;1";
        public const string DiskUsagePercent     = "dtmi:advantech:EdgeSync:SystemInfo:DiskUsagePercent;1";
        public const string DiskReadsCompleted   = "dtmi:advantech:EdgeSync:SystemInfo:DiskReadsCompleted;1";
        public const string DiskWritesCompleted  = "dtmi:advantech:EdgeSync:SystemInfo:DiskWritesCompleted;1";
        public const string DiskReadBytes        = "dtmi:advantech:EdgeSync:SystemInfo:DiskReadBytes;1";
        public const string DiskWrittenBytes     = "dtmi:advantech:EdgeSync:SystemInfo:DiskWrittenBytes;1";

        // System
        public const string SystemTime            = "dtmi:advantech:EdgeSync:SystemInfo:SystemTime;1";
        public const string SystemTimexOffset     = "dtmi:advantech:EdgeSync:SystemInfo:SystemTimexOffset;1";
        public const string SystemBootTime        = "dtmi:advantech:EdgeSync:SystemInfo:SystemBootTime;1";
        public const string SystemFilefdAllocated = "dtmi:advantech:EdgeSync:SystemInfo:SystemFilefdAllocated;1";
        public const string SystemFilefdMaximum   = "dtmi:advantech:EdgeSync:SystemInfo:SystemFilefdMaximum;1";
        public const string SystemProcsRunning    = "dtmi:advantech:EdgeSync:SystemInfo:SystemProcsRunning;1";
        public const string SystemProcsBlocked    = "dtmi:advantech:EdgeSync:SystemInfo:SystemProcsBlocked;1";
        public const string SystemIntrTotal       = "dtmi:advantech:EdgeSync:SystemInfo:SystemIntrTotal;1";

        // GPU
        public const string GpuUtilization = "dtmi:advantech:EdgeSync:SystemInfo:GpuUtilization;1";
    }

    public static class Enums
    {
        public const string MetricType        = "dtmi:advantech:EdgeSync:SystemAgent:MemoryDiskSystemGpu:MetricType;1";
        public const string MemoryMetricName  = "dtmi:advantech:EdgeSync:SystemAgent:MemoryDiskSystemGpu:MemoryMetricName;1";
        public const string DiskMetricName    = "dtmi:advantech:EdgeSync:SystemAgent:MemoryDiskSystemGpu:DiskMetricName;1";
        public const string SystemMetricName  = "dtmi:advantech:EdgeSync:SystemAgent:MemoryDiskSystemGpu:SystemMetricName;1";
        public const string GpuMetricName     = "dtmi:advantech:EdgeSync:SystemAgent:MemoryDiskSystemGpu:GpuMetricName;1";
        public const string SensorGroup       = "dtmi:advantech:EdgeSync:SystemAgent:MemoryDiskSystemGpu:SensorGroup;1";
        public const string SensorInfoSchema  = "dtmi:advantech:EdgeSync:SystemAgent:MemoryDiskSystemGpu:SensorInfoSchema;1";
    }
}
```

---

## 3. Load and parse the DTDL at startup

```csharp
using DTDLParser;
using DTDLParser.Models;
using System.Reflection;

namespace SystemAgentExample.Schema;

public sealed class MemoryDiskSystemGpuModel
{
    public IReadOnlyDictionary<Dtmi, DTEntityInfo> Entities { get; }
    public DTInterfaceInfo Interface { get; }

    private MemoryDiskSystemGpuModel(IReadOnlyDictionary<Dtmi, DTEntityInfo> entities)
    {
        Entities = entities;
        Interface = (DTInterfaceInfo)entities[new Dtmi(MemoryDiskSystemGpuDtmis.Interface)];
    }

    public static async Task<MemoryDiskSystemGpuModel> LoadAsync(CancellationToken ct = default)
    {
        var asm = Assembly.GetExecutingAssembly();
        await using var stream = asm.GetManifestResourceStream(
            "dtdl.02_MEMORY_DISK_SYSTEM_GPU.json")
            ?? throw new InvalidOperationException("DTDL resource not embedded.");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);

        var parser = new ModelParser();
        var entities = await parser.ParseAsync(new[] { json }, cancellationToken: ct);
        return new MemoryDiskSystemGpuModel(entities);
    }
}
```

> If your host also loads `01_CPU_NETWORK.dtdl.json`, parse them in a single `ParseAsync(new[] { json01, json02 })` call so the parser sees both Interfaces and cross-resolves DTMIs.

---

## 4. Extract Enum allow-lists

Same `GetStringEnumValues` extension as in `01_CPU_NETWORK_USAGE.md` §4 — paste it once into your project and reuse for both DTDL files.

```csharp
var memoryNames = model.Entities.GetStringEnumValues(MemoryDiskSystemGpuDtmis.Enums.MemoryMetricName);
//   → { "total", "available", "used", "free", "cached", "buffers", "swap_total", "swap_free" }

var diskNames = model.Entities.GetStringEnumValues(MemoryDiskSystemGpuDtmis.Enums.DiskMetricName);
//   → { "total", "available", "free", "used", "usage_percent", "reads_completed",
//       "writes_completed", "read_bytes", "written_bytes" }
```

---

## 5. Validate a `devicecfg.json` Sensor against the DTDL

Disk sensors need an extra `MountPoint` check the enums can't express — non-empty string, plus optionally a *resolvable* mount-point check at runtime.

```csharp
using ErrorOr;

public sealed class MemoryDiskSystemGpuSensorValidator
{
    private readonly IReadOnlySet<string> _metricTypes;
    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _metricNamesByType;
    private readonly IReadOnlySet<string> _sensorGroups;
    private readonly IReadOnlySet<string> _sensorInfoSchemas;
    private readonly IReadOnlyDictionary<(string Type, string Name), string> _expectedSchemaByMetric;

    public MemoryDiskSystemGpuSensorValidator(MemoryDiskSystemGpuModel model)
    {
        _metricTypes       = model.Entities.GetStringEnumValues(MemoryDiskSystemGpuDtmis.Enums.MetricType);
        _sensorGroups      = model.Entities.GetStringEnumValues(MemoryDiskSystemGpuDtmis.Enums.SensorGroup);
        _sensorInfoSchemas = model.Entities.GetStringEnumValues(MemoryDiskSystemGpuDtmis.Enums.SensorInfoSchema);

        _metricNamesByType = new Dictionary<string, IReadOnlySet<string>>
        {
            ["memory"] = model.Entities.GetStringEnumValues(MemoryDiskSystemGpuDtmis.Enums.MemoryMetricName),
            ["disk"]   = model.Entities.GetStringEnumValues(MemoryDiskSystemGpuDtmis.Enums.DiskMetricName),
            ["system"] = model.Entities.GetStringEnumValues(MemoryDiskSystemGpuDtmis.Enums.SystemMetricName),
            ["gpu"]    = model.Entities.GetStringEnumValues(MemoryDiskSystemGpuDtmis.Enums.GpuMetricName),
        };

        _expectedSchemaByMetric = BuildExpectedSchemaMap();
    }

    public ErrorOr<Success> Validate(SensorConfig sensor)
    {
        var errors = new List<Error>();
        var mt = sensor.Parameters.MetricType;
        var mn = sensor.Parameters.MetricName;

        // 1. MetricType in enum.
        if (!_metricTypes.Contains(mt))
            errors.Add(Error.Validation("Sensor.MetricType",
                $"'{mt}' not in allowed enum [{string.Join(", ", _metricTypes)}]."));

        // 2. MetricName in the per-type enum.
        if (_metricNamesByType.TryGetValue(mt, out var allowedNames) && !allowedNames.Contains(mn))
            errors.Add(Error.Validation("Sensor.MetricName",
                $"'{mn}' not valid for MetricType '{mt}'. Allowed: [{string.Join(", ", allowedNames)}]."));

        // 3. SensorGroup in enum (when present).
        if (sensor.SensorGroup is not null && !_sensorGroups.Contains(sensor.SensorGroup))
            errors.Add(Error.Validation("Sensor.SensorGroup",
                $"'{sensor.SensorGroup}' not in allowed enum [{string.Join(", ", _sensorGroups)}]."));

        // 4. SensorInfo.Schema in enum AND matches the Telemetry's native schema.
        if (!_sensorInfoSchemas.Contains(sensor.SensorInfo.Schema))
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"'{sensor.SensorInfo.Schema}' not in allowed enum."));

        if (_expectedSchemaByMetric.TryGetValue((mt, mn), out var expected) &&
            !string.Equals(expected, sensor.SensorInfo.Schema, StringComparison.Ordinal))
        {
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"For ({mt}, {mn}) expected schema '{expected}', got '{sensor.SensorInfo.Schema}'."));
        }

        // 5. Disk-specific: MountPoint required and non-empty.
        if (mt == "disk" && string.IsNullOrWhiteSpace(sensor.Parameters.MountPoint))
        {
            errors.Add(Error.Validation("Sensor.Parameters.MountPoint",
                "Disk sensors require a non-empty 'MountPoint' (e.g. '/', '/data', 'C:\\')."));
        }

        return errors.Count == 0 ? Result.Success : (ErrorOr<Success>)errors;
    }

    private static IReadOnlyDictionary<(string, string), string> BuildExpectedSchemaMap()
    {
        return new Dictionary<(string, string), string>
        {
            // memory
            { ("memory", "total"),      "long" },
            { ("memory", "available"),  "long" },
            { ("memory", "used"),       "long" },
            { ("memory", "free"),       "long" },
            { ("memory", "cached"),     "long" },
            { ("memory", "buffers"),    "long" },
            { ("memory", "swap_total"), "long" },
            { ("memory", "swap_free"),  "long" },
            // disk
            { ("disk", "total"),             "long"   },
            { ("disk", "available"),         "long"   },
            { ("disk", "free"),              "long"   },
            { ("disk", "used"),              "long"   },
            { ("disk", "usage_percent"),     "double" },
            { ("disk", "reads_completed"),   "long"   },
            { ("disk", "writes_completed"),  "long"   },
            { ("disk", "read_bytes"),        "long"   },
            { ("disk", "written_bytes"),     "long"   },
            // system
            { ("system", "time"),             "long"    },
            { ("system", "timex_offset"),     "double"  },
            { ("system", "boot_time"),        "long"    },
            { ("system", "filefd_allocated"), "long"    },
            { ("system", "filefd_maximum"),   "long"    },
            { ("system", "procs_running"),    "integer" },
            { ("system", "procs_blocked"),    "integer" },
            { ("system", "intr_total"),       "long"    },
            // gpu
            { ("gpu", "utilization"), "integer" },
        };
    }
}
```

Optional runtime-only check — verify the MountPoint actually exists on the host:

```csharp
private static bool MountPointResolves(string mountPoint)
{
    try
    {
        return DriveInfo.GetDrives()
            .Any(d => string.Equals(NormalizePath(d.Name), NormalizePath(mountPoint), StringComparison.Ordinal));
    }
    catch { return false; }
}

private static string NormalizePath(string p) =>
    string.IsNullOrEmpty(p) ? "/" : p.TrimEnd('\\', '/');
```

Wire this in after the config validator if you want to fail-fast at startup when a disk is missing.

---

## 6. Value validators driven by the DTDL `comment` field

Comments on each Telemetry encode the per-metric rule. Build predicate map per-DTMI (mirrors `01_CPU_NETWORK_USAGE.md` §6 pattern):

```csharp
public sealed class TelemetryValueValidator
{
    private static readonly long EpochMin = 1_262_304_000;  // 2010-01-01 UTC
    private static readonly long EpochMax = 4_102_444_800;  // 2100-01-01 UTC

    private static readonly Dictionary<string, Func<object?, bool>> Predicates = new()
    {
        // memory (bytes, non-negative)
        [MemoryDiskSystemGpuDtmis.Telemetries.MemoryTotal]      = v => v is long l && l > 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.MemoryAvailable]  = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.MemoryUsed]       = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.MemoryFree]       = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.MemoryCached]     = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.MemoryBuffers]    = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.MemorySwapTotal]  = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.MemorySwapFree]   = v => v is long l && l >= 0,

        // disk
        [MemoryDiskSystemGpuDtmis.Telemetries.DiskTotal]           = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.DiskAvailable]       = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.DiskFree]            = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.DiskUsed]            = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.DiskUsagePercent]    = v =>
            v is double d && d >= 0 && d <= 100 && !double.IsNaN(d) && !double.IsInfinity(d),
        [MemoryDiskSystemGpuDtmis.Telemetries.DiskReadsCompleted]  = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.DiskWritesCompleted] = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.DiskReadBytes]       = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.DiskWrittenBytes]    = v => v is long l && l >= 0,

        // system
        [MemoryDiskSystemGpuDtmis.Telemetries.SystemTime]            = v => v is long l && l >= EpochMin && l < EpochMax,
        [MemoryDiskSystemGpuDtmis.Telemetries.SystemTimexOffset]     = v =>
            v is double d && !double.IsNaN(d) && !double.IsInfinity(d),
        [MemoryDiskSystemGpuDtmis.Telemetries.SystemBootTime]        = v => v is long l && l >= EpochMin && l < EpochMax,
        [MemoryDiskSystemGpuDtmis.Telemetries.SystemFilefdAllocated] = v => v is long l && l >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.SystemFilefdMaximum]   = v => v is long l && l > 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.SystemProcsRunning]    = v => v is int i && i >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.SystemProcsBlocked]    = v => v is int i && i >= 0,
        [MemoryDiskSystemGpuDtmis.Telemetries.SystemIntrTotal]       = v => v is long l && l >= 0,

        // gpu
        [MemoryDiskSystemGpuDtmis.Telemetries.GpuUtilization] = v => v is int i && i >= 0 && i <= 100,
    };

    public bool IsValid(string telemetryDtmi, object? value) =>
        Predicates.TryGetValue(telemetryDtmi, out var fn) && fn(value);
}
```

Cross-field invariants (e.g. `MemoryUsed + MemoryAvailable == MemoryTotal`, `MemoryFree <= MemoryTotal`, `SystemFilefdAllocated <= SystemFilefdMaximum`) live outside this map — implement them after a coherent batch of telemetry arrives:

```csharp
public sealed record MemorySample(long Total, long Available, long Used, long Free);

public bool IsMemorySampleConsistent(MemorySample s) =>
    s.Total > 0 &&
    s.Available >= 0 && s.Available <= s.Total &&
    s.Used      >= 0 && s.Used      <= s.Total &&
    s.Free      >= 0 && s.Free      <= s.Total &&
    Math.Abs((s.Used + s.Available) - s.Total) <= s.Total / 1000; // 0.1 % rounding slack
```

---

## 7. Monotonic-counter checks (Disk I/O + `system.intr_total`)

Reuse the `MonotonicCounterValidator` from `01_CPU_NETWORK_USAGE.md` §6 — the contract is identical. Apply to:

- `DiskReadsCompleted`, `DiskWritesCompleted`, `DiskReadBytes`, `DiskWrittenBytes` — key by `(DTMI, mountPoint)` since each `mountPoint` gets its own counter.
- `SystemIntrTotal` — keyed by DTMI alone (host-global).

```csharp
public sealed class DiskIoCounterValidator
{
    private readonly Dictionary<(string Dtmi, string MountPoint), long> _last = new();
    private long _lastBootTime;

    public bool IsValid(string dtmi, string mountPoint, long value, long currentBootTime)
    {
        if (currentBootTime != _lastBootTime)
        {
            _last.Clear();
            _lastBootTime = currentBootTime;
        }

        var key = (dtmi, mountPoint);
        if (_last.TryGetValue(key, out var prev) && value < prev)
            return false;

        _last[key] = value;
        return true;
    }
}
```

---

## 8. Publish a `TelemetryMeasure` using a DTMI as `ResourceId`

```csharp
using Weda.SubNode.Abstractions.Telemetry;

public sealed class MemoryDiskSystemGpuPublisher
{
    private readonly ICommunication _communication;
    private readonly TelemetryValueValidator _validator;
    private readonly ILogger<MemoryDiskSystemGpuPublisher> _logger;

    public MemoryDiskSystemGpuPublisher(
        ICommunication communication,
        TelemetryValueValidator validator,
        ILogger<MemoryDiskSystemGpuPublisher> logger)
    {
        _communication = communication;
        _validator     = validator;
        _logger        = logger;
    }

    public Task PublishMemoryUsedAsync(long bytes, CancellationToken ct = default)
        => PublishAsync(MemoryDiskSystemGpuDtmis.Telemetries.MemoryUsed, bytes, ct);

    public Task PublishDiskUsagePercentAsync(string mountPoint, double pct, CancellationToken ct = default)
        => PublishAsync(MemoryDiskSystemGpuDtmis.Telemetries.DiskUsagePercent,
                        Math.Round(pct, 2), ct,
                        // mountPoint differentiates per-volume sensors that share a DTMI
                        discriminator: mountPoint);

    public Task PublishGpuUtilizationAsync(int? percent, CancellationToken ct = default)
    {
        if (percent is null)
        {
            // SIL2 fail-safe: GPU absent => suppress, don't publish 0.
            _logger.LogDebug("GPU absent -- suppressing GpuUtilization telemetry");
            return Task.CompletedTask;
        }
        return PublishAsync(MemoryDiskSystemGpuDtmis.Telemetries.GpuUtilization, percent.Value, ct);
    }

    private async Task PublishAsync(
        string dtmi, object value, CancellationToken ct, string? discriminator = null)
    {
        if (!_validator.IsValid(dtmi, value))
        {
            _logger.LogWarning("Dropping telemetry {Dtmi}={Value} (failed validation)", dtmi, value);
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

## 9. Integration test — round-trip the DTDL through the parser

```csharp
using DTDLParser;
using Xunit;

public sealed class MemoryDiskSystemGpuDtdlTests
{
    [Fact]
    public async Task Dtdl_parses_without_errors()
    {
        var json   = await File.ReadAllTextAsync("docs/Metrics/02_MEMORY_DISK_SYSTEM_GPU.dtdl.json");
        var parser = new ModelParser();
        var model  = await parser.ParseAsync(new[] { json });

        var iface = (DTInterfaceInfo)model[new Dtmi(MemoryDiskSystemGpuDtmis.Interface)];
        Assert.Equal(26, iface.Contents.Values.OfType<DTTelemetryInfo>().Count());
    }

    [Theory]
    [InlineData(MemoryDiskSystemGpuDtmis.Enums.MemoryMetricName, 8)]
    [InlineData(MemoryDiskSystemGpuDtmis.Enums.DiskMetricName,   9)]
    [InlineData(MemoryDiskSystemGpuDtmis.Enums.SystemMetricName, 8)]
    [InlineData(MemoryDiskSystemGpuDtmis.Enums.GpuMetricName,    1)]
    [InlineData(MemoryDiskSystemGpuDtmis.Enums.SensorGroup,      7)]
    [InlineData(MemoryDiskSystemGpuDtmis.Enums.SensorInfoSchema, 5)]
    public async Task Enum_has_expected_value_count(string enumDtmi, int expected)
    {
        var json   = await File.ReadAllTextAsync("docs/Metrics/02_MEMORY_DISK_SYSTEM_GPU.dtdl.json");
        var parser = new ModelParser();
        var model  = await parser.ParseAsync(new[] { json });

        Assert.Equal(expected, model.GetStringEnumValues(enumDtmi).Count);
    }

    [Fact]
    public async Task Disk_sensors_without_mountpoint_are_rejected()
    {
        var dtdl    = await MemoryDiskSystemGpuModel.LoadAsync();
        var sut     = new MemoryDiskSystemGpuSensorValidator(dtdl);
        var sensor  = new SensorConfig(
            Name: "disk_root_total",
            SensorGroup: "SYS",
            Parameters: new SensorParameters("disk", "total", MountPoint: ""),  // empty
            Report:     new SensorReport(true, 30000),
            SensorInfo: new SensorInfo("long", "x", "Disk Total"));

        var result = sut.Validate(sensor);
        Assert.True(result.IsError);
        Assert.Contains(result.Errors, e => e.Code == "Sensor.Parameters.MountPoint");
    }
}
```

---

## 10. End-to-end sketch

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// 1. Load + parse both DTDL files in one go so cross-DTMI references resolve.
builder.Services.AddSingleton<MemoryDiskSystemGpuModel>(_ =>
    MemoryDiskSystemGpuModel.LoadAsync().GetAwaiter().GetResult());

// 2. Config-time validators.
builder.Services.AddSingleton<MemoryDiskSystemGpuSensorValidator>();

// 3. Runtime value validators.
builder.Services.AddSingleton<TelemetryValueValidator>();
builder.Services.AddSingleton<DiskIoCounterValidator>();

// 4. Publisher.
builder.Services.AddSingleton<MemoryDiskSystemGpuPublisher>();

// 5. Reject malformed devicecfg.json before the agent starts.
builder.Services.AddSingleton<DeviceConfigLoader>();

var host = builder.Build();

host.Services
    .GetRequiredService<DeviceConfigLoader>()
    .LoadAndValidate("/opt/system-agent/devicecfg.json");

await host.RunAsync();
```

If both DTDL files are present, parse them together so DTMIs from both Interfaces are visible to consumer code:

```csharp
var json01 = await File.ReadAllTextAsync("docs/Metrics/01_CPU_NETWORK.dtdl.json");
var json02 = await File.ReadAllTextAsync("docs/Metrics/02_MEMORY_DISK_SYSTEM_GPU.dtdl.json");
var entities = await new ModelParser().ParseAsync(new[] { json01, json02 });
// entities now contains both Interfaces, both sets of Enums, and all Telemetries.
```

---

## Related

- [`02_MEMORY_DISK_SYSTEM_GPU_FIELDS.md`](./02_MEMORY_DISK_SYSTEM_GPU_FIELDS.md) — the configuration field reference this code consumes.
- [`02_MEMORY_DISK_SYSTEM_GPU.dtdl.json`](./02_MEMORY_DISK_SYSTEM_GPU.dtdl.json) — the source-of-truth DTDL Interface.
- [`01_CPU_NETWORK_USAGE.md`](./01_CPU_NETWORK_USAGE.md) — the equivalent guide for CPU and Network; share the `GetStringEnumValues` helper and the `MonotonicCounterValidator` between them.
- `Protocols/SystemMetricsParser.cs` — the runtime parser that emits the `TelemetryMeasure` instances published by the examples above.
