// WEDA System-Agent DTDL validator -- .NET 8 port of scripts/*.js.
//
// Usage:
//   dotnet run -- dtdl          # schema-level only
//   dotnet run -- samples       # instance-level only
//   dotnet run -- both          # both (default; alias: no args)
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
            "samples" => await SamplesValidator.RunAsync(),
            "both"    => await RunBothAsync(),
            _         => Unknown(mode),
        };
    }

    private static async Task<int> RunBothAsync()
    {
        var r1 = await DtdlValidator.RunAsync();
        if (r1 != 0) return r1;
        return await SamplesValidator.RunAsync();
    }

    private static int Unknown(string mode)
    {
        Console.Error.WriteLine($"ERR unknown mode '{mode}'. Use 'dtdl', 'samples', or 'both'.");
        return 2;
    }

    /// <summary>
    /// Returns the path to docs/Metrics/ (containing the *.dtdl.json files
    /// and the samples/ subfolder). When DTDL_BASE is set it wins; otherwise
    /// we walk up from the binary location.
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
