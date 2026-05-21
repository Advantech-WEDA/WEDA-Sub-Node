using System.Text.Json;
using DTDLParser;
using DTDLParser.Models;

namespace Weda.Dtdl.Validator;

/// <summary>
/// Validates the network rows in samples-config/01_SYSTEM_RESOURCE.configs.json
/// against the nested DTDL v2 Interface in dtdl/NetworkSensorConfig.v2.json.
///
/// The validator only uses DTDL-native checks: primitive type, Enum allow-list,
/// Object structure (recurses into Object Properties), and Array element type.
/// No comment-payload parsing, no ConfigRules, no cross-field logic.
///
/// Note about the v2 limit:  the v2 file drops the v1.1 Interfaces array (DTDL v2
/// forbids Array reachable from a Property's schema graph), so fixtures whose
/// JSON includes an Interfaces array still validate cleanly here because the
/// validator is lenient about extra JSON fields the DTDL doesn't declare.
/// </summary>
public static class NetworkV2Verifier
{
    private const string DtdlRelPath   = "dtdl/NetworkSensorConfig.v2.json";
    private const string CfgRelPath    = "samples-config/01_SYSTEM_RESOURCE.configs.json";
    private const string IfaceDtmi     = "dtmi:advantech:EdgeSync:SystemAgent:NetworkSensorConfig;1";
    private const string MetricTypeKey = "network";

    public static async Task<int> RunAsync()
    {
        var basePath = Program.ResolveBasePath();
        var dtdlPath = Path.Combine(basePath, DtdlRelPath);
        var cfgPath  = Path.Combine(basePath, CfgRelPath);

        if (!File.Exists(dtdlPath)) { Console.Error.WriteLine($"ERR DTDL not found: {dtdlPath}");  return 1; }
        if (!File.Exists(cfgPath))  { Console.Error.WriteLine($"ERR config not found: {cfgPath}"); return 1; }

        // Parse the DTDL file
        var dtdlJson = await File.ReadAllTextAsync(dtdlPath);
        var parser   = new ModelParser();
        var model    = await parser.ParseAsync(ToAsync(new[] { dtdlJson }));

        if (!model.TryGetValue(new Dtmi(IfaceDtmi), out var entity) || entity is not DTInterfaceInfo iface)
        {
            Console.Error.WriteLine($"ERR Interface {IfaceDtmi} not found in parsed model");
            return 1;
        }
        Console.WriteLine($"Loaded {Path.GetFileName(dtdlPath)} -- Interface {iface.Id}");
        Console.WriteLine($"  contents[]: {iface.Contents.Count} Properties");
        Console.WriteLine();

        // Pick the network rows out of the fixture
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(cfgPath));
        var rows = doc.RootElement.EnumerateArray()
            .Where(s => s.TryGetProperty("Parameters", out var p)
                     && p.TryGetProperty("MetricType", out var mt)
                     && mt.ValueKind == JsonValueKind.String
                     && mt.GetString() == MetricTypeKey)
            .ToArray();

        Console.WriteLine($"--- {Path.GetFileName(cfgPath)} (network rows: {rows.Length}) ---");

        int pass = 0, fail = 0, expectMismatch = 0;
        foreach (var row in rows)
        {
            var name = TryGetString(row, "Name") ?? "<unnamed>";
            var expect = TryGetString(row, "expect") ?? "?";

            var err = ValidateInterface(row, iface);
            var dtdlOk = err is null;
            if (dtdlOk) pass++; else fail++;

            // Compare to the fixture's declared expectation.  Strict-DTDL is allowed
            // to be stricter than the agent contract, so an 'expect: invalid' that
            // passes DTDL is still a layer divergence (printed) but NOT a mismatch
            // when the DTDL accepts it.  We only flag mismatches when DTDL OUTRIGHT
            // REJECTS something the fixture says should be valid.
            var verdict = (expect == "valid" && !dtdlOk) ? "MISMATCH" : "ok";
            if (verdict == "MISMATCH") expectMismatch++;

            var mark = dtdlOk ? "D+" : "D-";
            var line = $"  {mark}  expect={expect,-7}  {name}";
            if (err is not null) line += $"    -- {err}";
            Console.WriteLine(line);
        }

