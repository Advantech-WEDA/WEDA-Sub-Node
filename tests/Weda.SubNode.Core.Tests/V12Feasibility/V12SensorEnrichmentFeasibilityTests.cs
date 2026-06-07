using System.Text.Json.Nodes;

using Shouldly;

using Xunit;

namespace Weda.SubNode.Core.Tests.V12Feasibility;

// ─────────────────────────────────────────────────────────────────────────────
// Feasibility check for the v1.2 ConfigurationUploadRequest design.
//
// What we're asking: given a v1.2 payload (single-object dtdl wrapper +
// refModels[] + deviceCapabilities.sensors[]), can transceiver's existing
// sensor enrichment logic (replicated in TransceiverEnrichmentReplica)
// produce correctly-enriched telemetry where Dtmi flows through as the
// Telemetry @id we put in deviceCapabilities.sensors[i].dtmi?
//
// We synthesize the payload as JsonObject (the actual mapping code doesn't
// exist yet — that's intentional, this test predates implementation) and
// run dm-svc's expected transform inline. If these tests pass, the v1.2
// design is safe to implement against transceiver's current enrichment flow.
// ─────────────────────────────────────────────────────────────────────────────

public class V12SensorEnrichmentFeasibilityTests
{
    private const string SubNodeDeviceId = "54777925790076928";

    [Fact]
    public void SingleSensor_DtmiFlowsThroughEnrichment()
    {
        var payload = BuildV12Payload_SingleTemperature();
        var sensors = DerivekvStoreDict(payload);
        var enrichment = new TelemetryEnrichmentReplica(_ => sensors);

        var telemetry = new TelemetryData(SubNodeDeviceId, "report", 1, "group-1", 1780548114462L);
        telemetry.Measures.Add(new TelemetryMeasure(
            sensorId: ShortIdOf("a3f49f07-4a38-52df-8d42-9aaac9964c11"),
            value: 25.3,
            timestamp: 1780548114462L));

        var enriched = enrichment.Enrich(SubNodeDeviceId, telemetry);

        enriched.Measures.Count.ShouldBe(1);
        var measure = enriched.Measures[0];
        measure.Dtmi.ShouldBe("dtmi:autogen:temp:be10e40b;1");
        measure.SensorResourceId.ShouldBe("a3f49f07-4a38-52df-8d42-9aaac9964c11");
        measure.Name.ShouldBe("temperature_sensor");
        measure.DeviceResourceId.ShouldBe(SubNodeDeviceId);
        measure.ValueDouble.ShouldBe(25.3);
    }

    [Fact]
    public void MultiSensor_AllSensorsResolveWithCorrectDtmi()
    {
        // PowerAggregator scenario — 3 sensors flattened in deviceCapabilities.sensors[].
        // v1.1 silently kept only the first device; v1.2's flatten-all-sensors design
        // must let all three measures resolve through enrichment.
        var payload = BuildV12Payload_PowerAggregator();
        var sensors = DerivekvStoreDict(payload);
        var enrichment = new TelemetryEnrichmentReplica(_ => sensors);

        var telemetry = new TelemetryData(SubNodeDeviceId, "report", 1, "group-1", 1780566352335L);
        telemetry.Measures.Add(new TelemetryMeasure(ShortIdOf("68eeaed9-b616-5485-9802-22890c787660"), 120.5, 1780566352335L));
        telemetry.Measures.Add(new TelemetryMeasure(ShortIdOf("ca196b9a-3f3c-596a-bfa7-d1642415f289"), 220.0, 1780566352335L));
        telemetry.Measures.Add(new TelemetryMeasure(ShortIdOf("a79afd10-b619-5040-8495-419d62c5e16d"), 26510.0, 1780566352335L));

        var enriched = enrichment.Enrich(SubNodeDeviceId, telemetry);

        enriched.Measures.Count.ShouldBe(3);

        // Each measure carries the Telemetry @id we put in
        // deviceCapabilities.sensors[i].dtmi — not the sensor-type Interface
        // DTMI from refModels, which is the whole point of the design.
        var current = enriched.Measures.Single(m => m.Name == "current001");
        current.Dtmi.ShouldBe("dtmi:custom:current;1");

        var voltage = enriched.Measures.Single(m => m.Name == "voltage001");
        voltage.Dtmi.ShouldBe("dtmi:custom:voltage;1");

        var power = enriched.Measures.Single(m => m.Name == "power001");
        power.Dtmi.ShouldBe("dtmi:custom:power;1");

        // None of them are dummies (i.e., every measure resolved).
        enriched.Measures.ShouldAllBe(m => m.Dtmi != TelemetryEnrichmentReplica.UNKNOWN);
    }

