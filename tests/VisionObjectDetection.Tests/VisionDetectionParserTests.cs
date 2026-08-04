using System.Text;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;

using VisionObjectDetection.Protocols;

using Xunit;

namespace VisionObjectDetection.Tests;

/// <summary>
/// Verifies <see cref="VisionDetectionParser"/> against the exact MQTT telemetry
/// contract (§6) published by the Advantech YOLO object-detection container.
/// </summary>
public class VisionDetectionParserTests
{
    // Verbatim sample from cv-container-reference.md §6.
    private const string SampleDetections = """
    {
      "timestamp":   "2026-07-19T16:54:47.949Z",
      "deviceId":    "74fe488d5d54",
      "frame":       830,
      "lap":         1,
      "fps":         12.17,
      "objectCount": 7,
      "classCounts": { "bottle": 7 },
      "detections": [
        { "class": "bottle", "confidence": 0.8932, "bbox": [0.5, 116.1, 344.9, 1417.2] }
      ]
    }
    """;

    private const string DetectionsTopic = "advantech/74fe488d5d54/vision/detections";

    private static Sensor MakeSensor(string name, string field, bool enabled = true) => new()
    {
        ResourceId = $"rid-{name}",
        Name = name,
        SensorGroup = SensorGroup.AI,
        Parameters = new Dictionary<string, object> { ["Field"] = field },
        SensorInfo = new SensorInfo { Schema = "double" },
        Report = new SensorReport { Enabled = enabled, Interval = 1000 }
    };

    private static DeviceConfiguration MakeConfig(params Sensor[] sensors) => new()
    {
        DeviceName = "vision-test",
        DeviceCommunication = new Dictionary<string, object> { ["DeviceId"] = "+" },
        Sensors = sensors.ToList()
    };

    private static (VisionDetectionParser Parser, List<TelemetryMeasure> Captured, IPubSub Comm) BuildParser(
        DeviceConfiguration config)
    {
        var comm = Substitute.For<IPubSub>();
        var parser = new VisionDetectionParser(config, comm, Substitute.For<ILogger<VisionDetectionParser>>());

        var captured = new List<TelemetryMeasure>();
        parser.OnTelemetryReceived += measures => captured.AddRange(measures);

        return (parser, captured, comm);
    }

    private static void RaiseMessage(IPubSub comm, string topic, string payload) =>
        comm.MessageReceived += Raise.Event<EventHandler<MessageReceivedEvent<byte[]>>>(
            comm,
            new MessageReceivedEvent(topic, Encoding.UTF8.GetBytes(payload), DateTimeOffset.UtcNow));

    private static double ValueOf(List<TelemetryMeasure> measures, string field) =>
        Convert.ToDouble(measures.Single(m => (string)m.Metadata![ "field"] == field).Value);

    [Fact]
    public async Task Parses_all_configured_fields_from_the_reference_payload()
    {
        var config = MakeConfig(
            MakeSensor("object_count", "objectCount"),
            MakeSensor("inference_fps", "fps"),
            MakeSensor("confidence_max", "confidence.max"),
            MakeSensor("confidence_avg", "confidence.avg"),
            MakeSensor("bottle_count", "classCount.bottle"));

        var (parser, captured, comm) = BuildParser(config);
        await parser.StartAsync();

        RaiseMessage(comm, DetectionsTopic, SampleDetections);

        captured.Count.ShouldBe(5);
        ValueOf(captured, "objectCount").ShouldBe(7);
        ValueOf(captured, "fps").ShouldBe(12.17);
        ValueOf(captured, "confidence.max").ShouldBe(0.8932, 1e-9);
        ValueOf(captured, "confidence.avg").ShouldBe(0.8932, 1e-9);
        ValueOf(captured, "classCount.bottle").ShouldBe(7);
    }

    [Fact]
    public async Task Maps_the_source_timestamp_to_unix_milliseconds()
    {
        var config = MakeConfig(MakeSensor("object_count", "objectCount"));
        var (parser, captured, comm) = BuildParser(config);
        await parser.StartAsync();

        RaiseMessage(comm, DetectionsTopic, SampleDetections);

        var expected = DateTimeOffset.Parse("2026-07-19T16:54:47.949Z").ToUnixTimeMilliseconds();
        captured.Single().Timestamp.ShouldBe(expected);
    }