        Console.WriteLine();
        Console.WriteLine($"DTDL-native pass:        {pass}/{rows.Length}");
        Console.WriteLine($"DTDL-native fail:        {fail}/{rows.Length}");
        Console.WriteLine($"'expect=valid' rejected: {expectMismatch}  (DTDL stricter than the agent for these rows)");

        return 0;
    }

    /// <summary>
    /// Walks every Property declared on the Interface and applies DTDL-native
    /// checks against the same-named JSON key on the sensor row.  Returns null
    /// on success; a short reason string on the first failure.
    /// </summary>
    private static string? ValidateInterface(JsonElement sensor, DTInterfaceInfo iface)
    {
        if (sensor.ValueKind != JsonValueKind.Object)
            return $"top-level: expected object, got {sensor.ValueKind}";

        foreach (var kv in iface.Contents)
        {
            if (kv.Value is not DTPropertyInfo prop) continue;
            var pname = kv.Key;

            if (!sensor.TryGetProperty(pname, out var v) || v.ValueKind == JsonValueKind.Null
                                                         || v.ValueKind == JsonValueKind.Undefined)
                return $"Property '{pname}' missing";

            var err = CheckValue(v, prop.Schema, pname);
            if (err is not null) return err;
        }
        return null;
    }

    /// <summary>
    /// Native DTDL-shape check for one JSON value against a DTSchemaInfo.
    /// Recurses into Object Field schemas; lenient about extra JSON keys not
    /// declared on the Object (matches the existing verify-mode behaviour).
    /// </summary>
    private static string? CheckValue(JsonElement value, DTSchemaInfo schema, string path)
    {
        switch (schema.EntityKind)
        {
            case DTEntityKind.String:
                return value.ValueKind == JsonValueKind.String
                    ? null : $"{path}: expected string, got {value.ValueKind}";

            case DTEntityKind.Integer:
            case DTEntityKind.Long:
                if (value.ValueKind != JsonValueKind.Number) return $"{path}: expected integer, got {value.ValueKind}";
                return value.TryGetInt64(out _) ? null : $"{path}: expected integer (non-integral number)";

            case DTEntityKind.Double:
            case DTEntityKind.Float:
                return value.ValueKind == JsonValueKind.Number
                    ? null : $"{path}: expected number, got {value.ValueKind}";

            case DTEntityKind.Boolean:
                return (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                    ? null : $"{path}: expected boolean, got {value.ValueKind}";

            case DTEntityKind.Enum:
                {
                    var en = (DTEnumInfo)schema;
                    var allowed = en.EnumValues.Select(e => e.EnumValue?.ToString() ?? "").ToList();
                    var asStr = value.ValueKind switch
                    {
                        JsonValueKind.String => value.GetString() ?? "",
                        JsonValueKind.Number => value.GetRawText(),
                        _                    => value.GetRawText(),
                    };
                    return allowed.Contains(asStr) ? null
                        : $"{path}: '{asStr}' not in enum [{string.Join(", ", allowed)}]";
                }

            case DTEntityKind.Object:
                {
                    var obj = (DTObjectInfo)schema;
                    if (value.ValueKind != JsonValueKind.Object)
                        return $"{path}: expected object, got {value.ValueKind}";
                    foreach (var f in obj.Fields)
                    {
                        if (!value.TryGetProperty(f.Name, out var fv)
                            || fv.ValueKind == JsonValueKind.Null
                            || fv.ValueKind == JsonValueKind.Undefined)
                            return $"{path}.{f.Name}: missing";
                        var err = CheckValue(fv, f.Schema, $"{path}.{f.Name}");
                        if (err is not null) return err;
                    }
                    return null;
                }

            case DTEntityKind.Array:
                {
                    var arr = (DTArrayInfo)schema;
                    if (value.ValueKind != JsonValueKind.Array)
                        return $"{path}: expected array, got {value.ValueKind}";
                    int i = 0;
                    foreach (var elem in value.EnumerateArray())
                    {
                        var err = CheckValue(elem, arr.ElementSchema, $"{path}[{i}]");
                        if (err is not null) return err;
                        i++;
                    }
                    return null;
                }

            default:
                return $"{path}: unsupported schema kind {schema.EntityKind}";
        }
    }

    private static string? TryGetString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object
        && obj.TryGetProperty(name, out var v)
        && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static async IAsyncEnumerable<string> ToAsync(IEnumerable<string> source)
    {
        await Task.Yield();
        foreach (var item in source) yield return item;
    }
}