    [Fact]
    public void MeasureWithUnknownSensorId_BecomesDummyButDoesNotDrop()
    {
        // Negative test: an extra measure that doesn't correspond to any
        // sensor in deviceCapabilities. transceiver creates a dummy and
        // keeps the measure (rather than silently dropping it). v1.2 must
        // preserve that behavior — feasibility is about NOT introducing
        // regressions, not just the happy path.
        var payload = BuildV12Payload_SingleTemperature();
        var sensors = DerivekvStoreDict(payload);
        var enrichment = new TelemetryEnrichmentReplica(_ => sensors);

        var telemetry = new TelemetryData(SubNodeDeviceId, "report", 1, "group-1", 0L);
        telemetry.Measures.Add(new TelemetryMeasure(ShortIdOf("a3f49f07-4a38-52df-8d42-9aaac9964c11"), 25.3, 0L));
        telemetry.Measures.Add(new TelemetryMeasure("ghost-sensor", 999.0, 0L));

        var enriched = enrichment.Enrich(SubNodeDeviceId, telemetry);

        enriched.Measures.Count.ShouldBe(2);

        var real = enriched.Measures.Single(m => m.Name == "temperature_sensor");
        real.Dtmi.ShouldBe("dtmi:autogen:temp:be10e40b;1");

        var dummy = enriched.Measures.Single(m => m.Name == "ghost-sensor");
        dummy.Dtmi.ShouldBe(TelemetryEnrichmentReplica.UNKNOWN);
        dummy.SensorResourceId.ShouldBe(TelemetryEnrichmentReplica.UNKNOWN);
        dummy.DeviceResourceId.ShouldBe(TelemetryEnrichmentReplica.UNKNOWN);
    }

    [Fact]
    public void DeviceNotRegistered_AllMeasuresBecomeDummies()
    {
        // Edge case: KV store has no entry for this device (cold start /
        // KV-down). transceiver produces all-dummies so measures aren't
        // dropped. v1.2 shouldn't change this.
        var enrichment = new TelemetryEnrichmentReplica(_ => null);

        var telemetry = new TelemetryData(SubNodeDeviceId, "report", 1, "group-1", 0L);
        telemetry.Measures.Add(new TelemetryMeasure(ShortIdOf("a3f49f07-4a38-52df-8d42-9aaac9964c11"), 25.3, 0L));

        var enriched = enrichment.Enrich(SubNodeDeviceId, telemetry);

        enriched.Measures.Count.ShouldBe(1);
        enriched.Measures[0].Dtmi.ShouldBe(TelemetryEnrichmentReplica.UNKNOWN);
    }

    [Fact]
    public void SensorDtmi_IsTelemetryAtId_NotSensorTypeInterfaceDtmi()
    {
        // Pin the design intent: sensors[i].dtmi MUST equal the @id of the
        // Telemetry sitting inside dtdl.contents — not the sensor-type
        // Interface dtmi from refModels[]. This is the Q1 contract that
        // makes transceiver's TelemetryEnrichment work AND requires the
        // transceiver Command validators (separate ticket #37106) to stop
        // treating sensor.Dtmi as an Interface DTMI.
        var payload = BuildV12Payload_SingleTemperature();

        var sensorDtmi = payload["data"]!["deviceCapabilities"]!["sensors"]![0]!["dtmi"]!.GetValue<string>();
        var telemetryId = payload["data"]!["dtdl"]!["contents"]![0]!["@id"]!.GetValue<string>();
        var sensorTypeDtmi = payload["data"]!["refModels"]![0]!["@id"]!.GetValue<string>();

        sensorDtmi.ShouldBe(telemetryId);
        sensorDtmi.ShouldNotBe(sensorTypeDtmi);
    }

    // ─── helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Mirror what dm-svc would do when unpacking a v1.2 upload:
    /// deviceCapabilities.sensors[] -> Dictionary&lt;SensorShortResourceId, Sensor&gt;.
    /// The SensorShortResourceId derivation is dm-svc's call; this test uses
    /// a deterministic prefix of ResourceId so the test's measure SensorIds
    /// stay matchable.
    /// </summary>
    private static Dictionary<string, Sensor> DerivekvStoreDict(JsonObject payload)
    {
        var sensorsNode = payload["data"]!["deviceCapabilities"]!["sensors"]!.AsArray();
        var dict = new Dictionary<string, Sensor>();
        foreach (var node in sensorsNode)
        {
            var resourceId = node!["resourceId"]!.GetValue<string>();
            var shortId = ShortIdOf(resourceId);
            dict[shortId] = new Sensor(
                sensorResourceId: resourceId,
                sensorShortResourceId: shortId,
                deviceResourceId: node["deviceResourceId"]!.GetValue<string>(),
                name: node["name"]!.GetValue<string>(),
                dtmi: node["dtmi"]!.GetValue<string>());
        }
        return dict;
    }

    private static string ShortIdOf(string resourceId) =>
        resourceId.Replace("-", "").Substring(0, 8);

    // ─── v1.2 payload fixtures (built as JsonObject — no production mapping
    //     code is touched, so this test can run before Step 1.1 lands) ──────