    [Fact]
    public async Task Tags_each_measure_with_the_source_device_id()
    {
        var config = MakeConfig(MakeSensor("object_count", "objectCount"));
        var (parser, captured, comm) = BuildParser(config);
        await parser.StartAsync();

        RaiseMessage(comm, DetectionsTopic, SampleDetections);

        ((string)captured.Single().Metadata!["deviceId"]).ShouldBe("74fe488d5d54");
    }

    [Fact]
    public async Task Empty_detection_array_yields_zero_confidence()
    {
        const string payload = """
        { "timestamp":"2026-07-19T16:54:47.949Z", "deviceId":"dev01", "objectCount":0,
          "classCounts":{}, "detections":[] }
        """;
        var config = MakeConfig(
            MakeSensor("confidence_max", "confidence.max"),
            MakeSensor("confidence_avg", "confidence.avg"),
            MakeSensor("object_count", "objectCount"));
        var (parser, captured, comm) = BuildParser(config);
        await parser.StartAsync();

        RaiseMessage(comm, "advantech/dev01/vision/detections", payload);

        ValueOf(captured, "confidence.max").ShouldBe(0);
        ValueOf(captured, "confidence.avg").ShouldBe(0);
        ValueOf(captured, "objectCount").ShouldBe(0);
    }

    [Fact]
    public async Task Absent_class_count_resolves_to_zero()
    {
        var config = MakeConfig(MakeSensor("person_count", "classCount.person"));
        var (parser, captured, comm) = BuildParser(config);
        await parser.StartAsync();

        RaiseMessage(comm, DetectionsTopic, SampleDetections); // only "bottle" present

        ValueOf(captured, "classCount.person").ShouldBe(0);
    }

    [Fact]
    public async Task Averages_confidence_across_multiple_detections()
    {
        const string payload = """
        { "timestamp":"2026-07-19T16:54:47.949Z", "deviceId":"dev01", "objectCount":2,
          "classCounts":{"bottle":2},
          "detections":[
            {"class":"bottle","confidence":0.9,"bbox":[0,0,1,1]},
            {"class":"bottle","confidence":0.5,"bbox":[0,0,1,1]}
          ] }
        """;
        var config = MakeConfig(
            MakeSensor("confidence_max", "confidence.max"),
            MakeSensor("confidence_avg", "confidence.avg"));
        var (parser, captured, comm) = BuildParser(config);
        await parser.StartAsync();

        RaiseMessage(comm, "advantech/dev01/vision/detections", payload);

        ValueOf(captured, "confidence.max").ShouldBe(0.9, 1e-9);
        ValueOf(captured, "confidence.avg").ShouldBe(0.7, 1e-9);
    }

    [Fact]
    public async Task Disabled_sensor_is_excluded()
    {
        var config = MakeConfig(
            MakeSensor("object_count", "objectCount"),
            MakeSensor("inference_fps", "fps", enabled: false));
        var (parser, captured, comm) = BuildParser(config);
        await parser.StartAsync();

        RaiseMessage(comm, DetectionsTopic, SampleDetections);

        captured.ShouldHaveSingleItem();
        ((string)captured[0].Metadata!["field"]).ShouldBe("objectCount");
    }

    [Fact]
    public async Task Unknown_field_selector_produces_no_measure()
    {
        var config = MakeConfig(MakeSensor("bogus", "does.not.exist"));
        var (parser, captured, comm) = BuildParser(config);
        await parser.StartAsync();

        RaiseMessage(comm, DetectionsTopic, SampleDetections);

        captured.ShouldBeEmpty();
    }

    [Fact]
    public async Task Malformed_payload_is_ignored_without_throwing()
    {
        var config = MakeConfig(MakeSensor("object_count", "objectCount"));
        var (parser, captured, comm) = BuildParser(config);
        await parser.StartAsync();

        Should.NotThrow(() => RaiseMessage(comm, DetectionsTopic, "{ this is not json"));
        captured.ShouldBeEmpty();
    }

    [Fact]
    public async Task Non_detection_topics_produce_no_telemetry()
    {
        var config = MakeConfig(MakeSensor("object_count", "objectCount"));
        var (parser, captured, comm) = BuildParser(config);
        await parser.StartAsync();

        RaiseMessage(comm, "advantech/74fe488d5d54/vision/status", "\"online\"");
        RaiseMessage(comm, "advantech/74fe488d5d54/vision/meta",
            """{"model":"yolo11n.pt","source":"OD_bottle_2.mp4","confThreshold":0.4,"iouThreshold":0.45,"demoVersion":"1.1.0"}""");

        captured.ShouldBeEmpty();
    }
}
