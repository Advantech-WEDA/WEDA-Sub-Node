using Shouldly;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Configuration.Validators.Device;
using Xunit;

namespace Weda.SubNode.Core.Tests.Configuration.Validators;

/// <summary>
/// Unit tests for SensorsValidator, including IoTDB identifier validation.
/// </summary>
public class SensorsValidatorTests
{
    private readonly SensorsValidator _validator = new();

    #region IoTDB Sensor Name Validation Tests

    [Theory]
    [InlineData("temperature")]
    [InlineData("Temperature")]
    [InlineData("TEMPERATURE")]
    [InlineData("temp1")]
    [InlineData("temp_sensor")]
    [InlineData("_temp")]
    [InlineData("_123")]
    [InlineData("sensor_1_value")]
    [InlineData("A")]
    [InlineData("_")]
    [InlineData("abc123")]
    [InlineData("ABC_123_def")]
    public void Validate_Should_AcceptValidIoTDBSensorNames(string sensorName)
    {
        // Arrange
        var context = CreateValidationContext(sensorName);

        // Act
        var result = _validator.Validate(context);

        // Assert
        result.IsValid.ShouldBeTrue($"Sensor name '{sensorName}' should be valid");
    }

    [Theory]
    [InlineData("channel.0", "contains dot")]
    [InlineData("sensor-1", "contains hyphen")]
    [InlineData("sensor value", "contains space")]
    [InlineData("1sensor", "starts with digit")]
    [InlineData("123", "starts with digit")]
    [InlineData("sensor@home", "contains special character @")]
    [InlineData("sensor#1", "contains special character #")]
    [InlineData("sensor$value", "contains special character $")]
    [InlineData("sensor%", "contains special character %")]
    [InlineData("sensor/path", "contains slash")]
    [InlineData("sensor\\path", "contains backslash")]
    [InlineData("sensor:value", "contains colon")]
    [InlineData("sensor[0]", "contains brackets")]
    [InlineData("sensor(1)", "contains parentheses")]
    [InlineData("temp+sensor", "contains plus")]
    [InlineData("temp=sensor", "contains equals")]
    [InlineData("日本語", "contains non-ASCII characters")]
    [InlineData("センサー", "contains Japanese characters")]
    public void Validate_Should_RejectInvalidIoTDBSensorNames(string sensorName, string reason)
    {
        // Arrange
        var context = CreateValidationContext(sensorName);

        // Act
        var result = _validator.Validate(context);

        // Assert
        result.IsValid.ShouldBeFalse($"Sensor name '{sensorName}' should be invalid because it {reason}");
        result.ErrorMessage!.ShouldContain("invalid for IoTDB");
        result.ErrorMessage!.ShouldContain(sensorName);
    }

    [Fact]
    public void Validate_Should_ProvideHelpfulErrorMessage_ForInvalidSensorName()
    {
        // Arrange
        var context = CreateValidationContext("channel.0");

        // Act
        var result = _validator.Validate(context);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("must start with a letter or underscore");
        result.ErrorMessage!.ShouldContain("only letters, digits, and underscores");
    }

