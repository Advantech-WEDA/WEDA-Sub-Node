using System.Reflection;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

/// <summary>
/// Access to the captured CFX message payloads used across the CFX tests.
/// </summary>
/// <remarks>
/// Each fixture is a byte-for-byte capture of a real MQTT payload published by a bridged CFX
/// endpoint on an SMT line: GZip-compressed UTF-8 envelope JSON. Testing against real captures
/// rather than hand-written JSON is what catches the quirks the specification does not advertise —
/// the misspelled <c>Lenght</c> field in Hermes units, <c>TransactionId</c> vs <c>TransactionID</c>
/// casing drift, and nested <c>$type</c> discriminators inside inspection measurements.
/// </remarks>
internal static class CfxFixtures
{
    private static readonly string FixtureDirectory = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "Protocols",
        "Cfx",
        "Fixtures");

    /// <summary>
    /// Every captured message name, for use as xUnit theory data.
    /// </summary>
    public static TheoryData<string> AllMessageNames()
    {
        var data = new TheoryData<string>();

        foreach (var messageName in MessageNames())
        {
            data.Add(messageName);
        }

        return data;
    }

    /// <summary>
    /// Every captured message name.
    /// </summary>
    public static IEnumerable<string> MessageNames() =>
        Directory.EnumerateFiles(FixtureDirectory, "*.gz")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal);

    /// <summary>
    /// Reads the raw GZip-compressed payload for one message, exactly as it arrived on the wire.
    /// </summary>
    public static byte[] GzipPayload(string messageName) =>
        File.ReadAllBytes(Path.Combine(FixtureDirectory, $"{messageName}.gz"));
}
