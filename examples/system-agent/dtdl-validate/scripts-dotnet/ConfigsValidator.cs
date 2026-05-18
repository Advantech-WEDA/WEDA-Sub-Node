using System.Text.Json;

namespace Weda.Dtdl.Validator;

/// <summary>
/// Configuration-level validation: walks DTDL_BASE/samples-config/*.json,
/// applies the rules in <see cref="ConfigRules"/> to each sensor entry, and
/// prints a per-sensor verdict.  Returns exit 0 when every sample lands on
/// its declared expectation, 1 otherwise.
///
/// Mirror of dtdl-validate/scripts/validate-configs.js.
/// </summary>
public static class ConfigsValidator
{
    public static async Task<int> RunAsync()
    {
        var basePath = Program.ResolveBasePath();
        var configsDir = Path.Combine(basePath, "samples-config");

        if (!Directory.Exists(configsDir))
        {
            Console.Error.WriteLine($"ERR samples-config dir not found: {configsDir}");
            return 1;
        }

        int failed = 0, total = 0;

        var files = Directory.GetFiles(configsDir, "*.json")
                             .OrderBy(p => p, StringComparer.Ordinal)
                             .ToArray();

        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file);
            var sensors = JsonSerializer.Deserialize<List<Entry>>(json, JsonOpts)!;

            Console.WriteLine();
            Console.WriteLine($"--- {Path.GetFileName(file)} ({sensors.Count} sensors) ---");

            foreach (var s in sensors)
            {
                total++;
                ConfigRules.RuleResult result;
                try { result = ConfigRules.Validate(s.Sensor); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"  ERR rule threw for {s.Name ?? "<unnamed>"}: {ex.Message}");
                    failed++;
                    continue;
                }

                var expected = s.Expect == "valid";
                var label = (s.Expect ?? "unknown").PadRight(7);
                var name  = s.Name ?? "<unnamed>";

                if (result.Ok == expected)
                {
                    Console.WriteLine($"  OK  {label} {name}");
                }
                else
                {
                    var verdict = result.Ok ? "valid" : "invalid";
                    var got = result.Reason is null ? "" : $": {result.Reason}";
                    var exp = string.IsNullOrEmpty(s.Reason) ? "" : $"  // expected: {s.Reason}";
                    Console.Error.WriteLine(
                        $"  ERR {label} {name} (validator said {verdict}{got}){exp}");
                    failed++;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(failed == 0
            ? $"ALL {total} CONFIGS PASSED"
            : $"{failed} of {total} configs FAILED");

        return failed == 0 ? 0 : 1;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Fixture-row schema: the full sensor lives in additional properties
    /// captured via <see cref="JsonElement"/>; "expect" / "reason" are the
    /// fixture-specific test metadata.
    /// </summary>
    private sealed class Entry
    {
        public string? Name { get; set; }

        // Bind the whole JSON element so ConfigRules can read Parameters.*.
        // We re-serialize the row sans expect/reason for the rule input.
        public string? Expect { get; set; }
        public string? Reason { get; set; }

        // Convenience: a JsonElement view of this sensor row (sans test fields).
        // System.Text.Json doesn't have a "rest of object" binding, so we
        // reuse the deserializer's source via JsonExtensionData below.
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        public JsonElement Sensor
        {
            get
            {
                var dict = new Dictionary<string, object?>();
                if (!string.IsNullOrEmpty(Name)) dict["Name"] = Name;
                if (Extra is not null)
                    foreach (var kv in Extra) dict[kv.Key] = kv.Value;
                var bytes = JsonSerializer.SerializeToUtf8Bytes(dict);
                return JsonDocument.Parse(bytes).RootElement.Clone();
            }
        }
    }
}
