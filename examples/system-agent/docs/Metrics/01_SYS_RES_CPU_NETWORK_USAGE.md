# Using `01_SYSTEM_RESOURCE.dtdl.json` from .NET / C#

End-to-end examples showing how to consume [`01_SYSTEM_RESOURCE.dtdl.json`](./01_SYSTEM_RESOURCE.dtdl.json) from a C# application for the CPU and Network MetricTypes. Every example targets **.NET 8+** and uses the official Microsoft DTDL parser.

The DTDL is **config-only** (`contents[]` empty by design) and ships the reusable schemas you parse against:

| Asset | Where it lives in the JSON | Use in C# |
|-------|---------------------------|-----------|
| Reusable Enums | `schemas[]` | Allow-list validators for `Sensor.Parameters.MetricType`, `MetricName`, `SensorGroup`, `SensorInfo.Schema`. |
| Reusable Object schemas (`CpuSensorParameters`, `NetworkSensorParameters`) | `schemas[]` | Typed shape of the `Parameters` block per `MetricType`. |

---

## 1. Add the parser package

The Microsoft DTDL v2/v3 parser is published on NuGet as **`DTDLParser`**.

```bash
dotnet add package DTDLParser
```

```xml
<!-- *.csproj -->
<ItemGroup>
  <PackageReference Include="DTDLParser" Version="1.0.52" />
</ItemGroup>
```

Embed the DTDL file as a resource so it ships with the agent:

```xml
<ItemGroup>
  <EmbeddedResource Include="docs/Metrics/01_CPU_NETWORK.dtdl.json" LogicalName="dtdl.01_CPU_NETWORK.json" />
</ItemGroup>
```

---

## 2. Strongly-typed DTMI constants

Generate constants by hand (or with a source generator) so consumer code never hard-codes DTMI strings inline.

```csharp
// File: src/SystemAgent/Schema/CpuNetworkDtmis.cs
namespace SystemAgentExample.Schema;

/// <summary>
/// DTMI constants for dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork;1.
/// Keep in sync with docs/Metrics/01_CPU_NETWORK.dtdl.json.
/// </summary>
public static class CpuNetworkDtmis
{
    public const string Interface =
        "dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork;1";

    public static class Telemetries
    {
        public const string CpuUsage              = "dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1";
        public const string CpuLoad1              = "dtmi:advantech:EdgeSync:SystemInfo:CpuLoad1;1";
        public const string CpuLoad5              = "dtmi:advantech:EdgeSync:SystemInfo:CpuLoad5;1";
        public const string CpuLoad15             = "dtmi:advantech:EdgeSync:SystemInfo:CpuLoad15;1";
        public const string CpuContextSwitches    = "dtmi:advantech:EdgeSync:SystemInfo:CpuContextSwitches;1";
        public const string NetworkBytesSent      = "dtmi:advantech:EdgeSync:SystemInfo:NetworkBytesSent;1";
        public const string NetworkBytesReceived  = "dtmi:advantech:EdgeSync:SystemInfo:NetworkBytesReceived;1";
        public const string NetworkPacketsSent    = "dtmi:advantech:EdgeSync:SystemInfo:NetworkPacketsSent;1";
        public const string NetworkPacketsReceived= "dtmi:advantech:EdgeSync:SystemInfo:NetworkPacketsReceived;1";
        public const string NetworkErrors         = "dtmi:advantech:EdgeSync:SystemInfo:NetworkErrors;1";
        public const string NetworkErrorsIn       = "dtmi:advantech:EdgeSync:SystemInfo:NetworkErrorsIn;1";
        public const string NetworkErrorsOut      = "dtmi:advantech:EdgeSync:SystemInfo:NetworkErrorsOut;1";
    }

    public static class Enums
    {
        public const string MetricType        = "dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork:MetricType;1";
        public const string CpuMetricName     = "dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork:CpuMetricName;1";
        public const string NetworkMetricName = "dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork:NetworkMetricName;1";
        public const string SensorGroup       = "dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork:SensorGroup;1";
        public const string SensorInfoSchema  = "dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork:SensorInfoSchema;1";
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

public sealed class CpuNetworkModel
{
    public IReadOnlyDictionary<Dtmi, DTEntityInfo> Entities { get; }
    public DTInterfaceInfo Interface { get; }

    private CpuNetworkModel(IReadOnlyDictionary<Dtmi, DTEntityInfo> entities)
    {
        Entities = entities;
        Interface = (DTInterfaceInfo)entities[new Dtmi(CpuNetworkDtmis.Interface)];
    }

    public static async Task<CpuNetworkModel> LoadAsync(CancellationToken ct = default)
    {
        // Read the embedded resource (or File.ReadAllText from disk).
        var asm = Assembly.GetExecutingAssembly();
        await using var stream = asm.GetManifestResourceStream(
            "dtdl.01_CPU_NETWORK.json")
            ?? throw new InvalidOperationException("DTDL resource not embedded.");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);

        var parser = new ModelParser();
        var entities = await parser.ParseAsync(new[] { json }, cancellationToken: ct);
        return new CpuNetworkModel(entities);
    }
}
```

