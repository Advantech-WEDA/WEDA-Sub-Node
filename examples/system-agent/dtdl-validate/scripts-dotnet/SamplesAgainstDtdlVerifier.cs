using System.Text.Json;
using DTDLParser;
using DTDLParser.Models;

namespace Weda.Dtdl.Validator;

/// <summary>
/// Combined verification:
///   (a) Loads every Interface JSON in DTDL_BASE/dtdl/, parses with the
///       Microsoft DTDLParser, and validates each samples-config Sensor row
///       against the matching {MetricType}SensorConfig Interface using the
///       flat Property surface (Name, SensorGroup, MetricType, MetricName,
///       MountPoint, Interface(s), Source(s), PinId(s), Enabled, Interval,
///       Description, DisplayName, Schema) mapped to nested devicecfg.json
///       paths.  Native DTDL covers: primitive type, Enum allow-list, Array
///       element type, required-presence.
///   (b) Runs the existing <see cref="ConfigRules"/> against the same row to
///       cover what DTDL can't natively express (range, uniqueness, mutex,
///       conditional-required, Schema/MetricName cross-consistency).
///
/// The fixture verdict reflects the agent contract (ConfigRules); the DTDL
/// verdict is reported alongside.  Layer divergences are listed for
/// transparency.
///
/// Mirror of scripts/verify-samples-against-dtdl.js.
/// </summary>
public static class SamplesAgainstDtdlVerifier
{
    /// <summary>Parameters.MetricType -&gt; expected Interface name suffix.</summary>
    private static readonly IReadOnlyDictionary<string, string> MetricToIface =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cpu"]               = "CpuSensorConfig",
            ["memory"]            = "MemorySensorConfig",
            ["disk"]              = "DiskSensorConfig",
            ["network"]           = "NetworkSensorConfig",
            ["system"]            = "SystemSensorConfig",
            ["gpu"]               = "GpuSensorConfig",
            ["hwinfo"]            = "HwinfoSensorConfig",
            ["temperature"]       = "TemperatureSensorConfig",
            ["voltage"]           = "VoltageSensorConfig",
            ["fanspeed"]          = "FanspeedSensorConfig",
            ["gpio"]              = "GpioSensorConfig",
            ["watchdog"]          = "WatchdogSensorConfig",
            ["thermalprotection"] = "ThermalProtectionSensorConfig",
        };

    /// <summary>
    /// Properties that may legitimately be absent on a Sensor entry.
    /// Required-vs-optional for these is decided by ConfigRules, not by DTDL.
    /// </summary>
    private static readonly HashSet<string> OptionalProps = new(StringComparer.Ordinal)
    {
        "MountPoint",
        "Interface", "Interfaces",
        "Source",  "Sources",
        "PinId",   "PinIds",
    };

    public static async Task<int> RunAsync()
    {
        var basePath = Program.ResolveBasePath();
        var dtdlDir  = Path.Combine(basePath, "dtdl");
        var cfgDir   = Path.Combine(basePath, "samples-config");

        if (!Directory.Exists(dtdlDir))
        {
            Console.Error.WriteLine($"ERR DTDL dir not found: {dtdlDir}");
            return 1;
        }
        if (!Directory.Exists(cfgDir))
        {
            Console.Error.WriteLine($"ERR samples-config dir not found: {cfgDir}");
            return 1;
        }

        var dtdlFiles = Directory.GetFiles(dtdlDir, "*.json")
                                 .OrderBy(p => p, StringComparer.Ordinal)
                                 .ToArray();
        var jsons = await Task.WhenAll(dtdlFiles.Select(f => File.ReadAllTextAsync(f)));

        var parser = new ModelParser();
        var model = await parser.ParseAsync(ToAsync(jsons));

        var interfaces = model.Values.OfType<DTInterfaceInfo>()
            .ToDictionary(i => LastSegment(i.Id), StringComparer.Ordinal);

        Console.WriteLine($"Loaded {interfaces.Count} Interfaces from {dtdlDir}");

        var fixtureFiles = Directory.GetFiles(cfgDir, "*.json")
                                    .OrderBy(p => p, StringComparer.Ordinal)
                                    .ToArray();

        int total = 0, mismatches = 0, dtdlPass = 0, rulesPass = 0;
        var divergences = new List<string>();

        foreach (var fp in fixtureFiles)
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(fp));
            var sensors = doc.RootElement.EnumerateArray().ToArray();
            Console.WriteLine();
            Console.WriteLine($"--- {Path.GetFileName(fp)} ({sensors.Length} sensors) ---");

            foreach (var s in sensors)
            {
                total++;
                var name = TryGetString(s, "Name") ?? "<unnamed>";
                var expect = TryGetString(s, "expect");
                var expected = expect == "valid";
                var mt = TryGetString(GetPath(s, "Parameters", "MetricType"));

                bool dtdlOk = true;
                string dtdlReason = "";

                if (mt is null || !MetricToIface.TryGetValue(mt, out var ifaceName))
                {
                    dtdlOk = false;
                    dtdlReason = $"unknown MetricType '{mt ?? "<null>"}'";
                }
                else if (!interfaces.TryGetValue(ifaceName, out var iface))
                {
                    dtdlOk = false;
                    dtdlReason = $"Interface {ifaceName} not in model";
                }
                else
                {
                    foreach (var kv in iface.Contents)
                    {
                        if (kv.Value is not DTPropertyInfo prop) continue;
                        var pname = kv.Key;
                        var value = GetSensorProp(s, pname);
                        if (value is null || value.Value.ValueKind == JsonValueKind.Null
                                          || value.Value.ValueKind == JsonValueKind.Undefined)
                        {
                            if (OptionalProps.Contains(pname)) continue;
                            dtdlOk = false;
                            dtdlReason = $"Property '{pname}' missing";
                            break;
                        }
                        var err = CheckValue(value.Value, prop.Schema);
                        if (err is not null)
                        {
                            dtdlOk = false;
                            dtdlReason = $"Property '{pname}': {err}";
                            break;
                        }
                    }
                }
                if (dtdlOk) dtdlPass++;

                ConfigRules.RuleResult rulesResult;
                try { rulesResult = ConfigRules.Validate(StripFixtureFields(s)); }
                catch (Exception ex) { rulesResult = ConfigRules.RuleResult.Fail($"rule threw: {ex.Message}"); }
                if (rulesResult.Ok) rulesPass++;

                var agentOk = rulesResult.Ok;
                var verdictOk = agentOk == expected;

                var label = (expect ?? "?").PadRight(7);
                var dtdlMark  = dtdlOk        ? "D+" : "D-";
                var rulesMark = rulesResult.Ok ? "R+" : "R-";
                var line = $"  {(verdictOk ? "OK " : "ERR")} {label} {dtdlMark} {rulesMark}  {name}";

                if (verdictOk) Console.WriteLine(line);
                else
                {
                    var got = agentOk ? "valid" : "invalid";
                    var rr  = rulesResult.Reason is null ? "" : $": {rulesResult.Reason}";
                    Console.Error.WriteLine($"{line}    (agent verdict: {got}{rr})");
                    mismatches++;
                }

                if (dtdlOk != agentOk)
                {
                    var dtdlMsg  = dtdlOk         ? "pass" : $"fail ({dtdlReason})";
                    var rulesMsg = rulesResult.Ok ? "pass" : $"fail ({rulesResult.Reason ?? ""})";
                    divergences.Add(
                        $"    [{Path.GetFileName(fp)}] {name}  expect={expect ?? "?"}  dtdl={dtdlMsg}  rules={rulesMsg}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total sensors:              {total}");
        Console.WriteLine($"DTDL-native pass:           {dtdlPass}/{total}");
        Console.WriteLine($"config-rules pass:          {rulesPass}/{total}");
        Console.WriteLine($"Fixture verdict mismatches: {mismatches}");

        if (divergences.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Layer divergences ({divergences.Count}):  DTDL strict vs config-rules tolerant.");
            Console.WriteLine("  These are EXPECTED for fixtures that exercise wire-format tolerances or hardware-source variants.");
            foreach (var d in divergences) Console.WriteLine(d);
        }

        return mismatches == 0 ? 0 : 1;
    }

    /// <summary>
    /// Returns the last DTMI segment before ";version", matching the
    /// Interface-name lookup keys ("CpuSensorConfig", etc.).
    /// </summary>
    private static string LastSegment(Dtmi id)
    {
        var s = id.AbsoluteUri;
        var semi = s.IndexOf(';');
        if (semi >= 0) s = s.Substring(0, semi);
        var colon = s.LastIndexOf(':');
        return colon >= 0 ? s.Substring(colon + 1) : s;
    }

    /// <summary>
    /// Map a flat Property name on the *SensorConfig Interface to the nested
    /// JSON path on a devicecfg.json Sensor entry.
    /// </summary>
    private static JsonElement? GetSensorProp(JsonElement sensor, string prop) => prop switch
    {
        "Name"        => GetPath(sensor, "Name"),
        "SensorGroup" => GetPath(sensor, "SensorGroup"),
        "MetricType"  => GetPath(sensor, "Parameters", "MetricType"),
        "MetricName"  => GetPath(sensor, "Parameters", "MetricName"),
        "MountPoint"  => GetPath(sensor, "Parameters", "MountPoint"),
        "Interface"   => GetPath(sensor, "Parameters", "Interface"),
        "Interfaces"  => GetPath(sensor, "Parameters", "Interfaces"),
        "Source"      => GetPath(sensor, "Parameters", "Source"),
        "Sources"     => GetPath(sensor, "Parameters", "Sources"),
        "PinId"       => GetPath(sensor, "Parameters", "PinId"),
        "PinIds"      => GetPath(sensor, "Parameters", "PinIds"),
        "Enabled"     => GetPath(sensor, "Report", "Enabled"),
        "Interval"    => GetPath(sensor, "Report", "Interval"),
        "Description" => GetPath(sensor, "SensorInfo", "Description"),
        "DisplayName" => GetPath(sensor, "SensorInfo", "DisplayName"),
        "Schema"      => GetPath(sensor, "SensorInfo", "Schema"),
        _             => null,
    };

    private static JsonElement? GetPath(JsonElement root, params string[] path)
    {
        var cur = root;
        foreach (var seg in path)
        {
            if (cur.ValueKind != JsonValueKind.Object) return null;
            if (!cur.TryGetProperty(seg, out var next)) return null;
            cur = next;
        }
        return cur;
    }

    private static string? TryGetString(JsonElement? e) =>
        e is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static string? TryGetString(JsonElement e, string prop) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>
    /// Native DTDL-shape check for a single Property's JSON value.
    /// Returns null on success, a short reason on failure.
    /// </summary>
    private static string? CheckValue(JsonElement value, DTSchemaInfo schema)
    {
        var kind = schema.EntityKind;
        switch (kind)
        {
            case DTEntityKind.String:
                if (value.ValueKind != JsonValueKind.String)
                    return $"expected string, got {value.ValueKind}";
                return null;

            case DTEntityKind.Integer:
            case DTEntityKind.Long:
                if (value.ValueKind != JsonValueKind.Number) return $"expected integer, got {value.ValueKind}";
                if (!value.TryGetInt64(out _)) return "expected integer (non-integral number)";
                return null;

            case DTEntityKind.Double:
            case DTEntityKind.Float:
                if (value.ValueKind != JsonValueKind.Number) return $"expected number, got {value.ValueKind}";
                return null;

            case DTEntityKind.Boolean:
                if (value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                    return $"expected boolean, got {value.ValueKind}";
                return null;

            case DTEntityKind.Enum:
                {
                    var en = (DTEnumInfo)schema;
                    var allowed = en.EnumValues.Select(e => e.EnumValue?.ToString() ?? "").ToList();
                    var asStr = value.ValueKind switch
                    {
                        JsonValueKind.String => value.GetString() ?? "",
                        JsonValueKind.Number => value.GetRawText(),
                        _ => value.GetRawText(),
                    };
                    return allowed.Contains(asStr) ? null : $"not in enum [{string.Join(", ", allowed)}]";
                }

            case DTEntityKind.Array:
                {
                    if (value.ValueKind != JsonValueKind.Array)
                        return $"expected array, got {value.ValueKind}";
                    var arr = (DTArrayInfo)schema;
                    var i = 0;
                    foreach (var elem in value.EnumerateArray())
                    {
                        var err = CheckValue(elem, arr.ElementSchema);
                        if (err is not null) return $"element[{i}] {err}";
                        i++;
                    }
                    return null;
                }

            default:
                return $"unsupported schema kind {kind}";
        }
    }

    /// <summary>
    /// Re-serializes the row stripped of fixture-only fields ("expect",
    /// "reason") so ConfigRules sees the same shape it sees from
    /// ConfigsValidator's <c>Entry.Sensor</c> path.
    /// </summary>
    private static JsonElement StripFixtureFields(JsonElement row)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var p in row.EnumerateObject())
        {
            if (p.Name is "expect" or "reason") continue;
            dict[p.Name] = JsonSerializer.Deserialize<object?>(p.Value.GetRawText());
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(dict);
        return JsonDocument.Parse(bytes).RootElement.Clone();
    }

    private static async IAsyncEnumerable<string> ToAsync(IEnumerable<string> source)
    {
        await Task.Yield();
        foreach (var item in source) yield return item;
    }
}
