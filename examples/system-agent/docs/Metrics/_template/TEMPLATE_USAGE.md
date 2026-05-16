# Using `{{NN}}_{{NAME_UPPER}}.dtdl.json` from .NET / C#

End-to-end examples for consuming [`{{NN}}_{{NAME_UPPER}}.dtdl.json`](./{{NN}}_{{NAME_UPPER}}.dtdl.json) from a C# application. Targets **.NET 8+** with the official `DTDLParser` package. Read [`01_CPU_NETWORK_USAGE.md`](./01_CPU_NETWORK_USAGE.md) first for the shared helpers and overall pattern; this doc focuses on the deltas for {{name_human}}.

| Asset | Where it lives in the JSON | Use in C# |
|-------|---------------------------|-----------|
| {{N}} Telemetry definitions | `contents[]` | DTMI constants; payload schema lookup; validation-rule extraction. |
| {{M}} reusable Enums | `schemas[]` | Allow-list validators for `Sensor.Parameters.MetricType`, per-type `MetricName`, `SensorGroup`, `SensorInfo.Schema`. |

Key things to mention here (delete what doesn't apply):

- {{Type-specific extra parameter ({{extra_param_name}}, e.g. MountPoint) — validator must enforce it.}}
- {{v1.1 Sensor Expansion via {{expansion_param_singular}} / {{expansion_param_plural}} — uses ResourceId discriminator suffix at publish time.}}
- {{Platform-dependent — null-suppressed on unsupported hosts; SIL2 fail-safe.}}
- {{Semantic types {{SemanticType1}} (unit {{unit1}}) applied to the byte/temperature/voltage/etc. Telemetries.}}

---

## 1. Add the parser package

Skip if you already added it for an earlier metric category.

```bash
dotnet add package DTDLParser
```

```xml
<ItemGroup>
  <EmbeddedResource Include="docs/Metrics/{{NN}}_{{NAME_UPPER}}.dtdl.json" />
</ItemGroup>
```

---

## 2. Strongly-typed DTMI constants

```csharp
// File: src/SystemAgent/Schema/{{Name_Pascal}}Dtmis.cs
namespace SystemAgentExample.Schema;

public static class {{Name_Pascal}}Dtmis
{
    public const string Interface =
        "dtmi:advantech:WEDA:SystemAgent:{{Name_Pascal}};1";

    public static class Telemetries
    {
        public const string {{MetricType1Pascal}}{{MetricNameAPascal}} = "dtmi:advantech:WEDA:SystemInfo:{{MetricType1Pascal}}{{MetricNameAPascal}};1";
        public const string {{MetricType1Pascal}}{{MetricNameBPascal}} = "dtmi:advantech:WEDA:SystemInfo:{{MetricType1Pascal}}{{MetricNameBPascal}};1";
        // ... one per Telemetry in the DTDL file
    }

    public static class Enums
    {
        public const string MetricType                  = "dtmi:advantech:WEDA:SystemAgent:{{Name_Pascal}}:MetricType;1";
        public const string {{MetricType1Pascal}}MetricName = "dtmi:advantech:WEDA:SystemAgent:{{Name_Pascal}}:{{MetricType1Pascal}}MetricName;1";
        public const string SensorGroup                 = "dtmi:advantech:WEDA:SystemAgent:{{Name_Pascal}}:SensorGroup;1";
        public const string SensorInfoSchema            = "dtmi:advantech:WEDA:SystemAgent:{{Name_Pascal}}:SensorInfoSchema;1";
    }
}
```

---

## 3. Load and parse

```csharp
using DTDLParser;
using DTDLParser.Models;
using System.Reflection;

public sealed class {{Name_Pascal}}Model
{
    public IReadOnlyDictionary<Dtmi, DTEntityInfo> Entities { get; }
    public DTInterfaceInfo Interface { get; }

    private {{Name_Pascal}}Model(IReadOnlyDictionary<Dtmi, DTEntityInfo> entities)
    {
        Entities = entities;
        Interface = (DTInterfaceInfo)entities[new Dtmi({{Name_Pascal}}Dtmis.Interface)];
    }

    public static async Task<{{Name_Pascal}}Model> LoadAsync(CancellationToken ct = default)
    {
        var asm = Assembly.GetExecutingAssembly();
        await using var stream = asm.GetManifestResourceStream(
            "SystemAgentExample.docs.Metrics.{{NN}}_{{NAME_UPPER}}_dtdl.json")
            ?? throw new InvalidOperationException("DTDL resource not embedded.");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);

        var parser = new ModelParser();
        var entities = await parser.ParseAsync(new[] { json }, cancellationToken: ct);
        return new {{Name_Pascal}}Model(entities);
    }
}
```

---

## 4. Validate a Sensor against the DTDL

```csharp
using ErrorOr;

public sealed class {{Name_Pascal}}SensorValidator
{
    private readonly IReadOnlySet<string> _metricTypes;
    private readonly IReadOnlySet<string> _{{metricType1lower}}MetricNames;
    private readonly IReadOnlySet<string> _sensorGroups;
    private readonly IReadOnlySet<string> _sensorInfoSchemas;
    private readonly IReadOnlyDictionary<(string Type, string Name), string> _expectedSchemaByMetric;

    public {{Name_Pascal}}SensorValidator({{Name_Pascal}}Model model)
    {
        _metricTypes       = model.Entities.GetStringEnumValues({{Name_Pascal}}Dtmis.Enums.MetricType);
        _{{metricType1lower}}MetricNames = model.Entities.GetStringEnumValues({{Name_Pascal}}Dtmis.Enums.{{MetricType1Pascal}}MetricName);
        _sensorGroups      = model.Entities.GetStringEnumValues({{Name_Pascal}}Dtmis.Enums.SensorGroup);
        _sensorInfoSchemas = model.Entities.GetStringEnumValues({{Name_Pascal}}Dtmis.Enums.SensorInfoSchema);

        _expectedSchemaByMetric = new Dictionary<(string, string), string>
        {
            { ("{{metric_type_1}}", "{{metric_name_a}}"), "{{schema_a}}" },
            { ("{{metric_type_1}}", "{{metric_name_b}}"), "{{schema_b}}" },
            // ... one row per (MetricType, MetricName) pair
        };
    }

    public ErrorOr<Success> Validate(SensorConfig sensor)
    {
        var errors = new List<Error>();
        var p = sensor.Parameters;

        if (!_metricTypes.Contains(p.MetricType))
            errors.Add(Error.Validation("Sensor.MetricType", $"'{p.MetricType}' not in allowed enum."));

        if (p.MetricType == "{{metric_type_1}}" && !_{{metricType1lower}}MetricNames.Contains(p.MetricName))
            errors.Add(Error.Validation("Sensor.MetricName",
                $"'{p.MetricName}' not valid for MetricType '{{metric_type_1}}'."));

        // Extra parameter check (delete if N/A):
        // if (p.MetricType == "{{metric_type_1}}" && string.IsNullOrWhiteSpace(p.{{extra_param_name}}))
        //     errors.Add(Error.Validation("Sensor.Parameters.{{extra_param_name}}",
        //         "{{metric_type_1}} sensors require a non-empty '{{extra_param_name}}'."));

        if (sensor.SensorGroup is not null && !_sensorGroups.Contains(sensor.SensorGroup))
            errors.Add(Error.Validation("Sensor.SensorGroup", "..."));

        if (!_sensorInfoSchemas.Contains(sensor.SensorInfo.Schema))
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema", "..."));

        if (_expectedSchemaByMetric.TryGetValue((p.MetricType, p.MetricName), out var expected) &&
            !string.Equals(expected, sensor.SensorInfo.Schema, StringComparison.Ordinal))
        {
            errors.Add(Error.Validation("Sensor.SensorInfo.Schema",
                $"For ({p.MetricType}, {p.MetricName}) expected '{expected}', got '{sensor.SensorInfo.Schema}'."));
        }

        return errors.Count == 0 ? Result.Success : (ErrorOr<Success>)errors;
    }
}
```

---

## 5. Value validators driven by the DTDL `comment` field

```csharp
public sealed class TelemetryValueValidator
{
    private static readonly Dictionary<string, Func<object?, bool>> Predicates = new()
    {
        [{{Name_Pascal}}Dtmis.Telemetries.{{MetricType1Pascal}}{{MetricNameAPascal}}] = v =>
            // Implement the comment's validation rule here, e.g.
            v is {{C# type for schema_a}} x && /* bounds check */ true,
        // ... one entry per Telemetry
    };

    public bool IsValid(string telemetryDtmi, object? value) =>
        Predicates.TryGetValue(telemetryDtmi, out var fn) && fn(value);
}
```

---

## 6. Publish a `TelemetryMeasure` using a DTMI as `ResourceId`

```csharp
using Weda.SubNode.Abstractions.Telemetry;

public sealed class {{Name_Pascal}}Publisher
{
    private readonly ICommunication _communication;
    private readonly TelemetryValueValidator _validator;
    private readonly ILogger<{{Name_Pascal}}Publisher> _logger;

    public {{Name_Pascal}}Publisher(
        ICommunication communication,
        TelemetryValueValidator validator,
        ILogger<{{Name_Pascal}}Publisher> logger)
    {
        _communication = communication;
        _validator     = validator;
        _logger        = logger;
    }

    public Task Publish{{MetricType1Pascal}}{{MetricNameAPascal}}Async({{C# type}} value, CancellationToken ct = default)
        => PublishAsync({{Name_Pascal}}Dtmis.Telemetries.{{MetricType1Pascal}}{{MetricNameAPascal}}, value, ct);

    private async Task PublishAsync(string dtmi, object value, CancellationToken ct, string? discriminator = null)
    {
        if (!_validator.IsValid(dtmi, value))
        {
            _logger.LogWarning("Dropping {Dtmi}={Value}", dtmi, value);
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

## 7. Integration test

```csharp
using DTDLParser;
using Xunit;

public sealed class {{Name_Pascal}}DtdlTests
{
    [Fact]
    public async Task Dtdl_parses_without_errors()
    {
        var json   = await File.ReadAllTextAsync("docs/Metrics/{{NN}}_{{NAME_UPPER}}.dtdl.json");
        var parser = new ModelParser();
        var model  = await parser.ParseAsync(new[] { json });

        var iface = (DTInterfaceInfo)model[new Dtmi({{Name_Pascal}}Dtmis.Interface)];
        Assert.Equal({{N}}, iface.Contents.Values.OfType<DTTelemetryInfo>().Count());
    }

    [Theory]
    [InlineData({{Name_Pascal}}Dtmis.Enums.MetricType,                          {{ N1 }})]
    [InlineData({{Name_Pascal}}Dtmis.Enums.{{MetricType1Pascal}}MetricName,     {{ N2 }})]
    [InlineData({{Name_Pascal}}Dtmis.Enums.SensorGroup,                         7)]
    [InlineData({{Name_Pascal}}Dtmis.Enums.SensorInfoSchema,                    5)]
    public async Task Enum_has_expected_value_count(string enumDtmi, int expected)
    {
        var json   = await File.ReadAllTextAsync("docs/Metrics/{{NN}}_{{NAME_UPPER}}.dtdl.json");
        var model  = await new ModelParser().ParseAsync(new[] { json });
        Assert.Equal(expected, model.GetStringEnumValues(enumDtmi).Count);
    }
}
```

---

## 8. End-to-end sketch

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(_ => {{Name_Pascal}}Model.LoadAsync().GetAwaiter().GetResult());
builder.Services.AddSingleton<{{Name_Pascal}}SensorValidator>();
builder.Services.AddSingleton<TelemetryValueValidator>();
builder.Services.AddSingleton<{{Name_Pascal}}Publisher>();

var host = builder.Build();
await host.RunAsync();
```

---

## Related

- [`{{NN}}_{{NAME_UPPER}}_FIELDS.md`](./{{NN}}_{{NAME_UPPER}}_FIELDS.md) — the configuration field reference this code consumes.
- [`{{NN}}_{{NAME_UPPER}}.dtdl.json`](./{{NN}}_{{NAME_UPPER}}.dtdl.json) — the source-of-truth DTDL Interface.
- [`01_CPU_NETWORK_USAGE.md`](./01_CPU_NETWORK_USAGE.md) — the canonical USAGE doc with the shared helpers (`GetStringEnumValues`, `MonotonicCounterValidator`).
