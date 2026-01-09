using Shouldly;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Configuration;
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
        // Default minimum is 300000ms (5 minutes)
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Periods!.ReportConfiguration = 100000;
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
        // Default maximum is 86400000ms (24 hours)
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Periods!.ReportConfiguration = 100_000_000;
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

    #endregion

    #region Sensor Validation Tests

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
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Config!.Interval = -1;
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("interval");
        result.ErrorMessage?.ShouldContain("negative");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_UnknownSensorProvided()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors!.Add(
            new SubNodeSensorReportDto
            {
                Name = "unknown-sensor",
                Config = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 1000 }
            });
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("Unknown sensor");
        result.ErrorMessage?.ShouldContain("unknown-sensor");
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_AllowUnknownSensor_When_RejectUnknownSensorsDisabled()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors!.Add(
            new SubNodeSensorReportDto
            {
                Name = "unknown-sensor",
                Config = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 1000 }
            });
        var currentConfig = CreateDeviceConfiguration("TestDevice");
        var options = new ConfigUpdateOptions { RejectUnknownSensors = false, RequireAllSensors = false };

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig, options);

        // Assert - Should pass because unknown sensors are allowed
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateDeviceConfiguration_Should_ReturnFailure_When_RequiredSensorMissing()
    {
        // Arrange
        var message = CreateValidMessage();
        // Remove one sensor from the message
        message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors!.RemoveAt(1);
        var currentConfig = CreateDeviceConfiguration("TestDevice");

        // Act
        var result = ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, currentConfig);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage?.ShouldContain("Missing sensor");
        result.ErrorMessage?.ShouldContain("channel.1");
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
        var sensorConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Config!;
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
        var sensorConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Config!;
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
        var sensorConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Config!;
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
        var sensorConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Config!;
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
        var sensorConfig = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.DeviceConfigs!["TestDevice"].Sensors![0].Config!;
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
    public void ConfigUpdateOptions_Default_Should_UseReplaceMode()
    {
        // Act
        var options = ConfigUpdateOptions.Default;

        // Assert
        options.UpdateMode.ShouldBe(ConfigUpdateMode.Replace);
        options.ValidateDeviceName.ShouldBeTrue();
        options.ValidatePeriods.ShouldBeTrue();
        options.ValidateSensors.ShouldBeTrue();
        options.ValidateThresholds.ShouldBeTrue();
        options.RejectUnknownSensors.ShouldBeTrue();
        options.RequireAllSensors.ShouldBeTrue();
    }

    [Fact]
    public void ConfigUpdateOptions_Relaxed_Should_UsePatchMode()
    {
        // Act
        var options = ConfigUpdateOptions.Relaxed;

        // Assert
        options.UpdateMode.ShouldBe(ConfigUpdateMode.Patch);
        options.RejectUnknownSensors.ShouldBeFalse();
        options.RequireAllSensors.ShouldBeFalse();
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
                Name = "channel.0",
                Config = new SubNodeSensorRuntimeConfigDto { Enabled = false, Interval = 1000 }
            }
        };

        // Act
        var updatedSensors = ConfigurationUpdateHelper.ApplysensorReportUpdates(config, desiredSensors);

        // Assert
        updatedSensors.ShouldContain("channel.0");
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
                Name = "channel.0",
                Config = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 5000 }
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
                Name = "channel.0",
                Config = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 1000, Unit = "V" }
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
                Name = "unknown-sensor",
                Config = new SubNodeSensorRuntimeConfigDto { Enabled = false, Interval = 9999 }
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
                Name = "CHANNEL.0", // Uppercase
                Config = new SubNodeSensorRuntimeConfigDto { Enabled = false, Interval = 1000 }
            }
        };

        // Act
        var updatedSensors = ConfigurationUpdateHelper.ApplysensorReportUpdates(config, desiredSensors);

        // Assert - Should match "channel.0"
        updatedSensors.ShouldContain("channel.0");
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
                Name = "channel.0",
                Config = new SubNodeSensorRuntimeConfigDto
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

        var sensorBackup = backup.SensorBackups.First(s => s.Name == "channel.0");
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
            SensorBackups = new List<sensorReportBackup>
            {
                new()
                {
                    Name = "channel.0",
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
                    Name = "channel.1",
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
            SensorBackups = new List<sensorReportBackup>
            {
                new()
                {
                    Name = "unknown-sensor",
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

    #region Report Creation Tests

    [Fact]
    public void CreateUpdatingReport_Should_SetCorrectStatus()
    {
        // Arrange
        var incomingMessage = CreateValidMessage();
        var config = CreateDeviceConfiguration("TestDevice");

        // Act
        var report = ConfigurationUpdateHelper.CreateUpdatingReport(incomingMessage, config, "TestDevice");

        // Assert
        report.Data!.Cfg!.Reported!.Status.ShouldBe(ConfigUpdateStatus.Updating);
        report.Cmd.ShouldBe("updateCmdResponse");
        report.DeviceId.ShouldBe(incomingMessage.DeviceId);
    }

    [Fact]
    public void CreateSuccessReport_Should_SetCorrectStatus()
    {
        // Arrange
        var incomingMessage = CreateValidMessage();
        var config = CreateDeviceConfiguration("TestDevice");

        // Act
        var report = ConfigurationUpdateHelper.CreateSuccessReport(incomingMessage, config, "TestDevice");

        // Assert
        report.Data!.Cfg!.Reported!.Status.ShouldBe(ConfigUpdateStatus.Success);
    }

    [Fact]
    public void CreateFailedReport_Should_IncludeErrorMessage()
    {
        // Arrange
        var incomingMessage = CreateValidMessage();
        var config = CreateDeviceConfiguration("TestDevice");
        var errorMessage = "Connection timeout";

        // Act
        var report = ConfigurationUpdateHelper.CreateFailedReport(incomingMessage, config, "TestDevice", errorMessage);

        // Assert
        report.Data!.Cfg!.Reported!.Status.ShouldBe(ConfigUpdateStatus.Failed);
        report.Data.Cfg.Reported.ErrorMessage?.ShouldBe(errorMessage);
    }

    [Fact]
    public void CreateInvalidReport_Should_IncludeErrorMessage()
    {
        // Arrange
        var incomingMessage = CreateValidMessage();
        var config = CreateDeviceConfiguration("TestDevice");
        var errorMessage = "Invalid sensor configuration";

        // Act
        var report = ConfigurationUpdateHelper.CreateInvalidReport(incomingMessage, config, "TestDevice", errorMessage);

        // Assert
        report.Data!.Cfg!.Reported!.Status.ShouldBe(ConfigUpdateStatus.Invalid);
        report.Data.Cfg.Reported.ErrorMessage?.ShouldBe(errorMessage);
    }

    [Fact]
    public void CreatePeriodicReport_Should_HaveNoDesiredState()
    {
        // Arrange
        var config = CreateDeviceConfiguration("TestDevice");

        // Act
        var report = ConfigurationUpdateHelper.CreatePeriodicReport("device-123", "default", config, "TestDevice");

        // Assert
        report.Data!.Cfg!.Desired.ShouldBeNull();
        report.Data.Cfg.Reported.ShouldNotBeNull();
        report.Data.Cfg.Reported!.Status.ShouldBe(ConfigUpdateStatus.Success);
        report.Cmd.ShouldBe("configReport");
    }

    [Fact]
    public void CreateReport_Should_IncludeCurrentReportedState()
    {
        // Arrange
        var incomingMessage = CreateValidMessage();
        var config = CreateDeviceConfiguration("TestDevice");
        config.Sensors[0].Report.Enabled = false;
        config.Sensors[0].Report.Interval = 5000;

        // Act
        var report = ConfigurationUpdateHelper.CreateSuccessReport(incomingMessage, config, "TestDevice");

        // Assert
        var reportedConfig = report.Data!.Cfg!.Reported!.DeviceConfigs!["TestDevice"];
        reportedConfig.Sensors![0].Report!.Enabled.ShouldBeFalse();
        reportedConfig.Sensors[0].Report!.Interval.ShouldBe(5000);
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
        deviceConfig.Sensors![0].Config!.Enabled = false;
        deviceConfig.Sensors[0].Config!.Interval = 5000;

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
                                            Name = "channel.0",
                                            Dtmi = "dtmi:test:sensor;1",
                                            SensorGroup = "AI",
                                            Config = new SubNodeSensorRuntimeConfigDto
                                            {
                                                Enabled = true,
                                                Interval = 1000
                                            }
                                        },
                                        new()
                                        {
                                            Name = "channel.1",
                                            Dtmi = "dtmi:test:sensor;1",
                                            SensorGroup = "AI",
                                            Config = new SubNodeSensorRuntimeConfigDto
                                            {
                                                Enabled = true,
                                                Interval = 1000
                                            }
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
                    Name = "channel.0",
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
                    Name = "channel.1",
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
}