Register it as a singleton at host startup:

```csharp
// Program.cs
builder.Services.AddSingleton(sp =>
    CpuNetworkModel.LoadAsync().GetAwaiter().GetResult());
```

> The parser **validates** the DTDL on load — malformed schemas throw `ParsingException`. Catch it in `Main` and fail fast so a broken model never reaches production.

---

## 4. Extract Enum allow-lists from the parsed model

```csharp
public static class DtdlEnumExtensions
{
    /// <summary>
    /// Return the on-the-wire enum values for an Enum schema, e.g.
    /// CpuMetricName → { "usage", "load1", "load5", "load15", "context_switches" }.
    /// </summary>
    public static IReadOnlySet<string> GetStringEnumValues(
        this IReadOnlyDictionary<Dtmi, DTEntityInfo> model,
        string enumDtmi)
    {
        var dtmi = new Dtmi(enumDtmi);
        if (!model.TryGetValue(dtmi, out var entity) || entity is not DTEnumInfo enumInfo)
            throw new ArgumentException($"DTMI {enumDtmi} is not an Enum schema.");

        return enumInfo.EnumValues
            .Select(v => v.EnumValue?.ToString() ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToHashSet();
    }
}

// Usage
var cpuNames = model.Entities.GetStringEnumValues(CpuNetworkDtmis.Enums.CpuMetricName);
//   → { "usage", "load1", "load5", "load15", "context_switches" }

var groups = model.Entities.GetStringEnumValues(CpuNetworkDtmis.Enums.SensorGroup);
//   → { "AI", "AO", "DI", "DO", "TEMP", "PWR", "SYS" }
```

---

## 5. Validate a `devicecfg.json` Sensor against the DTDL

Use the enums to enforce the `(MetricType, MetricName, SensorGroup, SensorInfo.Schema)` contract at config-load time — before the agent starts publishing.

