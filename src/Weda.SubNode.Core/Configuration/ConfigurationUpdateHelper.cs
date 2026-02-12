using System.Text.Json;
using ErrorOr;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Configuration.Results;
using Weda.SubNode.Core.Configuration.Validators;
using Weda.SubNode.Core.Configuration.Validators.Device;
using Weda.SubNode.Core.Utilities;

namespace Weda.SubNode.Core.Configuration;

/// <summary>
/// Helper class for handling configuration updates from cloud.
/// Provides validation, mapping, and response building functionality.
/// </summary>
public static partial class ConfigurationUpdateHelper
{
    /// <summary>
    /// Thread-safe sequence counter for updateCmdResponse messages.
    /// </summary>
    private static long _updateCmdResponseSeqId;

    /// <summary>
    /// Thread-safe sequence counter for configReport messages.
    /// </summary>
    private static long _configReportSeqId;

    /// <summary>
    /// Gets the next sequence ID for updateCmdResponse messages.
    /// </summary>
    public static long GetNextUpdateCmdResponseSeqId() => Interlocked.Increment(ref _updateCmdResponseSeqId);

    /// <summary>
    /// Gets the next sequence ID for configReport messages.
    /// </summary>
    public static long GetNextConfigReportSeqId() => Interlocked.Increment(ref _configReportSeqId);

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
    /// Lazy-initialized validator registry for configuration properties.
    /// </summary>
    private static readonly Lazy<ConfigurationValidatorRegistry> _validatorRegistry =
        new(() => ConfigurationValidatorRegistry.CreateDefault());

    /// <summary>
    /// Gets the default validator registry.
    /// </summary>
    public static ConfigurationValidatorRegistry ValidatorRegistry => _validatorRegistry.Value;

    /// <summary>
    /// Validates the configuration update for a specific device.
    /// Uses the validator registry to validate each property.
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
        var deviceConfigs = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs;
        if (deviceConfigs == null)
            return ConfigurationValidationResult.Failure("No device configurations in desired state");

        // Try to find matching device config by DeviceName (using dictionary key)
        if (!deviceConfigs.TryGetValue(currentConfig.DeviceName, out var desiredConfig))
        {
            return ConfigurationValidationResult.Failure(
                $"No matching configuration found for device '{currentConfig.DeviceName}'");
        }

        // Create validation context
        var context = new ConfigurationValidationContext
        {
            CurrentConfig = currentConfig,
            DesiredConfig = desiredConfig,
            Options = options
        };

        // Validate all properties using the validator registry
        // (includes Pipeline validation via PipelineValidator)
        return ValidatorRegistry.ValidateAll(context);
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
            SensorBackups = config.Sensors.Select(s => new SensorReportBackup
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

            var wasUpdated = false;

            // Apply config updates (PATCH semantics - only update provided fields)
            if (desiredSensor.Config != null)
            {
                sensor.Report.Enabled = desiredSensor.Report?.Enabled ?? sensor.Report.Enabled;
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

                wasUpdated = true;
            }

            // Apply Dtmi update if provided and different
            if (!string.IsNullOrEmpty(desiredSensor.Dtmi) &&
                !string.Equals(sensor.Dtmi, desiredSensor.Dtmi, StringComparison.Ordinal))
            {
                sensor.Dtmi = desiredSensor.Dtmi;
                wasUpdated = true;
            }

            if (wasUpdated)
            {
                updatedSensors.Add(sensor.Name);
            }
        }

