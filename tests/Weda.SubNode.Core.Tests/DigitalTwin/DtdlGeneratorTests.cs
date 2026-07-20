using Shouldly;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Xunit;

namespace Weda.SubNode.Core.Tests.DigitalTwin;

/// <summary>
/// Tests for DtdlGenerator auto-generation functionality.
/// </summary>
public class DtdlGeneratorTests
{
    #region GenerateShortId Tests

    [Fact]
    public void GenerateShortId_Should_Return8CharHex()
    {
        // Arrange
        var name = "temperature_sensor";

        // Act
        var shortId = DtdlGenerator.GenerateShortId(name);

        // Assert
        shortId.ShouldNotBeNullOrEmpty();
        shortId.Length.ShouldBe(8);
        shortId.ShouldMatch("^[a-f0-9]{8}$");
    }

    [Fact]
    public void GenerateShortId_Should_BeDeterministic()
    {
        // Arrange
        var name = "channel.0";

        // Act
        var id1 = DtdlGenerator.GenerateShortId(name);
        var id2 = DtdlGenerator.GenerateShortId(name);

        // Assert
        id1.ShouldBe(id2);
    }

    [Fact]
    public void GenerateShortId_Should_BeCaseInsensitive()
    {
        // Arrange & Act
        var lower = DtdlGenerator.GenerateShortId("temperature");
        var upper = DtdlGenerator.GenerateShortId("TEMPERATURE");
        var mixed = DtdlGenerator.GenerateShortId("Temperature");

        // Assert
        lower.ShouldBe(upper);
        lower.ShouldBe(mixed);
    }

    [Fact]
    public void GenerateShortId_Should_ProduceDifferentIdsForDifferentNames()
    {
        // Arrange & Act
        var id1 = DtdlGenerator.GenerateShortId("sensor1");
        var id2 = DtdlGenerator.GenerateShortId("sensor2");

        // Assert
        id1.ShouldNotBe(id2);
    }

    #endregion

    #region GenerateDtmi Tests

    [Fact]
    public void GenerateDtmi_Should_FollowDtmiFormat()
    {
        // Arrange
        var name = "temperature_sensor";

        // Act
        var dtmi = DtdlGenerator.GenerateDtmi(name);

        // Assert
        dtmi.ShouldStartWith("dtmi:autogen:");
        dtmi.ShouldEndWith(";1");
    }

    [Fact]
    public void GenerateDtmi_Should_IncludeNamespaceSegment()
    {
        // Arrange
        var name = "channel.0";
        var namespaceSegment = "AI";

        // Act
        var dtmi = DtdlGenerator.GenerateDtmi(name, namespaceSegment);

        // Assert
        dtmi.ShouldStartWith("dtmi:autogen:ai:");
        dtmi.ShouldEndWith(";1");
    }

    [Fact]
    public void GenerateDtmi_Should_UseSpecifiedVersion()
    {
        // Arrange
        var name = "sensor";
        var version = 2;

        // Act
        var dtmi = DtdlGenerator.GenerateDtmi(name, version: version);

        // Assert
        dtmi.ShouldEndWith(";2");
    }

    [Fact]
    public void GenerateDtmi_Should_SanitizeNamespaceSegment()
    {
        // Arrange
        var name = "sensor";
        var namespaceSegment = "AI_Channel-1";

        // Act
        var dtmi = DtdlGenerator.GenerateDtmi(name, namespaceSegment);

        // Assert
        dtmi.ShouldContain(":aichannel1:");
    }

    [Fact]
    public void GenerateDtmi_Should_UseSubNamespaceWhenDeviceKeyProvided()
    {
        // Arrange
        var name = "channel.0";

        // Act
        var dtmi = DtdlGenerator.GenerateDtmi(name, "AI", deviceKey: "MyFirstDevice");

        // Assert
        dtmi.ShouldStartWith("dtmi:sub:MyFirstDevice:ai:");
        dtmi.ShouldEndWith(";1");
    }

    [Fact]
    public void GenerateDtmi_Should_PreserveDeviceKeyCasingAndUnderscores()
    {
        // The device key must stay recognizable in the DTMI so users can tell
        // which device a flattened sensor came from.
        var dtmi = DtdlGenerator.GenerateDtmi("sensor", "AI", deviceKey: "ModbusTcp_DeviceConfig");

        dtmi.ShouldStartWith("dtmi:sub:ModbusTcp_DeviceConfig:ai:");
    }