```csharp
using ErrorOr;

public sealed record SensorConfig(
    string Name,
    string? SensorGroup,
    SensorParameters Parameters,
    SensorReport Report,
    SensorInfo SensorInfo);

public sealed record SensorParameters(
    string MetricType,
    string MetricName,
    string? Interface = null,
    IReadOnlyList<string>? Interfaces = null);

public sealed record SensorReport(bool Enabled, int Interval);
public sealed record SensorInfo(string Schema, string Description, string DisplayName);

public sealed class CpuNetworkSensorValidator
{
    private readonly IReadOnlySet<string> _metricTypes;
    private readonly IReadOnlySet<string> _cpuMetricNames;
    private readonly IReadOnlySet<string> _networkMetricNames;
    private readonly IReadOnlySet<string> _sensorGroups;
    private readonly IReadOnlySet<string> _sensorInfoSchemas;
    private readonly IReadOnlyDictionary<(string Type, string Name), string> _expectedSchemaByMetric;

    public CpuNetworkSensorValidator(CpuNetworkModel model)
    {
        _metricTypes        = model.Entities.GetStringEnumValues(CpuNetworkDtmis.Enums.MetricType);
        _cpuMetricNames     = model.Entities.GetStringEnumValues(CpuNetworkDtmis.Enums.CpuMetricName);
        _networkMetricNames = model.Entities.GetStringEnumValues(CpuNetworkDtmis.Enums.NetworkMetricName);
        _sensorGroups       = model.Entities.GetStringEnumValues(CpuNetworkDtmis.Enums.SensorGroup);
        _sensorInfoSchemas  = model.Entities.GetStringEnumValues(CpuNetworkDtmis.Enums.SensorInfoSchema);

        // Build (MetricType, MetricName) -> expected DTDL schema map from the Telemetries themselves.
        _expectedSchemaByMetric = BuildExpectedSchemaMap(model.Interface);
    }

    public ErrorOr<Success> Validate(SensorConfig sensor)
    {
        var errors = new List<Error>();

        if (!_metricTypes.Contains(sensor.Parameters.MetricType))
            errors.Add(Error.Validation("Sensor.MetricType",
                $"'{sensor.Parameters.MetricType}' not in allowed enum [{string.Join(", ", _metricTypes)}]."));

        IReadOnlySet<string>? allowedNames = sensor.Parameters.MetricType switch
        {
            "cpu"     => _cpuMetricNames,
            "network" => _networkMetricNames,
            _ => null
        };
        if (allowedNames is not null && !allowedNames.Contains(sensor.Parameters.MetricName))
            errors.Add(Error.Validation("Sensor.MetricName",
                $"'{sensor.Parameters.MetricName}' not valid for MetricType '{sensor.Parameters.MetricType}'. Allowed: [{string.Join(", ", allowedNames)}]."));

        if (sensor.SensorGroup is not null && !_sensorGroups.Contains(sensor.SensorGroup))
            errors.Add(Error.Validation("Sensor.SensorGroup",
                $"'{sensor.SensorGroup}' not in allowed enum [{string.Join(", ", _sensorGroups)}]."));

        if (!_sensorInfoSchemas.Contains(sensor.SensorInfo.Schema))
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"'{sensor.SensorInfo.Schema}' not in allowed enum [{string.Join(", ", _sensorInfoSchemas)}]."));

        // Cross-check: declared schema MUST match the DTDL Telemetry's schema for this MetricName.
        var key = (sensor.Parameters.MetricType, sensor.Parameters.MetricName);
        if (_expectedSchemaByMetric.TryGetValue(key, out var expected) &&
            !string.Equals(expected, sensor.SensorInfo.Schema, StringComparison.Ordinal))
        {
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"For ({sensor.Parameters.MetricType}, {sensor.Parameters.MetricName}) expected schema '{expected}', got '{sensor.SensorInfo.Schema}'."));
        }

        return errors.Count == 0 ? Result.Success : (ErrorOr<Success>)errors;
    }

    private static IReadOnlyDictionary<(string, string), string> BuildExpectedSchemaMap(DTInterfaceInfo iface)
    {
        // Hand-coded mapping: the DTDL `comment`/`description` fields embed the (MetricType, MetricName)
        // pair, but the cleanest reading is straight from CpuNetworkDtmis. Keep this in sync.
        return new Dictionary<(string, string), string>
        {
            { ("cpu", "usage"),            "double" },
            { ("cpu", "load1"),            "double" },
            { ("cpu", "load5"),            "double" },
            { ("cpu", "load15"),           "double" },
            { ("cpu", "context_switches"), "long"   },
            { ("network", "bytes_sent"),       "long" },
            { ("network", "bytes_received"),   "long" },
            { ("network", "packets_sent"),     "long" },
            { ("network", "packets_received"), "long" },
            { ("network", "errors"),           "long" },
            { ("network", "errors_in"),        "long" },
            { ("network", "errors_out"),       "long" },
        };
    }
}
```

Wire it into config loading:

```csharp
public sealed class DeviceConfigLoader
{
    private readonly CpuNetworkSensorValidator _validator;
    private readonly ILogger<DeviceConfigLoader> _logger;

    public DeviceConfigLoader(CpuNetworkSensorValidator validator, ILogger<DeviceConfigLoader> logger)
    {
        _validator = validator;
        _logger = logger;
    }

    public IReadOnlyList<SensorConfig> LoadAndValidate(string path)
    {
        var json   = File.ReadAllText(path);
        var doc    = JsonSerializer.Deserialize<DeviceConfigDocument>(json)!;
        var sensors = doc.DeviceConfigs.SystemAgentDeviceConfig.Sensors;

        var failed = 0;
        foreach (var s in sensors)
        {
            var result = _validator.Validate(s);
            if (result.IsError)
            {
                foreach (var e in result.Errors)
                    _logger.LogError("Sensor '{Name}' rejected: {Code} -- {Desc}", s.Name, e.Code, e.Description);
                failed++;
            }
        }

        if (failed > 0)
            throw new InvalidOperationException($"{failed} sensor(s) failed DTDL validation. Refusing to start.");

        return sensors;
    }
}
```

---

## 6. Source-generate the DTMI constants (optional)

For larger fleets you'll want to *generate* `CpuNetworkDtmis.cs` from the DTDL so the constants and the JSON can never drift. Sketch using a `IIncrementalGenerator`:

