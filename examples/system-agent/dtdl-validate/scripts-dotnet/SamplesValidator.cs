using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weda.Dtdl.Validator;

public static class SamplesValidator
{
    public static async Task<int> RunAsync()
    {
        var basePath   = Program.ResolveBasePath();
        var samplesDir = Path.Combine(basePath, "samples");

        if (!Directory.Exists(samplesDir))
        {
            Console.Error.WriteLine($"ERR samples dir not found: {samplesDir}");
            return 1;
        }

        int failed = 0, total = 0;

        var files = Directory.GetFiles(samplesDir, "*.json")
                             .OrderBy(p => p, StringComparer.Ordinal)
                             .ToArray();

        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file);
            var samples = JsonSerializer.Deserialize<List<Sample>>(json, JsonOpts)!;

            Console.WriteLine();
            Console.WriteLine($"--- {Path.GetFileName(file)} ({samples.Count} samples) ---");

            foreach (var s in samples)
            {
                total++;

                if (!Predicates.Map.TryGetValue(s.ResourceId, out var fn))
                {
                    Console.Error.WriteLine($"  ERR no predicate registered for {s.ResourceId}");
                    failed++;
                    continue;
                }

                bool ok;
                try { ok = fn(s.Value); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"  ERR predicate threw for {s.ResourceId}: {ex.Message}");
                    failed++;
                    continue;
                }

                var expected = s.Expect == "valid";
                var display  = FormatValue(s.Value);
                var label    = s.Expect.PadRight(7);

                if (ok == expected)
                {
                    Console.WriteLine($"  OK  {label} {s.ResourceId} = {display}");
                }
                else
                {
                    var verdict = ok ? "valid" : "invalid";
                    var reason  = string.IsNullOrEmpty(s.Reason) ? "" : $"  {s.Reason}";
                    Console.Error.WriteLine(
                        $"  ERR {label} {s.ResourceId} = {display} (predicate said {verdict}){reason}");
                    failed++;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(failed == 0
            ? $"ALL {total} SAMPLES PASSED"
            : $"{failed} of {total} samples FAILED");

        return failed == 0 ? 0 : 1;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true,
    };

    private static string FormatValue(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => $"\"{v.GetString()}\"",
        JsonValueKind.Null   => "null",
        _                    => v.ToString(),
    };

    private sealed record Sample(
        [property: JsonPropertyName("ResourceId")] string ResourceId,
        [property: JsonPropertyName("Value")] JsonElement Value,
        [property: JsonPropertyName("expect")] string Expect,
        [property: JsonPropertyName("reason")] string? Reason);
}