        return updatedSensors;
    }

    public static List<string> ApplyNewSensors(
        DeviceConfiguration deviceConfig,
        IReadOnlyList<SubNodeSensorReportDto>? desiredSensors,
        string deviceResourceId)
    {
        var addedSensors = new List<string>();

        if (desiredSensors == null || desiredSensors.Count == 0)
            return addedSensors;

        var existingSensorNames = deviceConfig.Sensors
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var desiredSensor in desiredSensors)
        {
            if (existingSensorNames.Contains(desiredSensor!.Name))
                continue;

            var newSensor = MapToSensor(
                desiredSensor,
                deviceConfig.DeviceId ?? deviceResourceId,
                deviceConfig.DeviceName);
            deviceConfig.Sensors.Add(newSensor);
            addedSensors.Add(newSensor.Name);
        }

        // Invalidate sensor lookup cache after adding sensors
        if (addedSensors.Count > 0)
        {
            deviceConfig.InvalidateSensorLookup();
        }

        return addedSensors;
    }

    /// <summary>
    /// Replaces the sensor list with the desired sensors (REPLACE mode).
    /// This is a complete replacement - desired sensors become the new sensor list.
    /// Sensors not in desired list are removed, new sensors are added, existing sensors are updated.
    /// </summary>
    /// <param name="deviceConfig">The device configuration to update</param>
    /// <param name="desiredSensors">The desired sensor configurations (the new complete list)</param>
    /// <param name="deviceResourceId">Device resource ID for new sensors</param>
    /// <returns>Result containing lists of added, removed, and updated sensors</returns>
    public static SensorReplacementResult ReplaceSensors(
        DeviceConfiguration deviceConfig,
        IReadOnlyList<SubNodeSensorReportDto>? desiredSensors,
        string deviceResourceId)
    {
        var result = new SensorReplacementResult();

        if (desiredSensors == null)
            return result;

        var currentSensorNames = deviceConfig.Sensors
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var desiredSensorNames = desiredSensors
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Find sensors to remove (in current but not in desired)
        var sensorsToRemove = currentSensorNames
            .Except(desiredSensorNames, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Remove sensors not in desired list
        if (sensorsToRemove.Count > 0)
        {
            deviceConfig.Sensors.RemoveAll(s =>
                sensorsToRemove.Contains(s.Name, StringComparer.OrdinalIgnoreCase));
            result.RemovedSensors.AddRange(sensorsToRemove);
        }

        // Process each desired sensor
        foreach (var desiredSensor in desiredSensors)
        {
            var existingSensor = deviceConfig.Sensors.FirstOrDefault(s =>
                s.Name.Equals(desiredSensor.Name, StringComparison.OrdinalIgnoreCase));

            if (existingSensor == null)
            {
                // New sensor - add it
                var newSensor = MapToSensor(
                    desiredSensor,
                    deviceConfig.DeviceId ?? deviceResourceId,
                    deviceConfig.DeviceName);
                deviceConfig.Sensors.Add(newSensor);
                result.AddedSensors.Add(newSensor.Name);
            }
            else
            {
                // Existing sensor - update it
                var wasUpdated = UpdateExistingSensor(existingSensor, desiredSensor);
                if (wasUpdated)
                {
                    result.UpdatedSensors.Add(existingSensor.Name);
                }
            }
        }

        // Invalidate sensor lookup cache only when new sensors are added
        // - Removed sensors won't be queried (no new data from removed sensors)
        // - Updated sensors don't need invalidation (ResourceId unchanged, same object reference)
        if (result.AddedSensors.Count > 0)
        {
            deviceConfig.InvalidateSensorLookup();
        }

        return result;
    }

    /// <summary>
    /// Updates an existing sensor with values from desired sensor DTO.
    /// </summary>
    private static bool UpdateExistingSensor(Sensor sensor, SubNodeSensorReportDto desired)
    {
        var wasUpdated = false;

        // Update Dtmi if provided and different
        if (!string.IsNullOrEmpty(desired.Dtmi) &&
            !string.Equals(sensor.Dtmi, desired.Dtmi, StringComparison.Ordinal))
        {
            sensor.Dtmi = desired.Dtmi;
            wasUpdated = true;
        }

        // Update Report config if provided
        if (desired.Config != null)
        {
            sensor.Report.Enabled = desired.Report?.Enabled ?? sensor.Report.Enabled;
            sensor.Report.Interval = desired.Config.Interval;

            if (!string.IsNullOrEmpty(desired.Config.Unit))
            {
                sensor.Report.Unit = desired.Config.Unit;
            }

            if (desired.Config.Thresholds != null)
            {
                sensor.Report.Thresholds = new ThresholdConfig
                {
                    UpperCritical = desired.Config.Thresholds.UpperCritical,
                    UpperWarning = desired.Config.Thresholds.UpperWarning,
                    LowerWarning = desired.Config.Thresholds.LowerWarning,
                    LowerCritical = desired.Config.Thresholds.LowerCritical
                };
            }

            wasUpdated = true;
        }

        // Update Record config if provided
        if (desired.Record != null)
        {
            sensor.Record.Enabled = desired.Record.Enabled;
            sensor.Record.Interval = desired.Record.Interval;
            wasUpdated = true;
        }

        if (desired.Report != null)
        {
            sensor.Report.TransformPipeline = desired.Report.TransformPipeline?
                .Select(t => new TransformConfig
                {
                    Type = t.Type,
                    Enabled = t.Enabled,
                    Parameters = t.Parameters != null
                        ? new Dictionary<string, object>(t.Parameters)
                        : []
                }).ToList() ?? [];
        
            sensor.Report.DspPipeline = desired.Report.DspPipeline?
                .Select(t => new DspFilterConfig
                {
                    Type = t.Type,
                    Enabled = t.Enabled,
                    Parameters = t.Parameters != null
                        ? new Dictionary<string, object>(t.Parameters)
                        : []
                }).ToList() ?? [];
            
            wasUpdated = true;
        }

        return wasUpdated;
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
                if (PipelineValidator.TryGetConfigurableDspFilter(runtimeFilter, out var validateParams, out var updateParams))
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
                if (PipelineValidator.TryGetConfigurableTransform(runtimeTransform, out var validateParams, out var updateParams))
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
            if (desiredSensor.Config?.DspPipeline != null && desiredSensor.Report?.DspPipeline != null)
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
            if (desiredSensor.Config?.TransformPipeline != null && desiredSensor.Report?.TransformPipeline != null)
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
        var deviceConfigs = cachedMessage?.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs;
        if (deviceConfigs == null)
            return false;

        // Find matching device config by DeviceName (using dictionary key)
        if (!deviceConfigs.TryGetValue(baseConfig.DeviceName, out var matchingConfig) || matchingConfig == null)
        {
            return false;
        }

        // Apply sensor replacement (REPLACE mode - supports add/remove/update)
        ReplaceSensors(baseConfig, matchingConfig.Sensors, baseConfig.DeviceId ?? baseConfig.DeviceName);

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
        var reported = new SubNodeReportedConfig
        {
            SystemConfig = originalMessage.Data?.Cfg?.Desired?.SystemConfig,
            DeviceCfg = new SubNodeDeviceCfgDto
            {
                Message = new ConfigUpdateMessageDto
                {
                    Status = status,
                    ErrorMessage = errorMessage,
                    LastUpdateTime = DateTimeOffset.UtcNow
                }
            }
        };

        return new SubNodeConfigUpdateMessage
        {
            ProtoVer = originalMessage.ProtoVer,
            DeviceId = originalMessage.DeviceId,
            GroupId = originalMessage.GroupId,
            Cmd = "configResponse",
            SeqId = GetNextUpdateCmdResponseSeqId(),
            ReqSeqId = originalMessage.ReqSeqId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    Desired = originalMessage.Data?.Cfg?.Desired,
                    Reported = reported
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
        var reported = new SubNodeReportedConfig
        {
            CustomConfig = originalMessage.Data?.Cfg?.Desired?.CustomConfig,
            DeviceCfg = new SubNodeDeviceCfgDto
            {
                Message = new ConfigUpdateMessageDto
                {
                    Status = status,
                    ErrorMessage = errorMessage,
                    LastUpdateTime = DateTimeOffset.UtcNow
                }
            }
        };

        return new SubNodeConfigUpdateMessage
        {
            ProtoVer = originalMessage.ProtoVer,
            DeviceId = originalMessage.DeviceId,
            GroupId = originalMessage.GroupId,
            Cmd = "configResponse",
            SeqId = GetNextUpdateCmdResponseSeqId(),
            ReqSeqId = originalMessage.SeqId.ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    Desired = originalMessage.Data?.Cfg?.Desired,
                    Reported = reported
                }
            }
        };
    }

    /// <summary>
    /// Detects if there are new sensor or new DTMIs that require re-uploading DeviceCaps.
    /// A delta exists when:
    /// 1. A new sensor is added (not in current devicecfg)
    /// 2. An existing sensor's DTMI is changed
    /// </summary>
    /// <param name="currentCfg">Current device configuration</param>
    /// <param name="desiredSensors">Desired sensor configurations from cloud</param>
    /// <returns>True if DeviceCaps needs to be re-uploaded</returns>
    public static bool HasDtmiDelta(DeviceConfiguration currentCfg, IReadOnlyList<SubNodeSensorReportDto>? desiredSensors)
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

    public static List<(string SensorName, string? OldDtmi, string? NewDtmi)> GetDtmiChanges(
        DeviceConfiguration currentCfg, IReadOnlyList<SubNodeSensorReportDto>? desiredSensors)
    {
        var changes = new List<(string SensorName, string? OldDtmi, string? NewDtmi)>();

        if (desiredSensors == null || desiredSensors.Count == 0)
            return changes;

        var currentDtmis = currentCfg.Sensors
            .ToDictionary(s => s.Name, s => s.Dtmi, StringComparer.OrdinalIgnoreCase);

        foreach (var desiredSensor in desiredSensors)
        {
            // Case 1: New sensor (not in current config)
            if (!currentDtmis.TryGetValue(desiredSensor.Name, out var currentDtmi))
            {
                changes.Add((desiredSensor.Name, null, desiredSensor.Dtmi));
            } 
            else if (!string.IsNullOrEmpty(desiredSensor.Dtmi) && 
                     !string.Equals(currentDtmi, desiredSensor.Dtmi, StringComparison.Ordinal))
            {
                changes.Add((desiredSensor.Name, currentDtmi, desiredSensor.Dtmi));
            }
        }    

        return changes;
    }

    private static Sensor MapToSensor(SubNodeSensorReportDto dto, string subNodeDeviceId, string deviceName)
    {
        // Parse SensorGroup with fallback to AI if null or invalid
        var sensorGroup = SensorGroup.AI;
        if (!string.IsNullOrEmpty(dto.SensorGroup))
        {
            if (!Enum.TryParse(dto.SensorGroup, true, out sensorGroup))
            {
                sensorGroup = SensorGroup.AI; // Default to AI if parsing fails
            }
        }

        // Generate ResourceId using SubNode architecture
        var resourceId = ResourceIdGenerator.GenerateSensorResourceId(
            subNodeDeviceId,
            deviceName,
            dto.Name,
            groupId: "weda");

        // Generate Dtmi if not provided
        var dtmi = dto.Dtmi;
        if (string.IsNullOrEmpty(dtmi))
        {
            dtmi = DtdlGenerator.GenerateDtmi(dto.Name, sensorGroup.ToString());
        }

        var sensor = new Sensor
        {
            Name = dto.Name,
            ResourceId = resourceId,
            Dtmi = dtmi,
            SensorGroup = sensorGroup,
            Parameters = ConvertJsonElementsToNativeTypes(dto.Parameters),
            Metadata = ConvertJsonElementsToNativeTypes(dto.Metadata),
            DeviceResourceId = subNodeDeviceId,
            SensorInfo = new SensorInfo
            {
                Schema = dto.SensorInfo?.Schema ?? sensorGroup.GetDefaultSchema(),
                DisplayName = dto.SensorInfo?.DisplayName,
                Description = dto.SensorInfo?.Description
            }
        };

        if (dto.Report != null)
        {
            sensor.Report.Enabled = dto.Report.Enabled;
            sensor.Report.Interval = dto.Report.Interval;
            sensor.Report.Unit = dto.Report.Unit;

            sensor.Report.Thresholds = new ThresholdConfig
            {
                UpperCritical = dto.Report.Thresholds?.UpperCritical,
                UpperWarning = dto.Report.Thresholds?.UpperWarning,
                LowerWarning = dto.Report.Thresholds?.LowerWarning,
                LowerCritical = dto.Report.Thresholds?.LowerCritical
            };

            // Map TransformPipeline
            if (dto.Report.TransformPipeline != null)
            {
                sensor.Report.TransformPipeline = dto.Report.TransformPipeline
                    .Select(t => new TransformConfig
                    {
                        Type = t.Type,
                        Enabled = t.Enabled,
                        Parameters = t.Parameters != null
                            ? new Dictionary<string, object>(t.Parameters)
                            : []
                    })
                    .ToList();
            }

            // Map DspPipeline
            if (dto.Report.DspPipeline != null)
            {
                sensor.Report.DspPipeline = dto.Report.DspPipeline
                    .Select(d => new DspFilterConfig
                    {
                        Type = d.Type,
                        Enabled = d.Enabled,
                        Parameters = d.Parameters != null
                            ? new Dictionary<string, object>(d.Parameters)
                            : []
                    })
                    .ToList();
            }
        }

        // Map Record config
        if (dto.Record != null)
        {
            sensor.Record.Enabled = dto.Record.Enabled;
            sensor.Record.Interval = dto.Record.Interval;
        }

        return sensor;
    }

    /// <summary>
    /// Converts JsonElement values in a dictionary to their native .NET types.
    /// This is needed because JSON deserialization may leave values as JsonElement.
    /// </summary>
    private static Dictionary<string, object>? ConvertJsonElementsToNativeTypes(Dictionary<string, object>? source)
    {
        if (source == null)
            return null;

        var result = new Dictionary<string, object>(source.Count);
        foreach (var kvp in source)
        {
            result[kvp.Key] = ConvertJsonElement(kvp.Value);
        }
        return result;
    }

    /// <summary>
    /// Converts a single value from JsonElement to native type if needed.
    /// </summary>
    private static object ConvertJsonElement(object value)
    {
        if (value is not JsonElement element)
            return value;

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l :
                                   element.TryGetDouble(out var d) ? d : element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(e => ConvertJsonElement(e))
                .ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => value
        };
    }

    /// <summary>
    /// Creates an aggregated configuration report containing all device configurations.
    /// Used by SubNodeManager to publish a single report for transaction-based updates.
    /// Uses RawDeviceCfgJson from each device to preserve original JSON structure.
    /// </summary>
    /// <param name="incomingMessage">The original cloud message</param>
    /// <param name="deviceRegistry">Device registry to get current configurations</param>
    /// <param name="validationResults">Validation results for each device</param>
    /// <param name="applyResults">Apply results for each device (null if validation failed)</param>
    /// <param name="overallStatus">Overall transaction status</param>
    /// <returns>Aggregated configuration report message</returns>
    public static SubNodeConfigUpdateMessage CreateAggregatedReport(
        SubNodeConfigUpdateMessage incomingMessage,
        IDeviceRegistry deviceRegistry,
        Dictionary<string, ConfigUpdateValidationResult> validationResults,
        Dictionary<string, ConfigUpdateResult>? applyResults,
        string overallStatus)
    {
        string? errorMessage = null;

        // Capture first error message
        foreach (var (deviceName, validationResult) in validationResults)
        {
            if (errorMessage != null) break;

            if (!validationResult.IsValid)
            {
                errorMessage = $"Device '{deviceName}': {validationResult.ErrorMessage}";
            }
            else if (applyResults?.TryGetValue(deviceName, out var applyResult) == true &&
                     applyResult.Status == DeviceConfigUpdateStatus.Failed)
            {
                errorMessage = $"Device '{deviceName}': {applyResult.ErrorMessage}";
            }
        }

        // Build raw devicecfg JSON from all devices' RawDeviceCfgJson
        var rawDeviceCfg = BuildRawDeviceCfgFromDevices(deviceRegistry, incomingMessage);

        // Create message with status info
        var message = new ConfigUpdateMessageDto
        {
            Status = overallStatus,
            ErrorMessage = errorMessage,
            LastUpdateTime = DateTimeOffset.UtcNow
        };

        // Build reported config
        var reported = new SubNodeReportedConfig();

        if (rawDeviceCfg.HasValue)
        {
            reported.SetRawDeviceCfgWithMessage(rawDeviceCfg.Value, message);
        }
        else
        {
            // Fallback: build DeviceCfg from device configurations if no raw JSON available
            var deviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var device in deviceRegistry.GetAllDevices())
            {
                var dto = ToSubNodeDeviceConfigDto(device.Configuration);
                deviceConfigs[device.Configuration.DeviceName] = dto;
            }

            reported.DeviceCfg = new SubNodeDeviceCfgDto
            {
                Message = message,
                DeviceConfigs = deviceConfigs
            };
        }

        return new SubNodeConfigUpdateMessage
        {
            ProtoVer = incomingMessage.ProtoVer,
            DeviceId = incomingMessage.DeviceId,
            GroupId = incomingMessage.GroupId,
            Cmd = "updateCmdResponse",
            SeqId = GetNextUpdateCmdResponseSeqId(),
            ReqSeqId = incomingMessage.SeqId.ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    Desired = incomingMessage.Data?.Cfg?.Desired,
                    Reported = reported
                }
            }
        };
    }

    /// <summary>
    /// Creates a periodic aggregated configuration report for all devices.
    /// Used by SubNodeManager to periodically sync all device configurations to cloud.
    /// Preserves the last config update status to avoid overwriting error messages.
    /// </summary>
    /// <param name="subNodeId">SubNode ID for the report</param>
    /// <param name="groupId">Group ID for multi-tenant scenarios</param>
    /// <param name="deviceRegistry">Device registry containing all devices</param>
    /// <param name="lastStatus">Last config update status to preserve</param>
    /// <param name="lastErrorMessage">Last error message to preserve (null if success)</param>
    /// <param name="protoVer">Protocol version (default: eco1j)</param>
    public static SubNodeConfigUpdateMessage CreatePeriodicAggregatedReport(
        string subNodeId,
        IDeviceRegistry deviceRegistry,
        string lastStatus,
        string? lastErrorMessage,
        string groupId = "weda",
        string protoVer = "eco1j")
    {
        var message = new ConfigUpdateMessageDto
        {
            Status = lastStatus,
            ErrorMessage = lastErrorMessage,
            LastUpdateTime = DateTimeOffset.UtcNow
        };

        var reported = new SubNodeReportedConfig();

        // Try to get raw JSON from first device that has it
        JsonElement? baseRawJson = null;
        foreach (var device in deviceRegistry.GetAllDevices())
        {
            if (device.Configuration.RawDeviceCfgJson.HasValue)
            {
                baseRawJson = device.Configuration.RawDeviceCfgJson;
                break;
            }
        }

        if (baseRawJson.HasValue)
        {
            reported.SetRawDeviceCfgWithMessage(baseRawJson.Value, message);
        }
        else
        {
            var deviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var device in deviceRegistry.GetAllDevices())
            {
                var dto = ToSubNodeDeviceConfigDto(device.Configuration);
                deviceConfigs[device.Configuration.DeviceName] = dto;
            }

            reported.DeviceCfg = new SubNodeDeviceCfgDto
            {
                Message = message,
                DeviceConfigs = deviceConfigs
            };
        }

        return new SubNodeConfigUpdateMessage
        {
            ProtoVer = protoVer,
            DeviceId = subNodeId,
            GroupId = groupId,
            Cmd = "configReport",
            SeqId = GetNextConfigReportSeqId(),
            ReqSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    Desired = null,
                    Reported = reported
                }
            }
        };
    }

    /// <summary>
    /// Builds raw devicecfg JSON from all devices' RawDeviceCfgJson.
    /// Merges SubNode info and DeviceConfigs from each device.
    /// </summary>
    private static JsonElement? BuildRawDeviceCfgFromDevices(
        IDeviceRegistry deviceRegistry,
        SubNodeConfigUpdateMessage incomingMessage)
    {
        // Try to get raw devicecfg from incoming message's desired state first
        // This ensures we report back what was requested
        var desiredDeviceCfg = incomingMessage.Data?.Cfg?.Desired?.DeviceCfg;

        // Get first device's RawDeviceCfgJson as base (contains SubNode info)
        JsonElement? baseRawJson = null;
        foreach (var device in deviceRegistry.GetAllDevices())
        {
            if (device.Configuration.RawDeviceCfgJson.HasValue)
            {
                baseRawJson = device.Configuration.RawDeviceCfgJson;
                break;
            }
        }

        if (!baseRawJson.HasValue)
        {
            return null;
        }

        // If we have desired config, merge it with base (update DeviceConfigs)
        // Otherwise just return the base
        return baseRawJson;
    }
}