    [Fact]
    public void GenerateDtmi_Should_FallBackToAutogenWhenDeviceKeyMissing()
    {
        // Arrange & Act
        var withNull = DtdlGenerator.GenerateDtmi("sensor", "AI", deviceKey: null);
        var withEmpty = DtdlGenerator.GenerateDtmi("sensor", "AI", deviceKey: "");

        // Assert
        withNull.ShouldStartWith("dtmi:autogen:ai:");
        withEmpty.ShouldStartWith("dtmi:autogen:ai:");
    }

    [Fact]
    public void GenerateDtmi_Should_SanitizeDeviceKey()
    {
        // Dots, dashes and spaces become underscores; casing is preserved.
        var dtmi = DtdlGenerator.GenerateDtmi("sensor", "AI", deviceKey: "My Modbus-Device.1");

        dtmi.ShouldStartWith("dtmi:sub:My_Modbus_Device_1:ai:");
    }

    [Fact]
    public void GenerateDtmi_Should_PrefixDeviceKeyStartingWithDigit()
    {
        // DTMI path segments must start with a letter.
        var dtmi = DtdlGenerator.GenerateDtmi("sensor", "AI", deviceKey: "4012-Wise");

        dtmi.ShouldStartWith("dtmi:sub:d_4012_Wise:ai:");
    }

    [Fact]
    public void GenerateDtmi_Should_FallBackToHashSegmentWhenKeyHasNoAsciiChars()
    {
        // Non-ASCII keys (e.g. Chinese device names) fall back to a hash-based
        // segment so distinct devices still get distinct namespaces.
        var dtmiA = DtdlGenerator.GenerateDtmi("sensor", "AI", deviceKey: "溫度計");
        var dtmiB = DtdlGenerator.GenerateDtmi("sensor", "AI", deviceKey: "濕度計");

        dtmiA.ShouldMatch(@"^dtmi:sub:d[a-f0-9]{8}:ai:");
        dtmiA.ShouldNotBe(dtmiB);
    }

    [Fact]
    public void GenerateDtmi_Should_ProduceDistinctDtmisForSameSensorOnDifferentDevices()
    {
        // The device-cfg-key namespace exists so that same-named sensors on
        // different devices no longer collide on the same hash-based DTMI.
        var deviceA = DtdlGenerator.GenerateDtmi("channel.0", "AI", deviceKey: "DeviceA");
        var deviceB = DtdlGenerator.GenerateDtmi("channel.0", "AI", deviceKey: "DeviceB");

        deviceA.ShouldNotBe(deviceB);
    }

    #endregion

    #region GenerateTelemetryContent Tests

    [Fact]
    public void GenerateTelemetryContent_Should_CreateValidContent()
    {
        // Arrange
        var sensor = new Sensor
        {
            Name = "temperature_sensor",
            SensorGroup = SensorGroup.TEMP,
            SensorInfo = new SensorInfo { Description = "Main temperature reading" }
        };

        // Act
        var content = DtdlGenerator.GenerateTelemetryContent(sensor);

        // Assert
        content.ShouldNotBeNull();
        content.Type.ShouldBe("Telemetry");
        content.Name.ShouldBe("temperature_sensor");
        content.Schema.ShouldBe("double");
        content.Description.ShouldBe("Main temperature reading");
    }

    [Fact]
    public void GenerateTelemetryContent_Should_UseExistingDtmiIfProvided()
    {
        // Arrange
        var existingDtmi = "dtmi:advantech:EdgeSync:Temperature;1";
        var sensor = new Sensor
        {
            Name = "temperature",
            SensorGroup = SensorGroup.TEMP,
            Dtmi = existingDtmi,
            SensorInfo = new SensorInfo { Schema = "double" }
        };

        // Act
        var content = DtdlGenerator.GenerateTelemetryContent(sensor);

        // Assert
        content.Id.ShouldBe(existingDtmi);
    }

    [Fact]
    public void GenerateTelemetryContent_Should_AutoGenerateDtmiIfMissing()
    {
        // Arrange
        var sensor = new Sensor
        {
            Name = "channel.0",
            SensorGroup = SensorGroup.AI,
            SensorInfo = new SensorInfo { Schema = "double" }
        };

        // Act
        var content = DtdlGenerator.GenerateTelemetryContent(sensor);

        // Assert
        content.Id.ShouldStartWith("dtmi:autogen:ai:");
        content.Id.ShouldEndWith(";1");
    }

