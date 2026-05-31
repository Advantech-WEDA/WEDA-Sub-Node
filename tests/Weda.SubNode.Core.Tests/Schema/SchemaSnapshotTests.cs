using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using Weda.SubNode.Core.Commands;
using Weda.SubNode.Core.Dsp;
using Weda.SubNode.Core.Transforms;

using Xunit;

namespace Weda.SubNode.Core.Tests.Schema;

/// <summary>
/// Golden snapshot tests for production transform / DSP filter / command
/// DTDL v3 Interfaces. These guard against accidental wire-format drift when
/// the emitter logic changes — any change to a baseline file shows up as a
/// reviewable diff.
/// </summary>
/// <remarks>
/// Baselines live under <c>tests/Weda.SubNode.Core.Tests/Schema/Snapshots/</c>
/// and are loaded as embedded resources via project copy-to-output settings.
/// When a schema legitimately changes, regenerate the snapshot by setting the
/// <c>WEDA_UPDATE_SNAPSHOTS</c> environment variable to <c>1</c> and re-running
/// the tests.
/// </remarks>
public class SchemaSnapshotTests
{
    private static string SnapshotDir => Path.Combine(SourceFileDir(), "Snapshots");

    private static string SourceFileDir([CallerFilePath] string sourceFile = "") =>
        Path.GetDirectoryName(sourceFile)!;

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
    };

    [Theory]
    [InlineData("unitconversion", "UnitConversionTransform")]
    [InlineData("calibration", "CalibrationTransform")]
    [InlineData("chunking", "ChunkingTransform")]
    public void Transform_schema_matches_snapshot(string typeName, string snapshotName)
    {
        var descriptor = TransformFactory.GetDescriptors().First(d => d.TypeName == typeName);
        AssertMatchesSnapshot($"{snapshotName}.parameters.json", descriptor.ParameterSchema);
    }

    [Theory]
    [InlineData("movingaverage", "MovingAverageFilter")]
    [InlineData("kalman", "KalmanFilter")]
    [InlineData("relu", "ReluFilter")]
    public void DspFilter_schema_matches_snapshot(string typeName, string snapshotName)
    {
        var descriptor = DspFilterFactory.GetDescriptors().First(d => d.TypeName == typeName);
        AssertMatchesSnapshot($"{snapshotName}.parameters.json", descriptor.ParameterSchema);
    }

    [Theory]
    [InlineData("report.historical", "BatchReportCommand")]
    [InlineData("report.data", "ReportDataCommand")]
    [InlineData("do.set", "SetDigitalOutputCommand")]
    [InlineData("ao.set", "SetAnalogOutputCommand")]
    [InlineData("do.get", "GetDigitalOutputCommand")]
    [InlineData("ai.get", "GetAnalogInputCommand")]
    [InlineData("ao.get", "GetAnalogOutputCommand")]
    [InlineData("di.get", "GetDigitalInputCommand")]
    public void Command_schema_matches_snapshot(string commandName, string snapshotName)
    {
        var registry = new CommandRegistry();
        registry.ScanAssembly(typeof(CommandRegistry).Assembly);
        var descriptor = registry.GetDescriptors().First(d => d.Name == commandName);

        AssertMatchesSnapshot($"{snapshotName}.json", descriptor.Schema);
    }

    private static void AssertMatchesSnapshot(string fileName, JsonObject schema)
    {
        Directory.CreateDirectory(SnapshotDir);
        var path = Path.Combine(SnapshotDir, fileName);
        var actual = Normalize(JsonSerializer.Serialize(schema, PrettyJson));

        if (!File.Exists(path) || Environment.GetEnvironmentVariable("WEDA_UPDATE_SNAPSHOTS") == "1")
        {
            File.WriteAllText(path, actual);
            Assert.True(File.Exists(path), $"Wrote new snapshot to {path}");
            return;
        }

        var expected = Normalize(File.ReadAllText(path));
        Assert.True(expected == actual,
            $"Schema for {fileName} drifted from snapshot at {path}. " +
            $"Set WEDA_UPDATE_SNAPSHOTS=1 to refresh.\n\n--- expected ---\n{expected}\n\n--- actual ---\n{actual}");
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();
}
