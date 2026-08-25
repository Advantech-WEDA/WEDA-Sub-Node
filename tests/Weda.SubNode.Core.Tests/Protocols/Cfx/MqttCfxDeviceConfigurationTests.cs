using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.Cfx;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

public class MqttCfxDeviceConfigurationTests
{
    private const string Dtmi = "dtmi:advantech:EdgeSync:Cfx;1";

    [Fact]
    public void ToDeviceConfiguration_CarriesBrokerAndTopicSettingsIntoCommunication()
    {
        // Arrange
        var configuration = new MqttCfxDeviceConfiguration
        {
            DeviceName = "SLD880A Line 1",
            CfxHandle = "SUNJSONG.SLD880A.0001",
        }
            .WithBroker("192.168.100.19")
            .WithCredentials("admin", "admin");

        configuration.ClientId = "wiseiot-yujietest-sub-001";

        // Act
        var deviceConfiguration = configuration.ToDeviceConfiguration();

        // Assert
        var communication = deviceConfiguration.DeviceCommunication;
        Assert.Equal("192.168.100.19", communication["BrokerHost"]);
        Assert.Equal(1883, communication["BrokerPort"]);
        Assert.Equal(false, communication["UseTls"]);
        Assert.Equal("wiseiot-yujietest-sub-001", communication["ClientId"]);
        Assert.Equal("admin", communication["Username"]);
        Assert.Equal("admin", communication["Password"]);
        Assert.Equal("SUNJSONG.SLD880A.0001", communication[CfxPubSubParser.CfxHandleKey]);
        Assert.Equal("CFX", communication[CfxPubSubParser.TopicRootKey]);
        Assert.Equal(3, communication[CfxPubSubParser.HandleSegmentsKey]);
    }

    [Fact]
    public void ToDeviceConfiguration_WithoutOptionalSettings_OmitsThemEntirely()
    {
        // Arrange — an absent handle means "every endpoint", which the parser distinguishes by the
        // key being missing rather than blank.
        var configuration = new MqttCfxDeviceConfiguration { DeviceName = "Discovery" };

        // Act
        var communication = configuration.ToDeviceConfiguration().DeviceCommunication;

        // Assert
        Assert.False(communication.ContainsKey(CfxPubSubParser.CfxHandleKey));
        Assert.False(communication.ContainsKey("ClientId"));
        Assert.False(communication.ContainsKey("Username"));
        Assert.False(communication.ContainsKey("Password"));
    }

    [Fact]
    public void ToDeviceConfiguration_ProducesAParserThatSubscribesToTheConfiguredEndpoint()
    {
        // Arrange — the round trip that matters: typed configuration must drive the real filter.
        var configuration = new MqttCfxDeviceConfiguration
        {
            DeviceName = "SLD880A",
            CfxHandle = "SUNJSONG.SLD880A.0001",
        }.AddMessage("CFX.ResourcePerformance.StationStateChanged", Dtmi);

        using var transport = new FakePubSub();

        // Act
        var parser = new CfxPubSubParser(
            configuration.ToDeviceConfiguration(),
            transport,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CfxPubSubParser>.Instance);

        // Assert
        Assert.Equal("SUNJSONG/SLD880A/0001/CFX/#", parser.SubscriptionFilter);
    }

    [Fact]
    public void AddMessage_DerivesSensorNameFromMessageName()
    {
        // Arrange
        var configuration = new MqttCfxDeviceConfiguration { DeviceName = "Test" };

        // Act
        configuration.AddMessage("CFX.ResourcePerformance.StationStateChanged", Dtmi);

        // Assert
        var sensor = Assert.Single(configuration.ToDeviceConfiguration().Sensors);
        Assert.Equal("cfx_station_state_changed", sensor.Name);
        Assert.Equal(
            "CFX.ResourcePerformance.StationStateChanged",
            sensor.Parameters[CfxSensorParameters.MessageNameKey]);
    }

    [Fact]
    public void AddMessage_ExplicitNameOverridesTheDerivedOne()
    {
        // Arrange
        var configuration = new MqttCfxDeviceConfiguration { DeviceName = "Test" };

        // Act
        configuration.AddMessage("CFX.Production.WorkStarted", Dtmi, name: "work_started", interval: 250);

        // Assert
        var sensor = Assert.Single(configuration.ToDeviceConfiguration().Sensors);
        Assert.Equal("work_started", sensor.Name);
        Assert.Equal(250, sensor.Report.Interval);
    }

