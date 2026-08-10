using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.Cfx;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

public class CfxPubSubParserTests
{
    private const string Handle = "SUNJSONG.SLD880A.0001";
    private const string StationStateChanged = "CFX.ResourcePerformance.StationStateChanged";
    private const string FaultOccurred = "CFX.ResourcePerformance.FaultOccurred";

    [Fact]
    public async Task StartAsync_WithCfxHandle_SubscribesToThatEndpointOnly()
    {
        // Arrange
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);

        // Act
        await parser.StartAsync();

        // Assert
        Assert.Equal(["SUNJSONG/SLD880A/0001/CFX/#"], transport.Subscribed);
    }

    [Fact]
    public async Task StartAsync_WithoutCfxHandle_SubscribesToEveryEndpoint()
    {
        // Arrange
        var transport = new FakePubSub();
        var parser = CreateParser(transport, cfxHandle: null, StationStateChanged);

        // Act
        await parser.StartAsync();

        // Assert
        Assert.Equal(["+/+/+/CFX/#"], transport.Subscribed);
    }

    [Fact]
    public async Task StartAsync_CalledTwice_SubscribesOnce()
    {
        // Arrange
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);

        // Act
        await parser.StartAsync();
        await parser.StartAsync();

        // Assert
        Assert.Single(transport.Subscribed);
        Assert.Equal(1, transport.MessageReceivedHandlerCount);
    }

    [Fact]
    public async Task StartAsync_WhenBrokerRefuses_ThrowsWithContextAndLeaksNoHandler()
    {
        // Arrange
        var transport = new FakePubSub(failSubscribe: true);
        var parser = CreateParser(transport, Handle, StationStateChanged);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => parser.StartAsync());

        // Assert — the broker failure is wrapped with the filter that failed, and a retry must not
        // accumulate handlers.
        Assert.Contains("SUNJSONG/SLD880A/0001/CFX/#", ex.Message);
        Assert.IsType<IOException>(ex.InnerException);
        Assert.Equal(0, transport.MessageReceivedHandlerCount);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesAndDetachesHandler()
    {
        // Arrange
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);
        await parser.StartAsync();

        // Act
        await parser.StopAsync();

        // Assert
        Assert.Equal(["SUNJSONG/SLD880A/0001/CFX/#"], transport.Unsubscribed);
        Assert.Equal(0, transport.MessageReceivedHandlerCount);
    }

    [Fact]
    public async Task StopAsync_WhenBrokerRefuses_StillDetachesHandler()
    {
        // Arrange
        var transport = new FakePubSub(failUnsubscribe: true);
        var parser = CreateParser(transport, Handle, StationStateChanged);
        await parser.StartAsync();

        // Act — a broker that refuses the unsubscribe must not leave the parser wired up.
        await parser.StopAsync();

        // Assert
        Assert.Equal(0, transport.MessageReceivedHandlerCount);
    }

    [Theory]
    [MemberData(nameof(AllMessages))]
    public async Task Deliver_EveryCapturedMessage_EmitsBodyAsRawJson(string messageName)
    {
        // Arrange
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, messageName);

        List<TelemetryMeasure>? received = null;
        parser.OnTelemetryReceived += measures => received = measures;
        await parser.StartAsync();

        // Act
        transport.Deliver(TopicFor(messageName), CfxFixtures.GzipPayload(messageName));

        // Assert
        Assert.NotNull(received);
        var measure = Assert.Single(received);
        Assert.Equal($"{messageName}-resource", measure.ResourceId);

        // The body is nested verbatim under "body", so nothing in the payload is lost.
        using var payload = JsonDocument.Parse(Assert.IsType<string>(measure.Value));
        Assert.Equal(
            $"{messageName}, CFX",
            payload.RootElement.GetProperty("body").GetProperty("$type").GetString());
    }

    [Fact]
    public async Task Deliver_CarriesEnvelopeFieldsInsideJsonValue()
    {
        // Arrange
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);

        List<TelemetryMeasure>? received = null;
        parser.OnTelemetryReceived += measures => received = measures;
        await parser.StartAsync();

        var topic = TopicFor(StationStateChanged);

        // Act
        transport.Deliver(topic, CfxFixtures.GzipPayload(StationStateChanged));

        // Assert
        var measure = Assert.Single(received!);

        using var payload = JsonDocument.Parse(Assert.IsType<string>(measure.Value));
        var root = payload.RootElement;

        Assert.Equal(StationStateChanged, root.GetProperty("messageName").GetString());
        Assert.Equal(Handle, root.GetProperty("source").GetString());
        Assert.Equal("edd39f23-41b2-4e22-b4c2-3dd35c78afde", root.GetProperty("uniqueId").GetString());
        Assert.Equal("1.3", root.GetProperty("version").GetString());
        Assert.Equal(topic, root.GetProperty("topic").GetString());
        Assert.Contains("+08:00", root.GetProperty("timeStamp").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("body").ValueKind);
    }

    [Fact]
    public async Task Deliver_LeavesMeasureMetadataUnset()
    {
        // A measure's metadata is the framework's chunked-transfer descriptor. An application/json
        // value is never chunked, so it never receives a transferId — and the WedaNode telemetry
        // proxy rejects any measure that carries metadata without one, which silently drops every
        // CFX message. Regression guard for that failure.
        // Arrange
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);

        List<TelemetryMeasure>? received = null;
        parser.OnTelemetryReceived += measures => received = measures;
        await parser.StartAsync();

        // Act
        transport.Deliver(TopicFor(StationStateChanged), CfxFixtures.GzipPayload(StationStateChanged));

        // Assert
        Assert.Null(Assert.Single(received!).Metadata);
    }

    [Fact]
    public async Task Deliver_UsesPublisherTimestampNotArrivalTime()
    {
        // Arrange
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);

        List<TelemetryMeasure>? received = null;
        parser.OnTelemetryReceived += measures => received = measures;
        await parser.StartAsync();

        // Act
        transport.Deliver(TopicFor(StationStateChanged), CfxFixtures.GzipPayload(StationStateChanged));

        // Assert — 2026-07-23T17:06:00.831417+08:00
        var expected = DateTimeOffset
            .Parse("2026-07-23T17:06:00.831417+08:00")
            .ToUnixTimeMilliseconds();

        Assert.Equal(expected, Assert.Single(received!).Timestamp);
    }

    [Fact]
    public async Task Deliver_MessageRoutesByEnvelopeNotByTopic()
    {
        // Arrange — the envelope is authoritative, so a message delivered on a mismatched topic is
        // still routed to the sensor bound to its MessageName.
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);

        List<TelemetryMeasure>? received = null;
        parser.OnTelemetryReceived += measures => received = measures;
        await parser.StartAsync();

        // Act
        transport.Deliver("SUNJSONG/SLD880A/0001/CFX/Totally/Wrong/Path", CfxFixtures.GzipPayload(StationStateChanged));

        // Assert
        Assert.NotNull(received);
        Assert.Single(received);
    }

    [Fact]
    public async Task Deliver_UnboundMessage_RaisesUnmappedEventAndNoTelemetry()
    {
        // Arrange
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);

        var telemetryRaised = false;
        CfxEnvelope? unmapped = null;
        parser.OnTelemetryReceived += _ => telemetryRaised = true;
        parser.OnUnmappedMessageReceived += envelope => unmapped = envelope;
        await parser.StartAsync();

        // Act
        transport.Deliver(TopicFor(FaultOccurred), CfxFixtures.GzipPayload(FaultOccurred));

        // Assert
        Assert.False(telemetryRaised);
        Assert.NotNull(unmapped);
        Assert.Equal(FaultOccurred, unmapped.MessageName);
    }

    [Fact]
    public async Task Deliver_OneMessageBoundToTwoSensors_EmitsBoth()
    {
        // Arrange
        var transport = new FakePubSub();
        var configuration = ConfigurationFor(
            Handle,
            new SensorSpec("primary", StationStateChanged),
            new SensorSpec("shadow", StationStateChanged));
        var parser = new CfxPubSubParser(configuration, transport, NullLogger<CfxPubSubParser>.Instance);

        List<TelemetryMeasure>? received = null;
        parser.OnTelemetryReceived += measures => received = measures;
        await parser.StartAsync();

        // Act
        transport.Deliver(TopicFor(StationStateChanged), CfxFixtures.GzipPayload(StationStateChanged));

        // Assert
        Assert.Equal(2, received!.Count);
        Assert.Equal(["primary-resource", "shadow-resource"], received.Select(m => m.ResourceId));
    }

    [Fact]
    public async Task Deliver_DisabledSensor_EmitsNothing()
    {
        // Arrange
        var transport = new FakePubSub();
        var configuration = ConfigurationFor(
            Handle,
            new SensorSpec(StationStateChanged, StationStateChanged, Enabled: false));
        var parser = new CfxPubSubParser(configuration, transport, NullLogger<CfxPubSubParser>.Instance);

        var raised = false;
        parser.OnTelemetryReceived += _ => raised = true;
        await parser.StartAsync();

        // Act
        transport.Deliver(TopicFor(StationStateChanged), CfxFixtures.GzipPayload(StationStateChanged));

        // Assert
        Assert.False(raised);
    }

    [Fact]
    public async Task Deliver_MessageNameCasingDrift_StillRoutes()
    {
        // Arrange — publisher casing has been observed to drift between equipment vendors.
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);

        List<TelemetryMeasure>? received = null;
        parser.OnTelemetryReceived += measures => received = measures;
        await parser.StartAsync();

        var json = """
            {
              "MessageName": "cfx.resourceperformance.stationstatechanged",
              "MessageBody": { "NewState": "5000" }
            }
            """;

        // Act
        transport.Deliver(TopicFor(StationStateChanged), Encoding.UTF8.GetBytes(json));

        // Assert
        Assert.NotNull(received);
        Assert.Single(received);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cfx payload at all")]
    [InlineData("{\"MessageName\":")]
    [InlineData("{\"MessageName\":\"CFX.Production.WorkStarted\"}")]
    public async Task Deliver_UndecodableOrMalformedPayload_IsDroppedWithoutThrowing(string payload)
    {
        // Arrange — one bad message from one endpoint must not tear down the shared subscription.
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);

        var raised = false;
        parser.OnTelemetryReceived += _ => raised = true;
        await parser.StartAsync();

        // Act
        transport.Deliver(TopicFor(StationStateChanged), Encoding.UTF8.GetBytes(payload));

        // Assert
        Assert.False(raised);

        // The subscription survives, so a subsequent good message still reports.
        transport.Deliver(TopicFor(StationStateChanged), CfxFixtures.GzipPayload(StationStateChanged));
        Assert.True(raised);
    }

    [Fact]
    public async Task Deliver_SubscriberThatThrows_DoesNotBreakTheSubscription()
    {
        // Arrange
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);

        parser.OnTelemetryReceived += _ => throw new InvalidOperationException("downstream boom");
        await parser.StartAsync();

        // Act & Assert — the transport callback swallows downstream faults rather than letting them
        // propagate into the broker client.
        transport.Deliver(TopicFor(StationStateChanged), CfxFixtures.GzipPayload(StationStateChanged));
        transport.Deliver(TopicFor(StationStateChanged), CfxFixtures.GzipPayload(StationStateChanged));

        Assert.Equal(1, transport.MessageReceivedHandlerCount);
    }

    [Fact]
    public void Constructor_SensorWithoutMessageName_ThrowsAtStartup()
    {
        // Arrange
        var transport = new FakePubSub();
        var configuration = ConfigurationFor(Handle);
        configuration.Sensors.Add(new Sensor
        {
            ResourceId = "broken-resource",
            Name = "broken",
            Dtmi = "dtmi:advantech:EdgeSync:Cfx;1",
            DeviceResourceId = "device",
            SensorGroup = SensorGroup.SYS,
            Parameters = [],
            Report = new SensorReport { Enabled = true },
        });

        // Act
        var ex = Assert.Throws<InvalidOperationException>(
            () => new CfxPubSubParser(configuration, transport, NullLogger<CfxPubSubParser>.Instance));

        // Assert — a configuration typo must fail loudly, not produce a silent never-reporting sensor.
        Assert.Contains("MessageName", ex.Message);
        Assert.Contains("broken", ex.Message);
    }

    [Fact]
    public void Constructor_NonIntegerHandleSegments_ThrowsWithContext()
    {
        // Arrange
        var transport = new FakePubSub();
        var configuration = ConfigurationFor(cfxHandle: null, new SensorSpec("s", StationStateChanged));
        configuration.DeviceCommunication[CfxPubSubParser.HandleSegmentsKey] = "three";

        // Act
        var ex = Assert.Throws<InvalidOperationException>(
            () => new CfxPubSubParser(configuration, transport, NullLogger<CfxPubSubParser>.Instance));

        // Assert
        Assert.Contains("HandleSegments", ex.Message);
        Assert.Contains("three", ex.Message);
    }

    [Fact]
    public async Task ExecuteCommandAsync_AlwaysReportsUnsupported()
    {
        // Arrange
        var transport = new FakePubSub();
        var parser = CreateParser(transport, Handle, StationStateChanged);

        // Act
        var result = await parser.ExecuteCommandAsync(new DeviceCommand { DeviceCmd = "SetRecipe" });

        // Assert — the parser is subscribe-only; accepting a command it will never send would be
        // worse than reporting failure.
        Assert.True(result.IsError);
        Assert.Equal("Cfx.Command.NotSupported", result.FirstError.Code);
        Assert.Empty(transport.Published);
    }

    public static TheoryData<string> AllMessages() => CfxFixtures.AllMessageNames();

    private static string TopicFor(string messageName) =>
        $"{CfxTopic.ToTopicPrefix(Handle)}/{CfxTopic.DefaultRoot}/{CfxMessageCatalog.ToRelativeTopicPath(messageName)}";

    private static CfxPubSubParser CreateParser(
        FakePubSub transport,
        string? cfxHandle,
        params string[] messageNames)
    {
        var configuration = ConfigurationFor(
            cfxHandle,
            messageNames.Select(m => new SensorSpec(m, m)).ToArray());

        return new CfxPubSubParser(configuration, transport, NullLogger<CfxPubSubParser>.Instance);
    }

    private static DeviceConfiguration ConfigurationFor(string? cfxHandle, params SensorSpec[] sensors)
    {
        var communication = new Dictionary<string, object>
        {
            ["BrokerHost"] = "192.168.100.19",
            ["BrokerPort"] = 1883,
        };

        if (cfxHandle is not null)
        {
            communication[CfxPubSubParser.CfxHandleKey] = cfxHandle;
        }

        return new DeviceConfiguration
        {
            DeviceName = "CFX Endpoint Test",
            SubNodeInfo = new SubNodeInfo
            {
                Name = "TestSubNode",
                Manufacturer = "SUNJSONG",
                Model = "SLD880A",
                SwVersion = "1.0",
                SubNodeType = SubNodeType.CustomDevice,
            },
            DeviceCommunication = communication,
            Sensors = sensors.Select(spec => new Sensor
            {
                ResourceId = $"{spec.Name}-resource",
                Name = spec.Name,
                Dtmi = "dtmi:advantech:EdgeSync:Cfx;1",
                DeviceResourceId = "test-device",
                SensorGroup = SensorGroup.SYS,
                Parameters = new Dictionary<string, object>
                {
                    [CfxSensorParameters.MessageNameKey] = spec.MessageName,
                },
                Report = new SensorReport { Enabled = spec.Enabled },
            }).ToList(),
        };
    }

    private sealed record SensorSpec(string Name, string MessageName, bool Enabled = true);
}
