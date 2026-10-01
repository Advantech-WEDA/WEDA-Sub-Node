using System.Text.Json;

using Shouldly;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Configuration;
using Weda.SubNode.Core.Context;

using Xunit;

namespace Weda.SubNode.Core.Tests.Configuration;

/// <summary>
/// Unit tests for ConfigurationUpdateHelper.
/// Tests validation, update application, backup/restore, and report generation scenarios.
/// </summary>
public class ConfigurationUpdateHelperTests
{
    #region ValidateMessage Tests

    [Fact]
    public void ValidateMessage_Should_ReturnFailure_When_MessageIsNull()
    {
        // Act
        var result = ConfigurationUpdateHelper.ValidateMessage(null!);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("null");
    }

    [Fact]
    public void ValidateMessage_Should_ReturnFailure_When_DataIsNull()
    {
        // Arrange
        var message = new SubNodeConfigUpdateMessage { Data = null };

        // Act
        var result = ConfigurationUpdateHelper.ValidateMessage(message);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("desired configuration");
    }

    [Fact]
    public void ValidateMessage_Should_ReturnFailure_When_DesiredConfigIsNull()
    {
        // Arrange
        var message = new SubNodeConfigUpdateMessage
        {
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState { Desired = null }
            }
        };

        // Act
        var result = ConfigurationUpdateHelper.ValidateMessage(message);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("desired configuration");
    }

    [Fact]
    public void ValidateMessage_Should_ReturnNoUpdate_When_DeviceConfigsIsEmpty()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!.Clear();

        // Act
        var result = ConfigurationUpdateHelper.ValidateMessage(message);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.NoUpdateRequired.ShouldBeTrue();
    }

    [Fact]
    public void ValidateMessage_Should_ReturnSuccess_When_MessageIsValid()
    {
        // Arrange
        var message = CreateValidMessage();

        // Act
        var result = ConfigurationUpdateHelper.ValidateMessage(message);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.NoUpdateRequired.ShouldBeFalse();
    }

    #endregion

    #region ValidateDeviceConfiguration Tests

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_DeviceNameNotFound()
    {
        // Arrange
        var message = CreateValidMessage();
        var currentConfig = CreateDeviceConfiguration("NonExistentDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("NonExistentDevice");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnSuccess_When_DeviceNameMatches()
    {
        // Arrange
        var message = CreateValidMessage();
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_BeCaseInsensitive_ForDeviceName()
    {
        // Arrange
        var message = CreateValidMessage();
        var currentConfig = CreateDeviceConfiguration("testdevice"); // lowercase

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert - Should match "TestDevice" in message
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region Period Validation Tests

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_ReportHealthIsNegative()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Periods!.ReportHealth = -1;
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("ReportHealth");
        result.ErrorMessage?.ShouldContain("negative");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_AllowZero_ForReportHealth()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Periods!.ReportHealth = 0;
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_ReportConfigurationBelowMinimum()
    {
        // Arrange
        var message = CreateValidMessage();
        // Default minimum is 60000ms (1 minute)
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Periods!.ReportConfiguration = 30_000;
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("ReportConfiguration");
        result.ErrorMessage?.ShouldContain("below minimum");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_ReportConfigurationAboveMaximum()
    {
        // Arrange
        var message = CreateValidMessage();
        // Default maximum is 604800000ms (7 days)
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Periods!.ReportConfiguration = 700_000_000;
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("ReportConfiguration");
        result.ErrorMessage?.ShouldContain("exceeds maximum");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_AllowZero_ForReportConfiguration_AsDisabled()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Periods!.ReportConfiguration = 0;
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert - Zero means disabled, should be allowed
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_SkipPeriodValidation_When_ValidatePeriodsDisabled()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Periods!.ReportHealth = -100;
        var currentConfig = CreateDeviceConfiguration("TestDevice");
        var options = new ConfigUpdateOptions { ValidatePeriods = false };

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig, options);

        // Assert - Should pass because period validation is disabled
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ReplaceSensors_RecordWithInterval_KeepsIt()
    {
        var config = CreateDeviceConfiguration("TestDevice");
        var desired = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 1000 },
                Record = new SubNodeSensorRecordDto { Enabled = true, Interval = 5000 }
            }
        };

        ConfigurationUpdateHelper.ReplaceSensors(config, desired, "TestDevice");

        config.Sensors.First(s => s.Name == "channel_0").Record.Interval.ShouldBe(5000);
    }

    [Fact]
    public void ReplaceSensors_RecordNoInterval_FillsFromReport()
    {
        var config = CreateDeviceConfiguration("TestDevice");
        var desired = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 3000 },
                Record = new SubNodeSensorRecordDto { Enabled = false }
            }
        };

        ConfigurationUpdateHelper.ReplaceSensors(config, desired, "TestDevice");

        // report interval (3000) is above the 1000 record floor, so it is used as-is
        config.Sensors.First(s => s.Name == "channel_0").Record.Interval.ShouldBe(3000);
    }

    [Fact]
    public void ReplaceSensors_RecordDisabledWithInterval_KeepsIt()
    {
        var config = CreateDeviceConfiguration("TestDevice");
        var desired = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 1000 },
                // Disabled, but a valid explicit interval is supplied — the resolver keys off the
                // interval being positive, not off Enabled, so the given value is used (and here it
                // is already above the 1000 floor, so it survives the clamp unchanged).
                Record = new SubNodeSensorRecordDto { Enabled = false, Interval = 5000 }
            }
        };

        ConfigurationUpdateHelper.ReplaceSensors(config, desired, "TestDevice");

        config.Sensors.First(s => s.Name == "channel_0").Record.Interval.ShouldBe(5000);
    }

    [Fact]
    public void ReplaceSensors_RecordEnabledBelowFloorInterval_ClampsUp()
    {
        var config = CreateDeviceConfiguration("TestDevice");
        var desired = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 1000 },
                // An explicit positive interval below the record floor is clamped up to 1000, not
                // kept as given — a sub-floor record interval is a mistake we normalise.
                Record = new SubNodeSensorRecordDto { Enabled = true, Interval = 500 }
            }
        };

        ConfigurationUpdateHelper.ReplaceSensors(config, desired, "TestDevice");

        config.Sensors.First(s => s.Name == "channel_0").Record.Interval.ShouldBe(1000);
    }

    [Fact]
    public void ReplaceSensors_RecordEnabledNoInterval_FillsFromReport()
    {
        var config = CreateDeviceConfiguration("TestDevice");
        var desired = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 3000 },
                // Enabled with no interval — null means "use the default", so it is filled from the
                // report interval (3000, already above the 1000 floor).
                Record = new SubNodeSensorRecordDto { Enabled = true }
            }
        };

        ConfigurationUpdateHelper.ReplaceSensors(config, desired, "TestDevice");

        config.Sensors.First(s => s.Name == "channel_0").Record.Interval.ShouldBe(3000);
    }

    [Fact]
    public void ReplaceSensors_RecordNoInterval_ClampsReportBelowRecordFloor()
    {
        var config = CreateDeviceConfiguration("TestDevice");
        var desired = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                SensorGroup = "AI",
                // report interval 500 is a valid report value (floor 50) but below the record
                // floor (1000), so the resolved record interval must clamp up to 1000
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 500 },
                Record = new SubNodeSensorRecordDto { Enabled = false }
            }
        };

        ConfigurationUpdateHelper.ReplaceSensors(config, desired, "TestDevice");

        config.Sensors.First(s => s.Name == "channel_0").Record.Interval.ShouldBe(1000);
    }

    #endregion

    #region Sensor Validation Tests

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/octet-stream")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    public void ValidateDeviceConfiguration_Should_AcceptValidMimeTypeSchema(string schema)
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].SensorInfo = 
            new SubNodeSensorInfoDto { Schema = schema };
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Dtmi = null;
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);
        
        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("image/gif")]
    [InlineData("application/xml")]
    [InlineData("text/plain")]
    [InlineData("audio/mpeg")]
    public void ValidateDeviceConfiguration_Should_RejectInvalidMimeTypeSchema(string schema)
    {
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].SensorInfo = 
            new SubNodeSensorInfoDto { Schema = schema };
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Dtmi = null; 
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);
        
        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("Invalid schema");
        result.ErrorMessage!.ShouldContain(schema);
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_SensorNameIsEmpty()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Name = "";
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("Sensor name");
        result.ErrorMessage?.ShouldContain("empty");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_SensorIntervalIsNegative()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Report!.Interval = -1;
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("must not be less than 50");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_UnknownSensorProvided_WithStrictOptions()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors!.Add(
            new SubNodeSensorReportDto
            {
                Name = "unknown_sensor",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 1000 },
                SensorInfo = new SubNodeSensorInfoDto { Schema = "double" }
            });
        var currentConfig = CreateDeviceConfiguration("TestDevice");
        var options = ConfigUpdateOptions.Strict; // Strict mode rejects unknown sensors

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig, options);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("Unknown sensor");
        result.ErrorMessage?.ShouldContain("unknown_sensor");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_AllowUnknownSensor_When_RejectUnknownSensorsDisabled()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors!.Add(
            new SubNodeSensorReportDto
            {
                Name = "unknown_sensor",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 1000 },
                SensorInfo = new SubNodeSensorInfoDto { Schema = "double" }
            });
        var currentConfig = CreateDeviceConfiguration("TestDevice");
        var options = new ConfigUpdateOptions { RejectUnknownSensors = false, RequireAllSensors = false };

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig, options);

        // Assert - Should pass because unknown sensors are allowed
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_RequiredSensorMissing_WithStrictOptions()
    {
        // Arrange
        var message = CreateValidMessage();
        // Remove one sensor from the message
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors!.RemoveAt(1);
        var currentConfig = CreateDeviceConfiguration("TestDevice");
        var options = ConfigUpdateOptions.Strict; // Strict mode requires all sensors

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig, options);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("Missing sensor");
        result.ErrorMessage?.ShouldContain("channel_1");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_AllowMissingSensor_When_RequireAllSensorsDisabled()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors!.RemoveAt(1);
        var currentConfig = CreateDeviceConfiguration("TestDevice");
        var options = new ConfigUpdateOptions { RequireAllSensors = false };

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig, options);

        // Assert - Should pass because partial updates are allowed
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region Threshold Validation Tests

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_UpperCriticalLessThanUpperWarning()
    {
        // Arrange
        var message = CreateValidMessage();
        var sensorConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Report!;
        sensorConfig.Thresholds = new SubNodeThresholdsDto
        {
            UpperCritical = 80,
            UpperWarning = 90 // Invalid: UpperCritical < UpperWarning
        };
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("UpperCritical");
        result.ErrorMessage?.ShouldContain("UpperWarning");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_LowerWarningLessThanLowerCritical()
    {
        // Arrange
        var message = CreateValidMessage();
        var sensorConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Report!;
        sensorConfig.Thresholds = new SubNodeThresholdsDto
        {
            LowerWarning = 10,
            LowerCritical = 20 // Invalid: LowerWarning < LowerCritical
        };
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("LowerWarning");
        result.ErrorMessage?.ShouldContain("LowerCritical");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_UpperWarningLessThanLowerWarning()
    {
        // Arrange
        var message = CreateValidMessage();
        var sensorConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Report!;
        sensorConfig.Thresholds = new SubNodeThresholdsDto
        {
            UpperWarning = 30,
            LowerWarning = 50 // Invalid: UpperWarning < LowerWarning
        };
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("UpperWarning");
        result.ErrorMessage?.ShouldContain("LowerWarning");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_AllowValidThresholds()
    {
        // Arrange
        var message = CreateValidMessage();
        var sensorConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Report!;
        sensorConfig.Thresholds = new SubNodeThresholdsDto
        {
            UpperCritical = 100,
            UpperWarning = 80,
            LowerWarning = 20,
            LowerCritical = 0
        };
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_SkipThresholdValidation_When_ValidateThresholdsDisabled()
    {
        // Arrange
        var message = CreateValidMessage();
        var sensorConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Report!;
        sensorConfig.Thresholds = new SubNodeThresholdsDto
        {
            UpperCritical = 10,
            UpperWarning = 100 // Invalid but should be skipped
        };
        var currentConfig = CreateDeviceConfiguration("TestDevice");
        var options = new ConfigUpdateOptions { ValidateThresholds = false };

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig, options);

        // Assert - Should pass because threshold validation is disabled
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region ConfigUpdateOptions Tests

    [Fact]
    public void ConfigUpdateOptions_Default_Should_AllowPartialUpdates()
    {
        // Act
        var options = ConfigUpdateOptions.Default;

        // Assert - Default now uses whitelist-style (partial updates allowed)
        options.UpdateMode.ShouldBe(ConfigUpdateMode.Replace);
        options.ValidateDeviceName.ShouldBeTrue();
        options.ValidatePeriods.ShouldBeTrue();
        options.ValidateSensors.ShouldBeTrue();
        options.ValidateThresholds.ShouldBeTrue();
        options.RejectUnknownSensors.ShouldBeFalse(); // Changed: allows unknown sensors
        options.RequireAllSensors.ShouldBeFalse(); // Changed: allows partial updates
    }

    [Fact]
    public void ConfigUpdateOptions_Strict_Should_RequireCompletePayload()
    {
        // Act
        var options = ConfigUpdateOptions.Strict;

        // Assert - Strict mode requires complete payload
        options.UpdateMode.ShouldBe(ConfigUpdateMode.Replace);
        options.RejectUnknownSensors.ShouldBeTrue();
        options.RequireAllSensors.ShouldBeTrue();
    }

    [Fact]
    public void ConfigUpdateOptions_Relaxed_Should_BeAliasForDefault()
    {
        // Act
        var relaxed = ConfigUpdateOptions.Relaxed;
        var defaultOpts = ConfigUpdateOptions.Default;

        // Assert - Relaxed is alias for Default
        relaxed.UpdateMode.ShouldBe(defaultOpts.UpdateMode);
        relaxed.RejectUnknownSensors.ShouldBe(defaultOpts.RejectUnknownSensors);
        relaxed.RequireAllSensors.ShouldBe(defaultOpts.RequireAllSensors);
    }

    [Fact]
    public void ConfigUpdateOptions_Should_AllowCustomPeriodRanges()
    {
        // Arrange
        var options = new ConfigUpdateOptions
        {
            ReportConfigurationMinMs = 60_000,  // 1 minute
            ReportConfigurationMaxMs = 3_600_000 // 1 hour
        };

        // Act - Value within custom range
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Periods!.ReportConfiguration = 120_000; // 2 minutes
        var currentConfig = CreateDeviceConfiguration("TestDevice");
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig, options);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    #endregion

    #region ApplySensorConfigUpdates Tests

    [Fact]
    public void ApplySensorReportUpdates_Should_UpdateEnabledState()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");
        config.Sensors[0].Report.Enabled = true;

        var desiredSensors = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = false, Interval = 1000 }
            }
        };

        // Act
        var updatedSensors = ConfigurationUpdateHelper.ApplysensorReportUpdates(config, desiredSensors);

        // Assert
        updatedSensors.ShouldContain("channel_0");
        config.Sensors[0].Report.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void ApplySensorReportUpdates_Should_UpdateInterval()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");
        config.Sensors[0].Report.Interval = 1000;

        var desiredSensors = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 5000 }
            }
        };

        // Act
        ConfigurationUpdateHelper.ApplysensorReportUpdates(config, desiredSensors);

        // Assert
        config.Sensors[0].Report.Interval.ShouldBe(5000);
    }

    [Fact]
    public void ApplySensorReportUpdates_Should_UpdateUnit_WhenProvided()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");
        config.Sensors[0].Report.Unit = "mV";

        var desiredSensors = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 1000, Unit = "V" }
            }
        };

        // Act
        ConfigurationUpdateHelper.ApplysensorReportUpdates(config, desiredSensors);

        // Assert
        config.Sensors[0].Report.Unit.ShouldBe("V");
    }

    [Fact]
    public void ApplySensorReportUpdates_Should_SkipUnknownSensors()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");

        var desiredSensors = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "unknown_sensor",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = false, Interval = 9999 }
            }
        };

        // Act
        var updatedSensors = ConfigurationUpdateHelper.ApplysensorReportUpdates(config, desiredSensors);

        // Assert - Unknown sensor should be skipped
        updatedSensors.ShouldBeEmpty();
    }

    [Fact]
    public void ApplySensorReportUpdates_Should_BeCaseInsensitive_ForSensorName()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");
        config.Sensors[0].Report.Enabled = true;

        var desiredSensors = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "CHANNEL_0", // Uppercase
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = false, Interval = 1000 }
            }
        };

        // Act
        var updatedSensors = ConfigurationUpdateHelper.ApplysensorReportUpdates(config, desiredSensors);

        // Assert - Should match "channel_0"
        updatedSensors.ShouldContain("channel_0");
        config.Sensors[0].Report.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void ApplySensorReportUpdates_Should_UpdateThresholds()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");

        var desiredSensors = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                Report = new SubNodeSensorRuntimeConfigDto
                {
                    Enabled = true,
                    Interval = 1000,
                    Thresholds = new SubNodeThresholdsDto
                    {
                        UpperCritical = 100,
                        UpperWarning = 80,
                        LowerWarning = 20,
                        LowerCritical = 0
                    }
                }
            }
        };

        // Act
        ConfigurationUpdateHelper.ApplysensorReportUpdates(config, desiredSensors);

        // Assert
        var thresholds = config.Sensors[0].Report.Thresholds;
        thresholds.ShouldNotBeNull();
        thresholds!.UpperCritical.ShouldBe(100);
        thresholds.UpperWarning.ShouldBe(80);
        thresholds.LowerWarning.ShouldBe(20);
        thresholds.LowerCritical.ShouldBe(0);
    }

    [Fact]
    public void ApplySensorReportUpdates_Should_ReturnEmptyList_WhenDesiredSensorsIsNull()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");

        // Act
        var updatedSensors = ConfigurationUpdateHelper.ApplysensorReportUpdates(config, null);

        // Assert
        updatedSensors.ShouldBeEmpty();
    }

    #endregion

    #region Backup and Restore Tests

    [Fact]
    public void CreateBackup_Should_CaptureAllSensorConfigurations()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");
        config.Sensors[0].Report.Enabled = true;
        config.Sensors[0].Report.Interval = 2000;
        config.Sensors[0].Report.Unit = "mV";
        config.Sensors[0].Report.Thresholds = new ThresholdConfig
        {
            UpperCritical = 100,
            UpperWarning = 80
        };
        config.Periods.ReportHealth = 60000;
        config.Periods.ReportConfiguration = 1800000;

        // Act
        var backup = ConfigurationUpdateHelper.CreateBackup(config);

        // Assert
        backup.ReportHealthPeriod.ShouldBe(60000);
        backup.ReportConfigurationPeriod.ShouldBe(1800000);
        backup.SensorBackups.Count.ShouldBe(2);

        var sensorBackup = backup.SensorBackups.First(s => s.Name == "channel_0");
        sensorBackup.Enabled.ShouldBeTrue();
        sensorBackup.Interval.ShouldBe(2000);
        sensorBackup.Unit.ShouldBe("mV");
        sensorBackup.Thresholds.ShouldNotBeNull();
        sensorBackup.Thresholds!.UpperCritical.ShouldBe(100);
    }

    [Fact]
    public void RestoreBackup_Should_RestoreAllSensorConfigurations()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");

        var backup = new DeviceConfigurationBackup
        {
            ReportHealthPeriod = 120000,
            ReportConfigurationPeriod = 3600000,
            SensorBackups = new List<SensorReportBackup>
            {
                new()
                {
                    Name = "channel_0",
                    Enabled = false,
                    Interval = 5000,
                    Unit = "V",
                    Thresholds = new ThresholdConfig
                    {
                        UpperCritical = 200,
                        LowerCritical = -50
                    }
                },
                new()
                {
                    Name = "channel_1",
                    Enabled = true,
                    Interval = 3000,
                    Unit = "A"
                }
            }
        };

        // Act
        ConfigurationUpdateHelper.RestoreBackup(config, backup);

        // Assert
        config.Periods.ReportHealth.ShouldBe(120000);
        config.Periods.ReportConfiguration.ShouldBe(3600000);

        config.Sensors[0].Report.Enabled.ShouldBeFalse();
        config.Sensors[0].Report.Interval.ShouldBe(5000);
        config.Sensors[0].Report.Unit.ShouldBe("V");
        config.Sensors[0].Report.Thresholds!.UpperCritical.ShouldBe(200);

        config.Sensors[1].Report.Enabled.ShouldBeTrue();
        config.Sensors[1].Report.Interval.ShouldBe(3000);
    }

    [Fact]
    public void RestoreBackup_Should_SkipUnknownSensors()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");
        var originalInterval = config.Sensors[0].Report.Interval;

        var backup = new DeviceConfigurationBackup
        {
            ReportHealthPeriod = 60000,
            ReportConfigurationPeriod = 1800000,
            SensorBackups = new List<SensorReportBackup>
            {
                new()
                {
                    Name = "unknown_sensor",
                    Enabled = false,
                    Interval = 9999
                }
            }
        };

        // Act
        ConfigurationUpdateHelper.RestoreBackup(config, backup);

        // Assert - Original sensor should be unchanged
        config.Sensors[0].Report.Interval.ShouldBe(originalInterval);
    }

    [Fact]
    public void BackupAndRestore_Should_PreserveConfigurationAfterFailedUpdate()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");
        config.Sensors[0].Report.Enabled = true;
        config.Sensors[0].Report.Interval = 1000;
        config.Periods.ReportHealth = 60000;

        // Create backup before update
        var backup = ConfigurationUpdateHelper.CreateBackup(config);

        // Simulate failed update by modifying config
        config.Sensors[0].Report.Enabled = false;
        config.Sensors[0].Report.Interval = 9999;
        config.Periods.ReportHealth = 1;

        // Act - Restore backup after failed update
        ConfigurationUpdateHelper.RestoreBackup(config, backup);

        // Assert - Should be restored to original values
        config.Sensors[0].Report.Enabled.ShouldBeTrue();
        config.Sensors[0].Report.Interval.ShouldBe(1000);
        config.Periods.ReportHealth.ShouldBe(60000);
    }

    #endregion

    #region ApplyCachedConfiguration Tests

    [Fact]
    public void ApplyCachedConfiguration_Should_ReturnFalse_When_CachedMessageIsNull()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ApplyCachedConfiguration(config, null!);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ApplyCachedConfiguration_Should_ReturnFalse_When_DeviceNotFound()
    {
        // Arrange
        var config = CreateDeviceConfiguration("NonExistentDevice");
        var cachedMessage = CreateValidMessage();

        // Act
        var result = ConfigurationUpdateHelper.ApplyCachedConfiguration(config, cachedMessage);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ApplyCachedConfiguration_Should_ApplySensorUpdates()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");
        config.Sensors[0].Report.Enabled = true;
        config.Sensors[0].Report.Interval = 1000;

        var cachedMessage = CreateValidMessage();
        var deviceConfig = cachedMessage.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        deviceConfig.Sensors![0].Report!.Enabled = false;
        deviceConfig.Sensors[0].Report!.Interval = 5000;

        // Act
        var result = ConfigurationUpdateHelper.ApplyCachedConfiguration(config, cachedMessage);

        // Assert
        result.ShouldBeTrue();
        config.Sensors[0].Report.Enabled.ShouldBeFalse();
        config.Sensors[0].Report.Interval.ShouldBe(5000);
    }

    [Fact]
    public void ApplyCachedConfiguration_Should_ApplyPeriodUpdates()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");
        config.Periods.ReportHealth = 60000;
        config.Periods.ReportConfiguration = 1800000;

        var cachedMessage = CreateValidMessage();
        var deviceConfig = cachedMessage.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"];
        deviceConfig.Periods!.ReportHealth = 120000;
        deviceConfig.Periods!.ReportConfiguration = 3600000;

        // Act
        var result = ConfigurationUpdateHelper.ApplyCachedConfiguration(config, cachedMessage);

        // Assert
        result.ShouldBeTrue();
        config.Periods.ReportHealth.ShouldBe(120000);
        config.Periods.ReportConfiguration.ShouldBe(3600000);
    }

    [Fact]
    public void ApplyCachedConfiguration_Should_AddNewSensor_When_DesiredHasMoreSensors()
    {
        // Arrange: Start with 1 sensor
        var config = new DeviceConfiguration
        {
            DeviceName = "TestDevice",
            DeviceId = "test-device-id",
            SubNodeInfo = new SubNodeInfo
            {
                Name = "TestSubNode",
                SubNodeType = SubNodeType.CustomDevice,
                Manufacturer = "Test",
                Model = "TestModel",
                SwVersion = "1.0"
            },
            Dtdl = new DtdlConfig { AutoGenEnabled = true },
            DeviceCommunication = new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 502
            },
            Sensors =
            [
                new Sensor
                {
                    Name = "temperature_sensor",
                    Dtmi = "dtmi:test:sensor;1",
                    SensorGroup = SensorGroup.TEMP,
                    Parameters = new Dictionary<string, object>(),
                    Report = new SensorReport
                    {
                        Enabled = true,
                        Interval = 1000,
                        Unit = "celsius"
                    }
                }
            ]
        };

        config.Sensors.Count.ShouldBe(1);

        // Create cached message with 2 sensors
        var cachedMessage = new SubNodeConfigUpdateMessage
        {
            DeviceId = "device-123",
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    Desired = new SubNodeDesiredConfigSections
                    {
                        DeviceCfg = new SubNodeDeviceCfgDto
                        {
                            DeviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>
                            {
                                ["TestDevice"] = new SubNodeDeviceConfigDto
                                {
                                    Sensors =
                                    [
                                        new SubNodeSensorReportDto
                                        {
                                            Name = "temperature_sensor",
                                            SensorGroup = "TEMP",
                                            Report = new SubNodeSensorRuntimeConfigDto
                                            {
                                                Enabled = true,
                                                Interval = 2000
                                            },
                                            SensorInfo = new SubNodeSensorInfoDto { Schema = "double" }
                                        },
                                        new SubNodeSensorReportDto
                                        {
                                            Name = "temperature_sensor.2",
                                            SensorGroup = "TEMP",
                                            Report = new SubNodeSensorRuntimeConfigDto
                                            {
                                                Enabled = true,
                                                Interval = 3000
                                            },
                                            SensorInfo = new SubNodeSensorInfoDto { Schema = "double" }
                                        }
                                    ]
                                }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = ConfigurationUpdateHelper.ApplyCachedConfiguration(config, cachedMessage);

        // Assert
        result.ShouldBeTrue();
        config.Sensors.Count.ShouldBe(2);

        var sensor1 = config.Sensors.FirstOrDefault(s => s.Name == "temperature_sensor");
        sensor1.ShouldNotBeNull();
        sensor1!.Report.Interval.ShouldBe(2000);

        var sensor2 = config.Sensors.FirstOrDefault(s => s.Name == "temperature_sensor.2");
        sensor2.ShouldNotBeNull();
        sensor2!.Report.Interval.ShouldBe(3000);
    }

    [Fact]
    public void ApplyCachedConfiguration_Should_RestoreDisabledDevice_When_CacheDisablesDevice()
    {
        // Arrange - base devicecfg.json enables the device; the cloud later disabled it
        var config = CreateDeviceConfiguration("TestDevice");
        config.Enabled = true;

        var cachedMessage = CreateValidMessage();
        cachedMessage.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Enabled = false;

        // Act
        var result = ConfigurationUpdateHelper.ApplyCachedConfiguration(config, cachedMessage);

        // Assert - the disabled state survives a restart, and no sensor reports
        result.ShouldBeTrue();
        config.Enabled.ShouldBeFalse();
        config.Sensors.ShouldAllBe(s => !s.DeviceEnabled);
        config.Sensors.ShouldAllBe(s => !s.IsEffectivelyEnabled);
    }

    [Fact]
    public void ApplyCachedConfiguration_Should_RestoreEnabledDevice_When_BaseDisablesDevice()
    {
        // Arrange - base devicecfg.json disables the device; the cloud later enabled it
        var config = CreateDeviceConfiguration("TestDevice");
        config.Enabled = false;

        var cachedMessage = CreateValidMessage();
        cachedMessage.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Enabled = true;

        // Act
        var result = ConfigurationUpdateHelper.ApplyCachedConfiguration(config, cachedMessage);

        // Assert
        result.ShouldBeTrue();
        config.Enabled.ShouldBeTrue();
        config.Sensors.ShouldAllBe(s => s.DeviceEnabled);
    }

    #endregion

    #region Helper Methods

    private static SubNodeConfigUpdateMessage CreateValidMessage()
    {
        return new SubNodeConfigUpdateMessage
        {
            DeviceId = "device-123",
            GroupId = "default",
            Cmd = "updateCmd",
            SeqId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ReqSeqId = "req-456",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    Desired = new SubNodeDesiredConfigSections
                    {
                        DeviceCfg = new SubNodeDeviceCfgDto
                        {
                            DeviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>
                            {
                                ["TestDevice"] = new SubNodeDeviceConfigDto
                                {
                                    Enabled = true,
                                    DeviceType = "TcpModbus",
                                    Sensors = new List<SubNodeSensorReportDto>
                                    {
                                        new()
                                        {
                                            Name = "channel_0",
                                            Dtmi = "dtmi:test:sensor;1",
                                            SensorGroup = "AI",
                                            Report = new SubNodeSensorRuntimeConfigDto
                                            {
                                                Enabled = true,
                                                Interval = 1000
                                            },
                                            SensorInfo = new SubNodeSensorInfoDto { Schema = "double" }
                                        },
                                        new()
                                        {
                                            Name = "channel_1",
                                            Dtmi = "dtmi:test:sensor;1",
                                            SensorGroup = "AI",
                                            Report = new SubNodeSensorRuntimeConfigDto
                                            {
                                                Enabled = true,
                                                Interval = 1000
                                            },
                                            SensorInfo = new SubNodeSensorInfoDto { Schema = "double" }
                                        }
                                    },
                                    Periods = new SubNodePeriodsDto
                                    {
                                        ReportHealth = 60000,
                                        ReportConfiguration = 1800000
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static DeviceConfiguration CreateDeviceConfiguration(string deviceName)
    {
        return new DeviceConfiguration
        {
            DeviceName = deviceName,
            SubNodeInfo = new SubNodeInfo
            {
                Name = "TestSubNode",
                SubNodeType = SubNodeType.CustomDevice,
                Manufacturer = "Test",
                Model = "TestModel",
                SwVersion = "1.0"
            },
            Dtdl = new DtdlConfig { AutoGenEnabled = true },
            DeviceCommunication = new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 502
            },
            Sensors =
            [
                new Sensor
                {
                    Name = "channel_0",
                    Dtmi = "dtmi:test:sensor;1",
                    SensorGroup = SensorGroup.AI,
                    Parameters = new Dictionary<string, object>(),
                    Report = new SensorReport
                    {
                        Enabled = true,
                        Interval = 1000,
                        Unit = "mV"
                    }
                },
                new Sensor
                {
                    Name = "channel_1",
                    Dtmi = "dtmi:test:sensor;1",
                    SensorGroup = SensorGroup.AI,
                    Parameters = new Dictionary<string, object>(),
                    Report = new SensorReport
                    {
                        Enabled = true,
                        Interval = 1000,
                        Unit = "mV"
                    }
                }
            ],
            Periods = new BackgroundTaskPeriods
            {
                ReportHealth = 60000,
                ReportConfiguration = 1800000
            }
        };
    }

    #endregion

    #region ReplaceSensors Tests

    [Fact]
    public void ReplaceSensors_Should_AddNewSensors_When_DesiredContainsNewSensors()
    {
        // Arrange
        var deviceConfig = CreateDeviceConfiguration("TestDevice");
        var originalSensorCount = deviceConfig.Sensors.Count;

        var desiredSensors = new List<SubNodeSensorReportDto>
        {
            // Keep existing sensor
            new()
            {
                Name = "channel_0",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto
                {
                    Enabled = true,
                    Interval = 1000
                }
            },
            // Add new sensor
            new()
            {
                Name = "new.sensor",
                SensorGroup = "TEMP",
                Report = new SubNodeSensorRuntimeConfigDto
                {
                    Enabled = true,
                    Interval = 2000,
                    TransformPipeline =
                    [
                        new SubNodeTransformConfigDto
                        {
                            Type = "UnitConversion",
                            Enabled = true,
                            Parameters = new Dictionary<string, object>
                            {
                                ["FromUnit"] = "celsius",
                                ["ToUnit"] = "fahrenheit"
                            }
                        }
                    ]
                }
            }
        };

        // Act
        var result = ConfigurationUpdateHelper.ReplaceSensors(
            deviceConfig, desiredSensors, "test-device-id");

        // Assert
        result.AddedSensors.ShouldContain("new.sensor");
        result.RemovedSensors.ShouldContain("channel_1"); // Was in original but not in desired
        result.HasChanges.ShouldBeTrue();

        // Verify new sensor was added with TransformPipeline
        var newSensor = deviceConfig.Sensors.FirstOrDefault(s => s.Name == "new.sensor");
        newSensor.ShouldNotBeNull();
        newSensor.Report.TransformPipeline.Count.ShouldBe(1);
        newSensor.Report.TransformPipeline[0].Type.ShouldBe("UnitConversion");
        newSensor.Report.TransformPipeline[0].Parameters["FromUnit"].ShouldBe("celsius");
        newSensor.Report.TransformPipeline[0].Parameters["ToUnit"].ShouldBe("fahrenheit");
    }

    [Fact]
    public void ReplaceSensors_Should_RemoveSensors_When_NotInDesiredList()
    {
        // Arrange
        var deviceConfig = CreateDeviceConfiguration("TestDevice");
        deviceConfig.Sensors.Count.ShouldBe(2); // channel_0, channel_1

        // Desired only contains channel_0
        var desiredSensors = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto
                {
                    Enabled = true,
                    Interval = 1000
                }
            }
        };

        // Act
        var result = ConfigurationUpdateHelper.ReplaceSensors(
            deviceConfig, desiredSensors, "test-device-id");

        // Assert
        result.RemovedSensors.ShouldContain("channel_1");
        result.AddedSensors.ShouldBeEmpty();
        deviceConfig.Sensors.Count.ShouldBe(1);
        deviceConfig.Sensors[0].Name.ShouldBe("channel_0");
    }

    [Fact]
    public void ReplaceSensors_Should_UpdateExistingSensors_When_ConfigChanged()
    {
        // Arrange
        var deviceConfig = CreateDeviceConfiguration("TestDevice");
        var originalInterval = deviceConfig.Sensors[0].Report.Interval;

        var desiredSensors = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto
                {
                    Enabled = true,
                    Interval = 5000 // Changed from 1000
                }
            },
            new()
            {
                Name = "channel_1",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto
                {
                    Enabled = true,
                    Interval = 1000
                }
            }
        };

        // Act
        var result = ConfigurationUpdateHelper.ReplaceSensors(
            deviceConfig, desiredSensors, "test-device-id");

        // Assert
        result.UpdatedSensors.ShouldContain("channel_0");
        result.AddedSensors.ShouldBeEmpty();
        result.RemovedSensors.ShouldBeEmpty();
        deviceConfig.Sensors.First(s => s.Name == "channel_0").Report.Interval.ShouldBe(5000);
    }

    [Fact]
    public void ReplaceSensors_Should_ReturnNoChanges_When_DesiredSensorsIsNull()
    {
        // Arrange
        var deviceConfig = CreateDeviceConfiguration("TestDevice");
        var originalCount = deviceConfig.Sensors.Count;

        // Act
        var result = ConfigurationUpdateHelper.ReplaceSensors(
            deviceConfig, null, "test-device-id");

        // Assert
        result.HasChanges.ShouldBeFalse();
        deviceConfig.Sensors.Count.ShouldBe(originalCount);
    }

    [Fact]
    public void ReplaceSensors_Should_MapDspPipeline_When_AddingNewSensor()
    {
        // Arrange
        var deviceConfig = CreateDeviceConfiguration("TestDevice");

        var desiredSensors = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "sensor.with.dsp",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto
                {
                    Enabled = true,
                    Interval = 1000,
                    DspPipeline =
                    [
                        new SubNodeDspFilterConfigDto
                        {
                            Type = "kalman",
                            Enabled = true,
                            Parameters = new Dictionary<string, object>
                            {
                                ["ProcessNoise"] = 0.01,
                                ["MeasurementNoise"] = 0.1
                            }
                        }
                    ]
                }
            }
        };

        // Act
        var result = ConfigurationUpdateHelper.ReplaceSensors(
            deviceConfig, desiredSensors, "test-device-id");

        // Assert
        result.AddedSensors.ShouldContain("sensor.with.dsp");

        var newSensor = deviceConfig.Sensors.FirstOrDefault(s => s.Name == "sensor.with.dsp");
        newSensor.ShouldNotBeNull();
        newSensor.Report.DspPipeline.Count.ShouldBe(1);
        newSensor.Report.DspPipeline[0].Type.ShouldBe("kalman");
        newSensor.Report.DspPipeline[0].Parameters["ProcessNoise"].ShouldBe(0.01);
    }

    [Fact]
    public void ReplaceSensors_Should_RemoveTwoAndAddOne_When_NamesDoNotMatch()
    {
        // Arrange: Original has 3 sensors
        var deviceConfig = CreateDeviceConfiguration("TestDevice");
        deviceConfig.Sensors.Add(new Sensor
        {
            Name = "temperature_sensor3",
            Dtmi = "dtmi:test:temp3;1",
            SensorGroup = SensorGroup.AI,
            Parameters = new Dictionary<string, object>(),
            Report = new SensorReport { Enabled = true, Interval = 1000 }
        });
        deviceConfig.Sensors.Count.ShouldBe(3); // channel_0, channel_1, temperature_sensor3

        // Desired has 2 sensors with different names:
        // - channel_0 (exists)
        // - temperature_sensor.NEW (does not exist, should be added)
        var desiredSensors = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto
                {
                    Enabled = true,
                    Interval = 1000
                }
            },
            new()
            {
                Name = "temperature_sensor.NEW",
                SensorGroup = "TEMP",
                Report = new SubNodeSensorRuntimeConfigDto
                {
                    Enabled = true,
                    Interval = 2000,
                    TransformPipeline =
                    [
                        new SubNodeTransformConfigDto
                        {
                            Type = "UnitConversion",
                            Enabled = true,
                            Parameters = new Dictionary<string, object>
                            {
                                ["FromUnit"] = "celsius",
                                ["ToUnit"] = "kelvin"
                            }
                        }
                    ]
                }
            }
        };

        // Act
        var result = ConfigurationUpdateHelper.ReplaceSensors(
            deviceConfig, desiredSensors, "test-device-id");

        // Assert
        // Should remove 2 sensors: channel_1 and temperature_sensor3
        result.RemovedSensors.Count.ShouldBe(2);
        result.RemovedSensors.ShouldContain("channel_1");
        result.RemovedSensors.ShouldContain("temperature_sensor3");

        // Should add 1 sensor: temperature_sensor.NEW
        result.AddedSensors.Count.ShouldBe(1);
        result.AddedSensors.ShouldContain("temperature_sensor.NEW");

        // Final count should be 2 (channel_0 kept, temperature_sensor.NEW added)
        deviceConfig.Sensors.Count.ShouldBe(2);

        // Verify the new sensor was properly added
        var newSensor = deviceConfig.Sensors.FirstOrDefault(s => s.Name == "temperature_sensor.NEW");
        newSensor.ShouldNotBeNull();
        newSensor!.Report.TransformPipeline.Count.ShouldBe(1);
        newSensor.Report.TransformPipeline[0].Type.ShouldBe("UnitConversion");
    }

    #endregion

    #region CreatePeriodicAggregatedReport — restart desired-state Tests

    // Bug (QA): on SubNode restart, the baseline config report carries only `reported`
    // and drops `desired`, even though the cloud-desired state was persisted on the edge
    // (devicecfg.cache.json) and reloaded into DeviceConfiguration.RawDeviceCfgJson at startup.
    // Root cause: CreatePeriodicAggregatedReport hardcodes Cfg.Desired = null.

    [Fact]
    public void CreatePeriodicAggregatedReport_Should_IncludeDesired_When_CachedDesiredExists()
    {
        // Arrange — a device whose RawDeviceCfgJson came from the cached cloud desired
        // (this is what WedaApplicationContext.ApplyCachedConfigurationIfExists sets on restart).
        var rawDeviceCfg = JsonDocument.Parse(
            """
            {
              "subNode": { "name": "testSubNode", "model": "Demo", "manufacturer": "Advantech" },
              "deviceConfigs": {
                "testDevice": {
                  "enabled": true,
                  "sensors": [ { "name": "temperature_sensor" } ]
                }
              }
            }
            """).RootElement.Clone();

        var config = CreateDeviceConfiguration("TestDevice");
        config.RawDeviceCfgJson = rawDeviceCfg;

        var registry = new DeviceRegistry();
        registry.Register(new FakeDevice(config));

        // Act — restart: cached desired exists.
        var report = ConfigurationUpdateHelper.CreatePeriodicAggregatedReport(
            subNodeId: "353060712559411200",
            deviceRegistry: registry,
            lastStatus: "SUCCESS",
            lastErrorMessage: null,
            includeDesiredFromCache: true);

        // Assert — restart should report BOTH desired and reported.
        report.Data.ShouldNotBeNull();
        report.Data!.Cfg.ShouldNotBeNull();
        report.Data.Cfg!.Reported.ShouldNotBeNull();       // reported is present today
        report.Data.Cfg.Desired.ShouldNotBeNull();         // FAILS today: desired is hardcoded null
    }

    [Fact]
    public void CreatePeriodicAggregatedReport_Should_NotIncludeDesired_When_NoCachedDesired()
    {
        // Arrange — first-ever registration: no RawDeviceCfgJson (no cloud-desired persisted yet).
        var config = CreateDeviceConfiguration("TestDevice");
        config.RawDeviceCfgJson = null;

        var registry = new DeviceRegistry();
        registry.Register(new FakeDevice(config));

        // Act — first-ever registration: no cache signal.
        var report = ConfigurationUpdateHelper.CreatePeriodicAggregatedReport(
            subNodeId: "353060712559411200",
            deviceRegistry: registry,
            lastStatus: "SUCCESS",
            lastErrorMessage: null,
            includeDesiredFromCache: false);

        // Assert — with no cached desired, we must NOT invent a desired the cloud never sent.
        report.Data!.Cfg!.Reported.ShouldNotBeNull();
        report.Data.Cfg.Desired.ShouldBeNull();
    }

    [Fact]
    public void CreatePeriodicAggregatedReport_Should_NotIncludeDesired_When_CacheFlagSetButNoRawJson()
    {
        // Arrange — cache flag on, but the device has no RawDeviceCfgJson to echo.
        var config = CreateDeviceConfiguration("TestDevice");
        config.RawDeviceCfgJson = null;

        var registry = new DeviceRegistry();
        registry.Register(new FakeDevice(config));

        // Act
        var report = ConfigurationUpdateHelper.CreatePeriodicAggregatedReport(
            subNodeId: "353060712559411200",
            deviceRegistry: registry,
            lastStatus: "SUCCESS",
            lastErrorMessage: null,
            includeDesiredFromCache: true);

        // Assert — nothing to echo -> desired stays absent (no crash, no empty desired).
        report.Data!.Cfg!.Reported.ShouldNotBeNull();
        report.Data.Cfg.Desired.ShouldBeNull();
    }

    #endregion

    #region PatchReportedRecordIntervals (cloud round-trip)

    // The reported devicecfg echoes the desired raw JSON verbatim, and the cloud validator range-
    // checks the record interval regardless of the enabled flag. These tests pin that the edge
    // rewrites each sensor's record interval with the resolved, in-range value before reporting.

    private static (DeviceRegistry registry, DeviceConfiguration config) RegistryWithRecord(
        bool enabled, int? runtimeRecordInterval, double reportInterval = 1000)
    {
        var config = CreateDeviceConfiguration("TestDevice");
        var sensor = config.Sensors.First(s => s.Name == "channel_0");
        sensor.Report.Interval = reportInterval;
        sensor.Record.Enabled = enabled;
        sensor.Record.Interval = runtimeRecordInterval;

        var registry = new DeviceRegistry();
        registry.Register(new FakeDevice(config));
        return (registry, config);
    }

    private static JsonElement Prop(JsonElement el, string name)
    {
        // Case-insensitive lookup — the patched JSON preserves each key's original casing, and an
        // inserted interval uses the camelCase wire name; both are valid since the cloud reads
        // case-insensitively.
        foreach (var p in el.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        }
        throw new Xunit.Sdk.XunitException($"property {name} not found");
    }

    private static int ReadRecordInterval(JsonElement patched, string deviceName, string sensorName)
    {
        var sensors = Prop(Prop(Prop(patched, "DeviceConfigs"), deviceName), "Sensors");
        foreach (var s in sensors.EnumerateArray())
        {
            if (Prop(s, "Name").GetString() == sensorName)
                return Prop(Prop(s, "Record"), "Interval").GetInt32();
        }
        throw new Xunit.Sdk.XunitException($"sensor {sensorName} not found");
    }

    [Fact]
    public void PatchReportedRecordIntervals_DisabledZero_ClampedToFloor()
    {
        // Desired carried a disabled record with interval 0 (valid locally, but the cloud would
        // reject 0). The runtime interval is already resolved to the report interval; patch must
        // write that in-range value into the echoed JSON.
        var (registry, _) = RegistryWithRecord(enabled: false, runtimeRecordInterval: 1000, reportInterval: 1000);
        var raw = JsonDocument.Parse(
            """
            { "DeviceConfigs": { "TestDevice": { "Sensors": [
              { "Name": "channel_0", "Record": { "Enabled": false, "Interval": 0 } }
            ] } } }
            """).RootElement.Clone();

        var patched = ConfigurationUpdateHelper.PatchReportedRecordIntervals(raw, registry);

        ReadRecordInterval(patched, "TestDevice", "channel_0").ShouldBe(1000);
    }

    [Fact]
    public void PatchReportedRecordIntervals_MissingInterval_Inserted()
    {
        // Desired omitted the interval entirely; patch must insert the resolved in-range value so
        // the cloud sees an interval field rather than a missing one.
        var (registry, _) = RegistryWithRecord(enabled: false, runtimeRecordInterval: 3000, reportInterval: 3000);
        var raw = JsonDocument.Parse(
            """
            { "DeviceConfigs": { "TestDevice": { "Sensors": [
              { "Name": "channel_0", "Record": { "Enabled": false } }
            ] } } }
            """).RootElement.Clone();

        var patched = ConfigurationUpdateHelper.PatchReportedRecordIntervals(raw, registry);

        ReadRecordInterval(patched, "TestDevice", "channel_0").ShouldBe(3000);
    }

    [Fact]
    public void PatchReportedRecordIntervals_EnabledValue_Preserved()
    {
        var (registry, _) = RegistryWithRecord(enabled: true, runtimeRecordInterval: 5000);
        var raw = JsonDocument.Parse(
            """
            { "DeviceConfigs": { "TestDevice": { "Sensors": [
              { "Name": "channel_0", "Record": { "Enabled": true, "Interval": 5000 } }
            ] } } }
            """).RootElement.Clone();

        var patched = ConfigurationUpdateHelper.PatchReportedRecordIntervals(raw, registry);

        ReadRecordInterval(patched, "TestDevice", "channel_0").ShouldBe(5000);
    }

    [Fact]
    public void PatchReportedRecordIntervals_CamelCaseKeys_Patched()
    {
        // Wire-echo JSON is camelCase; the patch must find and rewrite it regardless of casing.
        var (registry, _) = RegistryWithRecord(enabled: false, runtimeRecordInterval: 1000, reportInterval: 500);
        var raw = JsonDocument.Parse(
            """
            { "deviceConfigs": { "TestDevice": { "sensors": [
              { "name": "channel_0", "record": { "enabled": false, "interval": 0 } }
            ] } } }
            """).RootElement.Clone();

        var patched = ConfigurationUpdateHelper.PatchReportedRecordIntervals(raw, registry);

        var sensor = patched.GetProperty("deviceConfigs").GetProperty("TestDevice")
            .GetProperty("sensors").EnumerateArray().First();
        // report interval 500 is below the record floor, so the resolved value clamps up to 1000.
        sensor.GetProperty("record").GetProperty("interval").GetInt32().ShouldBe(1000);
    }

    [Fact]
    public void PatchReportedRecordIntervals_SensorWithoutRecord_Untouched()
    {
        var (registry, _) = RegistryWithRecord(enabled: false, runtimeRecordInterval: null);
        var raw = JsonDocument.Parse(
            """
            { "DeviceConfigs": { "TestDevice": { "Sensors": [
              { "Name": "channel_0", "SensorInfo": { "Schema": "double" } }
            ] } } }
            """).RootElement.Clone();

        var patched = ConfigurationUpdateHelper.PatchReportedRecordIntervals(raw, registry);

        var sensor = patched.GetProperty("DeviceConfigs").GetProperty("TestDevice")
            .GetProperty("Sensors").EnumerateArray().First();
        sensor.TryGetProperty("Record", out _).ShouldBeFalse();
        // Unrelated fields are preserved verbatim.
        sensor.GetProperty("SensorInfo").GetProperty("Schema").GetString().ShouldBe("double");
    }

    [Fact]
    public void RecordInterval_ApplyThenPatch_EndToEnd_ReportsResolvedValue()
    {
        // End-to-end over the real logic: a desired disabled record carries interval 0 (which the
        // cloud would reject). Apply resolves it into the runtime sensor via ReplaceSensors, then the
        // reported raw JSON is patched from that runtime state — so the value echoed to the cloud is
        // the resolved, in-range one, not the desired 0.
        var config = CreateDeviceConfiguration("TestDevice");
        config.Sensors.First(s => s.Name == "channel_0").Report.Interval = 3000;

        var desired = new List<SubNodeSensorReportDto>
        {
            new()
            {
                Name = "channel_0",
                SensorGroup = "AI",
                Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 3000 },
                Record = new SubNodeSensorRecordDto { Enabled = false, Interval = 0 }
            }
        };

        // Real apply path resolves the runtime record interval (0 -> report interval 3000).
        ConfigurationUpdateHelper.ReplaceSensors(config, desired, "TestDevice");
        config.Sensors.First(s => s.Name == "channel_0").Record.Interval.ShouldBe(3000);

        var registry = new DeviceRegistry();
        registry.Register(new FakeDevice(config));

        // The desired raw JSON still carries the out-of-range 0; the patch must replace it.
        var raw = JsonDocument.Parse(
            """
            { "DeviceConfigs": { "TestDevice": { "Sensors": [
              { "Name": "channel_0", "Record": { "Enabled": false, "Interval": 0 } }
            ] } } }
            """).RootElement.Clone();

        var patched = ConfigurationUpdateHelper.PatchReportedRecordIntervals(raw, registry);

        ReadRecordInterval(patched, "TestDevice", "channel_0").ShouldBe(3000);
    }

    #endregion

    #region ToSubNodeDeviceConfigDto record mapping (fallback report path)

    [Fact]
    public void ToSubNodeDeviceConfigDto_MapsRecord_WithResolvedInterval()
    {
        // The fallback report path (used when no raw devicecfg JSON is available) must also carry the
        // resolved, in-range record interval so the cloud range check passes.
        var config = CreateDeviceConfiguration("TestDevice");
        var sensor = config.Sensors.First(s => s.Name == "channel_0");
        sensor.Report.Interval = 2000;
        sensor.Record.Enabled = false;
        sensor.Record.Interval = 0; // unresolved sentinel; DTO mapping must resolve it

        var dto = ConfigurationUpdateHelper.ToSubNodeDeviceConfigDto(config);

        var sensorDto = dto.Sensors!.First(s => s.Name == "channel_0");
        sensorDto.Record.ShouldNotBeNull();
        sensorDto.Record!.Enabled.ShouldBeFalse();
        // 0 resolves to the report interval (2000), already above the 1000 floor.
        sensorDto.Record.Interval.ShouldBe(2000);
    }

    [Fact]
    public void ToSubNodeDeviceConfigDto_MapsRecord_ClampsSubFloorToFloor()
    {
        var config = CreateDeviceConfiguration("TestDevice");
        var sensor = config.Sensors.First(s => s.Name == "channel_0");
        sensor.Report.Interval = 500;  // below the record floor
        sensor.Record.Enabled = true;
        sensor.Record.Interval = 500;

        var dto = ConfigurationUpdateHelper.ToSubNodeDeviceConfigDto(config);

        var sensorDto = dto.Sensors!.First(s => s.Name == "channel_0");
        // report interval 500 is below the record floor, so the resolved value clamps up to 1000.
        sensorDto.Record!.Interval.ShouldBe(1000);
    }

    #endregion
}