    [Fact]
    public void GenerateTelemetryContent_Should_UseEffectiveSchema()
    {
        // Arrange - AI sensor without explicit schema should use "double"
        var aiSensor = new Sensor { Name = "voltage", SensorGroup = SensorGroup.AI, SensorInfo = new SensorInfo { Schema = "double" } };

        // Arrange - DI sensor without explicit schema should use "boolean"
        var diSensor = new Sensor { Name = "switch", SensorGroup = SensorGroup.DI, SensorInfo = new SensorInfo { Schema = "boolean" } };

        // Arrange - Sensor with explicit schema
        var customSensor = new Sensor
        {
            Name = "counter",
            SensorGroup = SensorGroup.SYS,
            SensorInfo = new SensorInfo { Schema = "integer" }
        };

        // Act
        var aiContent = DtdlGenerator.GenerateTelemetryContent(aiSensor);
        var diContent = DtdlGenerator.GenerateTelemetryContent(diSensor);
        var customContent = DtdlGenerator.GenerateTelemetryContent(customSensor);

        // Assert
        aiContent.Schema.ShouldBe("double");
        diContent.Schema.ShouldBe("boolean");
        customContent.Schema.ShouldBe("integer");
    }

    [Fact]
    public void GenerateTelemetryContent_Should_UseEffectiveDisplayName()
    {
        // Arrange - Sensor without DisplayName should derive from Name
        var sensorWithoutDisplayName = new Sensor
        {
            Name = "temperature_sensor",
            SensorGroup = SensorGroup.TEMP,
            SensorInfo = new SensorInfo { Schema = "double" }
        };

        // Arrange - Sensor with explicit DisplayName
        var sensorWithDisplayName = new Sensor
        {
            Name = "temp",
            SensorGroup = SensorGroup.TEMP,
            SensorInfo = new SensorInfo { DisplayName = "Main Temperature Sensor" }
        };

        // Act
        var content1 = DtdlGenerator.GenerateTelemetryContent(sensorWithoutDisplayName);
        var content2 = DtdlGenerator.GenerateTelemetryContent(sensorWithDisplayName);

        // Assert
        content1.DisplayName.ShouldBe("Temperature Sensor");
        content2.DisplayName.ShouldBe("Main Temperature Sensor");
    }

    [Fact]
    public void GenerateTelemetryContent_Should_SanitizeSensorName()
    {
        // Arrange
        var sensor = new Sensor
        {
            Name = "channel.0",
            SensorGroup = SensorGroup.AI,
            SensorInfo = new SensorInfo { Schema = "double" }
        };

        // Act
        var content = DtdlGenerator.GenerateTelemetryContent(sensor);

        // Assert
        // DTDL names cannot contain dots, should be replaced with underscores
        content.Name.ShouldBe("channel_0");
    }

    [Fact]
    public void GenerateTelemetryContent_Should_NotEmitUnit_BecauseDtdlV3RejectsIt()
    {
        // `unit` on a bare Telemetry is rejected by DTDLParser under core
        // DTDL v3 (undefined term). The unit travels in the upload payload via
        // deviceCapabilities.sensors[i].unit instead, not in the DTDL itself.
        var sensor = new Sensor
        {
            Name = "temperature",
            SensorGroup = SensorGroup.TEMP,
            Report = new SensorReport { Unit = "celsius" },
            SensorInfo = new SensorInfo { Schema = "double" }
        };

        var content = DtdlGenerator.GenerateTelemetryContent(sensor);

        // The DtdlContent.Unit property has been removed entirely; nothing to
        // assert on directly. The contract is: the serialized JSON must not
        // contain a "unit" key.
        var json = System.Text.Json.JsonSerializer.Serialize(content);
        json.ShouldNotContain("\"unit\"");
    }

    #endregion

    #region GenerateInterface Tests

    [Fact]
    public void GenerateInterface_Should_CreateValidInterface()
    {
        // Arrange
        var deviceName = "MyModbusDevice";
        var sensors = new List<Sensor>
        {
            new() { Name = "channel.0", SensorGroup = SensorGroup.AI, SensorInfo = new SensorInfo { Schema = "double" } },
            new() { Name = "channel.1", SensorGroup = SensorGroup.AI, SensorInfo = new SensorInfo { Schema = "double" } }
        };

        // Act
        var dtdlInterface = DtdlGenerator.GenerateInterface(deviceName, sensors);

        // Assert
        dtdlInterface.ShouldNotBeNull();
        dtdlInterface.Type.ShouldBe("Interface");
        dtdlInterface.Context.ShouldBe("dtmi:dtdl:context;3");
        dtdlInterface.Id.ShouldStartWith("dtmi:sub:MyModbusDevice:interface:");
        dtdlInterface.Contents.Count.ShouldBe(2);
    }

