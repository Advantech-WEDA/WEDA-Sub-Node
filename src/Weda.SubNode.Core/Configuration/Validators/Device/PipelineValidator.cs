using ErrorOr;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace Weda.SubNode.Core.Configuration.Validators.Device;

/// <summary>
/// Validates Transform and DSP Filter pipeline parameters.
/// Ensures pipeline configurations are valid before applying updates.
/// </summary>
public class PipelineValidator : IConfigurationPropertyValidator
{
    public string PropertyName => "Pipeline";

    public ConfigurationValidationResult Validate(ConfigurationValidationContext context)
    {
        var desiredConfig = context.DesiredConfig;
        var currentConfig = context.CurrentConfig;
        var options = context.Options;

        // Skip if pipeline validation is disabled
        if (!options.ValidatePipelineParameters)
            return ConfigurationValidationResult.Success;

        // Skip if no sensors to validate
        if (desiredConfig.Sensors == null)
            return ConfigurationValidationResult.Success;

        return ValidatePipelineParameters(currentConfig, desiredConfig.Sensors);
    }

    /// <summary>
    /// Validates Transform and DSP Filter pipeline parameters without applying updates.
    /// </summary>
    private static ConfigurationValidationResult ValidatePipelineParameters(
        DeviceConfiguration currentConfig,
        IReadOnlyList<SubNodeSensorReportDto> desiredSensors)
    {
        foreach (var desiredSensor in desiredSensors)
        {
            var sensor = currentConfig.Sensors.FirstOrDefault(s =>
                s.Name.Equals(desiredSensor.Name, StringComparison.OrdinalIgnoreCase));

            if (sensor == null)
                continue;

            // Validate DSP pipeline parameters
            if (desiredSensor.Config?.DspPipeline != null && desiredSensor.Report?.DspPipeline != null)
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
            if (desiredSensor.Config?.TransformPipeline != null && desiredSensor.Report?.TransformPipeline != null)
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
    /// Tries to get ValidateParameters method from a DSP filter
    /// that implements IConfigurableDspFilter.
    /// </summary>
    public static bool TryGetConfigurableDspFilter(
        IDspFilter filter,
        out Func<Dictionary<string, object>, ErrorOr<Success>> validateParams,
        out Action<Dictionary<string, object>> updateParams)
    {
        var filterType = filter.GetType();
        var configurableInterface = filterType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IConfigurableDspFilter<>));

        if (configurableInterface != null)
        {
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
    /// Tries to get ValidateParameters method from a transform
    /// that implements IConfigurableTransform.
    /// </summary>
    public static bool TryGetConfigurableTransform(
        ITelemetryTransform transform,
        out Func<Dictionary<string, object>, ErrorOr<Success>> validateParams,
        out Action<Dictionary<string, object>> updateParams)
    {
        var transformType = transform.GetType();
        var configurableInterface = transformType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IConfigurableTransform<>));

        if (configurableInterface != null)
        {
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