/// <summary>
/// Minimal IDevice test double for CreatePeriodicAggregatedReport, which only reads
/// <see cref="IDevice.Configuration"/>. All other members are unsupported by design.
/// </summary>
internal sealed class FakeDevice(DeviceConfiguration configuration) : IDevice
{
    public DeviceConfiguration Configuration { get; } = configuration;
    public string SubNodeId => Configuration.DeviceId ?? "";
    public string DeviceName => Configuration.DeviceName;
    public SubNodeType SubNodeType => Configuration.SubNodeType;

    private static NotSupportedException NotUsed([System.Runtime.CompilerServices.CallerMemberName] string member = "")
        => new($"FakeDevice.{member} is not used by CreatePeriodicAggregatedReport.");

    public IReadOnlyDictionary<string, object> Properties => throw NotUsed();
    public DeviceStatus Status => throw NotUsed();
    public CommunicationState ConnectionState => throw NotUsed();
    public Task<bool> InitializeAsync(CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<bool> StartAsync(CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<bool> StopAsync(CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<List<TelemetryMeasure>> ReadSensorTelemetryAsync(string sensorResourceId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<DeviceHealth> GetHealthAsync(CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<string?> RegisterAsync(CancellationToken cancellationToken = default) => throw NotUsed();
    public Task SendTelemetryAsync(IAsyncEnumerable<TelemetryMeasure> data, CancellationToken cancellationToken = default, params IDspFilter[] runtimeFilters) => throw NotUsed();
    public Task ReportHealthAsync(CancellationToken cancellationToken = default) => throw NotUsed();
    public event EventHandler<DataReceivedEvent>? DataReceived { add { } remove { } }
    public bool EnableDataReceivedTracking { get; set; }
    public event EventHandler<DataProcessedEvent>? DataProcessed { add { } remove { } }
    public bool EnableDataProcessedTracking { get; set; }
    public event EventHandler<ConnectionStateChangedEvent>? ConnectionStateChanged { add { } remove { } }
    public bool EnableConnectionStateTracking { get; set; }
    public event EventHandler<DeviceStatusChangedEvent>? DeviceStatusChanged { add { } remove { } }
    public bool EnableDeviceStatusTracking { get; set; }
    public event EventHandler<TelemetrySentEvent>? TelemetrySent { add { } remove { } }
    public bool EnableTelemetrySentTracking { get; set; }
    public event EventHandler<UpdateConfigurationEvent>? ConfigurationUpdateReceived { add { } remove { } }
    public bool EnableConfigurationUpdateTracking { get; set; }
    public event EventHandler<TelemetryValueChangedEvent>? ValueChanged { add { } remove { } }
    public bool EnableValueChangeTracking { get; set; }
    public Sensor GetSensor(string sensorName) => throw NotUsed();
    public Sensor? FindSensor(string sensorName) => throw NotUsed();
    public Sensor GetSensorByResourceId(string resourceId) => throw NotUsed();
    public Sensor? FindSensorByResourceId(string resourceId) => throw NotUsed();
    public Task<ConfigUpdateValidationResult> ValidateConfigurationUpdateAsync(SubNodeConfigUpdateMessage message, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<ConfigUpdateResult> ApplyValidatedConfigurationAsync(SubNodeConfigUpdateMessage message, DeviceConfigurationBackup backup, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task RollbackConfigurationAsync(DeviceConfigurationBackup backup, CancellationToken cancellationToken = default) => throw NotUsed();
    public DeviceConfigurationBackup CreateConfigurationBackup() => throw NotUsed();
    public void Dispose() { }
}