```csharp
// File: src/SystemAgent.SourceGen/DtdlConstantsGenerator.cs
[Generator]
public sealed class DtdlConstantsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var dtdlFiles = context.AdditionalTextsProvider
            .Where(t => t.Path.EndsWith(".dtdl.json", StringComparison.OrdinalIgnoreCase));

        context.RegisterSourceOutput(dtdlFiles, (spc, file) =>
        {
            var json = file.GetText()?.ToString();
            if (json is null) return;

            using var doc = JsonDocument.Parse(json);
            var ifaceDtmi = doc.RootElement.GetProperty("@id").GetString();
            var contents  = doc.RootElement.GetProperty("contents");

            var sb = new StringBuilder();
            sb.AppendLine("namespace SystemAgentExample.Schema;");
            sb.AppendLine("public static class GeneratedCpuNetworkDtmis {");
            sb.AppendLine($"    public const string Interface = \"{ifaceDtmi}\";");
            foreach (var t in contents.EnumerateArray())
            {
                var name = t.GetProperty("name").GetString();
                var id   = t.GetProperty("@id").GetString();
                sb.AppendLine($"    public const string {name} = \"{id}\";");
            }
            sb.AppendLine("}");

            spc.AddSource("GeneratedCpuNetworkDtmis.g.cs", sb.ToString());
        });
    }
}
```

```xml
<!-- *.csproj -->
<ItemGroup>
  <AdditionalFiles Include="docs/Metrics/01_CPU_NETWORK.dtdl.json" />
</ItemGroup>
```

---

## 7. Integration test — round-trip the DTDL through the parser

The DTDL is **config-only**: `contents[]` is empty by design. The asserts below check that the Interface parses, that it ships no Telemetries, and that each reusable Enum has the expected size.

```csharp
using DTDLParser;
using DTDLParser.Models;
using Xunit;

public sealed class CpuNetworkDtdlTests
{
    private const string DtdlPath = "docs/Metrics/01_SYSTEM_RESOURCE.dtdl.json";

    [Fact]
    public async Task Dtdl_parses_and_declares_no_telemetries()
    {
        var json   = await File.ReadAllTextAsync(DtdlPath);
        var parser = new ModelParser();
        var model  = await parser.ParseAsync(new[] { json });

        var iface = (DTInterfaceInfo)model[new Dtmi(CpuNetworkDtmis.Interface)];
        Assert.Empty(iface.Contents.Values.OfType<DTTelemetryInfo>());
    }

    [Theory]
    [InlineData(CpuNetworkDtmis.Enums.CpuMetricName, 5)]
    [InlineData(CpuNetworkDtmis.Enums.NetworkMetricName, 7)]
    [InlineData(CpuNetworkDtmis.Enums.SensorGroup, 7)]
    [InlineData(CpuNetworkDtmis.Enums.SensorInfoSchema, 5)]
    public async Task Enum_has_expected_value_count(string enumDtmi, int expected)
    {
        var json   = await File.ReadAllTextAsync(DtdlPath);
        var parser = new ModelParser();
        var model  = await parser.ParseAsync(new[] { json });

        Assert.Equal(expected, model.GetStringEnumValues(enumDtmi).Count);
    }
}
```

---

## 8. End-to-end sketch

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// 1. Load + parse DTDL once at startup.
builder.Services.AddSingleton<CpuNetworkModel>(_ =>
    CpuNetworkModel.LoadAsync().GetAwaiter().GetResult());

// 2. Build a config validator from the parsed Enums.
builder.Services.AddSingleton<CpuNetworkSensorValidator>();

// 3. Config loader rejects malformed devicecfg.json before the agent starts.
builder.Services.AddSingleton<DeviceConfigLoader>();

var host = builder.Build();

// On startup, validate every Sensor entry against the DTDL Enums and the
// (MetricType, MetricName) -> schema map. Crash on validation failure.
host.Services
    .GetRequiredService<DeviceConfigLoader>()
    .LoadAndValidate("/opt/system-agent/devicecfg.json");

await host.RunAsync();
```

The agent is now config-locked: bad `MetricName`s, wrong `SensorInfo.Schema`, or illegal `SensorGroup`s fail at config time — exactly the kind of fail-safe behaviour required by the SIL2 contract. Telemetry values are forwarded as-is; ingest-side validation owns the per-metric range / monotonicity / NaN-rejection rules documented in `*_FIELDS.md`.

---

## Related

- [`01_SYS_RES_CPU_NETWORK_FIELDS.md`](./01_SYS_RES_CPU_NETWORK_FIELDS.md) — the configuration field reference and DTDL schema this code consumes.
- [`01_SYSTEM_RESOURCE.dtdl.json`](./01_SYSTEM_RESOURCE.dtdl.json) — the source-of-truth DTDL Interface (config-only).
- `Protocols/SystemMetricsParser.cs` — the runtime parser that emits the `TelemetryMeasure` instances published by the examples above.
