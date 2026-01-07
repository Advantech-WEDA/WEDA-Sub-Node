using ErrorOr;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace Weda.SubNode.Core.Configuration;

/// <summary>
/// Helper class for handling configuration updates from cloud.
/// Provides validation, mapping, and response building functionality.
/// </summary>
public static partial class ConfigurationUpdateHelper
{
    /// <summary>
    /// Validates the configuration update message structure.
    /// </summary>
    /// <param name="message">The configuration update message to validate</param>
    /// <param name="options">Validation options (uses Default if null)</param>
    /// <returns>Validation result</returns>
    public static ConfigurationValidationResult ValidateMessage(
        SubNodeConfigUpdateMessage message,
        ConfigUpdateOptions? options = null)
    {
        if (message == null)
            return ConfigurationValidationResult.Failure("Configuration update message is null");

        if (message.Data?.Cfg?.Desired == null)
            return ConfigurationValidationResult.Failure("Missing desired configuration in update message");

        // Empty desired config is valid but means no update is required
        // This can happen when cloud sends a sync message with empty desired state
        var deviceConfigs = message.Data.Cfg.Desired.SubNodeDeviceConfig?.DeviceConfigs;
        if (deviceConfigs == null || deviceConfigs.Count == 0)
            return ConfigurationValidationResult.NoUpdate;

        // Check for duplicate keys (case-insensitive)
        // Note: DeviceConfigs dictionary uses StringComparer.OrdinalIgnoreCase, so duplicates
        // would be merged during deserialization. This check catches any edge cases.
        var duplicateCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in deviceConfigs.Keys)
        {
            if (!duplicateCheck.Add(key))
            {
                return ConfigurationValidationResult.Failure(
                    $"Duplicate device configuration key detected (case-insensitive): '{key}'. " +
                    $"Device configuration keys must be unique regardless of case.");
            }
        }

