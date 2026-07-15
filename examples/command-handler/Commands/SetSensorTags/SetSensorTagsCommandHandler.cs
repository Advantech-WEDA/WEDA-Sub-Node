using CommandHandlerExample.Commands.SetSensorTags.Models;

using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace CommandHandlerExample.Commands.SetSensorTags;

/// <summary>
/// Handler for the "tag.set" command that merges a key/value tag map into a
/// sensor's runtime metadata.
/// </summary>
/// <remarks>
/// Device resolution follows the same rules as sensor.read:
/// - If DeviceName is specified, only that device is searched
/// - If DeviceName is empty/null, all registered devices are searched
///
/// Existing metadata keys are overwritten by incoming tags; unrelated
/// metadata entries are preserved. The merge builds a new dictionary and
/// swaps it in, so concurrent readers never observe a half-merged map.
/// </remarks>
[Validation(typeof(SetSensorTagsCommandValidator))]
[Logging(LogLevel.Information)]
public class SetSensorTagsCommandHandler : ICommandHandler<SetSensorTagsCommand, SetSensorTagsResult>
{
    public Task<ErrorOr<SetSensorTagsResult>> HandleAsync(
        SetSensorTagsCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<SetSensorTagsCommandHandler>();
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var parameters = command.Parameters;

        logger.LogInformation(
            "Processing tag.set command: SensorName={SensorName}, DeviceName={DeviceName}, TagCount={TagCount}",
            parameters.SensorName, parameters.DeviceName ?? "(all)", parameters.Tags.Count);

        cancellationToken.ThrowIfCancellationRequested();

        var devices = ResolveTargetDevices(context, parameters.DeviceName);
        if (devices.Count == 0)
        {
            logger.LogWarning("No matching device registered for '{DeviceName}'", parameters.DeviceName ?? "(all)");
            return Task.FromResult<ErrorOr<SetSensorTagsResult>>(SetSensorTagsResult.Error(
                CommandStatusCode.NotFound,
                parameters.DeviceName is null
                    ? "No devices are registered"
                    : $"Device '{parameters.DeviceName}' not found",
                executedAt));
        }

        var (device, sensor) = FindSensor(devices, parameters.SensorName);
        if (device is null || sensor is null)
        {
            logger.LogWarning("Sensor '{SensorName}' not found on any target device", parameters.SensorName);
            return Task.FromResult<ErrorOr<SetSensorTagsResult>>(SetSensorTagsResult.Error(
                CommandStatusCode.NotFound,
                $"Sensor '{parameters.SensorName}' not found",
                executedAt));
        }

        // Merge into a NEW dictionary and swap it in (never mutate the live
        // map in place), so concurrent readers see either the old or the
        // fully-merged metadata.
        var merged = sensor.Metadata is null
            ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(sensor.Metadata, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in parameters.Tags)
        {
            merged[key] = value;
        }
        sensor.Metadata = merged;

        logger.LogInformation(
            "tag.set completed: {DeviceName}/{SensorName} tagged with [{Tags}] ({Total} metadata entries)",
            device.DeviceName, sensor.Name,
            string.Join(", ", parameters.Tags.Select(t => $"{t.Key}={t.Value}")),
            merged.Count);

        var resultData = new SetSensorTagsResultData
        {
            DeviceName = device.DeviceName,
            SensorName = sensor.Name,
            AppliedCount = parameters.Tags.Count,
            Tags = new Dictionary<string, string>(parameters.Tags),
            TotalMetadataCount = merged.Count
        };

        return Task.FromResult<ErrorOr<SetSensorTagsResult>>(
            SetSensorTagsResult.Success(resultData, executedAt));
    }

    /// <summary>
    /// Resolves target devices: a single named device, or all registered devices.
    /// </summary>
    private static IReadOnlyCollection<IDevice> ResolveTargetDevices(
        IWedaApplicationContext context,
        string? deviceName)
    {
        var allDevices = context.GetAllDevices<IDevice>();
        if (string.IsNullOrEmpty(deviceName))
        {
            return allDevices;
        }

        var device = allDevices.FirstOrDefault(d =>
            d.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
        return device is null ? [] : [device];
    }

    /// <summary>
    /// Finds the first device that has a sensor with the given name.
    /// </summary>
    private static (IDevice? Device, Sensor? Sensor) FindSensor(
        IReadOnlyCollection<IDevice> devices,
        string sensorName)
    {
        foreach (var device in devices)
        {
            var sensor = device.FindSensor(sensorName);
            if (sensor is not null)
            {
                return (device, sensor);
            }
        }

        return (null, null);
    }
}