    [Fact]
    public void Validate_Should_SkipNameValidation_When_ValidateSensorsDisabled()
    {
        // Arrange
        var context = CreateValidationContext("channel.0", validateSensors: false);

        // Act
        var result = _validator.Validate(context);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_ValidateAllSensors_AndFailOnFirstInvalid()
    {
        // Arrange
        var context = CreateValidationContextWithMultipleSensors(
            ["valid_sensor", "invalid.sensor", "another_valid"]);

        // Act
        var result = _validator.Validate(context);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("invalid.sensor");
    }

    #endregion

    #region Per-Sensor Record Interval Validation

    // When recording is enabled the interval is mandatory and must already be at least the record
    // floor (1000 ms): the resolved value is reported back to the cloud, whose validator range-checks
    // the interval regardless of the enabled flag, so the edge must not emit a null/zero/sub-floor
    // value for an enabled record. A disabled record may omit the interval (or send a sub-floor/zero
    // one) — the apply path fills and clamps it before it is reported.

    [Fact]
    public void Validate_Record_EnabledWithFloorInterval_Ok()
    {
        // { "record": { "enabled": true, "interval": 1000 } }
        var context = CreateRecordContext(new SubNodeSensorRecordDto { Enabled = true, Interval = 1000 });
        _validator.Validate(context).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Record_EnabledNoInterval_Rejected()
    {
        // { "record": { "enabled": true } } — an enabled record must carry an explicit interval.
        var context = CreateRecordContext(new SubNodeSensorRecordDto { Enabled = true });
        _validator.Validate(context).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_Record_EnabledBelowFloorInterval_Rejected()
    {
        // { "record": { "enabled": true, "interval": 500 } } — below the record floor is rejected;
        // an enabled record must declare a value that is already in range.
        var context = CreateRecordContext(new SubNodeSensorRecordDto { Enabled = true, Interval = 500 });
        _validator.Validate(context).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_Record_EnabledZeroInterval_Rejected()
    {
        // { "record": { "enabled": true, "interval": 0 } } — 0 is neither unset nor usable.
        var context = CreateRecordContext(new SubNodeSensorRecordDto { Enabled = true, Interval = 0 });
        _validator.Validate(context).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_Record_DisabledNoInterval_Ok()
    {
        // { "record": { "enabled": false } } — disabled records may omit the interval.
        var context = CreateRecordContext(new SubNodeSensorRecordDto { Enabled = false });
        _validator.Validate(context).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Record_DisabledBelowFloorInterval_Ok()
    {
        // { "record": { "enabled": false, "interval": 0 } } — a disabled record is not range-checked
        // locally; the apply path clamps it before it is reported.
        var context = CreateRecordContext(new SubNodeSensorRecordDto { Enabled = false, Interval = 0 });
        _validator.Validate(context).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Record_EmptyObject_Ok()
    {
        // { "record": {} } — Enabled defaults to false, so no interval required
        var context = CreateRecordContext(new SubNodeSensorRecordDto());
        _validator.Validate(context).IsValid.ShouldBeTrue();
    }

    private static ConfigurationValidationContext CreateRecordContext(SubNodeSensorRecordDto record)
    {
        var desiredConfig = new SubNodeDeviceConfigDto
        {
            Sensors =
            [
                new SubNodeSensorReportDto
                {
                    Name = "temperature",
                    SensorGroup = "AI",
                    Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 1000 },
                    SensorInfo = new SubNodeSensorInfoDto { Schema = "double" },
                    Record = record
                }
            ]
        };

        return new ConfigurationValidationContext
        {
            DesiredConfig = desiredConfig,
            CurrentConfig = CreateDeviceConfiguration("temperature"),
            Options = new ConfigUpdateOptions { ValidateSensors = true }
        };
    }

    #endregion

    #region Helper Methods

    private static ConfigurationValidationContext CreateValidationContext(
        string sensorName,
        bool validateSensors = true)
    {
        var desiredConfig = new SubNodeDeviceConfigDto
        {
            Sensors =
            [
                new SubNodeSensorReportDto
                {
                    Name = sensorName,
                    SensorGroup = "AI",
                    Report = new SubNodeSensorRuntimeConfigDto
                    {
                        Enabled = true,
                        Interval = 1000
                    },
                    SensorInfo = new SubNodeSensorInfoDto { Schema = "double" }
                }
            ]
        };

        var currentConfig = CreateDeviceConfiguration(sensorName);

        return new ConfigurationValidationContext
        {
            DesiredConfig = desiredConfig,
            CurrentConfig = currentConfig,
            Options = new ConfigUpdateOptions { ValidateSensors = validateSensors }
        };
    }

    private static ConfigurationValidationContext CreateValidationContextWithMultipleSensors(
        string[] sensorNames)
    {
        var sensors = sensorNames.Select(name => new SubNodeSensorReportDto
        {
            Name = name,
            SensorGroup = "AI",
            Report = new SubNodeSensorRuntimeConfigDto
            {
                Enabled = true,
                Interval = 1000
            },
            SensorInfo = new SubNodeSensorInfoDto { Schema = "double" }
        }).ToList();

        var desiredConfig = new SubNodeDeviceConfigDto { Sensors = sensors };

        var currentConfig = new DeviceConfiguration
        {
            DeviceName = "TestDevice",
            SubNodeInfo = new SubNodeInfo
            {
                Name = "TestSubNode",
                SubNodeType = SubNodeType.CustomDevice,
                Manufacturer = "Test",
                Model = "TestModel",
                SwVersion = "1.0"
            },
            Dtdl = new DtdlConfig { AutoGenEnabled = true },
            Sensors = sensorNames.Select(name => new Sensor
            {
                Name = name,
                SensorGroup = SensorGroup.AI,
                SensorInfo = new SensorInfo { Schema = "double" },
                Report = new SensorReport { Enabled = true, Interval = 1000 }
            }).ToList()
        };

        return new ConfigurationValidationContext
        {
            DesiredConfig = desiredConfig,
            CurrentConfig = currentConfig,
            Options = ConfigUpdateOptions.Default
        };
    }

    private static DeviceConfiguration CreateDeviceConfiguration(string sensorName)
    {
        return new DeviceConfiguration
        {
            DeviceName = "TestDevice",
            SubNodeInfo = new SubNodeInfo
            {
                Name = "TestSubNode",
                SubNodeType = SubNodeType.CustomDevice,
                Manufacturer = "Test",
                Model = "TestModel",
                SwVersion = "1.0"
            },
            Dtdl = new DtdlConfig { AutoGenEnabled = true },
            Sensors =
            [
                new Sensor
                {
                    Name = sensorName,
                    SensorGroup = SensorGroup.AI,
                    SensorInfo = new SensorInfo { Schema = "double" },
                    Report = new SensorReport
                    {
                        Enabled = true,
                        Interval = 1000
                    }
                }
            ]
        };
    }

    #endregion
}