    [Fact]
    public void AddAllCatalogMessages_AddsOneSensorPerCatalogueEntry()
    {
        // Arrange
        var configuration = new MqttCfxDeviceConfiguration { DeviceName = "Full line" };

        // Act
        configuration.AddAllCatalogMessages(Dtmi);

        // Assert
        var sensors = configuration.ToDeviceConfiguration().Sensors;
        Assert.Equal(CfxMessageCatalog.SupportedMessageNames.Count, sensors.Count);
        Assert.Equal(
            CfxMessageCatalog.SupportedMessageNames.OrderBy(n => n, StringComparer.Ordinal),
            sensors
                .Select(s => (string)s.Parameters[CfxSensorParameters.MessageNameKey])
                .OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void AddSensor_AppendsAFullySpecifiedSensor()
    {
        // Arrange
        var configuration = new MqttCfxDeviceConfiguration { DeviceName = "Test" };

        // Act
        configuration.AddSensor(new CfxSensorConfiguration
        {
            Name = "faults",
            Dtmi = Dtmi,
            MessageName = "CFX.ResourcePerformance.FaultOccurred",
            SensorGroup = SensorGroup.SYS,
            Enabled = false,
        });

        // Assert
        var sensor = Assert.Single(configuration.ToDeviceConfiguration().Sensors);
        Assert.Equal("faults", sensor.Name);
        Assert.False(sensor.Report.Enabled);
    }

    [Fact]
    public void AddSensor_NullSensor_Throws()
    {
        var configuration = new MqttCfxDeviceConfiguration { DeviceName = "Test" };

        Assert.Throws<ArgumentNullException>(() => configuration.AddSensor(null!));
    }

    [Fact]
    public void CfxSensorConfiguration_DeclaresTheApplicationJsonSchema()
    {
        // A CFX sensor reports a message body, which is a JSON document. The declared schema is what
        // drives the DTDL DTMI (dtmi:advantech:app:json) and the recording SchemaType, so declaring a
        // primitive such as "string" would transmit correctly while misreporting the payload.
        var sensor = new CfxSensorConfiguration
        {
            Name = "cfx_work_started",
            Dtmi = Dtmi,
            MessageName = "CFX.Production.WorkStarted",
        };

        Assert.Equal("application/json", sensor.Info.Schema);
    }

    [Fact]
    public void AddAllCatalogMessages_DeclaresApplicationJsonForEverySensor()
    {
        // Arrange
        var configuration = new MqttCfxDeviceConfiguration { DeviceName = "Full line" }
            .AddAllCatalogMessages(Dtmi);

        // Act
        var schemas = configuration.ToDeviceConfiguration().Sensors
            .Select(s => s.SensorInfo.Schema)
            .Distinct()
            .ToList();

        // Assert
        Assert.Equal(["application/json"], schemas);
    }

    [Fact]
    public void ApplicationJsonSchema_YieldsTheSharedJsonDtmi()
    {
        // Arrange — the schema is what selects the DTMI convention: MIME-typed sensors receive the
        // shared dtmi:advantech:app:json rather than a per-sensor generated DTMI.
        var sensors = new MqttCfxDeviceConfiguration { DeviceName = "SLD880A" }
            .AddMessage("CFX.Production.WorkStarted", dtmi: string.Empty)
            .AddMessage("CFX.ResourcePerformance.FaultOccurred", dtmi: string.Empty)
            .ToDeviceConfiguration()
            .Sensors;

        // Act
        DtdlGenerator.PopulateSensorDtmis(sensors, deviceKey: "SLD880A");

        // Assert
        Assert.Equal(["dtmi:advantech:app:json"], sensors.Select(s => s.Dtmi).Distinct());
    }

    [Fact]
    public void ApplicationJsonSensors_AreExcludedFromTheAutoGeneratedInterface()
    {
        // Arrange — MIME payloads are not emitted as DTDL telemetry contents; they are described by
        // their shared MIME DTMI instead.
        var sensors = new MqttCfxDeviceConfiguration { DeviceName = "SLD880A" }
            .AddAllCatalogMessages(Dtmi)
            .ToDeviceConfiguration()
            .Sensors;

        // Act
        var dtdlInterface = DtdlGenerator.GenerateInterface("SLD880A", sensors);

        // Assert
        Assert.Empty(dtdlInterface.Contents);
    }

    [Fact]
    public void CfxSensorConfiguration_DefaultsToSystemGroup()
    {
        // CFX messages are process events, not analog or digital channels.
        var sensor = new CfxSensorConfiguration
        {
            Name = "n",
            Dtmi = Dtmi,
            MessageName = "CFX.Production.WorkStarted",
        };

        Assert.Equal(SensorGroup.SYS, sensor.SensorGroup);
        Assert.True(sensor.Enabled);
        Assert.Equal(string.Empty, sensor.Unit);
    }

    [Fact]
    public void ToDeviceConfiguration_WithGroupId_GeneratesDeterministicResourceIds()
    {
        // Arrange
        static MqttCfxDeviceConfiguration Build() => new MqttCfxDeviceConfiguration
        {
            DeviceName = "SLD880A",
            GroupId = "11111111-1111-1111-1111-111111111111",
        }.AddMessage("CFX.Production.WorkStarted", Dtmi);

        // Act
        var first = Assert.Single(Build().ToDeviceConfiguration().Sensors).ResourceId;
        var second = Assert.Single(Build().ToDeviceConfiguration().Sensors).ResourceId;

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void ToDeviceConfiguration_WithoutGroupId_StillProducesDistinctResourceIds()
    {
        // Arrange
        var configuration = new MqttCfxDeviceConfiguration { DeviceName = "SLD880A" }
            .AddMessage("CFX.Production.WorkStarted", Dtmi)
            .AddMessage("CFX.Production.WorkCompleted", Dtmi);

        // Act
        var resourceIds = configuration.ToDeviceConfiguration().Sensors
            .Select(s => s.ResourceId)
            .ToList();

        // Assert
        Assert.Equal(2, resourceIds.Distinct().Count());
    }

    [Fact]
    public void ToDeviceConfiguration_ExplicitResourceId_IsPreserved()
    {
        // Arrange
        var configuration = new MqttCfxDeviceConfiguration { DeviceName = "SLD880A" };
        configuration.AddSensor(new CfxSensorConfiguration
        {
            Name = "work_started",
            Dtmi = Dtmi,
            MessageName = "CFX.Production.WorkStarted",
            ResourceId = "fixed-resource-id",
        });

        // Act
        var sensor = Assert.Single(configuration.ToDeviceConfiguration().Sensors);

        // Assert
        Assert.Equal("fixed-resource-id", sensor.ResourceId);
    }

    [Fact]
    public void ToDeviceConfiguration_HonoursCustomTopicRootAndHandleSegments()
    {
        // Arrange
        var configuration = new MqttCfxDeviceConfiguration
        {
            DeviceName = "Test",
            TopicRoot = "IPCCFX",
            HandleSegments = 2,
        }.AddMessage("CFX.Production.WorkStarted", Dtmi);

        using var transport = new FakePubSub();

        // Act
        var parser = new CfxPubSubParser(
            configuration.ToDeviceConfiguration(),
            transport,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CfxPubSubParser>.Instance);

        // Assert
        Assert.Equal("+/+/IPCCFX/#", parser.SubscriptionFilter);
    }

    [Fact]
    public void ToDeviceConfiguration_PropagatesEnabledFlagAndDtdlPath()
    {
        // Arrange
        var configuration = new MqttCfxDeviceConfiguration
        {
            DeviceName = "Test",
            Enabled = false,
            DtdlPath = "models/cfx.json",
            DeviceId = "explicit-device-id",
        };

        // Act
        var deviceConfiguration = configuration.ToDeviceConfiguration();

        // Assert
        Assert.False(deviceConfiguration.Enabled);
        Assert.Equal("models/cfx.json", deviceConfiguration.Dtdl.DtdlPath);
        Assert.Equal("explicit-device-id", deviceConfiguration.DeviceId);
    }

    [Fact]
    public void WithBroker_AndWithCredentials_AreChainable()
    {
        // Arrange & Act
        var configuration = new MqttCfxDeviceConfiguration { DeviceName = "Test" }
            .WithBroker("broker.internal", 8883, useTls: true)
            .WithCredentials("user", "pass");

        // Assert
        Assert.Equal("broker.internal", configuration.BrokerHost);
        Assert.Equal(8883, configuration.BrokerPort);
        Assert.True(configuration.UseTls);
        Assert.Equal("user", configuration.Username);
        Assert.Equal("pass", configuration.Password);
    }
}
