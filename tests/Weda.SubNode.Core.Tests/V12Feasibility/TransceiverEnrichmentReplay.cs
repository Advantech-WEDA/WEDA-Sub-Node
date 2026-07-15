using System.Globalization;

namespace Weda.SubNode.Core.Tests.V12Feasibility;

// ─────────────────────────────────────────────────────────────────────────────
// Minimal replicas of transceiver's Telemetry model + enrichment logic.
// Source: ~/advantech/projects/transceiver/DigitaltwinTransceiver/src/
//   - Domain/Telemetry/Models/Sensor.cs
//   - Domain/Telemetry/Models/TelemetryMeasure.cs
//   - Domain/Telemetry/Models/TelemetryData.cs
//   - Domain/Telemetry/Models/EnrichedTelemetryData.cs
//   - Domain/Telemetry/Models/EnrichedTelemetryMeasure.cs
//   - Domain/Telemetry/Services/TelemetryEnrichmentManager.cs
//
// Why replicate instead of project-reference: transceiver lives in a separate
// solution and depends on Volo.Abp. Replicating only what enrichment actually
// touches gives a focused feasibility check without dragging in ABP.
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class Sensor
{
    public string SensorResourceId { get; }
    public string SensorShortResourceId { get; }
    public string DeviceResourceId { get; }
    public string Name { get; }
    public string Dtmi { get; }

    public Sensor(
        string sensorResourceId,
        string sensorShortResourceId,
        string deviceResourceId,
        string name,
        string dtmi)
    {
        SensorResourceId = sensorResourceId;
        SensorShortResourceId = sensorShortResourceId;
        DeviceResourceId = deviceResourceId;
        Name = name;
        Dtmi = dtmi;
    }
}

internal sealed class TelemetryMeasure
{
    public string SensorId { get; }
    public double ValueDouble { get; }
    public object? ValueObject { get; }
    public long Timestamp { get; }

    public TelemetryMeasure(string sensorId, object value, long timestamp)
    {
        SensorId = sensorId;
        ValueObject = value;
        Timestamp = timestamp;
        ValueDouble = ToDouble(value);
    }

    private static double ToDouble(object value) => value switch
    {
        double d => d,
        float f => f,
        long l => l,
        int i => i,
        string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => 0
    };
}

internal sealed class TelemetryData
{
    public string DeviceId { get; }
    public string Command { get; }
    public ulong SequenceId { get; }
    public string GroupId { get; }
    public long Timestamp { get; }
    public List<TelemetryMeasure> Measures { get; } = [];

    public TelemetryData(string deviceId, string command, ulong sequenceId, string groupId, long timestamp)
    {
        DeviceId = deviceId;
        Command = command;
        SequenceId = sequenceId;
        GroupId = groupId;
        Timestamp = timestamp;
    }
}

internal sealed class EnrichedTelemetryMeasure
{
    public string SensorResourceId { get; }
    public string SensorShortResourceId { get; }
    public string DeviceResourceId { get; }
    public string Dtmi { get; }
    public string Name { get; }
    public double ValueDouble { get; }
    public object Value { get; }
    public long Timestamp { get; }

    public EnrichedTelemetryMeasure(
        string sensorResourceId,
        string sensorShortResourceId,
        string deviceResourceId,
        string dtmi,
        string name,
        double valueDouble,
        object valueObject,
        long timestamp)
    {
        SensorResourceId = sensorResourceId;
        SensorShortResourceId = sensorShortResourceId;
        DeviceResourceId = deviceResourceId;
        Dtmi = dtmi;
        Name = name;
        ValueDouble = valueDouble;
        Value = valueObject;
        Timestamp = timestamp;
    }
}

internal sealed class EnrichedTelemetryData
{
    public string DeviceId { get; }
    public string Command { get; }
    public ulong SequenceId { get; }
    public string GroupId { get; }
    public long Timestamp { get; }
    public List<EnrichedTelemetryMeasure> Measures { get; } = [];

    public EnrichedTelemetryData(string deviceId, string command, ulong sequenceId, string groupId, long timestamp)
    {
        DeviceId = deviceId;
        Command = command;
        SequenceId = sequenceId;
        GroupId = groupId;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Carbon copy of TelemetryEnrichmentManager.EnrichTelemetryDataAsync.
/// Repository is abstracted so each test can hand it a synthesized dict
/// (mirroring what dm-svc would write to the KV store after unpacking
/// our v1.2 upload payload).
/// </summary>
internal sealed class TelemetryEnrichmentReplica
{
    public const string UNKNOWN = "unknown";

    private readonly Func<string, Dictionary<string, Sensor>?> _lookup;

    public TelemetryEnrichmentReplica(Func<string, Dictionary<string, Sensor>?> lookup)
    {
        _lookup = lookup;
    }

    public EnrichedTelemetryData Enrich(string deviceId, TelemetryData telemetryData)
    {
        var enriched = new EnrichedTelemetryData(
            deviceId,
            telemetryData.Command,
            telemetryData.SequenceId,
            telemetryData.GroupId,
            telemetryData.Timestamp);

        var sensors = _lookup(deviceId);

        if (sensors is null || sensors.Count == 0)
        {
            foreach (var measure in telemetryData.Measures)
            {
                enriched.Measures.Add(MakeDummy(measure));
            }
            return enriched;
        }

        foreach (var measure in telemetryData.Measures)
        {
            if (sensors.TryGetValue(measure.SensorId, out var sensor))
            {
                enriched.Measures.Add(new EnrichedTelemetryMeasure(
                    sensor.SensorResourceId,
                    sensor.SensorShortResourceId,
                    sensor.DeviceResourceId,
                    sensor.Dtmi,
                    sensor.Name,
                    measure.ValueDouble,
                    measure.ValueObject ?? 0,
                    measure.Timestamp));
            }
            else
            {
                enriched.Measures.Add(MakeDummy(measure));
            }
        }

        return enriched;
    }

    private static EnrichedTelemetryMeasure MakeDummy(TelemetryMeasure measure) =>
        new(
            sensorResourceId: UNKNOWN,
            sensorShortResourceId: measure.SensorId,
            deviceResourceId: UNKNOWN,
            dtmi: UNKNOWN,
            name: measure.SensorId,
            valueDouble: measure.ValueDouble,
            valueObject: measure.ValueObject ?? 0,
            timestamp: measure.Timestamp);
}
