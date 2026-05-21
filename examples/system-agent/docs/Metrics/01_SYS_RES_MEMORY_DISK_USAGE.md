# Using Memory · Disk · System Telemetries from .NET / C#

End-to-end examples for consuming the Memory, Disk, and System telemetries inside [`01_SYSTEM_RESOURCE.dtdl.json`](./01_SYSTEM_RESOURCE.dtdl.json) from a C# application. Targets **.NET 8+** with the official `DTDLParser` package. Mirrors the approach in [`01_SYS_RES_CPU_NETWORK_USAGE.md`](./01_SYS_RES_CPU_NETWORK_USAGE.md) — read that first if you haven't; this doc focuses on the deltas for Memory / Disk / System.

> The `01_SYSTEM_RESOURCE.dtdl.json` Interface covers **five** MetricTypes — `cpu`, `network`, `memory`, `disk`, and `system` — all under a single DTMI (`dtmi:advantech:EdgeSync:SystemAgent:SystemResource;1`). CPU and Network examples live in their own USAGE doc; this doc handles the remaining three.

Key things to know:

- **No type-specific extra parameters** for `memory` and `system` (only `metricType` / `metricName`).
- **`disk` requires `Parameters.MountPoint`** — the only required type-specific parameter in this group.
- **No v1.1 Sensor Expansion** for these three types.
- **More schema diversity** than CPU / Network: all three numeric schemas appear (`long`, `double`, `integer`).

---

## 1. Add the parser package

Skip if you already added it for `01_SYS_RES_CPU_NETWORK_USAGE.md`.

```bash
dotnet add package DTDLParser
```

```xml
<ItemGroup>
  <EmbeddedResource Include="docs/Metrics/01_SYSTEM_RESOURCE.dtdl.json"
                    LogicalName="dtdl.01_SYSTEM_RESOURCE.json" />
</ItemGroup>
```

---

## 2. Strongly-typed DTMI constants

Add Memory / Disk / System rows to the same `SystemResourceDtmis` class introduced in `01_SYS_RES_CPU_NETWORK_USAGE.md`:

```csharp
// File: src/SystemAgent/Schema/SystemResourceDtmis.cs (extension)
public static partial class SystemResourceDtmis
{
    public static partial class Telemetries
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
    }

    public static partial class Enums
    {
        public const string MemoryMetricName = "dtmi:advantech:EdgeSync:SystemAgent:SystemResource:MemoryMetricName;1";
        public const string DiskMetricName   = "dtmi:advantech:EdgeSync:SystemAgent:SystemResource:DiskMetricName;1";
        public const string SystemMetricName = "dtmi:advantech:EdgeSync:SystemAgent:SystemResource:SystemMetricName;1";
    }
}
```

---

## 3. Validate a `devicecfg.json` Sensor against the DTDL

Disk sensors need an extra `MountPoint` check the enums can't express — non-empty string plus optionally a *resolvable* mount-point check at runtime.

```csharp
using ErrorOr;

public sealed class MemoryDiskSystemSensorValidator
{
    private readonly IReadOnlySet<string> _metricTypes;
    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _metricNamesByType;
    private readonly IReadOnlySet<string> _sensorGroups;
    private readonly IReadOnlySet<string> _sensorInfoSchemas;
    private readonly IReadOnlyDictionary<(string Type, string Name), string> _expectedSchemaByMetric;

    public MemoryDiskSystemSensorValidator(SystemResourceModel model)
    {
        _metricTypes       = model.Entities.GetStringEnumValues(SystemResourceDtmis.Enums.MetricType);
        _sensorGroups      = model.Entities.GetStringEnumValues(SystemResourceDtmis.Enums.SensorGroup);
        _sensorInfoSchemas = model.Entities.GetStringEnumValues(SystemResourceDtmis.Enums.SensorInfoSchema);

        _metricNamesByType = new Dictionary<string, IReadOnlySet<string>>
        {
            ["memory"] = model.Entities.GetStringEnumValues(SystemResourceDtmis.Enums.MemoryMetricName),
            ["disk"]   = model.Entities.GetStringEnumValues(SystemResourceDtmis.Enums.DiskMetricName),
            ["system"] = model.Entities.GetStringEnumValues(SystemResourceDtmis.Enums.SystemMetricName),
        };

        _expectedSchemaByMetric = BuildExpectedSchemaMap();
    }

    public ErrorOr<Success> Validate(SensorConfig sensor)
    {
        var errors = new List<Error>();
        var mt = sensor.Parameters.MetricType;
        var mn = sensor.Parameters.MetricName;

        if (!_metricTypes.Contains(mt))
            errors.Add(Error.Validation("Sensor.MetricType", $"'{mt}' not allowed."));

        if (_metricNamesByType.TryGetValue(mt, out var allowed) && !allowed.Contains(mn))
            errors.Add(Error.Validation("Sensor.MetricName",
                $"'{mn}' not valid for MetricType '{mt}'. Allowed: [{string.Join(", ", allowed)}]."));

        if (sensor.SensorGroup is not null && !_sensorGroups.Contains(sensor.SensorGroup))
            errors.Add(Error.Validation("Sensor.SensorGroup", "..."));

        if (!_sensorInfoSchemas.Contains(sensor.SensorInfo.Schema))
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema", "..."));

        if (_expectedSchemaByMetric.TryGetValue((mt, mn), out var expected) &&
            !string.Equals(expected, sensor.SensorInfo.Schema, StringComparison.Ordinal))
        {
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"For ({mt}, {mn}) expected schema '{expected}', got '{sensor.SensorInfo.Schema}'."));
        }

        // Disk-specific: MountPoint required and non-empty.
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
        };
    }
}
```

---

## 4. Integration test

```csharp
using DTDLParser;
using Xunit;

public sealed class MemoryDiskSystemDtdlTests
{
    [Theory]
    [InlineData(SystemResourceDtmis.Enums.MemoryMetricName, 8)]
    [InlineData(SystemResourceDtmis.Enums.DiskMetricName,   9)]
    [InlineData(SystemResourceDtmis.Enums.SystemMetricName, 8)]
    public async Task Enum_has_expected_value_count(string enumDtmi, int expected)
    {
        var json   = await File.ReadAllTextAsync("docs/Metrics/01_SYSTEM_RESOURCE.dtdl.json");
        var model  = await new ModelParser().ParseAsync(new[] { json });
        Assert.Equal(expected, model.GetStringEnumValues(enumDtmi).Count);
    }

    [Fact]
    public async Task Disk_sensors_without_mountpoint_are_rejected()
    {
        var dtdl    = await SystemResourceModel.LoadAsync();
        var sut     = new MemoryDiskSystemSensorValidator(dtdl);
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

## Related

- [`01_SYS_RES_MEMORY_DISK_FIELDS.md`](./01_SYS_RES_MEMORY_DISK_FIELDS.md) — the configuration field reference this code consumes.
- [`01_SYS_RES_CPU_NETWORK_USAGE.md`](./01_SYS_RES_CPU_NETWORK_USAGE.md) — sibling USAGE doc for the CPU + Network MetricTypes in the same Interface.
- [`02_GPU_USAGE.md`](./02_GPU_USAGE.md) — GPU MetricType (own Interface `GpuResource`).
- [`01_SYSTEM_RESOURCE.dtdl.json`](./01_SYSTEM_RESOURCE.dtdl.json) — the source-of-truth DTDL Interface.
