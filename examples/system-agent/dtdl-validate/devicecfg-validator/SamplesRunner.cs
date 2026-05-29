using System.Text.Json;
using System.Text.Json.Nodes;

namespace Weda.DeviceCfg.Validator;

/// <summary>
/// Runs the samples-config fixture corpus through the per-MetricType devicecfg
/// schemas. Each fixture file (docs/Metrics/samples-config/*.configs.json) is a
/// JSON array of Sensor entries carrying two test-metadata fields -- "expect"
/// ("valid" | "invalid") and "reason". Each sensor is routed by
/// Parameters.MetricType to the matching devicecfg/{Type}Sensor.dtdl.json
/// schema, validated, and the schema verdict compared against "expect".
///
/// Outcomes per sensor:
///   MATCH    -- schema verdict == expect
///   STRICTER -- expect=valid  but schema=invalid (schema rejects a config the
///               agent tolerates, e.g. CSV-string interface lists)
///   LOOSER   -- expect=invalid but schema=valid (the rule is a cross-field /
///               runtime rule DTDL+extension cannot express)
///   SKIP     -- the sensor's MetricType has no devicecfg schema
///
/// STRICTER / LOOSER are the documented DTDL-vs-runtime gaps, not validator
/// defects -- the run is a report and always exits 0 (2 on a setup error).
/// </summary>
public static class SamplesRunner
{
    private static readonly Dictionary<string, string> SchemaByMetricType = new(StringComparer.Ordinal)
    {
        ["cpu"]               = "CpuSensor.dtdl.json",
        ["memory"]            = "MemorySensor.dtdl.json",
        ["disk"]              = "DiskSensor.dtdl.json",
        ["network"]           = "NetworkSensor.dtdl.json",
        ["system"]            = "SystemSensor.dtdl.json",
        ["gpu"]               = "GpuSensor.dtdl.json",
        ["hwinfo"]            = "HwinfoSensor.dtdl.json",
        ["temperature"]       = "TemperatureSensor.dtdl.json",
        ["voltage"]           = "VoltageSensor.dtdl.json",
        ["fanspeed"]          = "FanspeedSensor.dtdl.json",
        ["gpio"]              = "GpioSensor.dtdl.json",
        ["watchdog"]          = "WatchdogSensor.dtdl.json",
        ["thermalprotection"] = "ThermalProtectionSensor.dtdl.json",
    };

    public static int Run(string basePath, string samplesPath)
    {
        var devicecfgDir = Path.Combine(basePath, "devicecfg");

        List<string> fixtures;
        if (Directory.Exists(samplesPath))
            fixtures = Directory.GetFiles(samplesPath, "*.json")
                                .OrderBy(p => p, StringComparer.Ordinal).ToList();
        else if (File.Exists(samplesPath))
            fixtures = new List<string> { samplesPath };
        else { Console.Error.WriteLine($"ERR samples path not found: {samplesPath}"); return 2; }

        // Load + cache the per-MetricType schema roots (keep the docs alive).
        var schemaDocs  = new List<JsonDocument>();
        var schemaRoots = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (mt, file) in SchemaByMetricType)
        {
            var p = Path.Combine(devicecfgDir, file);
            if (!File.Exists(p)) continue;
            var doc = JsonDocument.Parse(File.ReadAllText(p));
            schemaDocs.Add(doc);
            schemaRoots[mt] = doc.RootElement;
        }

        Console.WriteLine("=== devicecfg-validator -- samples-config run ===");
        Console.WriteLine($"Schemas : devicecfg/  ({schemaRoots.Count} per-MetricType schemas loaded)");
        Console.WriteLine($"Samples : {samplesPath}  ({fixtures.Count} fixture file(s))");
        Console.WriteLine();

        int total = 0, match = 0, stricter = 0, looser = 0, skip = 0;

        foreach (var fixturePath in fixtures)
        {
            JsonDocument fdoc;
            try { fdoc = JsonDocument.Parse(File.ReadAllText(fixturePath)); }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"ERR {Path.GetFileName(fixturePath)}: malformed JSON: {ex.Message}");
                continue;
            }
            using (fdoc)
            {
                if (fdoc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    Console.Error.WriteLine($"ERR {Path.GetFileName(fixturePath)}: root is not a JSON array");
                    continue;
                }
                var sensors = fdoc.RootElement.EnumerateArray().ToArray();
                Console.WriteLine($"--- {Path.GetFileName(fixturePath)} ({sensors.Length} sensors) ---");

                foreach (var sensor in sensors)
                {
                    total++;
                    var name   = GetStr(sensor, "Name")   ?? "<unnamed>";
                    var expect = GetStr(sensor, "expect") ?? "?";
                    var mt = sensor.TryGetProperty("Parameters", out var pp)
                             && pp.TryGetProperty("MetricType", out var m)
                             && m.ValueKind == JsonValueKind.String
                             ? m.GetString() : null;

                    if (mt is null || !schemaRoots.TryGetValue(mt, out var root))
                    {
                        skip++;
                        Console.WriteLine($"  SKIP      expect={expect,-7}  {name}"
                                        + $"  (no devicecfg schema for MetricType '{mt ?? "<null>"}')");
                        continue;
                    }

                    using var sd = JsonDocument.Parse(StripMeta(sensor));
                    var result  = new SchemaValidator(root).Validate(sd.RootElement);
                    var verdict = result.Ok ? "valid" : "invalid";

                    if (verdict == expect)
                    {
                        match++;
                        Console.WriteLine($"  MATCH     expect={expect,-7}  {name}  schema={verdict}");
                    }
                    else if (expect == "valid")
                    {
                        stricter++;
                        var why = result.Errors.Count > 0 ? result.Errors[0] : "";
                        Console.WriteLine($"  STRICTER  expect={expect,-7}  {name}  schema=invalid: {why}");
                    }
                    else
                    {
                        looser++;
                        Console.WriteLine($"  LOOSER    expect={expect,-7}  {name}"
                                        + $"  schema=valid (rule not expressible in DTDL+extension)");
                    }
                }
                Console.WriteLine();
            }
        }
        foreach (var d in schemaDocs) d.Dispose();

        Console.WriteLine("=== summary ===");
        Console.WriteLine($"  total sensors : {total}");
        Console.WriteLine($"  MATCH         : {match}");
        Console.WriteLine($"  STRICTER      : {stricter}  (schema rejects an agent-tolerated config)");
        Console.WriteLine($"  LOOSER        : {looser}  (schema misses a cross-field / runtime rule)");
        Console.WriteLine($"  SKIP          : {skip}  (MetricType has no devicecfg schema)");
        return 0;
    }

    /// <summary>Re-serialize a fixture sensor without the test-metadata fields.</summary>
    private static string StripMeta(JsonElement sensor)
    {
        var node = JsonNode.Parse(sensor.GetRawText())!.AsObject();
        node.Remove("expect");
        node.Remove("reason");
        return node.ToJsonString();
    }

    private static string? GetStr(JsonElement o, string k)
        => o.ValueKind == JsonValueKind.Object && o.TryGetProperty(k, out var v)
           && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
