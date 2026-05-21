// WEDA System-Agent DTDL validator -- .NET 8 port of scripts/*.js.
//
// Usage:
//   dotnet run -- dtdl          # schema-level only (parses *.dtdl.json at the
//                                 DTDL_BASE root -- the legacy 5 array-form files)
//   dotnet run -- configs       # config-level only (Parameters.* shape checks)
//   dotnet run -- both          # legacy dtdl + configs (default; alias: no args)
//   dotnet run -- verify        # DTDL-native (loads DTDL_BASE/dtdl/*.json --
//                                 the split per-MetricType Interfaces) +
//                                 ConfigRules, both layers per fixture
//   dotnet run -- verify-net-v2 # DTDL-native ONLY (loads
//                                 DTDL_BASE/dtdl/NetworkSensorConfig.v2.json
//                                 and walks the nested Object schemas against
//                                 every network row in samples-config)
//
// Paths:
//   DTDL_BASE env var when set; else parent of AppContext.BaseDirectory.

namespace Weda.Dtdl.Validator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0] : "both";

        return mode switch
        {
            "dtdl"    => await DtdlValidator.RunAsync(),
            "configs" => await ConfigsValidator.RunAsync(),
            "both"    => await RunBothAsync(),
            "all"     => await RunBothAsync(),
            "verify"        => await SamplesAgainstDtdlVerifier.RunAsync(),
            "verify-net-v2" => await NetworkV2Verifier.RunAsync(),
            _               => Unknown(mode),
        };
    }

    private static async Task<int> RunBothAsync()
    {
        var r1 = await DtdlValidator.RunAsync();
        if (r1 != 0) return r1;
        return await ConfigsValidator.RunAsync();
    }

    private static int Unknown(string mode)
    {
        Console.Error.WriteLine(
            $"ERR unknown mode '{mode}'. Use 'dtdl', 'configs', 'both', 'verify', or 'verify-net-v2'.");
        return 2;
    }

    /// <summary>
    /// Returns the path to docs/Metrics/ (containing the *.dtdl.json files
    /// and the samples-config/ subfolder). When DTDL_BASE is set it wins;
    /// otherwise we walk up from the binary location.
    /// </summary>
    public static string ResolveBasePath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("DTDL_BASE");
        if (!string.IsNullOrEmpty(fromEnv))
            return Path.GetFullPath(fromEnv);

        // Local dev: AppContext.BaseDirectory ends in
        //   dtdl-validate/scripts-dotnet/bin/<Config>/net8.0/
        // Walk up five levels to land on the project root, then sideways to
        // docs/Metrics/.
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "docs", "Metrics"));
    }
}