    private static JsonObject BuildV12Payload_SingleTemperature() => new()
    {
        ["reqSeqId"] = "47afaa68-5edd-4104-a721-03347972abab",
        ["timestamp"] = 1780548114462L,
        ["data"] = new JsonObject
        {
            ["deviceId"] = SubNodeDeviceId,
            ["dtdl"] = new JsonObject
            {
                ["@context"] = "dtmi:dtdl:context;3",
                ["@id"] = "dtmi:advantech:edgesync:subnode_a1b2c3d4;1",
                ["@type"] = "Interface",
                ["displayName"] = "MyFirstApplication",
                ["description"] = "My first implementation of SubNode",
                ["contents"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["@id"] = "dtmi:autogen:temp:be10e40b;1",
                        ["@type"] = "Telemetry",
                        ["name"] = "temperature_sensor",
                        ["displayName"] = "Temperature Sensor",
                        ["description"] = "Sensor for temperature sampling",
                        ["schema"] = "double",
                        ["unit"] = "celsius"
                    }
                }
            },
            ["refModels"] = new JsonArray
            {
                new JsonObject
                {
                    ["@context"] = "dtmi:dtdl:context;3",
                    ["@id"] = "dtmi:weda:tcpmodbus:temperature;1",
                    ["@type"] = "Interface",
                    ["extends"] = "dtmi:weda:sensor:base;1",
                    ["displayName"] = "TcpModbusTemperatureSensor"
                }
            },
            ["deviceCapabilities"] = new JsonObject
            {
                ["manufacturer"] = "Advantech",
                ["model"] = "Demo",
                ["deviceType"] = "adamEthernet",
                ["subDeviceSwVersion"] = "1.0.0",
                ["deviceName"] = "TestDevice",
                ["deviceInfo"] = new JsonObject(),
                ["sensors"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["resourceId"] = "a3f49f07-4a38-52df-8d42-9aaac9964c11",
                        ["dtmi"] = "dtmi:autogen:temp:be10e40b;1",
                        ["name"] = "temperature_sensor",
                        ["sensorGroup"] = "TEMP",
                        ["deviceResourceId"] = SubNodeDeviceId
                    }
                }
            }
        }
    };

    private static JsonObject BuildV12Payload_PowerAggregator() => new()
    {
        ["reqSeqId"] = "b94ac957-f4a5-4031-9bfc-9f1cb711dc5c",
        ["timestamp"] = 1780566352335L,
        ["data"] = new JsonObject
        {
            ["deviceId"] = SubNodeDeviceId,
            ["dtdl"] = new JsonObject
            {
                ["@context"] = "dtmi:dtdl:context;3",
                ["@id"] = "dtmi:advantech:edgesync:subnode_pa1b2c3d;1",
                ["@type"] = "Interface",
                ["displayName"] = "MyPowerAggregator",
                ["description"] = "Aggregates current, voltage, computed power",
                ["contents"] = new JsonArray
                {
                    TelemetryNode("dtmi:custom:current;1", "current001", "Current Sensor", "double", "A"),
                    TelemetryNode("dtmi:custom:voltage;1", "voltage001", "Voltage Sensor", "double", "V"),
                    TelemetryNode("dtmi:custom:power;1",   "power001",   "Power Sensor",   "double", "W")
                }
            },
            ["refModels"] = new JsonArray(),
            ["deviceCapabilities"] = new JsonObject
            {
                ["manufacturer"] = "Advantech",
                ["model"] = "Power-Calculator",
                ["deviceType"] = "customDevice",
                ["subDeviceSwVersion"] = "1.0.0",
                ["deviceName"] = "MyPowerAggregator-1",
                ["deviceInfo"] = new JsonObject(),
                ["sensors"] = new JsonArray
                {
                    SensorNode("68eeaed9-b616-5485-9802-22890c787660", "dtmi:custom:current;1", "current001", "AI"),
                    SensorNode("ca196b9a-3f3c-596a-bfa7-d1642415f289", "dtmi:custom:voltage;1", "voltage001", "AI"),
                    SensorNode("a79afd10-b619-5040-8495-419d62c5e16d", "dtmi:custom:power;1",   "power001",   "PWR")
                }
            }
        }
    };

    private static JsonObject TelemetryNode(string id, string name, string displayName, string schema, string unit) => new()
    {
        ["@id"] = id,
        ["@type"] = "Telemetry",
        ["name"] = name,
        ["displayName"] = displayName,
        ["schema"] = schema,
        ["unit"] = unit
    };

    private static JsonObject SensorNode(string resourceId, string dtmi, string name, string group) => new()
    {
        ["resourceId"] = resourceId,
        ["dtmi"] = dtmi,
        ["name"] = name,
        ["sensorGroup"] = group,
        ["deviceResourceId"] = SubNodeDeviceId
    };
}
