using DTDLParser;

namespace Weda.Dtdl.Validator;

public static class DtdlValidator
{
    private static readonly string[] Files =
    {
        "01_CPU_NETWORK.dtdl.json",
        "02_MEMORY_DISK_SYSTEM_GPU.dtdl.json",
        "03_HARDWARE_INFO.dtdl.json",
        "04_ONBOARD_SENSOR.dtdl.json",
        "05_HARDWARE_FEATURE.dtdl.json",
    };

    public static async Task<int> RunAsync()
    {
        var basePath = Program.ResolveBasePath();
        var failed = 0;

        // Per-file pass -- catches per-Interface issues.
        foreach (var f in Files)
        {
            var path = Path.Combine(basePath, f);
            try
            {
                var json = await File.ReadAllTextAsync(path);
                var parser = new ModelParser();
                var model = await parser.ParseAsync(ToAsync(new[] { json }));
                Console.WriteLine($"OK  {f}: {model.Count} entities");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERR {f}:");
                foreach (var line in TruncateMessage(ex.Message))
                    Console.Error.WriteLine($"  {line}");
                failed++;
            }
        }

        // Combined pass -- catches DTMI collisions across Interfaces.
        try
        {
            var jsons = await Task.WhenAll(Files.Select(f =>
                File.ReadAllTextAsync(Path.Combine(basePath, f))));
            var parser = new ModelParser();
            var model = await parser.ParseAsync(ToAsync(jsons));
            Console.WriteLine($"OK  combined: {model.Count} entities");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERR combined:");
            foreach (var line in TruncateMessage(ex.Message))
                Console.Error.WriteLine($"  {line}");
            failed++;
        }

        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// Adapt an <see cref="IEnumerable{T}"/> to the
    /// <see cref="IAsyncEnumerable{T}"/> shape the DTDLParser expects.
    /// </summary>
    private static async IAsyncEnumerable<string> ToAsync(IEnumerable<string> source)
    {
        await Task.Yield();
        foreach (var item in source)
            yield return item;
    }

    private static IEnumerable<string> TruncateMessage(string msg) =>
        msg.Split('\n').Take(20);
}