        return ConfigurationValidationResult.Success;
    }

    /// <summary>
    /// Validates the configuration update for a specific device.
    /// Checks if the DeviceName matches and validates update parameters based on options.
    /// </summary>
    /// <param name="message">The configuration update message</param>
    /// <param name="currentConfig">The current device configuration</param>
    /// <param name="options">Validation options controlling which checks are enabled (uses Default if null)</param>
    /// <returns>Validation result</returns>
    public static ConfigurationValidationResult ValidateDeviceConfiguration(
        SubNodeConfigUpdateMessage message,
        DeviceConfiguration currentConfig,
        ConfigUpdateOptions? options = null)
    {
        options ??= ConfigUpdateOptions.Default;

        // First validate the message structure
        var messageResult = ValidateMessage(message, options);
        if (!messageResult.IsValid || messageResult.NoUpdateRequired)
            return messageResult;

        // Find the device config for this device
        // Note: If DeviceConfigs is null/empty, ValidateMessage already returned NoUpdate
        var deviceConfigs = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs;
        if (deviceConfigs == null)
            return ConfigurationValidationResult.Failure("No device configurations in desired state");

        // Try to find matching device config by DeviceName (using dictionary key)
        // DeviceConfigs dictionary is case-insensitive, so we can directly lookup
        if (!deviceConfigs.TryGetValue(currentConfig.DeviceName, out var desiredConfig))
        {
            return ConfigurationValidationResult.Failure($"No matching configuration found for device '{currentConfig.DeviceName}'");
        }

        // Validate periods if provided (if enabled)
        if (options.ValidatePeriods && desiredConfig.Periods != null)
        {
            if (desiredConfig.Periods.ReportHealth < 0)
                return ConfigurationValidationResult.Failure("ReportHealth period cannot be negative");

            // Validate ReportConfiguration period range (default: 5 minutes ~ 24 hours)
            // Value of 0 means disabled, which is allowed
            if (desiredConfig.Periods.ReportConfiguration > 0)
            {
                if (desiredConfig.Periods.ReportConfiguration < options.ReportConfigurationMinMs)
                    return ConfigurationValidationResult.Failure(
                        $"ReportConfiguration period ({desiredConfig.Periods.ReportConfiguration}ms) is below minimum ({options.ReportConfigurationMinMs}ms)");
                if (desiredConfig.Periods.ReportConfiguration > options.ReportConfigurationMaxMs)
                    return ConfigurationValidationResult.Failure(
                        $"ReportConfiguration period ({desiredConfig.Periods.ReportConfiguration}ms) exceeds maximum ({options.ReportConfigurationMaxMs}ms)");
            }
        }

        // Validate sensor configurations if provided
        if (desiredConfig.Sensors != null)
        {
            foreach (var sensor in desiredConfig.Sensors)
            {
                // Validate sensor name (if enabled)
                if (options.ValidateSensors && string.IsNullOrEmpty(sensor.Name))
                    return ConfigurationValidationResult.Failure("Sensor name cannot be empty");

                // Check for unknown sensors (if enabled)
                if (options.RejectUnknownSensors)
                {
                    var existingSensor = currentConfig.Sensors.FirstOrDefault(s =>
                        s.Name.Equals(sensor.Name, StringComparison.OrdinalIgnoreCase));
                    if (existingSensor == null)
                        return ConfigurationValidationResult.Failure($"Unknown sensor '{sensor.Name}' in configuration update");
                }

                if (options.ValidateSensors && sensor.Config != null)
                {
                    if (sensor.Config.Interval < 0)
                        return ConfigurationValidationResult.Failure($"Sensor '{sensor.Name}' interval cannot be negative");

                    // Validate thresholds if provided (if enabled)
                    if (options.ValidateThresholds && sensor.Config.Thresholds != null)
                    {
                        var t = sensor.Config.Thresholds;
                        if (t.UpperCritical.HasValue && t.UpperWarning.HasValue &&
                            t.UpperCritical < t.UpperWarning)
                            return ConfigurationValidationResult.Failure(
                                $"Sensor '{sensor.Name}': UpperCritical must be >= UpperWarning");

                        if (t.LowerWarning.HasValue && t.LowerCritical.HasValue &&
                            t.LowerWarning < t.LowerCritical)
                            return ConfigurationValidationResult.Failure(
                                $"Sensor '{sensor.Name}': LowerWarning must be >= LowerCritical");

                        if (t.UpperWarning.HasValue && t.LowerWarning.HasValue &&
                            t.UpperWarning < t.LowerWarning)
                            return ConfigurationValidationResult.Failure(
                                $"Sensor '{sensor.Name}': UpperWarning must be >= LowerWarning");
                    }
                }
            }
        }

        // Check if all sensors are required (if enabled)
        if (options.RequireAllSensors && desiredConfig.Sensors != null)
        {
            foreach (var existingSensor in currentConfig.Sensors)
            {
                var found = desiredConfig.Sensors.Any(s =>
                    s.Name.Equals(existingSensor.Name, StringComparison.OrdinalIgnoreCase));
                if (!found)
                    return ConfigurationValidationResult.Failure(
                        $"Missing sensor '{existingSensor.Name}' in configuration update (RequireAllSensors is enabled)");
            }
        }

        // Validate Transform and DSP Filter parameters (if enabled)
        if (options.ValidatePipelineParameters && desiredConfig.Sensors != null)
        {
            var pipelineValidationResult = ValidatePipelineParameters(currentConfig, desiredConfig.Sensors);
            if (!pipelineValidationResult.IsValid)
                return pipelineValidationResult;
        }

        return ConfigurationValidationResult.Success;
    }

    /// <summary>
    /// Creates a configuration report message for the "updating" status (first report).
    /// Contains the new desired state and the current reported state.
    /// </summary>
    public static SubNodeConfigUpdateMessage CreateUpdatingReport(
        SubNodeConfigUpdateMessage incomingMessage,
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
    public static SubNodeConfigUpdateMessage CreateSuccessReport(
        SubNodeConfigUpdateMessage incomingMessage,
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
    public static SubNodeConfigUpdateMessage CreateFailedReport(
        SubNodeConfigUpdateMessage incomingMessage,
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
    public static SubNodeConfigUpdateMessage CreateInvalidReport(
        SubNodeConfigUpdateMessage incomingMessage,
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
    /// Creates a periodic configuration report message for routine sync.
    /// Used when periodically reporting device configuration to cloud
    /// to ensure reported state is synchronized even if update response fails.
    /// </summary>
    /// <param name="deviceId">Device ID for the report</param>
    /// <param name="groupId">Group ID for multi-tenant scenarios</param>
    /// <param name="currentConfig">Current device configuration</param>
    /// <param name="deviceTypeName">Device type name for the report</param>
    /// <returns>Configuration report message with current reported state</returns>
    public static SubNodeConfigUpdateMessage CreatePeriodicReport(
        string deviceId,
        string groupId,
        DeviceConfiguration currentConfig,
        string deviceTypeName)
    {
        var reportedDeviceConfig = ToSubNodeDeviceConfigDto(currentConfig);

        return new SubNodeConfigUpdateMessage
        {
            DeviceId = deviceId,
            GroupId = groupId,
            Cmd = "configReport",
            SeqId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ReqSeqId = string.Empty,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    // No desired state for periodic report (this is a device-initiated report)
                    Desired = null,
                    // Include the current reported state
                    Reported = new SubNodeReportedConfig
                    {
                        DeviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>
                        {
                            [deviceTypeName] = reportedDeviceConfig
                        },
                        Status = ConfigUpdateStatus.Success,
                        ErrorMessage = null,
                        LastUpdateTime = DateTimeOffset.UtcNow
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates a backup snapshot of device configuration for rollback purposes.
    /// </summary>
    public static DeviceConfigurationBackup CreateBackup(DeviceConfiguration config)
    {
        return new DeviceConfigurationBackup
        {
            ReportHealthPeriod = config.Periods.ReportHealth,
            ReportConfigurationPeriod = config.Periods.ReportConfiguration,
            SensorBackups = config.Sensors.Select(s => new sensorReportBackup
            {
                Name = s.Name,
                Enabled = s.Report.Enabled,
                Interval = s.Report.Interval,
                Unit = s.Report.Unit,
                Thresholds = s.Report.Thresholds != null
                    ? new ThresholdConfig
                    {
                        UpperCritical = s.Report.Thresholds.UpperCritical,
                        UpperWarning = s.Report.Thresholds.UpperWarning,
                        LowerWarning = s.Report.Thresholds.LowerWarning,
                        LowerCritical = s.Report.Thresholds.LowerCritical
                    }
                    : null
            }).ToList()
        };
    }

    /// <summary>
    /// Restores device configuration from a backup snapshot.
    /// </summary>
    public static void RestoreBackup(DeviceConfiguration config, DeviceConfigurationBackup backup)
    {
        // Restore periods
        config.Periods.ReportHealth = backup.ReportHealthPeriod;
        config.Periods.ReportConfiguration = backup.ReportConfigurationPeriod;

        // Restore sensor configurations
        foreach (var sensorBackup in backup.SensorBackups)
        {
            var sensor = config.Sensors.FirstOrDefault(s =>
                s.Name.Equals(sensorBackup.Name, StringComparison.OrdinalIgnoreCase));

            if (sensor != null)
            {
                sensor.Report.Enabled = sensorBackup.Enabled;
                sensor.Report.Interval = sensorBackup.Interval;
                sensor.Report.Unit = sensorBackup.Unit;
                sensor.Report.Thresholds = sensorBackup.Thresholds;
            }
        }
    }

    /// <summary>
    /// Applies sensor configuration updates from the desired config to the device configuration.
    /// Uses PATCH semantics - only updates fields that are explicitly provided.
    /// </summary>
    /// <param name="deviceConfig">The device configuration to update</param>
    /// <param name="desiredSensors">The desired sensor configurations</param>
    /// <returns>List of sensor names that were updated</returns>
    public static List<string> ApplysensorReportUpdates(
        DeviceConfiguration deviceConfig,
        IReadOnlyList<SubNodeSensorReportDto>? desiredSensors)
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

            // Apply config updates (PATCH semantics - only update provided fields)
            if (desiredSensor.Config != null)
            {
                sensor.Report.Enabled = desiredSensor.Report.Enabled;
                sensor.Report.Interval = desiredSensor.Config.Interval;

                // Only update unit if provided
                if (!string.IsNullOrEmpty(desiredSensor.Config.Unit))
                {
                    sensor.Report.Unit = desiredSensor.Config.Unit;
                }

                // Apply thresholds if provided
                if (desiredSensor.Config.Thresholds != null)
                {
                    sensor.Report.Thresholds = new ThresholdConfig
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
    /// Applies DSP filter pipeline updates for a specific sensor.
    /// Updates existing filters' Enabled state and parameters without recreating instances.
    /// </summary>
    /// <param name="sensorReport">The sensor configuration containing the runtime DSP filters</param>
    /// <param name="desiredDspPipeline">The desired DSP filter configurations from cloud</param>
    /// <returns>Result indicating success or validation errors</returns>
    public static ErrorOr<DspPipelineUpdateResult> ApplyDspPipelineUpdates(
        SensorReport sensorReport,
        IReadOnlyList<SubNodeDspFilterConfigDto>? desiredDspPipeline)
    {
        var result = new DspPipelineUpdateResult();

        if (desiredDspPipeline == null || desiredDspPipeline.Count == 0)
            return result;

        var runtimeFilters = sensorReport.RuntimeDspFilters;
        var configFilters = sensorReport.DspPipeline;

        // Strategy: Match filters by index (same position = same filter)
        // For config-based filters, update the config directly
        // For runtime filters, use UpdateParameters

        // Update config-based DSP pipeline
        for (int i = 0; i < desiredDspPipeline.Count; i++)
        {
            var desired = desiredDspPipeline[i];

            // Update config-based filters (DspPipeline)
            if (i < configFilters.Count)
            {
                var configFilter = configFilters[i];

                // Type mismatch means structural change - skip for now (would require rebuild)
                if (!string.Equals(configFilter.Type, desired.Type, StringComparison.OrdinalIgnoreCase))
                {
                    result.SkippedFilters.Add($"Config filter at index {i}: type mismatch ({configFilter.Type} vs {desired.Type})");
                    continue;
                }

                // Update enabled state
                configFilter.Enabled = desired.Enabled;

                // Update parameters
                if (desired.Parameters != null)
                {
                    configFilter.Parameters = new Dictionary<string, object>(desired.Parameters);
                }

                result.UpdatedConfigFilters.Add($"{configFilter.Type}[{i}]");
            }

            // Also update runtime filters if they exist at this index
            if (i < runtimeFilters.Count)
            {
                var runtimeFilter = runtimeFilters[i];
                var parameters = desired.Parameters ?? new Dictionary<string, object>();

                // Try to validate and update parameters if the filter implements IConfigurableDspFilter
                if (TryGetConfigurableDspFilter(runtimeFilter, out var validateParams, out var updateParams))
                {
                    var validationResult = validateParams(parameters);
                    if (validationResult.IsError)
                    {
                        return Error.Validation(
                            code: "DspPipeline.ValidationFailed",
                            description: $"DSP filter at index {i}: {validationResult.FirstError.Description}");
                    }

                    // Update parameters (preserves internal state)
                    updateParams(parameters);
                }

                // Update enabled state
                runtimeFilter.Enabled = desired.Enabled;

                result.UpdatedRuntimeFilters.Add($"RuntimeFilter[{i}]");
            }
        }

        return result;
    }

    /// <summary>
    /// Applies transform pipeline updates for a specific sensor.
    /// Updates existing transforms' Enabled state and parameters without recreating instances.
    /// </summary>
    /// <param name="sensorReport">The sensor configuration containing the runtime transforms</param>
    /// <param name="desiredTransformPipeline">The desired transform configurations from cloud</param>
    /// <returns>Result indicating success or validation errors</returns>
    public static ErrorOr<TransformPipelineUpdateResult> ApplyTransformPipelineUpdates(
        SensorReport sensorReport,
        IReadOnlyList<SubNodeTransformConfigDto>? desiredTransformPipeline)
    {
        var result = new TransformPipelineUpdateResult();

        if (desiredTransformPipeline == null || desiredTransformPipeline.Count == 0)
            return result;

        var runtimeTransforms = sensorReport.RuntimeTransforms;
        var configTransforms = sensorReport.TransformPipeline;

        // Strategy: Match transforms by index (same position = same transform)
        // For config-based transforms, update the config directly
        // For runtime transforms, use UpdateParameters

        // Update config-based transform pipeline
        for (int i = 0; i < desiredTransformPipeline.Count; i++)
        {
            var desired = desiredTransformPipeline[i];

            // Update config-based transforms (TransformPipeline)
            if (i < configTransforms.Count)
            {
                var configTransform = configTransforms[i];

                // Type mismatch means structural change - skip for now (would require rebuild)
                if (!string.Equals(configTransform.Type, desired.Type, StringComparison.OrdinalIgnoreCase))
                {
                    result.SkippedTransforms.Add($"Config transform at index {i}: type mismatch ({configTransform.Type} vs {desired.Type})");
                    continue;
                }

                // Update enabled state
                configTransform.Enabled = desired.Enabled;

                // Update parameters
                if (desired.Parameters != null)
                {
                    configTransform.Parameters = new Dictionary<string, object>(desired.Parameters);
                }

                result.UpdatedConfigTransforms.Add($"{configTransform.Type}[{i}]");
            }

            // Also update runtime transforms if they exist at this index
            if (i < runtimeTransforms.Count)
            {
                var runtimeTransform = runtimeTransforms[i];
                var parameters = desired.Parameters ?? new Dictionary<string, object>();

                // Try to validate and update parameters if the transform implements IConfigurableTransform
                if (TryGetConfigurableTransform(runtimeTransform, out var validateParams, out var updateParams))
                {
                    var validationResult = validateParams(parameters);
                    if (validationResult.IsError)
                    {
                        return Error.Validation(
                            code: "TransformPipeline.ValidationFailed",
                            description: $"Transform at index {i}: {validationResult.FirstError.Description}");
                    }

                    // Update parameters (preserves internal state where applicable)
                    updateParams(parameters);
                }

                // Update enabled state
                runtimeTransform.Enabled = desired.Enabled;

                result.UpdatedRuntimeTransforms.Add($"{runtimeTransform.Name}[{i}]");
            }
        }

        return result;
    }

    /// <summary>
    /// Applies all DSP and Transform pipeline updates for sensors in a device configuration.
    /// This is a convenience method that combines ApplyDspPipelineUpdates and ApplyTransformPipelineUpdates.
    /// </summary>
    /// <param name="deviceConfig">The device configuration to update</param>
    /// <param name="desiredSensors">The desired sensor configurations from cloud</param>
    /// <returns>Result indicating success or validation errors</returns>
    public static ErrorOr<PipelineUpdateSummary> ApplyAllPipelineUpdates(
        DeviceConfiguration deviceConfig,
        IReadOnlyList<SubNodeSensorReportDto>? desiredSensors)
    {
        var summary = new PipelineUpdateSummary();

        if (desiredSensors == null || desiredSensors.Count == 0)
            return summary;

        foreach (var desiredSensor in desiredSensors)
        {
            // Find matching sensor by name
            var sensor = deviceConfig.Sensors.FirstOrDefault(s =>
                s.Name.Equals(desiredSensor.Name, StringComparison.OrdinalIgnoreCase));

            if (sensor == null)
                continue;

            // Apply DSP pipeline updates
            if (desiredSensor.Config?.DspPipeline != null)
            {
                var dspResult = ApplyDspPipelineUpdates(sensor.Report, desiredSensor.Report.DspPipeline);
                if (dspResult.IsError)
                {
                    return Error.Validation(
                        code: "PipelineUpdate.DspFailed",
                        description: $"Sensor '{sensor.Name}': {dspResult.FirstError.Description}");
                }
                summary.DspResults[sensor.Name] = dspResult.Value;
            }

            // Apply Transform pipeline updates
            if (desiredSensor.Config?.TransformPipeline != null)
            {
                var transformResult = ApplyTransformPipelineUpdates(sensor.Report, desiredSensor.Report.TransformPipeline);
                if (transformResult.IsError)
                {
                    return Error.Validation(
                        code: "PipelineUpdate.TransformFailed",
                        description: $"Sensor '{sensor.Name}': {transformResult.FirstError.Description}");
                }
                summary.TransformResults[sensor.Name] = transformResult.Value;
            }
        }

        return summary;
    }

    /// <summary>
    /// Applies cached cloud configuration to a base DeviceConfiguration.
    /// This method merges the cloud configuration (from cache) with the base configuration
    /// (from appsettings.json), updating only the fields present in the cloud config.
    /// </summary>
    /// <param name="baseConfig">The base device configuration from appsettings.json</param>
    /// <param name="cachedMessage">The cached cloud configuration message</param>
    /// <returns>True if configuration was found and applied, false otherwise</returns>
    public static bool ApplyCachedConfiguration(
        DeviceConfiguration baseConfig,
        SubNodeConfigUpdateMessage cachedMessage)
    {
        if (cachedMessage?.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs == null)
            return false;

        var deviceConfigs = cachedMessage.Data.Cfg.Desired.SubNodeDeviceConfig.DeviceConfigs;

        // Find matching device config by DeviceName (using dictionary key)
        if (!deviceConfigs.TryGetValue(baseConfig.DeviceName, out var matchingConfig))
        {
            return false;
        }

        // Apply sensor configuration updates
        ApplysensorReportUpdates(baseConfig, matchingConfig.Sensors);

        // Apply pipeline updates (Transform and DSP filters)
        ApplyAllPipelineUpdates(baseConfig, matchingConfig.Sensors);

        // Apply background task periods if provided
        if (matchingConfig.Periods != null)
        {
            if (matchingConfig.Periods.ReportHealth > 0)
            {
                baseConfig.Periods.ReportHealth = matchingConfig.Periods.ReportHealth;
            }
            if (matchingConfig.Periods.ReportConfiguration >= 0)
            {
                baseConfig.Periods.ReportConfiguration = matchingConfig.Periods.ReportConfiguration;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the device configuration DTO from a cached cloud message by device name.
    /// </summary>
    /// <param name="cachedMessage">The cached cloud configuration message</param>
    /// <param name="deviceName">The device name to search for</param>
    /// <returns>The matching device configuration DTO, or null if not found</returns>
    public static SubNodeDeviceConfigDto? GetDeviceConfigFromCachedMessage(
        SubNodeConfigUpdateMessage? cachedMessage,
        string deviceName)
    {
        if (cachedMessage?.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs == null)
            return null;

        var deviceConfigs = cachedMessage.Data.Cfg.Desired.SubNodeDeviceConfig.DeviceConfigs;
        if (deviceConfigs == null)
            return null;

        // Use dictionary key for lookup (case-insensitive)
        return deviceConfigs.TryGetValue(deviceName, out var config) ? config : null;
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
            DeviceType = config.SubNodeType.ToString(),
            Dtdl = new SubNodeDtdlConfigDto
            {
                AutoGenEnabled = config.Dtdl.AutoGenEnabled,
                DtdlPath = config.Dtdl.DtdlPath,
                DtdlInterface = config.DtdlInterface
            },
            DeviceCapabilities = new SubNodeDeviceCapabilitiesDto
            {
                Manufacturer = config.Manufacturer,
                Model = config.Model,
                SubNodeSwVersion = config.SwVersion,
                DeviceInfo = config.Metadata
            },
            Communication = config.DeviceCommunication,
            Periods = new SubNodePeriodsDto
            {
                ReportHealth = config.Periods.ReportHealth,
                ReportConfiguration = config.Periods.ReportConfiguration
            },
            Sensors = config.Sensors.Select(s => new SubNodeSensorReportDto
            {
                Name = s.Name,
                Dtmi = s.Dtmi,
                SensorGroup = s.SensorGroup.ToString(),
                Parameters = s.Parameters,
                Config = new SubNodeSensorRuntimeConfigDto
                {
                    Enabled = s.Report.Enabled,
                    Interval = (int)s.Report.Interval,
                    Unit = s.Report.Unit,
                    Thresholds = s.Report.Thresholds != null
                        ? new SubNodeThresholdsDto
                        {
                            UpperCritical = s.Report.Thresholds.UpperCritical,
                            UpperWarning = s.Report.Thresholds.UpperWarning,
                            LowerWarning = s.Report.Thresholds.LowerWarning,
                            LowerCritical = s.Report.Thresholds.LowerCritical
                        }
                        : null
                }
            }).ToList()
        };
    }

    private static SubNodeConfigUpdateMessage CreateReport(
        SubNodeConfigUpdateMessage incomingMessage,
        DeviceConfiguration currentConfig,
        string deviceTypeName,
        string status,
        string? errorMessage)
    {
        var reportedDeviceConfig = ToSubNodeDeviceConfigDto(currentConfig);

        return new SubNodeConfigUpdateMessage
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
                        DeviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>
                        {
                            [deviceTypeName] = reportedDeviceConfig
                        },
                        Status = status,
                        ErrorMessage = errorMessage,
                        LastUpdateTime = DateTimeOffset.UtcNow
                    }
                }
            }
        };
    }

    /// <summary>
    /// Validates Transform and DSP Filter pipeline parameters without applying updates.
    /// This method is called during validation phase to detect invalid parameters early.
    /// </summary>
    /// <param name="currentConfig">The current device configuration containing runtime instances</param>
    /// <param name="desiredSensors">The desired sensor configurations from cloud</param>
    /// <returns>Validation result indicating success or failure with error message</returns>
    private static ConfigurationValidationResult ValidatePipelineParameters(
        DeviceConfiguration currentConfig,
        IReadOnlyList<SubNodeSensorReportDto> desiredSensors)
    {
        foreach (var desiredSensor in desiredSensors)
        {
            // Find matching sensor by name
            var sensor = currentConfig.Sensors.FirstOrDefault(s =>
                s.Name.Equals(desiredSensor.Name, StringComparison.OrdinalIgnoreCase));

            if (sensor == null)
                continue;

            // Validate DSP pipeline parameters
            if (desiredSensor.Config?.DspPipeline != null)
            {
                var dspValidationResult = ValidateDspPipelineParameters(
                    sensor.Name,
                    sensor.Report.RuntimeDspFilters,
                    sensor.Report.DspPipeline,
                    desiredSensor.Report.DspPipeline);

                if (!dspValidationResult.IsValid)
                    return dspValidationResult;
            }

            // Validate Transform pipeline parameters
            if (desiredSensor.Config?.TransformPipeline != null)
            {
                var transformValidationResult = ValidateTransformPipelineParameters(
                    sensor.Name,
                    sensor.Report.RuntimeTransforms,
                    sensor.Report.TransformPipeline,
                    desiredSensor.Report.TransformPipeline);

                if (!transformValidationResult.IsValid)
                    return transformValidationResult;
            }
        }

        return ConfigurationValidationResult.Success;
    }

    /// <summary>
    /// Validates DSP filter pipeline parameters without applying updates.
    /// </summary>
    private static ConfigurationValidationResult ValidateDspPipelineParameters(
        string sensorName,
        List<IDspFilter> runtimeFilters,
        List<DspFilterConfig> configFilters,
        IReadOnlyList<SubNodeDspFilterConfigDto> desiredPipeline)
    {
        for (int i = 0; i < desiredPipeline.Count; i++)
        {
            var desired = desiredPipeline[i];

            // Check config-based filter type match
            if (i < configFilters.Count)
            {
                var configFilter = configFilters[i];
                if (!string.Equals(configFilter.Type, desired.Type, StringComparison.OrdinalIgnoreCase))
                {
                    return ConfigurationValidationResult.Failure(
                        $"Sensor '{sensorName}': DSP filter at index {i} type mismatch " +
                        $"(expected '{configFilter.Type}', got '{desired.Type}')");
                }
            }

            // Validate runtime filter parameters if exists
            if (i < runtimeFilters.Count)
            {
                var runtimeFilter = runtimeFilters[i];
                var parameters = desired.Parameters ?? new Dictionary<string, object>();

                if (TryGetConfigurableDspFilter(runtimeFilter, out var validateParams, out _))
                {
                    var validationResult = validateParams(parameters);
                    if (validationResult.IsError)
                    {
                        return ConfigurationValidationResult.Failure(
                            $"Sensor '{sensorName}': DSP filter '{desired.Type}' at index {i}: " +
                            $"{validationResult.FirstError.Description}");
                    }
                }
            }
        }

        return ConfigurationValidationResult.Success;
    }

    /// <summary>
    /// Validates Transform pipeline parameters without applying updates.
    /// </summary>
    private static ConfigurationValidationResult ValidateTransformPipelineParameters(
        string sensorName,
        List<ITelemetryTransform> runtimeTransforms,
        List<TransformConfig> configTransforms,
        IReadOnlyList<SubNodeTransformConfigDto> desiredPipeline)
    {
        for (int i = 0; i < desiredPipeline.Count; i++)
        {
            var desired = desiredPipeline[i];

            // Check config-based transform type match
            if (i < configTransforms.Count)
            {
                var configTransform = configTransforms[i];
                if (!string.Equals(configTransform.Type, desired.Type, StringComparison.OrdinalIgnoreCase))
                {
                    return ConfigurationValidationResult.Failure(
                        $"Sensor '{sensorName}': Transform at index {i} type mismatch " +
                        $"(expected '{configTransform.Type}', got '{desired.Type}')");
                }
            }

            // Validate runtime transform parameters if exists
            if (i < runtimeTransforms.Count)
            {
                var runtimeTransform = runtimeTransforms[i];
                var parameters = desired.Parameters ?? new Dictionary<string, object>();

                if (TryGetConfigurableTransform(runtimeTransform, out var validateParams, out _))
                {
                    var validationResult = validateParams(parameters);
                    if (validationResult.IsError)
                    {
                        return ConfigurationValidationResult.Failure(
                            $"Sensor '{sensorName}': Transform '{desired.Type}' at index {i}: " +
                            $"{validationResult.FirstError.Description}");
                    }
                }
            }
        }

        return ConfigurationValidationResult.Success;
    }

    /// <summary>
    /// Tries to get ValidateParameters and UpdateParameters methods from a DSP filter
    /// that implements IConfigurableDspFilter.
    /// </summary>
    private static bool TryGetConfigurableDspFilter(
        IDspFilter filter,
        out Func<Dictionary<string, object>, ErrorOr<Success>> validateParams,
        out Action<Dictionary<string, object>> updateParams)
    {
        // Check if the filter type implements IConfigurableDspFilter<T>
        var filterType = filter.GetType();
        var configurableInterface = filterType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IConfigurableDspFilter<>));

        if (configurableInterface != null)
        {
            // Get the methods via reflection
            var validateMethod = filterType.GetMethod("ValidateParameters",
                [typeof(Dictionary<string, object>)]);
            var updateMethod = filterType.GetMethod("UpdateParameters",
                [typeof(Dictionary<string, object>)]);

            if (validateMethod != null && updateMethod != null)
            {
                validateParams = parameters =>
                    (ErrorOr<Success>)validateMethod.Invoke(filter, [parameters])!;
                updateParams = parameters =>
                    updateMethod.Invoke(filter, [parameters]);
                return true;
            }
        }

        validateParams = null!;
        updateParams = null!;
        return false;
    }

    /// <summary>
    /// Tries to get ValidateParameters and UpdateParameters methods from a transform
    /// that implements IConfigurableTransform.
    /// </summary>
    private static bool TryGetConfigurableTransform(
        ITelemetryTransform transform,
        out Func<Dictionary<string, object>, ErrorOr<Success>> validateParams,
        out Action<Dictionary<string, object>> updateParams)
    {
        // Check if the transform type implements IConfigurableTransform<T>
        var transformType = transform.GetType();
        var configurableInterface = transformType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IConfigurableTransform<>));

        if (configurableInterface != null)
        {
            // Get the methods via reflection
            var validateMethod = transformType.GetMethod("ValidateParameters",
                [typeof(Dictionary<string, object>)]);
            var updateMethod = transformType.GetMethod("UpdateParameters",
                [typeof(Dictionary<string, object>)]);

            if (validateMethod != null && updateMethod != null)
            {
                validateParams = parameters =>
                    (ErrorOr<Success>)validateMethod.Invoke(transform, [parameters])!;
                updateParams = parameters =>
                    updateMethod.Invoke(transform, [parameters]);
                return true;
            }
        }

        validateParams = null!;
        updateParams = null!;
        return false;
    }
}

/// <summary>
/// Backup snapshot of device configuration for rollback purposes.
/// </summary>
public class DeviceConfigurationBackup
{
    public int ReportHealthPeriod { get; set; }
    public int ReportConfigurationPeriod { get; set; }
    public List<sensorReportBackup> SensorBackups { get; set; } = [];
}

/// <summary>
/// Backup snapshot of sensor configuration.
/// </summary>
public class sensorReportBackup
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public double Interval { get; set; }
    public string? Unit { get; set; }
    public ThresholdConfig? Thresholds { get; set; }
}

/// <summary>
/// Result of applying DSP pipeline updates.
/// </summary>
public class DspPipelineUpdateResult
{
    /// <summary>
    /// List of config-based filters that were updated.
    /// </summary>
    public List<string> UpdatedConfigFilters { get; } = [];

    /// <summary>
    /// List of runtime filters that were updated.
    /// </summary>
    public List<string> UpdatedRuntimeFilters { get; } = [];

    /// <summary>
    /// List of filters that were skipped (e.g., due to type mismatch).
    /// </summary>
    public List<string> SkippedFilters { get; } = [];

    /// <summary>
    /// Total count of updated filters.
    /// </summary>
    public int TotalUpdated => UpdatedConfigFilters.Count + UpdatedRuntimeFilters.Count;
}

/// <summary>
/// Result of applying transform pipeline updates.
/// </summary>
public class TransformPipelineUpdateResult
{
    /// <summary>
    /// List of config-based transforms that were updated.
    /// </summary>
    public List<string> UpdatedConfigTransforms { get; } = [];

    /// <summary>
    /// List of runtime transforms that were updated.
    /// </summary>
    public List<string> UpdatedRuntimeTransforms { get; } = [];

    /// <summary>
    /// List of transforms that were skipped (e.g., due to type mismatch).
    /// </summary>
    public List<string> SkippedTransforms { get; } = [];

    /// <summary>
    /// Total count of updated transforms.
    /// </summary>
    public int TotalUpdated => UpdatedConfigTransforms.Count + UpdatedRuntimeTransforms.Count;
}

/// <summary>
/// Summary of all pipeline updates applied to a device configuration.
/// </summary>
public class PipelineUpdateSummary
{
    /// <summary>
    /// DSP pipeline update results by sensor name.
    /// </summary>
    public Dictionary<string, DspPipelineUpdateResult> DspResults { get; } = [];

    /// <summary>
    /// Transform pipeline update results by sensor name.
    /// </summary>
    public Dictionary<string, TransformPipelineUpdateResult> TransformResults { get; } = [];

    /// <summary>
    /// Total count of sensors that had DSP updates.
    /// </summary>
    public int TotalDspSensorsUpdated => DspResults.Count;

    /// <summary>
    /// Total count of sensors that had transform updates.
    /// </summary>
    public int TotalTransformSensorsUpdated => TransformResults.Count;
}

// ===== System Config and Custom Config Report Helpers =====

public static partial class ConfigurationUpdateHelper
{
    /// <summary>
    /// Creates a configuration report for system-config updates.
    /// </summary>
    /// <param name="originalMessage">The original cloud message</param>
    /// <param name="deviceTypeName">Device type name</param>
    /// <param name="status">Update status</param>
    /// <param name="errorMessage">Error message if failed</param>
    /// <returns>Configuration report message</returns>
    public static SubNodeConfigUpdateMessage CreateSystemConfigReport(
        SubNodeConfigUpdateMessage originalMessage,
        string deviceTypeName,
        string status,
        string? errorMessage)
    {
        return new SubNodeConfigUpdateMessage
        {
            DeviceId = originalMessage.DeviceId,
            GroupId = originalMessage.GroupId,
            Cmd = "configResponse",
            SeqId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ReqSeqId = originalMessage.SeqId.ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    // Echo back the desired state
                    Desired = originalMessage.Data?.Cfg?.Desired,
                    // Report current system config status
                    Reported = new SubNodeReportedConfig
                    {
                        SystemConfig = originalMessage.Data?.Cfg?.Desired?.SystemConfig,
                        Status = status,
                        ErrorMessage = errorMessage,
                        LastUpdateTime = DateTimeOffset.UtcNow
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates a configuration report for custom-config updates.
    /// </summary>
    /// <param name="originalMessage">The original cloud message</param>
    /// <param name="deviceTypeName">Device type name</param>
    /// <param name="status">Update status</param>
    /// <param name="errorMessage">Error message if failed</param>
    /// <returns>Configuration report message</returns>
    public static SubNodeConfigUpdateMessage CreateCustomConfigReport(
        SubNodeConfigUpdateMessage originalMessage,
        string deviceTypeName,
        string status,
        string? errorMessage)
    {
        return new SubNodeConfigUpdateMessage
        {
            DeviceId = originalMessage.DeviceId,
            GroupId = originalMessage.GroupId,
            Cmd = "configResponse",
            SeqId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ReqSeqId = originalMessage.SeqId.ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    // Echo back the desired state
                    Desired = originalMessage.Data?.Cfg?.Desired,
                    // Report current custom config status
                    Reported = new SubNodeReportedConfig
                    {
                        CustomConfig = originalMessage.Data?.Cfg?.Desired?.CustomConfig,
                        Status = status,
                        ErrorMessage = errorMessage,
                        LastUpdateTime = DateTimeOffset.UtcNow
                    }
                }
            }
        };
    }

    public static bool HasDtmiDelta(DeviceConfiguration currentCfg, IReadOnlyList<SubNodeSensorReportDto> desiredSensors)
    {
        if (desiredSensors == null || desiredSensors.Count == 0)
            return false;

        var currentDtmis = currentCfg.Sensors
            .ToDictionary(s => s.Name, s => s.Dtmi, StringComparer.OrdinalIgnoreCase);

        foreach (var desiredSensor in desiredSensors)
        {
            // Case 1: New sensor (not in current config)
            if (!currentDtmis.TryGetValue(desiredSensor.Name, out var currentDtmi))
            {
                return true;
            }

            // Case 2: DTMI changed
            if (!string.IsNullOrEmpty(desiredSensor.Dtmi) && 
                !string.Equals(currentDtmi, desiredSensor.Dtmi, StringComparison.Ordinal))
            {
                return true;
            }
        }    

        return false;    
    }
}