    [Fact]
    public void GenerateInterface_Should_UseCustomDisplayNameAndDescription()
    {
        // Arrange
        var deviceName = "WISE-4012";
        var displayName = "WISE-4012 Modbus Device";
        var description = "4-channel analog input device";
        var sensors = new List<Sensor>
        {
            new() { Name = "ch0", SensorGroup = SensorGroup.AI, SensorInfo = new SensorInfo { Schema = "double" } }
        };

        // Act
        var dtdlInterface = DtdlGenerator.GenerateInterface(
            deviceName, sensors, displayName, description);

        // Assert
        dtdlInterface.DisplayName.ShouldBe(displayName);
        dtdlInterface.Description.ShouldBe(description);
    }

    [Fact]
    public void GenerateInterface_Should_GenerateDefaultDisplayNameFromDeviceName()
    {
        // Arrange
        var deviceName = "my_modbus_device";
        var sensors = new List<Sensor>();

        // Act
        var dtdlInterface = DtdlGenerator.GenerateInterface(deviceName, sensors);

        // Assert
        dtdlInterface.DisplayName.ShouldBe("My Modbus Device");
    }

    [Fact]
    public void GenerateInterface_Should_HandleEmptySensorList()
    {
        // Arrange
        var deviceName = "EmptyDevice";
        var sensors = new List<Sensor>();

        // Act
        var dtdlInterface = DtdlGenerator.GenerateInterface(deviceName, sensors);

        // Assert
        dtdlInterface.ShouldNotBeNull();
        dtdlInterface.Contents.ShouldBeEmpty();
    }

    #endregion

    #region PopulateSensorDtmis Tests

