using System.Text.Json;

namespace Weda.DeviceCfg.Validator;

/// <summary>
/// CLI entry point for the standalone devicecfg validator.
///
/// Usage: devicecfg-validator [payload.json] [--schema &lt;dtdl&gt;] [--at &lt;dotpath&gt;]
///        devicecfg-validator --samples [path]
///   payload   target JSON file   (default: devicecfg/samples/valid.json)
///   --schema  DTDL schema file   (default: devicecfg/SystemAgentDeviceConfig.dtdl.json)
///   --at      dot-path into JSON (default: DeviceConfigs.SystemAgentDeviceConfig)
///   --samples run the samples-config fixture corpus -- each sensor routed by
///             MetricType to its devicecfg/{Type}Sensor schema (default path:
///             samples-config/). See SamplesRunner.
///
/// Non-absolute paths resolve relative to DTDL_BASE (env var; the docs/Metrics
/// directory). Exit: 0 valid, 1 invalid, 2 setup error.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string? payloadArg = null, schemaArg = null, atArg = null, samplesArg = null;
        var samplesMode = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--schema" when i + 1 < args.Length: schemaArg = args[++i]; break;
                case "--at"     when i + 1 < args.Length: atArg     = args[++i]; break;
                case "--samples":
                    samplesMode = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) samplesArg = args[++i];
                    break;
                case "-h" or "--help": PrintUsage(); return 0;
                default:
                    if (payloadArg is null) payloadArg = args[i];
                    break;
            }
        }

        var basePath = Environment.GetEnvironmentVariable("DTDL_BASE");
        if (string.IsNullOrEmpty(basePath)) basePath = Directory.GetCurrentDirectory();
        var devicecfgDir = Path.Combine(basePath, "devicecfg");

        if (samplesMode)
        {
            var samplesPath = Resolve(samplesArg, basePath, Path.Combine(basePath, "samples-config"));
            return SamplesRunner.Run(basePath, samplesPath);
        }

        var schemaPath  = Resolve(schemaArg,  basePath,
                              Path.Combine(devicecfgDir, "SystemAgentDeviceConfig.dtdl.json"));
        var payloadPath = Resolve(payloadArg, basePath,
                              Path.Combine(devicecfgDir, "samples", "valid.json"));
        var atPath      = atArg ?? "DeviceConfigs.SystemAgentDeviceConfig";

        if (!File.Exists(schemaPath))  { Console.Error.WriteLine($"ERR schema not found: {schemaPath}");  return 2; }
        if (!File.Exists(payloadPath)) { Console.Error.WriteLine($"ERR payload not found: {payloadPath}"); return 2; }

        JsonDocument schemaDoc, payloadDoc;
        try
        {
            schemaDoc  = JsonDocument.Parse(File.ReadAllText(schemaPath));
            payloadDoc = JsonDocument.Parse(File.ReadAllText(payloadPath));
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"ERR malformed JSON: {ex.Message}");
            return 2;
        }

        using (schemaDoc)
        using (payloadDoc)
        {
            if (!TryResolvePath(payloadDoc.RootElement, atPath, out var payload)
                || payload.ValueKind != JsonValueKind.Object)
            {
                Console.Error.WriteLine($"ERR payload has no object at path '{atPath}'");
                return 2;
            }

            var schemaRoot = schemaDoc.RootElement;
            var ifaceId = schemaRoot.TryGetProperty("@id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                          ? idEl.GetString() : "(unknown)";

            Console.WriteLine("=== devicecfg-validator (.NET) ===");
            Console.WriteLine($"Schema : {Path.GetFileName(schemaPath)}  ({ifaceId})");
            Console.WriteLine($"Target : {Path.GetFileName(payloadPath)} -> {(atPath.Length == 0 ? "(root)" : atPath)}");
            Console.WriteLine();

            var result = new SchemaValidator(schemaRoot).Validate(payload);

            Console.WriteLine(result.Errors.Count > 0 ? $"ERRORS ({result.Errors.Count}):" : "ERRORS (0): none");
            foreach (var e in result.Errors) Console.WriteLine($"  - {e}");
            Console.WriteLine();

            if (result.Warnings.Count > 0)
            {
                Console.WriteLine($"WARNINGS ({result.Warnings.Count}):");
                foreach (var w in result.Warnings) Console.WriteLine($"  - {w}");
                Console.WriteLine();
            }

            var writable  = result.WritableMap.Where(kv =>  kv.Value).Select(kv => kv.Key);
            var immutable = result.WritableMap.Where(kv => !kv.Value).Select(kv => kv.Key);
            Console.WriteLine("WRITABLE MAP (native `writable` + ConfigConstraint.writable):");
            Console.WriteLine($"  writable  : {string.Join(", ", writable)}");
            Console.WriteLine($"  immutable : {string.Join(", ", immutable)}");
            Console.WriteLine();

            if (result.Defaults.Count > 0)
            {
                Console.WriteLine("DEFAULTS (ConfigConstraint.default -- form pre-fill hints):");
                foreach (var kv in result.Defaults) Console.WriteLine($"  {kv.Key} = {kv.Value}");
                Console.WriteLine();
            }

            Console.WriteLine($"RESULT: {(result.Ok ? "VALID" : "INVALID")}");
            return result.Ok ? 0 : 1;
        }
    }

    private static string Resolve(string? arg, string basePath, string fallback)
        => string.IsNullOrEmpty(arg) ? fallback
         : Path.IsPathRooted(arg) ? arg : Path.GetFullPath(Path.Combine(basePath, arg));

    private static bool TryResolvePath(JsonElement root, string dotPath, out JsonElement result)
    {
        result = root;
        if (dotPath.Length == 0) return true;
        var cur = root;
        foreach (var seg in dotPath.Split('.'))
        {
            if (cur.ValueKind == JsonValueKind.Object)
            {
                if (!cur.TryGetProperty(seg, out cur)) return false;
            }
            else if (cur.ValueKind == JsonValueKind.Array && int.TryParse(seg, out var idx))
            {
                if (idx < 0 || idx >= cur.GetArrayLength()) return false;
                cur = cur[idx];
            }
            else return false;
        }
        result = cur;
        return true;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("devicecfg-validator -- validate a devicecfg.json payload against a");
        Console.WriteLine("ConfigConstraint-annotated DTDL schema (docs/Metrics/devicecfg/*.dtdl.json).");
        Console.WriteLine();
        Console.WriteLine("Usage: devicecfg-validator [payload.json] [--schema <dtdl>] [--at <dotpath>]");
        Console.WriteLine("       devicecfg-validator --samples [path]");
        Console.WriteLine("  payload   target JSON file   (default: devicecfg/samples/valid.json)");
        Console.WriteLine("  --schema  DTDL schema file   (default: devicecfg/SystemAgentDeviceConfig.dtdl.json)");
        Console.WriteLine("  --at      dot-path into JSON (default: DeviceConfigs.SystemAgentDeviceConfig)");
        Console.WriteLine("  --samples run the samples-config fixture corpus; each sensor is routed by");
        Console.WriteLine("            MetricType to its devicecfg/{Type}Sensor schema (default: samples-config/)");
        Console.WriteLine();
        Console.WriteLine("Non-absolute paths resolve relative to DTDL_BASE. Exit: 0 valid, 1 invalid, 2 error.");
    }
}
