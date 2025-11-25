using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Configuration;

/// <summary>
/// Helper class for handling configuration updates from cloud.
/// Provides validation, mapping, and response building functionality.
/// </summary>
public static class ConfigurationUpdateHelper
{
    /// <summary>
    /// Validates the configuration update message.
    /// </summary>
    /// <param name="message">The configuration update message to validate</param>
    /// <param name="errorMessage">Error message if validation fails</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateConfigurationUpdate(
        SubNodeConfigurationUpdateMessage message,
        out string? errorMessage)
    {
        errorMessage = null;

        if (message == null)
        {
            errorMessage = "Configuration update message is null";
            return false;
        }

        if (message.Data?.Cfg?.Desired == null)
        {
            errorMessage = "Missing desired configuration in update message";
            return false;
        }

        if (message.Data.Cfg.Desired.SubNodeDeviceConfig?.DeviceConfigs == null ||
            message.Data.Cfg.Desired.SubNodeDeviceConfig.DeviceConfigs.Count == 0)
        {
            errorMessage = "Missing device configurations in desired state";
            return false;
        }

        // Validate each device config
        foreach (var (deviceKey, deviceConfig) in message.Data.Cfg.Desired.SubNodeDeviceConfig.DeviceConfigs)
        {
            if (string.IsNullOrEmpty(deviceConfig.DeviceName))
            {
                errorMessage = $"Device '{deviceKey}' is missing DeviceName";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Creates a configuration report message for the "updating" status (first report).
    /// Contains the new desired state and the current reported state.
    /// </summary>
    public static SubNodeConfigurationUpdateMessage CreateUpdatingReport(
        SubNodeConfigurationUpdateMessage incomingMessage,
        DeviceConfiguration currentConfig,
        string deviceTypeName)
    {
        return CreateReport(
            incomingMessage,
            currentConfig,
            deviceTypeName,
            ConfigUpdateStatus.Updating,
            null);
    }

    /// <summary>
    /// Creates a configuration report message for the "success" status (second report).
    /// Contains the desired state and the updated reported state.
    /// </summary>
    public static SubNodeConfigurationUpdateMessage CreateSuccessReport(
        SubNodeConfigurationUpdateMessage incomingMessage,
        DeviceConfiguration updatedConfig,
        string deviceTypeName)
    {
        return CreateReport(
            incomingMessage,
            updatedConfig,
            deviceTypeName,
            ConfigUpdateStatus.Success,
            null);
    }

    /// <summary>
    /// Creates a configuration report message for the "failed" status.
    /// </summary>
    public static SubNodeConfigurationUpdateMessage CreateFailedReport(
        SubNodeConfigurationUpdateMessage incomingMessage,
        DeviceConfiguration currentConfig,
        string deviceTypeName,
        string errorMessage)
    {
        return CreateReport(
            incomingMessage,
            currentConfig,
            deviceTypeName,
            ConfigUpdateStatus.Failed,
            errorMessage);
    }

    /// <summary>
    /// Creates a configuration report message for the "invalid" status.
    /// </summary>
    public static SubNodeConfigurationUpdateMessage CreateInvalidReport(
        SubNodeConfigurationUpdateMessage incomingMessage,
        DeviceConfiguration currentConfig,
        string deviceTypeName,
        string errorMessage)
    {
        return CreateReport(
            incomingMessage,
            currentConfig,
            deviceTypeName,
            ConfigUpdateStatus.Invalid,
            errorMessage);
    }

    /// <summary>
    /// Applies sensor configuration updates from the desired config to the device configuration.
    /// </summary>
    /// <param name="deviceConfig">The device configuration to update</param>
    /// <param name="desiredSensors">The desired sensor configurations</param>
    /// <returns>List of sensor names that were updated</returns>
    public static List<string> ApplySensorConfigUpdates(
        DeviceConfiguration deviceConfig,
        IReadOnlyList<SubNodeSensorConfigDto>? desiredSensors)
    {
        var updatedSensors = new List<string>();

        if (desiredSensors == null || desiredSensors.Count == 0)
            return updatedSensors;

        foreach (var desiredSensor in desiredSensors)
        {
            // Find matching sensor by name
            var sensor = deviceConfig.Sensors.FirstOrDefault(s =>
                s.Name.Equals(desiredSensor.Name, StringComparison.OrdinalIgnoreCase));

            if (sensor == null)
                continue;

            // Apply config updates
            if (desiredSensor.Config != null)
            {
                sensor.Config.Enabled = desiredSensor.Config.Enabled;
                sensor.Config.Interval = desiredSensor.Config.Interval;

                // Apply thresholds if provided
                if (desiredSensor.Config.Thresholds != null)
                {
                    sensor.Config.Thresholds = new ThresholdConfig
                    {
                        UpperCritical = desiredSensor.Config.Thresholds.UpperCritical,
                        UpperWarning = desiredSensor.Config.Thresholds.UpperWarning,
                        LowerWarning = desiredSensor.Config.Thresholds.LowerWarning,
                        LowerCritical = desiredSensor.Config.Thresholds.LowerCritical
                    };
                }

                updatedSensors.Add(sensor.Name);
            }
        }

        return updatedSensors;
    }

    /// <summary>
    /// Converts a DeviceConfiguration to SubNodeDeviceConfigDto for reporting.
    /// </summary>
    public static SubNodeDeviceConfigDto ToSubNodeDeviceConfigDto(
        DeviceConfiguration config)
    {
        return new SubNodeDeviceConfigDto
        {
            Enabled = true,
            DeviceName = config.DeviceName,
            DeviceType = config.DeviceType.ToString(),
            DtdlPath = config.DtdlPath,
            DeviceCapabilities = new SubNodeDeviceCapabilitiesDto
            {
                Manufacturer = config.DeviceCapabilities.Manufacturer,
                Model = config.DeviceCapabilities.Model,
                SubNodeSwVersion = config.DeviceCapabilities.SubNodeSwVersion,
                DeviceInfo = config.DeviceCapabilities.DeviceInfo
            },
            Communication = config.Communication,
            Sensors = config.Sensors.Select(s => new SubNodeSensorConfigDto
            {
                Name = s.Name,
                Dtmi = s.Dtmi,
                SensorGroup = s.SensorGroup.ToString(),
                Parameters = s.Parameters,
                Config = new SubNodeSensorRuntimeConfigDto
                {
                    Enabled = s.Config.Enabled,
                    Interval = (int)s.Config.Interval,
                    Unit = s.Config.Unit,
                    Thresholds = s.Config.Thresholds != null
                        ? new SubNodeThresholdsDto
                        {
                            UpperCritical = s.Config.Thresholds.UpperCritical,
                            UpperWarning = s.Config.Thresholds.UpperWarning,
                            LowerWarning = s.Config.Thresholds.LowerWarning,
                            LowerCritical = s.Config.Thresholds.LowerCritical
                        }
                        : null
                }
            }).ToList()
        };
    }

    private static SubNodeConfigurationUpdateMessage CreateReport(
        SubNodeConfigurationUpdateMessage incomingMessage,
        DeviceConfiguration currentConfig,
        string deviceTypeName,
        string status,
        string? errorMessage)
    {
        var reportedDeviceConfig = ToSubNodeDeviceConfigDto(currentConfig);

        return new SubNodeConfigurationUpdateMessage
        {
            DeviceId = incomingMessage.DeviceId,
            GroupId = incomingMessage.GroupId,
            Cmd = "updateCmdResponse",
            SeqId = incomingMessage.SeqId,
            ReqSeqId = incomingMessage.ReqSeqId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    // Include the original desired state
                    Desired = incomingMessage.Data?.Cfg?.Desired,
                    // Include the current/updated reported state
                    Reported = new SubNodeReportedConfig
                    {
                        SubNodeDeviceConfig = new SubNodeDeviceConfigWrapper
                        {
                            DeviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>
                            {
                                [deviceTypeName] = reportedDeviceConfig
                            }
                        },
                        Status = status,
                        ErrorMessage = errorMessage,
                        LastUpdateTime = DateTimeOffset.UtcNow
                    }
                }
            }
        };
    }
}