    [Fact]
    public void PopulateSensorDtmis_Should_FillEmptyDtmis()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "channel.0", SensorGroup = SensorGroup.AI, SensorInfo = new SensorInfo { Schema = "double" } },
            new() { Name = "channel.1", SensorGroup = SensorGroup.AI, Dtmi = "", SensorInfo = new SensorInfo { Schema = "double" } },
            new() { Name = "channel.2", SensorGroup = SensorGroup.AI, SensorInfo = new SensorInfo { Schema = "double" } }
        };

        // Act
        DtdlGenerator.PopulateSensorDtmis(sensors);

        // Assert
        sensors[0].Dtmi.ShouldStartWith("dtmi:autogen:ai:");
        sensors[1].Dtmi.ShouldStartWith("dtmi:autogen:ai:");
        sensors[2].Dtmi.ShouldStartWith("dtmi:autogen:ai:");
    }

    [Fact]
    public void PopulateSensorDtmis_Should_PreserveExistingDtmis()
    {
        // Arrange
        var existingDtmi = "dtmi:advantech:EdgeSync:AI;1";
        var sensors = new List<Sensor>
        {
            new() { Name = "channel.0", SensorGroup = SensorGroup.AI, Dtmi = existingDtmi, SensorInfo = new SensorInfo { Schema = "double" } },
            new() { Name = "channel.1", SensorGroup = SensorGroup.AI, SensorInfo = new SensorInfo { Schema = "double" } }
        };

        // Act
        DtdlGenerator.PopulateSensorDtmis(sensors);

        // Assert
        sensors[0].Dtmi.ShouldBe(existingDtmi);
        sensors[1].Dtmi.ShouldStartWith("dtmi:autogen:ai:");
    }

    [Fact]
    public void PopulateSensorDtmis_Should_GenerateDifferentDtmisForDifferentSensors()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "temperature", SensorGroup = SensorGroup.TEMP, SensorInfo = new SensorInfo { Schema = "double" } },
            new() { Name = "humidity", SensorGroup = SensorGroup.TEMP, SensorInfo = new SensorInfo { Schema = "double" } }
        };

        // Act
        DtdlGenerator.PopulateSensorDtmis(sensors);

        // Assert
        sensors[0].Dtmi.ShouldNotBe(sensors[1].Dtmi);
    }

    #endregion

    #region ValidateManualDtdlConfiguration Tests

    [Fact]
    public void ValidateManualDtdlConfiguration_Should_ReturnEmptyForValidConfig()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "ch0", Dtmi = "dtmi:test:sensor1;1", SensorInfo = new SensorInfo { Schema = "double" } },
            new() { Name = "ch1", Dtmi = "dtmi:test:sensor2;1", SensorInfo = new SensorInfo { Schema = "double" } }
        };
        var dtdlPath = "dtdl/device.json";

        // Act
        var errors = DtdlGenerator.ValidateManualDtdlConfiguration(sensors, dtdlPath);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateManualDtdlConfiguration_Should_ErrorOnMissingDtdlPath()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "ch0", Dtmi = "dtmi:test:sensor1;1", SensorInfo = new SensorInfo { Schema = "double" } }
        };

        // Act
        var errors = DtdlGenerator.ValidateManualDtdlConfiguration(sensors, null);

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].ShouldContain("DtdlPath is required");
    }

    [Fact]
    public void ValidateManualDtdlConfiguration_Should_ErrorOnEmptyDtdlPath()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "ch0", Dtmi = "dtmi:test:sensor1;1", SensorInfo = new SensorInfo { Schema = "double" } }
        };

        // Act
        var errors = DtdlGenerator.ValidateManualDtdlConfiguration(sensors, "   ");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].ShouldContain("DtdlPath is required");
    }

    [Fact]
    public void ValidateManualDtdlConfiguration_Should_ErrorOnMissingSensorDtmi()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "ch0", Dtmi = "dtmi:test:sensor1;1", SensorInfo = new SensorInfo { Schema = "double" } },
            new() { Name = "ch1", SensorInfo = new SensorInfo { Schema = "double" } },  // Missing Dtmi
            new() { Name = "ch2", Dtmi = "", SensorInfo = new SensorInfo { Schema = "double" } }  // Empty Dtmi
        };
        var dtdlPath = "dtdl/device.json";

        // Act
        var errors = DtdlGenerator.ValidateManualDtdlConfiguration(sensors, dtdlPath);

        // Assert
        errors.Count.ShouldBe(2);
        errors.ShouldContain(e => e.Contains("ch1") && e.Contains("missing required Dtmi"));
        errors.ShouldContain(e => e.Contains("ch2") && e.Contains("missing required Dtmi"));
    }

    [Fact]
    public void ValidateManualDtdlConfiguration_Should_ReturnAllErrors()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "ch0", SensorInfo = new SensorInfo { Schema = "double" } },  // Missing Dtmi
            new() { Name = "ch1", SensorInfo = new SensorInfo { Schema = "double" } }   // Missing Dtmi
        };

        // Act
        var errors = DtdlGenerator.ValidateManualDtdlConfiguration(sensors, null);

        // Assert
        errors.Count.ShouldBe(3);  // 1 for DtdlPath + 2 for sensors
    }

    #endregion

    #region IsMimeType Tests

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("application/json", true)]
    [InlineData("application/octet-stream", true)]
    [InlineData("video/mp4", true)]
    [InlineData("double", false)]
    [InlineData("integer", false)]
    [InlineData("boolean", false)]
    [InlineData("string", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsMimeType_Should_CorrectlyIdentifyMimeTypes(string? schema, bool expected)
    {
        // Act
        var result = DtdlGenerator.IsMimeType(schema);

        // Assert
        result.ShouldBe(expected);
    }

    #endregion

    #region GenerateMimeTypeDtmi Tests

    [Fact]
    public void GenerateMimeTypeDtmi_Should_ConvertImageJpeg()
    {
        // Arrange
        var schema = "image/jpeg";

        // Act
        var dtmi = DtdlGenerator.GenerateMimeTypeDtmi(schema);

        // Assert
        dtmi.ShouldBe("dtmi:advantech:image:jpeg");
    }

    [Fact]
    public void GenerateMimeTypeDtmi_Should_ConvertImagePng()
    {
        // Arrange
        var schema = "image/png";

        // Act
        var dtmi = DtdlGenerator.GenerateMimeTypeDtmi(schema);

        // Assert
        dtmi.ShouldBe("dtmi:advantech:image:png");
    }

    [Fact]
    public void GenerateMimeTypeDtmi_Should_ShortenApplicationToApp()
    {
        // Arrange
        var schema = "application/json";

        // Act
        var dtmi = DtdlGenerator.GenerateMimeTypeDtmi(schema);

        // Assert
        dtmi.ShouldBe("dtmi:advantech:app:json");
    }

    [Fact]
    public void GenerateMimeTypeDtmi_Should_ConvertApplicationOctetStream()
    {
        // Arrange
        var schema = "application/octet-stream";

        // Act
        var dtmi = DtdlGenerator.GenerateMimeTypeDtmi(schema);

        // Assert
        dtmi.ShouldBe("dtmi:advantech:app:octet-stream");
    }

    [Fact]
    public void GenerateMimeTypeDtmi_Should_ReplacePlusWithHyphen()
    {
        // Arrange - application/vnd.api+json should become app:vnd.api-json
        var schema = "application/vnd.api+json";

        // Act
        var dtmi = DtdlGenerator.GenerateMimeTypeDtmi(schema);

        // Assert
        dtmi.ShouldBe("dtmi:advantech:app:vnd.api-json");
    }

    [Fact]
    public void GenerateMimeTypeDtmi_Should_HandleVideoMimeType()
    {
        // Arrange
        var schema = "video/mp4";

        // Act
        var dtmi = DtdlGenerator.GenerateMimeTypeDtmi(schema);

        // Assert
        dtmi.ShouldBe("dtmi:advantech:video:mp4");
    }

    [Fact]
    public void GenerateMimeTypeDtmi_Should_HandleAudioMimeType()
    {
        // Arrange
        var schema = "audio/mpeg";

        // Act
        var dtmi = DtdlGenerator.GenerateMimeTypeDtmi(schema);

        // Assert
        dtmi.ShouldBe("dtmi:advantech:audio:mpeg");
    }

    #endregion

    #region GenerateInterface MimeType Tests

    [Fact]
    public void GenerateInterface_Should_SkipMimeTypeSensors()
    {
        // Arrange
        var deviceName = "CameraDevice";
        var sensors = new List<Sensor>
        {
            new() { Name = "temperature", SensorGroup = SensorGroup.TEMP, SensorInfo = new SensorInfo { Schema = "double" } },
            new() { Name = "snapshot", SensorGroup = SensorGroup.SYS, SensorInfo = new SensorInfo { Schema = "image/jpeg" } },
            new() { Name = "humidity", SensorGroup = SensorGroup.TEMP, SensorInfo = new SensorInfo { Schema = "double" } }
        };

        // Act
        var dtdlInterface = DtdlGenerator.GenerateInterface(deviceName, sensors);

        // Assert
        dtdlInterface.Contents.Count.ShouldBe(2); // Only temperature and humidity
        dtdlInterface.Contents.ShouldAllBe(c => c.Name != "snapshot");
        dtdlInterface.Contents.ShouldContain(c => c.Name == "temperature");
        dtdlInterface.Contents.ShouldContain(c => c.Name == "humidity");
    }

    [Fact]
    public void GenerateInterface_Should_HandleAllMimeTypeSensors()
    {
        // Arrange - Device with only MIME type sensors
        var deviceName = "CameraOnlyDevice";
        var sensors = new List<Sensor>
        {
            new() { Name = "snapshot", SensorGroup = SensorGroup.SYS, SensorInfo = new SensorInfo { Schema = "image/jpeg" } },
            new() { Name = "video", SensorGroup = SensorGroup.SYS, SensorInfo = new SensorInfo { Schema = "video/mp4" } }
        };

        // Act
        var dtdlInterface = DtdlGenerator.GenerateInterface(deviceName, sensors);

        // Assert
        dtdlInterface.Contents.ShouldBeEmpty();
    }

    #endregion

    #region PopulateSensorDtmis MimeType Tests

    [Fact]
    public void PopulateSensorDtmis_Should_UseMimeTypeDtmiForMimeTypeSensors()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "temperature", SensorGroup = SensorGroup.TEMP, SensorInfo = new SensorInfo { Schema = "double" } },
            new() { Name = "snapshot", SensorGroup = SensorGroup.SYS, SensorInfo = new SensorInfo { Schema = "image/jpeg" } }
        };

        // Act
        DtdlGenerator.PopulateSensorDtmis(sensors);

        // Assert
        sensors[0].Dtmi.ShouldStartWith("dtmi:autogen:temp:");
        sensors[1].Dtmi.ShouldBe("dtmi:advantech:image:jpeg");
    }

    [Fact]
    public void PopulateSensorDtmis_Should_NotOverwriteExistingDtmiForMimeTypeSensors()
    {
        // Arrange - User should NOT set custom DTMI for MIME type, but if they do (before validation catches it)
        var existingDtmi = "dtmi:custom:image:sensor;1";
        var sensors = new List<Sensor>
        {
            new() { Name = "snapshot", SensorGroup = SensorGroup.SYS, SensorInfo = new SensorInfo { Schema = "image/jpeg" }, Dtmi = existingDtmi }
        };

        // Act
        DtdlGenerator.PopulateSensorDtmis(sensors);

        // Assert - Should preserve existing (validation layer will catch the invalid combination)
        sensors[0].Dtmi.ShouldBe(existingDtmi);
    }

    [Fact]
    public void PopulateSensorDtmis_Should_HandleMixedSensorTypes()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "ch0", SensorGroup = SensorGroup.AI, SensorInfo = new SensorInfo { Schema = "double" } },
            new() { Name = "snapshot", SensorGroup = SensorGroup.SYS, SensorInfo = new SensorInfo { Schema = "image/jpeg" } },
            new() { Name = "ch1", SensorGroup = SensorGroup.AI, SensorInfo = new SensorInfo { Schema = "integer" } },
            new() { Name = "config", SensorGroup = SensorGroup.SYS, SensorInfo = new SensorInfo { Schema = "application/json" } }
        };

        // Act
        DtdlGenerator.PopulateSensorDtmis(sensors);

        // Assert
        sensors[0].Dtmi.ShouldStartWith("dtmi:autogen:ai:");
        sensors[1].Dtmi.ShouldBe("dtmi:advantech:image:jpeg");
        sensors[2].Dtmi.ShouldStartWith("dtmi:autogen:ai:");
        sensors[3].Dtmi.ShouldBe("dtmi:advantech:app:json");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void FullWorkflow_Should_GenerateCompleteDtdlInterface()
    {
        // Arrange - Simulate a real device configuration
        var deviceName = "WISE-4012";
        var sensors = new List<Sensor>
        {
            new()
            {
                Name = "channel.0",
                SensorGroup = SensorGroup.AI,
                SensorInfo = new SensorInfo
                {
                    Schema = "double",
                    DisplayName = "Analog Input 0",
                    Description = "First analog input channel"
                },
                Report = new SensorReport { Unit = "mV" }
            },
            new()
            {
                Name = "channel.1",
                SensorGroup = SensorGroup.AI,
                SensorInfo = new SensorInfo { DisplayName = "Analog Input 1" }
            },
            new()
            {
                Name = "do.0",
                SensorGroup = SensorGroup.DO,
                SensorInfo = new SensorInfo
                {
                    DisplayName = "Digital Output 0",
                    Schema = "boolean"
                }
            }
        };

        // Act - Populate DTMIs first (as would happen in InitializeDtdl)
        DtdlGenerator.PopulateSensorDtmis(sensors);

        // Then generate the interface
        var dtdlInterface = DtdlGenerator.GenerateInterface(
            deviceName,
            sensors,
            description: "WISE-4012 4-channel analog input module");

        // Assert
        dtdlInterface.ShouldNotBeNull();
        dtdlInterface.Context.ShouldBe("dtmi:dtdl:context;3");
        dtdlInterface.Type.ShouldBe("Interface");
        dtdlInterface.Contents.Count.ShouldBe(3);

        // Verify all sensors have DTMIs populated
        foreach (var sensor in sensors)
        {
            sensor.Dtmi.ShouldNotBeNullOrEmpty();
            sensor.Dtmi.ShouldStartWith("dtmi:");
        }

        // Verify content details. `unit` is intentionally not emitted on the
        // Telemetry — see DtdlGenerator.GenerateTelemetryContent. The unit
        // surfaces in the upload payload via DeviceCapDto.Sensors[i].Unit.
        var ch0Content = dtdlInterface.Contents[0];
        ch0Content.Name.ShouldBe("channel_0");
        ch0Content.DisplayName.ShouldBe("Analog Input 0");
        ch0Content.Description.ShouldBe("First analog input channel");
        ch0Content.Schema.ShouldBe("double");

        var doContent = dtdlInterface.Contents[2];
        doContent.Schema.ShouldBe("boolean");
    }

    #endregion
}
